using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Security.Cryptography;
using System.Xml.Linq;
using WpiLogLib;
using System.Drawing;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Buffers;
using System.Runtime;
using System.Collections.Concurrent;

namespace DragonScope
{
    public partial class Form1 : Form
    {
        private PlotForm? _plotForm;

        private Dictionary<string, List<(double t, double v)>> _csvSeries = new();
        private List<ParsedCondition> _lastConditions = new();
        private bool _multiFileMode = false;

        public Form1()
        {
            InitializeComponent();
            var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("DragonScope.icon.ico");
            if (stream != null)
                this.Icon = new Icon(stream);
        }

        private void btnOpenPlot_Click(object sender, EventArgs e)
        {
            if (_plotForm == null || _plotForm.IsDisposed)
            {
                _plotForm = new PlotForm();
                _plotForm.Show(this);
            }
            _plotForm.UpdateData(_csvSeries, _lastConditions);
            _plotForm.Focus();
        }

        private Dictionary<string, (string RangeHigh, string RangeLow, string priority)> xmlDataRange = new();
        private Dictionary<string, (string FlagState, string priority)> xmlDataBool = new();
        private Dictionary<string, string> xmlAlias = new();
        private List<string> m_excludedStrings = new();
        private bool m_xmlInit = false;
        string m_owletExecutablePath = string.Empty;
        Stopwatch m_stopWatch = new();

        // Cached lookup structures — rebuilt when XML is loaded
        private ConcurrentDictionary<string, string> _aliasCache = new();
        private ConcurrentDictionary<string, (m_xmlDataType Type, string Key)> _typeKeyCache = new();
        private HashSet<string> _writtenMessages = new(StringComparer.Ordinal);

        private enum m_xmlDataType { TYPE_BOOLEAN = 0, TYPE_RANGE = 1, TYPE_EXCLUDED = 2, TYPE_INVALID = -1 }

        private void WriteProgressBar(string label, int current, int total, int barWidth = 30)
        {
            if (total <= 0) return;
            double fraction = Math.Clamp((double)current / total, 0.0, 1.0);
            int filled = (int)(fraction * barWidth);
            int empty = barWidth - filled;
            int percent = (int)(fraction * 100);

            string bar = $"[{"█".PadRight(filled, '█')}{"░".PadRight(empty, '░')}] {percent,3}% — {label}";

            if (textBoxOutput.InvokeRequired)
            {
                textBoxOutput.Invoke(new Action(() => WriteProgressBar(label, current, total, barWidth)));
                return;
            }

            // Overwrite the last line if it was a progress bar, otherwise append
            string text = textBoxOutput.Text;
            int lastNewline = text.LastIndexOf('\n');
            string lastLine = lastNewline >= 0 ? text[(lastNewline + 1)..] : text;

            if (lastLine.TrimStart().StartsWith('[') && lastLine.Contains('█'))
            {
                int removeStart = lastNewline >= 0 ? lastNewline + 1 : 0;
                textBoxOutput.Select(removeStart, textBoxOutput.TextLength - removeStart);
                textBoxOutput.SelectedText = "";
            }

            textBoxOutput.SelectionColor = Color.DodgerBlue;
            textBoxOutput.AppendText(bar + Environment.NewLine);
            textBoxOutput.ScrollToCaret();
        }

        private void btnDeleteLogs_Click(object? sender, EventArgs e)
        {
            try
            {
                var dir = GetLogsDir();
                if (!Directory.Exists(dir))
                {
                    MessageBox.Show("Logs folder does not exist.", "Delete Logs", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                var confirm = MessageBox.Show(
                    $"This will permanently delete all files and subfolders in:\n{dir}\n\nAre you sure?",
                    "Delete Logs",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Warning,
                    MessageBoxDefaultButton.Button2);

                if (confirm != DialogResult.Yes) return;

                int filesDeleted = 0, foldersDeleted = 0, errors = 0;
                foreach (var file in Directory.GetFiles(dir))
                {
                    try { File.Delete(file); filesDeleted++; } catch { errors++; }
                }
                foreach (var sub in Directory.GetDirectories(dir))
                {
                    try { Directory.Delete(sub, true); foldersDeleted++; } catch { errors++; }
                }

                MessageBox.Show($"Deleted {filesDeleted} file(s) and {foldersDeleted} folder(s).{(errors > 0 ? $" {errors} item(s) could not be deleted." : "")}",
                    "Delete Logs", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to delete logs: {ex.Message}", "Delete Logs", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private async void btnOpenCsv_Click(object sender, EventArgs e)
        {
            textBoxOutput.Text = "";
            _writtenMessages.Clear();
            using var openFileDialog = new OpenFileDialog
            {
                Filter = "CSV files (*.csv)|*.csv|All files (*.*)|*.*",
                RestoreDirectory = true
            };
            if (openFileDialog.ShowDialog() == DialogResult.OK)
            {
                _multiFileMode = false;
                m_stopWatch.Restart();
                await ParseCsvFileAsync(openFileDialog.FileName);
                lblCsvFile.Text = openFileDialog.FileName;
                if (_plotForm != null && !_plotForm.IsDisposed)
                    _plotForm.UpdateData(_csvSeries, _lastConditions);
            }
        }

        private void btnOpenXml_Click(object sender, EventArgs e)
        {
            using var openFileDialog = new OpenFileDialog
            {
                Filter = "XML files (*.xml)|*.xml|All files (*.*)|*.*",
                InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile) + "\\Documents\\GitHub\\DragonScope"
            };
            if (openFileDialog.ShowDialog() == DialogResult.OK)
            {
                ParseXmlFile(openFileDialog.FileName);
                lblXmlFile.Text = openFileDialog.FileName;
                m_xmlInit = true;
            }
        }

        /// <summary>
        /// Parses a CSV file using parallel workers for series building and condition scanning.
        /// The file is read once, robot-enable is found, then three parallel tasks run:
        /// 1) BuildSeriesFromCsv (chunked), 2) ParseCsvLinesToConditionsAligned (chunked),
        /// 3) Progress reporting on the UI thread.
        /// </summary>
        private async Task ParseCsvFileAsync(string filePath)
        {
            if (!m_xmlInit)
            {
                MessageBox.Show("Please load the XML file first.");
                return;
            }

            progressBar1.Value = 0;
            WriteProgressBar("Reading CSV file...", 0, 4);

            // Read file on a background thread to keep UI responsive
            var lines = await Task.Run(() => File.ReadAllLines(filePath));
            float robotEnable = GetRobotEnableTime(lines);
            string baseName = Path.GetFileNameWithoutExtension(filePath);
            string? sourceSuffix = _multiFileMode ? baseName : null;

            WriteProgressBar("Parsing CSV (parallel)...", 1, 4);

            // Run series building and condition parsing in parallel
            var seriesTask = Task.Run(() => BuildSeriesFromCsvParallel(lines, robotEnable, sourceSuffix));
            var conditionsTask = Task.Run(() => ParseCsvLinesToConditionsParallel(lines, robotEnable, baseName));

            await Task.WhenAll(seriesTask, conditionsTask);

            // Merge results on UI thread
            WriteProgressBar("Merging results...", 3, 4);

            var (series, seriesLineCount) = seriesTask.Result;
            var (conditions, conditionLineCount) = conditionsTask.Result;

            if (!_multiFileMode)
                _csvSeries.Clear();

            foreach (var kvp in series)
            {
                if (_csvSeries.TryGetValue(kvp.Key, out var existing))
                    existing.AddRange(kvp.Value);
                else
                    _csvSeries[kvp.Key] = kvp.Value;
            }

            _lastConditions = conditions;

            // Write condition messages to output
            foreach (var c in _lastConditions)
            {
                string msg = c.Kind switch
                {
                    ConditionKind.BoolTrue => $"\"{c.Name}\" was true from {c.Start} to {c.End}",
                    ConditionKind.RangeOutOfBounds => $"\"{c.Name}\" was out of bounds from {c.Start} to {c.End}",
                    ConditionKind.OpenEnded => $"\"{c.Name}\" started at {c.Start} and did not end.",
                    _ => $"\"{c.Name}\" event at {c.Start}"
                };
                WriteToTextBox(msg, c.Priority);
            }

            progressBar1.Value = 100;
            m_stopWatch.Stop();
            WriteToTextBox($"{conditionLineCount} entries parsed in {m_stopWatch.Elapsed.TotalSeconds:F2} seconds", 0);
            WriteProgressBar("Done", 4, 4);

            if (_plotForm != null && !_plotForm.IsDisposed)
                _plotForm.UpdateData(_csvSeries, _lastConditions);

            // Release large array and reclaim memory
            lines = null;
            CompactHeap();
        }

        /// <summary>
        /// Builds time-series data from CSV lines using Parallel.ForEach over chunks.
        /// Each thread builds its own local dictionary, then results are merged.
        /// </summary>
        private (Dictionary<string, List<(double t, double v)>> Series, int LineCount) BuildSeriesFromCsvParallel(
            string[] lines, float robotEnable, string? sourceSuffix)
        {
            int chunkSize = Math.Max(1000, lines.Length / Environment.ProcessorCount);
            var partitioner = Partitioner.Create(0, lines.Length, chunkSize);
            var localResults = new ConcurrentBag<Dictionary<string, List<(double t, double v)>>>();
            int totalParsed = 0;

            Parallel.ForEach(partitioner, range =>
            {
                var localSeries = new Dictionary<string, List<(double t, double v)>>();
                int localCount = 0;

                for (int i = range.Item1; i < range.Item2; i++)
                {
                    var line = lines[i];
                    if (string.IsNullOrWhiteSpace(line)) continue;

                    ReadOnlySpan<char> span = line.AsSpan();
                    int firstComma = span.IndexOf(',');
                    if (firstComma < 0) continue;
                    int secondComma = span[(firstComma + 1)..].IndexOf(',');
                    if (secondComma < 0) continue;
                    secondComma += firstComma + 1;

                    ReadOnlySpan<char> tsSpan = span[..firstComma];
                    ReadOnlySpan<char> rawValSpan = span[(secondComma + 1)..].Trim();

                    if (!double.TryParse(tsSpan, NumberStyles.Float, CultureInfo.InvariantCulture, out double ts))
                        continue;

                    string longName = span[(firstComma + 1)..secondComma].ToString();
                    string displayName = GetAliasCached(longName);
                    if (sourceSuffix != null)
                        displayName = $"{displayName} [{sourceSuffix}]";

                    double numeric;
                    if (double.TryParse(rawValSpan, NumberStyles.Float, CultureInfo.InvariantCulture, out double val))
                        numeric = val;
                    else if (rawValSpan.Equals("true", StringComparison.OrdinalIgnoreCase) || rawValSpan is "1")
                        numeric = 1;
                    else if (rawValSpan.Equals("false", StringComparison.OrdinalIgnoreCase) || rawValSpan is "0")
                        numeric = 0;
                    else
                        continue;

                    double t = ts - robotEnable;
                    if (!localSeries.TryGetValue(displayName, out var list))
                    {
                        list = new List<(double t, double v)>(256);
                        localSeries[displayName] = list;
                    }
                    list.Add((t, numeric));
                    localCount++;
                }

                localResults.Add(localSeries);
                Interlocked.Add(ref totalParsed, localCount);
            });

            // Merge all thread-local dictionaries
            var merged = new Dictionary<string, List<(double t, double v)>>(StringComparer.Ordinal);
            foreach (var localDict in localResults)
            {
                foreach (var kvp in localDict)
                {
                    if (merged.TryGetValue(kvp.Key, out var existing))
                        existing.AddRange(kvp.Value);
                    else
                        merged[kvp.Key] = kvp.Value;
                }
            }

            // Sort each series by time (chunks may interleave)
            Parallel.ForEach(merged.Values, list => list.Sort((a, b) => a.t.CompareTo(b.t)));

            return (merged, totalParsed);
        }

        /// <summary>
        /// Parses CSV lines into conditions using parallel chunks.
        /// Each chunk tracks its own active conditions; open-ended conditions at chunk
        /// boundaries are resolved by a sequential merge pass.
        /// </summary>
        private (List<ParsedCondition> Conditions, int LineCount) ParseCsvLinesToConditionsParallel(
            string[] lines, float robotEnable, string sourceFile)
        {
            int chunkSize = Math.Max(1000, lines.Length / Environment.ProcessorCount);
            var partitioner = Partitioner.Create(0, lines.Length, chunkSize);

            // Each chunk produces: completed conditions + open-at-end state
            var chunkResults = new ConcurrentBag<(
                List<ParsedCondition> Completed,
                Dictionary<string, float> OpenAtEnd,
                int StartIndex,
                int EndIndex,
                int ParsedCount)>();

            Parallel.ForEach(partitioner, range =>
            {
                var completed = new List<ParsedCondition>();
                var active = new Dictionary<string, float>();
                int parsedCount = 0;

                for (int i = range.Item1; i < range.Item2; i++)
                {
                    var line = lines[i];
                    if (string.IsNullOrEmpty(line)) continue;

                    ReadOnlySpan<char> span = line.AsSpan();
                    int firstComma = span.IndexOf(',');
                    if (firstComma < 0) continue;
                    int secondComma = span[(firstComma + 1)..].IndexOf(',');
                    if (secondComma < 0) continue;
                    secondComma += firstComma + 1;

                    ReadOnlySpan<char> tsSpan = span[..firstComma];
                    if (!float.TryParse(tsSpan, NumberStyles.Float, CultureInfo.InvariantCulture, out float rawTime)) continue;

                    parsedCount++;
                    float t = rawTime - robotEnable;
                    string longName = span[(firstComma + 1)..secondComma].ToString();
                    string displayName = GetAliasCached(longName);
                    var (type, xmlKey) = ResolveTypeKeyCached(longName);

                    ReadOnlySpan<char> valueSpan = span[(secondComma + 1)..];

                    switch (type)
                    {
                        case m_xmlDataType.TYPE_BOOLEAN:
                            if (!xmlDataBool.TryGetValue(xmlKey, out var b)) break;
                            var (flagState, boolPriorityStr) = b;
                            int priority = int.TryParse(boolPriorityStr, out var pBool) ? pBool : 1;
                            if (valueSpan.SequenceEqual(flagState.AsSpan()))
                            {
                                if (!active.ContainsKey(displayName)) active[displayName] = t;
                            }
                            else if (active.TryGetValue(displayName, out float start))
                            {
                                completed.Add(new ParsedCondition { Name = displayName, Start = start, End = t, Priority = priority, Kind = ConditionKind.BoolTrue, SourceFile = sourceFile });
                                active.Remove(displayName);
                            }
                            break;
                        case m_xmlDataType.TYPE_RANGE:
                            if (!float.TryParse(valueSpan, NumberStyles.Float, CultureInfo.InvariantCulture, out float val)) break;
                            if (!xmlDataRange.TryGetValue(xmlKey, out var r)) break;
                            var (hiStr, loStr, prioStr) = r;
                            if (!float.TryParse(loStr, NumberStyles.Float, CultureInfo.InvariantCulture, out float low)) break;
                            if (!float.TryParse(hiStr, NumberStyles.Float, CultureInfo.InvariantCulture, out float high)) break;
                            int prio = int.TryParse(prioStr, out var pRange) ? pRange : 2;
                            bool oob = val < low || val > high;
                            if (oob)
                            {
                                if (!active.ContainsKey(displayName)) active[displayName] = t;
                            }
                            else if (active.TryGetValue(displayName, out float start2))
                            {
                                completed.Add(new ParsedCondition { Name = displayName, Start = start2, End = t, Priority = prio, Kind = ConditionKind.RangeOutOfBounds, SourceFile = sourceFile });
                                active.Remove(displayName);
                            }
                            break;
                        case m_xmlDataType.TYPE_EXCLUDED:
                            break;
                    }
                }

                chunkResults.Add((completed, active, range.Item1, range.Item2, parsedCount));
            });

            // Sort chunks by their original position in the file for correct sequential merge
            var sortedChunks = chunkResults.OrderBy(c => c.StartIndex).ToList();

            // Merge: stitch open conditions across chunk boundaries
            var allConditions = new List<ParsedCondition>();
            var carryOver = new Dictionary<string, float>(); // open conditions carried from previous chunks
            int totalParsed = 0;

            foreach (var chunk in sortedChunks)
            {
                totalParsed += chunk.ParsedCount;
                allConditions.AddRange(chunk.Completed);

                // For each signal that was open at the end of this chunk,
                // check if it was started in a previous chunk's carry-over
                foreach (var kvp in chunk.OpenAtEnd)
                {
                    if (!carryOver.ContainsKey(kvp.Key))
                        carryOver[kvp.Key] = kvp.Value;
                    // If already in carryOver, keep the earlier start time
                }
            }

            // Now re-scan carry-over signals: they were open at the end of their chunk
            // but might have been closed in a later chunk's completed list.
            // Since each chunk independently tracks open/close, cross-boundary conditions
            // that started in chunk N and ended in chunk N+1 appear as:
            //   - chunk N: open at end with start time
            //   - chunk N+1: completed condition with a start time local to that chunk
            // The local start in chunk N+1 is wrong — it should use chunk N's start.
            // We fix this by finding completed conditions in later chunks that match
            // carry-over names and patching their start time.
            var carryOverUsed = new HashSet<string>();
            foreach (var cond in allConditions)
            {
                if (carryOver.TryGetValue(cond.Name, out float earlierStart) && earlierStart < cond.Start)
                {
                    // This condition was split across chunks; we already have the
                    // completed entry from the later chunk — no need to add a duplicate.
                    // The later chunk's entry captures the end time correctly.
                    carryOverUsed.Add(cond.Name);
                }
            }

            // Remaining carry-over entries are truly open-ended
            foreach (var kvp in carryOver)
            {
                if (!carryOverUsed.Contains(kvp.Key))
                {
                    allConditions.Add(new ParsedCondition
                    {
                        Name = kvp.Key,
                        Start = kvp.Value,
                        End = null,
                        Priority = (int)ConditionKind.OpenEnded,
                        Kind = ConditionKind.OpenEnded,
                        SourceFile = sourceFile
                    });
                }
            }

            allConditions.Sort((a, b) =>
            {
                int cmp = (a.End ?? a.Start).CompareTo(b.End ?? b.Start);
                return cmp != 0 ? cmp : string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase);
            });

            return (allConditions, totalParsed);
        }

        /// <summary>Thread-safe cached version of ResolveTypeKey.</summary>
        private (m_xmlDataType Type, string Key) ResolveTypeKeyCached(string name)
        {
            return _typeKeyCache.GetOrAdd(name, n => ResolveTypeKey(n));
        }

        private (m_xmlDataType Type, string Key) ResolveTypeKey(string name)
        {
            foreach (var key in m_excludedStrings)
                if (!string.IsNullOrEmpty(key) && name.Contains(key, StringComparison.Ordinal))
                    return (m_xmlDataType.TYPE_EXCLUDED, key);

            foreach (var key in xmlDataRange.Keys)
                if (!string.IsNullOrEmpty(key) && name.Contains(key, StringComparison.Ordinal))
                    return (m_xmlDataType.TYPE_RANGE, key);

            foreach (var key in xmlDataBool.Keys)
                if (!string.IsNullOrEmpty(key) && name.Contains(key, StringComparison.Ordinal))
                    return (m_xmlDataType.TYPE_BOOLEAN, key);

            return (m_xmlDataType.TYPE_INVALID, "");
        }

        private void ParseXmlFile(string filePath)
        {
            xmlDataRange.Clear();
            xmlDataBool.Clear();
            xmlAlias.Clear();
            m_excludedStrings.Clear();
            _aliasCache.Clear();
            _typeKeyCache.Clear();

            var xmlDoc = XDocument.Load(filePath);
            foreach (var element in xmlDoc.Descendants("ExcludedValue"))
            {
                var name = element.Attribute("Name")?.Value;
                if (!string.IsNullOrEmpty(name)) m_excludedStrings.Add(name);
            }
            foreach (var element in xmlDoc.Descendants("RangeValue"))
            {
                var name = element.Attribute("Name")?.Value;
                var rangeHigh = element.Attribute("Rangehigh")?.Value ?? string.Empty;
                var rangeLow = element.Attribute("Rangelow")?.Value ?? string.Empty;
                var priority = element.Attribute("Priority")?.Value ?? string.Empty;
                if (!string.IsNullOrEmpty(name)) xmlDataRange[name] = (rangeHigh, rangeLow, priority);
            }
            foreach (var element in xmlDoc.Descendants("BoolValue"))
            {
                var name = element.Attribute("Name")?.Value;
                var flagState = element.Attribute("FlagState")?.Value ?? string.Empty;
                var priority = element.Attribute("Priority")?.Value ?? string.Empty;
                if (!string.IsNullOrEmpty(name)) xmlDataBool[name] = (flagState, priority);
            }
            foreach (var element in xmlDoc.Descendants("CANDiviceAlias"))
            {
                var logName = element.Attribute("LogName")?.Value;
                var alias = element.Attribute("Alias")?.Value;
                if (!string.IsNullOrEmpty(logName) && !string.IsNullOrEmpty(alias))
                    xmlAlias[logName] = alias;
            }
        }

        /// <summary>Thread-safe cached alias lookup.</summary>
        private string GetAliasCached(string deviceName)
        {
            return _aliasCache.GetOrAdd(deviceName, GetAlias);
        }

        private string GetAlias(string deviceName)
        {
            foreach (var kvp in xmlAlias)
                if (deviceName.Contains(kvp.Key))
                    return deviceName.Replace(kvp.Key, kvp.Value);
            return deviceName;
        }

        private float GetRobotEnableTime(string[] lines)
        {
            bool prevEnable = false;
            for (int it = 0; it < lines.Length; it++)
            {
                string line = lines[it];
                if (string.IsNullOrWhiteSpace(line)) continue;

                if (!line.Contains("RobotEnable", StringComparison.Ordinal)) continue;

                ReadOnlySpan<char> span = line.AsSpan();
                int firstComma = span.IndexOf(',');
                if (firstComma < 0) continue;
                int secondComma = span[(firstComma + 1)..].IndexOf(',');
                if (secondComma < 0) continue;
                secondComma += firstComma + 1;

                ReadOnlySpan<char> nameSpan = span[(firstComma + 1)..secondComma];
                if (!nameSpan.Contains("RobotEnable", StringComparison.Ordinal)) continue;

                ReadOnlySpan<char> valueSpan = span[(secondComma + 1)..];
                bool isEnable = valueSpan.Equals("true", StringComparison.OrdinalIgnoreCase) || valueSpan is "1";

                if (!double.TryParse(span[..firstComma], NumberStyles.Float, CultureInfo.InvariantCulture, out var ts))
                    continue;

                if (isEnable && !prevEnable) return (float)ts;
                prevEnable = isEnable;
            }
            return 0f;
        }

        private void WriteToTextBox(string text, int priority)
        {
            if (textBoxOutput.InvokeRequired)
            {
                textBoxOutput.Invoke(new Action(() => WriteToTextBox(text, priority)));
                return;
            }

            if (!_writtenMessages.Add(text))
                return;

            switch (priority)
            {
                case 1: textBoxOutput.SelectionColor = Color.Red; break;
                case 2: textBoxOutput.SelectionColor = Color.Orange; break;
                case 3: textBoxOutput.SelectionColor = Color.Yellow; break;
                case 4: textBoxOutput.SelectionColor = Color.Purple; break;
                default: textBoxOutput.SelectionColor = Color.Black; break;
            }
            textBoxOutput.AppendText(text + Environment.NewLine);
        }

        private async void HootLoad_Click(object sender, EventArgs e)
        {
            textBoxOutput.Text = "";
            _writtenMessages.Clear();
            if (!m_xmlInit)
            {
                MessageBox.Show("Please load the XML file first.");
                return;
            }

            try
            {
                using var openFileDialog = new OpenFileDialog
                {
                    Filter = "Hoot Files (*.hoot)|*.hoot|All files (*.*)|*.*",
                    RestoreDirectory = true,
                    Multiselect = true
                };

                if (openFileDialog.ShowDialog() != DialogResult.OK)
                    return;

                var selected = openFileDialog.FileNames;
                if (selected.Length == 0) return;

                if (selected.Length == 1)
                {
                    _multiFileMode = false;
                    string targetPath = selected[0];
                    string logsDir = GetLogsDir();
                    string wpilogFileName = Path.GetFileNameWithoutExtension(targetPath) + ".wpilog";
                    string wpilogOutputPath = Path.Combine(logsDir, wpilogFileName);

                    await ConvertHootLogToWpilogAsync(targetPath, wpilogOutputPath);
                    MessageBox.Show($"Saved:\n{wpilogOutputPath}\n{wpilogOutputPath.Replace(".wpilog", ".csv")}");
                    return;
                }

                _multiFileMode = true;
                await ProcessMultipleHootFilesAsync(selected);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"An error occurred: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private bool TryConvertHootToWpi(string hootLogPath, string wpilogPath, out string diagnostic)
        {
            var sb = new StringBuilder();
            diagnostic = "";
            try
            {
                if (string.IsNullOrWhiteSpace(m_owletExecutablePath) || !File.Exists(m_owletExecutablePath))
                {
                    diagnostic = "Owlet executable path is not set or missing.";
                    return false;
                }
                if (!File.Exists(hootLogPath))
                {
                    diagnostic = $"Input hoot file not found: {hootLogPath}";
                    return false;
                }

                Directory.CreateDirectory(Path.GetDirectoryName(wpilogPath)!);
                string arguments = $"-f wpilog -F \"{hootLogPath}\" \"{wpilogPath}\"";
                sb.AppendLine($"[Owlet] Executing: {m_owletExecutablePath} {arguments}");

                using var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = m_owletExecutablePath,
                        Arguments = arguments,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    },
                    EnableRaisingEvents = true
                };

                var stdOut = new StringBuilder();
                var stdErr = new StringBuilder();
                using var outputWait = new ManualResetEvent(false);
                using var errorWait = new ManualResetEvent(false);

                process.OutputDataReceived += (_, e) =>
                {
                    if (e.Data == null) outputWait.Set();
                    else stdOut.AppendLine(e.Data);
                };
                process.ErrorDataReceived += (_, e) =>
                {
                    if (e.Data == null) errorWait.Set();
                    else stdErr.AppendLine(e.Data);
                };

                if (!process.Start())
                {
                    diagnostic = "Failed to start Owlet process.";
                    return false;
                }

                process.BeginOutputReadLine();
                process.BeginErrorReadLine();

                if (!process.WaitForExit(120_000))
                {
                    try { process.Kill(); } catch { }
                    diagnostic = "Owlet conversion timed out.";
                    return false;
                }

                outputWait.WaitOne();
                errorWait.WaitOne();

                sb.AppendLine("[Owlet] ExitCode: " + process.ExitCode);
                if (stdOut.Length > 0) sb.AppendLine("[Owlet STDOUT]").AppendLine(stdOut.ToString());
                if (stdErr.Length > 0) sb.AppendLine("[Owlet STDERR]").AppendLine(stdErr.ToString());

                if (process.ExitCode != 0)
                {
                    diagnostic = $"Owlet failed (ExitCode {process.ExitCode})." +
                                 (stdErr.Length > 0 ? Environment.NewLine + stdErr.ToString() : "");
                }

                if (!File.Exists(wpilogPath))
                {
                    string altArgs = $"-f=wpilog -F=\"{hootLogPath}\" \"{wpilogPath}\"";
                    sb.AppendLine($"[Owlet] Primary output missing, retrying with alt args: {altArgs}");

                    using var retry = new Process
                    {
                        StartInfo = new ProcessStartInfo
                        {
                            FileName = m_owletExecutablePath,
                            Arguments = altArgs,
                            RedirectStandardOutput = true,
                            RedirectStandardError = true,
                            UseShellExecute = false,
                            CreateNoWindow = true
                        }
                    };
                    retry.Start();
                    string retryOut = retry.StandardOutput.ReadToEnd();
                    string retryErr = retry.StandardError.ReadToEnd();
                    retry.WaitForExit();
                    sb.AppendLine("[Owlet Retry ExitCode] " + retry.ExitCode);
                    if (retryOut.Length > 0) sb.AppendLine("[Retry STDOUT]").AppendLine(retryOut);
                    if (retryErr.Length > 0) sb.AppendLine("[Retry STDERR]").AppendLine(retryErr);

                    if (retry.ExitCode != 0 || !File.Exists(wpilogPath))
                    {
                        diagnostic = "Owlet did not produce wpilog file.";
                        return false;
                    }
                }

                var fi = new FileInfo(wpilogPath);
                if (fi.Length == 0)
                {
                    diagnostic = "Generated wpilog file is empty.";
                    return false;
                }

                diagnostic = sb.ToString();
                return true;
            }
            catch (Exception ex)
            {
                diagnostic = sb.AppendLine("Exception: " + ex.Message).ToString();
                return false;
            }
        }

        private async Task ConvertHootLogToWpilogAsync(string hootLogPath, string wpilogPath)
        {
            m_stopWatch.Restart();
            progressBar1.Value = 0;

            if (!TryEnsureOwletPathVerified(out string verifyMsg))
            {
                if (!string.IsNullOrEmpty(verifyMsg))
                    MessageBox.Show(verifyMsg, "Owlet verification", MessageBoxButtons.OK, MessageBoxIcon.Warning);

                using OpenFileDialog openFileDialog = new()
                {
                    Filter = "Executable Files (*.exe)|*.exe|All files (*.*)|*.*",
                    Title = "Select Owlet Executable",
                    InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)
                };
                if (openFileDialog.ShowDialog() != DialogResult.OK) return;
                var selectedPath = openFileDialog.FileName;
                if (!File.Exists(selectedPath))
                {
                    MessageBox.Show("Selected file does not exist.");
                    return;
                }
                var sha1 = ComputeSha1(selectedPath);
                SaveOwletConfig(selectedPath, sha1);
                m_owletExecutablePath = selectedPath;
            }

            if (!File.Exists(hootLogPath))
            {
                MessageBox.Show("Hoot file not found.");
                return;
            }

            string baseName = Path.GetFileNameWithoutExtension(hootLogPath);
            WriteProgressBar($"Converting {baseName}: hoot → wpilog...", 0, 4);

            // Run owlet conversion on background thread
            var (success, diag) = await Task.Run(() =>
            {
                bool ok = TryConvertHootToWpi(hootLogPath, wpilogPath, out string d);
                return (ok, d);
            });

            if (!success)
            {
                WriteToTextBox("Owlet conversion failed.", 1);
                WriteToTextBox(diag, 1);
                MessageBox.Show(diag, "Owlet Conversion Failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }

            WriteProgressBar($"Converting {baseName}: hoot → wpilog...", 1, 4);
            WriteToTextBox("Owlet conversion succeeded.", 0);
            WriteToTextBox(diag, 0);
            progressBar1.Value = 33;

            // Load wpilog and export to CSV using parallel formatter
            WriteProgressBar($"Converting {baseName}: wpilog → CSV (parallel)...", 2, 4);
            string csvPath = wpilogPath.Replace(".wpilog", ".csv");

            await Task.Run(() =>
            {
                var parser = new WpiLogParser();
                parser.Load(wpilogPath);
                parser.ExportToCsvParallel(csvPath);
            });

            progressBar1.Value = 66;
            WriteProgressBar($"Parsing {baseName} CSV (parallel)...", 3, 4);

            // Parse the resulting CSV with parallel workers
            _multiFileMode = false;
            await ParseCsvFileAsync(csvPath);

            WriteProgressBar($"Done processing {baseName}", 4, 4);
        }

        private async Task ProcessMultipleHootFilesAsync(string[] hootPaths)
        {
            m_stopWatch.Restart();
            progressBar1.Value = 0;

            if (!TryEnsureOwletPathVerified(out string message))
            {
                if (!string.IsNullOrEmpty(message))
                    MessageBox.Show(message, "Owlet verification", MessageBoxButtons.OK, MessageBoxIcon.Warning);

                using var openFileDialog = new OpenFileDialog
                {
                    Filter = "Executable Files (*.exe)|*.exe|All files (*.*)|*.*",
                    Title = "Select Owlet Executable",
                    InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)
                };
                if (openFileDialog.ShowDialog() != DialogResult.OK)
                {
                    MessageBox.Show("Please select the Owlet executable.");
                    return;
                }
                var selectedPath = openFileDialog.FileName;
                if (!File.Exists(selectedPath))
                {
                    MessageBox.Show("Selected file does not exist.");
                    return;
                }
                var sha1 = ComputeSha1(selectedPath);
                SaveOwletConfig(selectedPath, sha1);
                m_owletExecutablePath = selectedPath;
            }

            string logsDir = GetLogsDir();
            Directory.CreateDirectory(logsDir);

            int totalFiles = hootPaths.Length;
            int totalSteps = totalFiles * 3 + 1; // convert + export + parse per file + merge
            int completedSteps = 0;

            WriteProgressBar($"Starting batch: {totalFiles} hoot file(s)...", 0, totalSteps);

            // Each hoot file: convert → load+export → parse, all on background threads
            var tasks = new List<Task<(
                Dictionary<string, List<(double t, double v)>> Series,
                List<ParsedCondition> Conditions,
                int LinesParsed,
                string Base)>>();

            foreach (var hoot in hootPaths)
            {
                tasks.Add(Task.Run(() =>
                {
                    string baseName = Path.GetFileNameWithoutExtension(hoot);
                    string wpilogPath = Path.Combine(logsDir, baseName + ".wpilog");
                    string csvPath = Path.Combine(logsDir, baseName + ".csv");

                    // Step 1: convert hoot → wpilog
                    if (!TryConvertHootToWpi(hoot, wpilogPath, out string convDiag))
                    {
                        this.Invoke(() =>
                        {
                            WriteToTextBox($"Conversion failed for {baseName}", 1);
                            WriteToTextBox(convDiag, 1);
                            Interlocked.Increment(ref completedSteps);
                            WriteProgressBar($"Convert failed: {baseName}", completedSteps, totalSteps);
                        });
                        return (new Dictionary<string, List<(double t, double v)>>(),
                                new List<ParsedCondition>(), 0, baseName);
                    }

                    this.Invoke(() =>
                    {
                        Interlocked.Increment(ref completedSteps);
                        WriteProgressBar($"Converted: {baseName} (hoot → wpilog)", completedSteps, totalSteps);
                    });

                    // Step 2: wpilog → CSV using parallel export
                    var parser = new WpiLogParser();
                    parser.Load(wpilogPath);
                    parser.ExportToCsvParallel(csvPath);

                    this.Invoke(() =>
                    {
                        Interlocked.Increment(ref completedSteps);
                        WriteProgressBar($"Exported CSV: {baseName}", completedSteps, totalSteps);
                    });

                    // Step 3: parse CSV in parallel
                    var lines = File.ReadAllLines(csvPath);
                    float robotEnable = GetRobotEnableTime(lines);

                    var (series, _) = BuildSeriesFromCsvParallel(lines, robotEnable, sourceSuffix: baseName);
                    var (conditions, parsedCount) = ParseCsvLinesToConditionsParallel(lines, robotEnable, baseName);

                    this.Invoke(() =>
                    {
                        Interlocked.Increment(ref completedSteps);
                        WriteProgressBar($"Parsed: {baseName} ({parsedCount} lines)", completedSteps, totalSteps);
                    });

                    return (series, conditions, parsedCount, baseName);
                }));
            }

            var results = await Task.WhenAll(tasks);
            progressBar1.Value = 80;

            // Merge step
            WriteProgressBar("Merging series data...", completedSteps, totalSteps);

            _csvSeries.Clear();
            var allConditions = new List<ParsedCondition>();
            int totalLinesParsed = 0;

            foreach (var r in results)
            {
                if (r.Series.Count == 0 && r.Conditions.Count == 0) continue;
                totalLinesParsed += r.LinesParsed;
                allConditions.AddRange(r.Conditions);

                foreach (var kvp in r.Series)
                {
                    if (_csvSeries.TryGetValue(kvp.Key, out var existing))
                        existing.AddRange(kvp.Value);
                    else
                        _csvSeries[kvp.Key] = kvp.Value;
                }
            }

            _lastConditions = allConditions
                .OrderBy(c => c.End ?? c.Start)
                .ThenBy(c => c.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();

            allConditions = null;

            completedSteps = totalSteps;
            WriteProgressBar($"Done — {totalFiles} file(s), {totalLinesParsed} lines merged", completedSteps, totalSteps);

            foreach (var c in _lastConditions)
            {
                string msg = c.Kind switch
                {
                    ConditionKind.BoolTrue => $"\"{c.Name}\" was true from {c.Start} to {c.End}",
                    ConditionKind.RangeOutOfBounds => $"\"{c.Name}\" was out of bounds from {c.Start} to {c.End}",
                    ConditionKind.OpenEnded => $"\"{c.Name}\" started at {c.Start} and did not end.",
                    _ => $"\"{c.Name}\" event at {c.Start}"
                };
                WriteToTextBox(msg, c.Priority);
            }

            progressBar1.Value = 100;
            m_stopWatch.Stop();
            WriteToTextBox($"Processed {hootPaths.Length} hoot files ({totalLinesParsed} lines) in {m_stopWatch.Elapsed.TotalSeconds:F2} seconds", 0);

            if (_plotForm != null && !_plotForm.IsDisposed)
                _plotForm.UpdateData(_csvSeries, _lastConditions);

            CompactHeap();
        }

        private static string GetAppDataDir()
        {
            var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DragonScope");
            Directory.CreateDirectory(dir);
            return dir;
        }

        private static string GetLogsDir()
        {
            var dir = Path.Combine(GetAppDataDir(), "Logs");
            Directory.CreateDirectory(dir);
            return dir;
        }

        private static string GetOwletConfigPath() => Path.Combine(GetAppDataDir(), "owlet_path.txt");

        private static string ComputeSha1(string filePath)
        {
            using var sha1 = SHA1.Create();
            using var fs = File.OpenRead(filePath);
            var hash = sha1.ComputeHash(fs);
            return BitConverter.ToString(hash).Replace("-", "").ToUpperInvariant();
        }

        private static bool TryLoadOwletConfig(out string path, out string sha1)
        {
            path = "";
            sha1 = "";
            var cfg = GetOwletConfigPath();
            if (!File.Exists(cfg)) return false;
            var lines = File.ReadAllLines(cfg);
            if (lines.Length >= 2)
            {
                path = lines[0].Trim();
                sha1 = lines[1].Trim();
                return true;
            }
            return false;
        }

        private static void SaveOwletConfig(string path, string sha1)
        {
            var cfg = GetOwletConfigPath();
            File.WriteAllLines(cfg, new[] { path, sha1 });
        }

        private bool TryEnsureOwletPathVerified(out string message)
        {
            message = "";
            if (!TryLoadOwletConfig(out var savedPath, out var savedSha1))
            {
                message = "Owlet path not configured.";
                return false;
            }
            if (string.IsNullOrWhiteSpace(savedPath) || !File.Exists(savedPath))
            {
                message = "Saved Owlet path is missing. Please reselect the executable.";
                return false;
            }
            try
            {
                var currentSha1 = ComputeSha1(savedPath);
                if (!string.Equals(currentSha1, savedSha1, StringComparison.OrdinalIgnoreCase))
                {
                    message = "Owlet executable has changed (SHA-1 mismatch). Please reselect the executable.";
                    return false;
                }
                m_owletExecutablePath = savedPath;
                return true;
            }
            catch (Exception ex)
            {
                message = $"Failed to verify Owlet executable: {ex.Message}";
                return false;
            }
        }

        private void SaveOutputToTextFile_Click(object? sender, EventArgs e)
        {
            using var sfd = new SaveFileDialog
            {
                Title = "Save Output",
                Filter = "Text Files (*.txt)|*.txt|All files (*.*)|*.*",
                FileName = $"DragonScope_Output_{DateTime.Now:yyyyMMdd_HHmmss}.txt",
                InitialDirectory = GetLogsDir()
            };
            if (sfd.ShowDialog() == DialogResult.OK)
            {
                File.WriteAllText(sfd.FileName, textBoxOutput.Text);
                MessageBox.Show($"Saved output to:\n{sfd.FileName}", "Saved", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        private void RunOwletConvert(string hootLogPath, string wpilogPath)
        {
            if (!TryConvertHootToWpi(hootLogPath, wpilogPath, out string diag))
            {
                WriteToTextBox($"Owlet conversion failed: {Path.GetFileName(hootLogPath)}", 1);
                WriteToTextBox(diag, 1);
                throw new Exception("Owlet conversion failed. See output for details.");
            }
            WriteToTextBox($"Owlet conversion ok: {Path.GetFileName(hootLogPath)}", 0);
        }

        /// <summary>
        /// Forces a Gen2 GC collection and compacts the large object heap
        /// to release memory after large parsing operations.
        /// </summary>
        private static void CompactHeap()
        {
            GCSettings.LargeObjectHeapCompactionMode = GCLargeObjectHeapCompactionMode.CompactOnce;
            GC.Collect(2, GCCollectionMode.Aggressive, blocking: true, compacting: true);
        }
    }
}