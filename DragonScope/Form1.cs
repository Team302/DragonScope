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

namespace DragonScope
{
    public partial class Form1 : Form
    {
        private PlotForm? _plotForm;

        private readonly Dictionary<string, List<(double t, double v)>> _csvSeries = new();
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
        string m_currentxmlType = "";
        Stopwatch m_stopWatch = new();

        private enum m_xmlDataType { TYPE_BOOLEAN = 0, TYPE_RANGE = 1, TYPE_EXCLUDED = 2, TYPE_INVALID = -1 }

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

        private void btnOpenCsv_Click(object sender, EventArgs e)
        {
            textBoxOutput.Text = "";
            using var openFileDialog = new OpenFileDialog
            {
                Filter = "CSV files (*.csv)|*.csv|All files (*.*)|*.*",
                RestoreDirectory = true
            };
            if (openFileDialog.ShowDialog() == DialogResult.OK)
            {
                _multiFileMode = false;
                ParseCsvFile(openFileDialog.FileName);
                lblCsvFile.Text = openFileDialog.FileName;
                m_stopWatch.Restart();
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

        private void ParseCsvFile(string filePath)
        {
            if (!m_xmlInit)
            {
                MessageBox.Show("Please load the XML file first.");
                return;
            }

            var activeConditions = new Dictionary<string, float>();
            var lines = File.ReadAllLines(filePath);
            float robotenable = GetRobotEnableTime(lines);

            BuildSeriesFromCsv(lines, sourceSuffix: _multiFileMode ? Path.GetFileNameWithoutExtension(filePath) : null);
            _lastConditions = ParseCsvLinesToConditionsAligned(lines, sourceFile: Path.GetFileNameWithoutExtension(filePath), out _);

            int parsedLines = 0;
            for (int it = 0; it < lines.Length; it++)
            {
                string line = lines[it];
                var values = line.Split(',');
                if (values.Length > 2)
                {
                    if (float.TryParse(values[0], NumberStyles.Float, CultureInfo.InvariantCulture, out _))
                        parsedLines++;
                    var currentxmlIndex = GetTypeFromXml(values[1]);
                    string displayName = GetAlias(values[1]);
                    switch (currentxmlIndex)
                    {
                        case m_xmlDataType.TYPE_BOOLEAN:
                            {
                                var (flagState, boolPriority) = xmlDataBool[m_currentxmlType];
                                if (values[2] == flagState)
                                {
                                    if (!activeConditions.ContainsKey(values[1]) &&
                                        float.TryParse(values[0], NumberStyles.Float, CultureInfo.InvariantCulture, out float timeValue))
                                    {
                                        activeConditions[values[1]] = timeValue - robotenable;
                                    }
                                }
                                else if (activeConditions.ContainsKey(values[1]))
                                {
                                    if (float.TryParse(values[0], NumberStyles.Float, CultureInfo.InvariantCulture, out float timeValue) &&
                                        int.TryParse(boolPriority, out int boolPriorityInt))
                                    {
                                        float startTime = activeConditions[values[1]];
                                        float endTime = timeValue - robotenable;
                                        WriteToTextBox($"\"{displayName}\" was true from {startTime} to {endTime}", boolPriorityInt);
                                        activeConditions.Remove(values[1]);
                                    }
                                }
                            }
                            break;
                        case m_xmlDataType.TYPE_RANGE:
                            {
                                if (float.TryParse(values[2], NumberStyles.Float, CultureInfo.InvariantCulture, out float intValue))
                                {
                                    var (rangeHigh, rangeLow, rangePriority) = xmlDataRange[m_currentxmlType];
                                    if (float.TryParse(rangeLow, NumberStyles.Float, CultureInfo.InvariantCulture, out float low) &&
                                        float.TryParse(rangeHigh, NumberStyles.Float, CultureInfo.InvariantCulture, out float high))
                                    {
                                        if (intValue < low || intValue > high)
                                        {
                                            if (!activeConditions.ContainsKey(values[1]) &&
                                                float.TryParse(values[0], NumberStyles.Float, CultureInfo.InvariantCulture, out float timeValue))
                                            {
                                                activeConditions[values[1]] = timeValue - robotenable;
                                            }
                                        }
                                        else if (activeConditions.ContainsKey(values[1]))
                                        {
                                            if (float.TryParse(values[0], NumberStyles.Float, CultureInfo.InvariantCulture, out float timeValue) &&
                                                int.TryParse(rangePriority, out int rangePriorityInt))
                                            {
                                                float startTime = activeConditions[values[1]];
                                                float endTime = timeValue - robotenable;
                                                WriteToTextBox($"\"{displayName}\" was out of bounds from {startTime} to {endTime}", rangePriorityInt);
                                                activeConditions.Remove(values[1]);
                                            }
                                        }
                                    }
                                }
                            }
                            break;
                        case m_xmlDataType.TYPE_EXCLUDED:
                            break;
                        default:
                            m_currentxmlType = string.Empty;
                            break;
                    }
                    m_currentxmlType = "";
                    progressBar1.Value = (int)((float)it / lines.Length * 100);
                }
            }

            foreach (var condition in activeConditions)
                WriteToTextBox($"\"{GetAlias(condition.Key)}\" started at {condition.Value} and did not end.", 4);

            progressBar1.Value = 100;
            m_stopWatch.Stop();
            WriteToTextBox(parsedLines + " entries parsed in " + m_stopWatch.Elapsed.TotalSeconds + " seconds", 0);

            if (_plotForm != null && !_plotForm.IsDisposed)
                _plotForm.UpdateData(_csvSeries, _lastConditions);
        }

        private void BuildSeriesFromCsv(string[] lines, string? sourceSuffix = null)
        {
            if (!_multiFileMode)
                _csvSeries.Clear();

            float robotEnable = GetRobotEnableTime(lines);
            for (int i = 0; i < lines.Length; i++)
            {
                var line = lines[i];
                if (string.IsNullOrWhiteSpace(line)) continue;
                var values = line.Split(',');
                if (values.Length <= 2) continue;
                if (!double.TryParse(values[0], NumberStyles.Float, CultureInfo.InvariantCulture, out double ts))
                    continue;

                string longName = values[1];
                string displayName = GetAlias(longName);
                if (sourceSuffix != null)
                    displayName = $"{displayName} [{sourceSuffix}]";

                string rawVal = values[2].Trim();
                double numeric;
                if (double.TryParse(rawVal, NumberStyles.Float, CultureInfo.InvariantCulture, out double val))
                    numeric = val;
                else if (string.Equals(rawVal, "true", StringComparison.OrdinalIgnoreCase) || rawVal == "1")
                    numeric = 1;
                else if (string.Equals(rawVal, "false", StringComparison.OrdinalIgnoreCase) || rawVal == "0")
                    numeric = 0;
                else
                    continue;

                double t = ts - robotEnable;
                if (!_csvSeries.TryGetValue(displayName, out var list))
                {
                    list = new List<(double t, double v)>();
                    _csvSeries[displayName] = list;
                }
                list.Add((t, numeric));
            }
        }

        private m_xmlDataType GetTypeFromXml(string name)
        {
            foreach (var key in m_excludedStrings) if (name.Contains(key)) { m_currentxmlType = key; return m_xmlDataType.TYPE_EXCLUDED; }
            foreach (var key in xmlDataRange.Keys) if (name.Contains(key)) { m_currentxmlType = key; return m_xmlDataType.TYPE_RANGE; }
            foreach (var key in xmlDataBool.Keys) if (name.Contains(key)) { m_currentxmlType = key; return m_xmlDataType.TYPE_BOOLEAN; }
            return m_xmlDataType.TYPE_INVALID;
        }

        private void ParseXmlFile(string filePath)
        {
            xmlDataRange.Clear();
            xmlDataBool.Clear();
            xmlAlias.Clear();
            m_excludedStrings.Clear();

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
                var values = line.Split(',');
                if (values.Length <= 2) continue;
                if (!values[1].Contains("RobotEnable")) continue;

                bool isEnable = values[2].Equals("true", StringComparison.OrdinalIgnoreCase) || values[2] == "1";
                if (!double.TryParse(values[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var ts))
                    continue;

                if (isEnable && !prevEnable) return (float)ts;
                prevEnable = isEnable;
            }
            return 0f;
        }

        private void WriteToTextBox(string text, int priority)
        {
            switch (priority)
            {
                case 1: textBoxOutput.SelectionColor = Color.Red; break;
                case 2: textBoxOutput.SelectionColor = Color.Orange; break;
                case 3: textBoxOutput.SelectionColor = Color.Yellow; break;
                case 4: textBoxOutput.SelectionColor = Color.Purple; break;
                default: textBoxOutput.SelectionColor = Color.Black; break;
            }
            if (!textBoxOutput.Text.Contains(text))
                textBoxOutput.AppendText(text + Environment.NewLine);
        }

        private async void HootLoad_Click(object sender, EventArgs e)
        {
            textBoxOutput.Text = "";
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

                    ConvertHootLogToWpilog(targetPath, wpilogOutputPath);
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
                    return false;
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

        private void ConvertHootLogToWpilog(string hootLogPath, string wpilogPath)
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

            if (!TryConvertHootToWpi(hootLogPath, wpilogPath, out string diag))
            {
                WriteToTextBox("Owlet conversion failed.", 1);
                WriteToTextBox(diag, 1);
                MessageBox.Show(diag, "Owlet Conversion Failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            WriteToTextBox("Owlet conversion succeeded.", 0);
            WriteToTextBox(diag, 0);
            progressBar1.Value = 50;
            ConvertWpilogToCsv(wpilogPath, wpilogPath.Replace(".wpilog", ".csv"));
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

            var tasks = new List<Task<(List<ParsedCondition> Conditions, int LinesParsed, string[] CsvLines, string Base)>>();

            foreach (var hoot in hootPaths)
            {
                tasks.Add(Task.Run(() =>
                {
                    string baseName = Path.GetFileNameWithoutExtension(hoot);
                    string wpilogPath = Path.Combine(logsDir, baseName + ".wpilog");
                    string csvPath = Path.Combine(logsDir, baseName + ".csv");

                    if (!TryConvertHootToWpi(hoot, wpilogPath, out string convDiag))
                    {
                        lock (_csvSeries)
                        {
                            WriteToTextBox($"Conversion failed for {baseName}", 1);
                            WriteToTextBox(convDiag, 1);
                        }
                        return (new List<ParsedCondition>(), 0, Array.Empty<string>(), baseName);
                    }

                    var parser = new WpiLogParser();
                    parser.Load(wpilogPath);
                    parser.ExportToCsv(csvPath);
                    var lines = File.ReadAllLines(csvPath);
                    var conditions = ParseCsvLinesToConditionsAligned(lines, sourceFile: baseName, out int parsedCount);
                    return (conditions, parsedCount, lines, baseName);
                }));
            }

            var results = await Task.WhenAll(tasks);
            progressBar1.Value = 80;

            _csvSeries.Clear();
            var allConditions = new List<ParsedCondition>();
            int totalLinesParsed = 0;
            foreach (var r in results)
            {
                if (r.CsvLines.Length == 0) continue;
                totalLinesParsed += r.LinesParsed;
                allConditions.AddRange(r.Conditions);
                BuildSeriesFromCsv(r.CsvLines, sourceSuffix: r.Base);
            }

            _lastConditions = allConditions
                .OrderBy(c => c.End ?? c.Start)
                .ThenBy(c => c.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();

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
        }

        private List<ParsedCondition> ParseCsvLinesToConditionsAligned(string[] lines, string sourceFile, out int linesParsed)
        {
            var result = new List<ParsedCondition>();
            var active = new Dictionary<string, float>();
            float robotEnable = GetRobotEnableTime(lines);
            int parsedCount = 0;

            for (int i = 0; i < lines.Length; i++)
            {
                var line = lines[i];
                if (string.IsNullOrWhiteSpace(line)) continue;
                var values = line.Split(',');
                if (values.Length <= 2) continue;
                if (!float.TryParse(values[0], NumberStyles.Float, CultureInfo.InvariantCulture, out float rawTime)) continue;

                parsedCount++;
                float t = rawTime - robotEnable;
                string longName = values[1];
                string displayName = GetAlias(longName);
                var (type, xmlKey) = ResolveTypeKey(longName);
                switch (type)
                {
                    case m_xmlDataType.TYPE_BOOLEAN:
                        if (!xmlDataBool.TryGetValue(xmlKey, out var b)) break;
                        var (flagState, boolPriorityStr) = b;
                        int priority = int.TryParse(boolPriorityStr, out var pBool) ? pBool : 1;
                        if (values[2] == flagState)
                        {
                            if (!active.ContainsKey(displayName)) active[displayName] = t;
                        }
                        else if (active.TryGetValue(displayName, out float start))
                        {
                            result.Add(new ParsedCondition { Name = displayName, Start = start, End = t, Priority = priority, Kind = ConditionKind.BoolTrue, SourceFile = sourceFile });
                            active.Remove(displayName);
                        }
                        break;
                    case m_xmlDataType.TYPE_RANGE:
                        if (!float.TryParse(values[2], NumberStyles.Float, CultureInfo.InvariantCulture, out float val)) break;
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
                            result.Add(new ParsedCondition { Name = displayName, Start = start2, End = t, Priority = prio, Kind = ConditionKind.RangeOutOfBounds, SourceFile = sourceFile });
                            active.Remove(displayName);
                        }
                        break;
                    case m_xmlDataType.TYPE_EXCLUDED:
                        break;
                }
            }

            foreach (var kv in active)
                result.Add(new ParsedCondition { Name = kv.Key, Start = kv.Value, End = null, Priority = (int)ConditionKind.OpenEnded, Kind = ConditionKind.OpenEnded, SourceFile = sourceFile });

            linesParsed = parsedCount;
            return result;
        }

        private void ConvertWpilogToCsv(string wpilogPath, string csvPath)
        {
            try
            {
                var parser = new WpiLogParser();
                parser.Load(wpilogPath);
                progressBar1.Value = 75;
                parser.ExportToCsv(csvPath);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"An error occurred with wpilog conversion: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            _multiFileMode = false;
            ParseCsvFile(csvPath);
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

        private (m_xmlDataType Type, string Key) ResolveTypeKey(string name)
        {
            // Determines which XML classification the log entry name matches.
            // Returns the matched type and the key used to lookup range/bool metadata.
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
    }
}