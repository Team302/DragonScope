using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Security.Cryptography;
using System.Xml.Linq;
using WpiLogLib;

namespace DragonScope
{
    public partial class Form1 : Form
    {
        public Form1()
        {
            InitializeComponent();
            var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("DragonScope.icon.ico");
            if (stream != null)
            {
                this.Icon = new Icon(stream);
            }
        }

        private Dictionary<string, (string RangeHigh, string RangeLow, string priority)> xmlDataRange;
        private Dictionary<string, (string FlagState, string priority)> xmlDataBool;
        private Dictionary<string, string> xmlAlias;
        private List<string> m_excludedStrings = new List<string>();
        private bool m_xmlInit = false;
        string m_owletExecutablePath = string.Empty;
        string m_currentxmlType = "";
        Stopwatch m_stopWatch = new Stopwatch();

        private enum m_xmlDataType
        {
            TYPE_BOOLEAN = 0,
            TYPE_RANGE = 1,
            TYPE_EXCLUDED = 2,
            TYPE_INVALID = -1
        }

        // Aggregation types for multi-file parsing
        private enum ConditionKind { BoolTrue = 1, RangeOutOfBounds = 2, OpenEnded = 4 }

        private sealed class ParsedCondition
        {
            public string Name { get; init; } = "";
            public float Start { get; init; }           // time aligned per-file (minus RobotEnable)
            public float? End { get; init; }            // null = open-ended
            public int Priority { get; init; }          // 1..4 per existing coloring
            public ConditionKind Kind { get; init; }
            public string SourceFile { get; init; } = ""; // optional: which file produced it
        }

        // NEW: Delete-all-logs handler
        private void DeleteAllLogs_Click(object? sender, EventArgs e)
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

                MessageBox.Show(
                    $"Deleted {filesDeleted} file(s) and {foldersDeleted} folder(s).{(errors > 0 ? $" {errors} item(s) could not be deleted." : "")}",
                    "Delete Logs",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to delete logs: {ex.Message}", "Delete Logs", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void btnOpenCsv_Click(object sender, EventArgs e)
        {
            textBoxOutput.Text = "";
            using (OpenFileDialog openFileDialog = new OpenFileDialog())
            {
                openFileDialog.Filter = "CSV files (*.csv)|*.csv|All files (*.*)|*.*";
                openFileDialog.RestoreDirectory = true;
                if (openFileDialog.ShowDialog() == DialogResult.OK)
                {
                    ParseCsvFile(openFileDialog.FileName);
                    lblCsvFile.Text = openFileDialog.FileName;
                    m_stopWatch.Restart();
                }
            }
        }

        private void btnOpenXml_Click(object sender, EventArgs e)
        {
            using (OpenFileDialog openFileDialog = new OpenFileDialog())
            {
                openFileDialog.Filter = "XML files (*.xml)|*.xml|All files (*.*)|*.*";
                openFileDialog.InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile) + "\\Documents\\GitHub\\DragonScope";
                if (openFileDialog.ShowDialog() == DialogResult.OK)
                {
                    ParseXmlFile(openFileDialog.FileName);
                    lblXmlFile.Text = openFileDialog.FileName;
                    m_xmlInit = true;
                }
            }
        }

        private void ParseCsvFile(string filePath)
        {
            if (!m_xmlInit)
            {
                MessageBox.Show("Please load the XML file first.");
                return;
            }
            var activeConditions = new Dictionary<string, float>(); // Tracks active faults or out-of-bounds conditions
            var lines = File.ReadAllLines(filePath);
            float robotenable = GetRobotEnableTime(lines);
            string[] xmlRangeNames = xmlDataRange.Keys.ToArray();
            string[] xmlBoolNames = xmlDataBool.Keys.ToArray();

            int parsedLines = 0;

            for (int it = 0; it < lines.Length; it++)
            {
                string line = lines[it];
                var values = line.Split(',');

                if (values.Length > 2)
                {
                    // Count a line as parsed if it has timestamp we can parse
                    if (float.TryParse(values[0], out _))
                    {
                        parsedLines++;
                    }

                    var currentxmlIndex = GetTypeFromXml(values[1]); // Get the type from XML data
                    string displayName = GetAlias(values[1]); // Get alias or original name
                    switch (currentxmlIndex)
                    {
                        case m_xmlDataType.TYPE_BOOLEAN:
                            var (flagState, boolPriority) = xmlDataBool[m_currentxmlType]; // Renamed 'priority' to 'boolPriority'
                            if (values[2] == flagState)
                            {
                                if (!activeConditions.ContainsKey(values[1]))
                                {
                                    if (float.TryParse(values[0], out float timeValue))
                                    {
                                        activeConditions[values[1]] = timeValue - robotenable; // Start time
                                    }
                                }
                            }
                            else
                            {
                                if (activeConditions.ContainsKey(values[1]))
                                {
                                    if (float.TryParse(values[0], out float timeValue) && int.TryParse(boolPriority, out int boolPriorityInt))
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
                            if (float.TryParse(values[2], out float intValue))
                            {
                                var (rangeHigh, rangeLow, rangePriority) = xmlDataRange[m_currentxmlType]; // Renamed 'priority' to 'rangePriority'
                                if (float.TryParse(rangeLow, out float low) && float.TryParse(rangeHigh, out float high))
                                {
                                    if (intValue < low || intValue > high)
                                    {
                                        if (!activeConditions.ContainsKey(values[1]))
                                        {
                                            if (float.TryParse(values[0], out float timeValue))
                                            {
                                                activeConditions[values[1]] = timeValue - robotenable; // Start time
                                            }
                                        }
                                    }
                                    else
                                    {
                                        if (activeConditions.ContainsKey(values[1]))
                                        {
                                            if (float.TryParse(values[0], out float timeValue) && int.TryParse(rangePriority, out int rangePriorityInt))
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
                    // Reset the xml name found flag
                    m_currentxmlType = "";

                    progressBar1.Value = (int)((float)it / lines.Length * 100); // Update progress bar
                }
            }

            // Handle any remaining active conditions at the end of the file
            foreach (var condition in activeConditions)
            {
                WriteToTextBox($"\"{GetAlias(condition.Key)}\" started at {condition.Value} and did not end.", 4);
            }

            progressBar1.Value = 100; // Ensure progress bar is full at the end
            m_stopWatch.Stop();
            WriteToTextBox(parsedLines.ToString() + " entries parsed in " + m_stopWatch.Elapsed.TotalSeconds.ToString() + " seconds", 0);
        }

        private m_xmlDataType GetTypeFromXml(string name)
        {
            foreach (var key in m_excludedStrings)
            {
                if (name.Contains(key))
                {
                    m_currentxmlType = key;
                    return m_xmlDataType.TYPE_EXCLUDED;
                }
            }
            foreach (var key in xmlDataRange.Keys)
            {
                if (name.Contains(key))
                {
                    m_currentxmlType = key;
                    return m_xmlDataType.TYPE_RANGE;
                }
            }
            foreach (var key in xmlDataBool.Keys)
            {
                if (name.Contains(key))
                {
                    m_currentxmlType = key;
                    return m_xmlDataType.TYPE_BOOLEAN;
                }
            }
            return m_xmlDataType.TYPE_INVALID;
        }

        private void ParseXmlFile(string filePath)
        {
            xmlDataRange = new Dictionary<string, (string RangeHigh, string RangeLow, string priority)>();
            xmlDataBool = new Dictionary<string, (string FlagState, string priority)>();
            xmlAlias = new Dictionary<string, string>(); // Ensure it's initialized
            var xmlDoc = XDocument.Load(filePath);
            foreach (var element in xmlDoc.Descendants("ExcludedValue"))
            {
                var name = element.Attribute("Name")?.Value;
                if (!string.IsNullOrEmpty(name))
                {
                    m_excludedStrings.Add(name);
                }
            }
            foreach (var element in xmlDoc.Descendants("RangeValue"))
            {
                var name = element.Attribute("Name")?.Value;
                var rangeHigh = element.Attribute("Rangehigh")?.Value ?? string.Empty; // Ensure non-null value
                var rangeLow = element.Attribute("Rangelow")?.Value ?? string.Empty;   // Ensure non-null value
                var priority = element.Attribute("Priority")?.Value ?? string.Empty;  // Ensure non-null value

                if (!string.IsNullOrEmpty(name))
                {
                    xmlDataRange[name] = (rangeHigh, rangeLow, priority);
                }
            }
            foreach (var element in xmlDoc.Descendants("BoolValue"))
            {
                var name = element.Attribute("Name")?.Value;
                var flagState = element.Attribute("FlagState")?.Value ?? string.Empty; // Ensure non-null value
                var priority = element.Attribute("Priority")?.Value ?? string.Empty;  // Ensure non-null value

                if (!string.IsNullOrEmpty(name))
                {
                    xmlDataBool[name] = (flagState, priority);
                }
            }
            foreach (var element in xmlDoc.Descendants("CANDiviceAlias"))
            {
                var logName = element.Attribute("LogName")?.Value;
                var alias = element.Attribute("Alias")?.Value;

                if (!string.IsNullOrEmpty(logName) && !string.IsNullOrEmpty(alias))
                {
                    xmlAlias[logName] = alias;
                }
            }
        }

        // 3. Add a helper to get alias or fallback to original name
        private string GetAlias(string deviceName)
        {
            foreach (var kvp in xmlAlias)
            {
                if (deviceName.Contains(kvp.Key))
                {
                    // Replace only the matching part with the alias
                    return deviceName.Replace(kvp.Key, kvp.Value);
                }
            }
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

                if (isEnable && !prevEnable)
                    return (float)ts; // rising edge

                prevEnable = isEnable;
            }
            return 0f;
        }

        private void WriteToTextBox(string text, int priority)
        {
            // Apply the color based on priority
            switch (priority)
            {
                case 1:
                    textBoxOutput.SelectionColor = System.Drawing.Color.Red;
                    break;
                case 2:
                    textBoxOutput.SelectionColor = System.Drawing.Color.Orange;
                    break;
                case 3:
                    textBoxOutput.SelectionColor = System.Drawing.Color.Yellow;
                    break;
                case 4:
                    textBoxOutput.SelectionColor = System.Drawing.Color.Purple;
                    break;
                default:
                    textBoxOutput.SelectionColor = System.Drawing.Color.Black;
                    break;
            }
            if (!textBoxOutput.Text.Contains(text))
            {
                textBoxOutput.AppendText(text + $"{Environment.NewLine}");
            }
        }

        // MULTI-FILE: Select multiple .hoot files, convert each on its own thread, align timestamps, display
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

                // If only one file, keep the original single-file path
                if (selected.Length == 1)
                {
                    string targetPath = selected[0];
                    string logsDir = GetLogsDir();
                    string wpilogFileName = Path.GetFileNameWithoutExtension(targetPath) + ".wpilog";
                    string wpilogOutputPath = Path.Combine(logsDir, wpilogFileName);

                    ConvertHootLogToWpilog(targetPath, wpilogOutputPath);
                    MessageBox.Show($"Saved:\n{wpilogOutputPath}\n{wpilogOutputPath.Replace(".wpilog", ".csv")}");
                    return;
                }

                // Multi-file path
                await ProcessMultipleHootFilesAsync(selected);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"An error occurred: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private async Task ProcessMultipleHootFilesAsync(string[] hootPaths)
        {
            m_stopWatch.Restart();
            progressBar1.Value = 0;

            // Ensure Owlet is configured/verified once
            if (!TryEnsureOwletPathVerified(out string message))
            {
                if (!string.IsNullOrEmpty(message))
                    MessageBox.Show(message, "Owlet verification", MessageBoxButtons.OK, MessageBoxIcon.Warning);

                using var openFileDialog = new OpenFileDialog
                {
                    Filter = "Owlet Executable|owlet.exe|All files (*.*)|*.*",
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

            // Phase 1: parallel convert and parse each file
            var tasks = new List<Task<(List<ParsedCondition> Conditions, int LinesParsed)>>();
            foreach (var hoot in hootPaths)
            {
                tasks.Add(Task.Run(() => ConvertAndParseFile(hoot, logsDir)));
            }

            var results = await Task.WhenAll(tasks);
            progressBar1.Value = 80;

            // Phase 2: merge and order by time (already aligned to RobotEnable per file)
            var all = results.SelectMany(r => r.Conditions).ToList();
            int totalLinesParsed = results.Sum(r => r.LinesParsed);

            var ordered = all
                .OrderBy(c => c.End ?? c.Start)
                .ThenBy(c => c.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();

            // Phase 3: display
            foreach (var c in ordered)
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
        }

        // Convert a single hoot -> wpilog -> csv, then parse conditions aligned to that file's RobotEnable time
        private (List<ParsedCondition> Conditions, int LinesParsed) ConvertAndParseFile(string hootLogPath, string logsDir)
        {
            string baseName = Path.GetFileNameWithoutExtension(hootLogPath);
            string wpilogPath = Path.Combine(logsDir, baseName + ".wpilog");
            string csvPath = Path.Combine(logsDir, baseName + ".csv");

            RunOwletConvert(hootLogPath, wpilogPath);

            var parser = new WpiLogParser();
            parser.Load(wpilogPath);
            parser.ExportToCsv(csvPath);

            var lines = File.ReadAllLines(csvPath);
            var conditions = ParseCsvLinesToConditionsAligned(lines, sourceFile: baseName, out int parsedCount);
            return (conditions, parsedCount);
        }

        // Run owlet conversion (no UI)
        private void RunOwletConvert(string hootLogPath, string wpilogPath)
        {
            if (!File.Exists(hootLogPath))
                throw new FileNotFoundException("Hoot file not found", hootLogPath);

            Directory.CreateDirectory(Path.GetDirectoryName(wpilogPath)!);

            string arguments = $"-f wpilog -F \"{hootLogPath}\" \"{wpilogPath}\"";

            var process = new System.Diagnostics.Process
            {
                StartInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = m_owletExecutablePath,
                    Arguments = arguments,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            process.Start();
            string output = process.StandardOutput.ReadToEnd();
            string error = process.StandardError.ReadToEnd();
            process.WaitForExit();

            if (process.ExitCode != 0)
            {
                throw new Exception($"Owlet conversion failed: {error}");
            }
        }

        // Return type + matched key atomically, no shared state
        private (m_xmlDataType Type, string Key) ResolveTypeKey(string name)
        {
            foreach (var key in m_excludedStrings) if (name.Contains(key)) return (m_xmlDataType.TYPE_EXCLUDED, key);
            foreach (var key in xmlDataRange.Keys) if (name.Contains(key)) return (m_xmlDataType.TYPE_RANGE, key);
            foreach (var key in xmlDataBool.Keys) if (name.Contains(key)) return (m_xmlDataType.TYPE_BOOLEAN, key);
            return (m_xmlDataType.TYPE_INVALID, "");
        }

        // Build conditions from CSV lines using alias display names and aligning by RobotEnable for that file
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

                // Count this as a parsed line
                parsedCount++;

                float t = rawTime - robotEnable;
                string longName = values[1];
                string displayName = GetAlias(longName);

                var (type, xmlKey) = ResolveTypeKey(longName);
                switch (type)
                {
                    case m_xmlDataType.TYPE_BOOLEAN:
                    {
                        if (!xmlDataBool.TryGetValue(xmlKey, out var b)) break;
                        var (flagState, boolPriorityStr) = b;
                        int priority = ParsePriorityOrDefault(boolPriorityStr, 1);

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
                    }
                    case m_xmlDataType.TYPE_RANGE:
                    {
                        if (!float.TryParse(values[2], NumberStyles.Float, CultureInfo.InvariantCulture, out float val)) break;
                        if (!xmlDataRange.TryGetValue(xmlKey, out var r)) break;

                        var (hiStr, loStr, prioStr) = r;
                        if (!float.TryParse(loStr, NumberStyles.Float, CultureInfo.InvariantCulture, out float low)) break;
                        if (!float.TryParse(hiStr, NumberStyles.Float, CultureInfo.InvariantCulture, out float high)) break;
                        int priority = ParsePriorityOrDefault(prioStr, 2);

                        bool oob = val < low || val > high;
                        if (oob)
                        {
                            if (!active.ContainsKey(displayName)) active[displayName] = t;
                        }
                        else if (active.TryGetValue(displayName, out float start))
                        {
                            result.Add(new ParsedCondition { Name = displayName, Start = start, End = t, Priority = priority, Kind = ConditionKind.RangeOutOfBounds, SourceFile = sourceFile });
                            active.Remove(displayName);
                        }
                        break;
                    }
                    case m_xmlDataType.TYPE_EXCLUDED:
                        // ignore
                        break;
                }
            }

            foreach (var kv in active)
                result.Add(new ParsedCondition { Name = kv.Key, Start = kv.Value, End = null, Priority = (int)ConditionKind.OpenEnded, Kind = ConditionKind.OpenEnded, SourceFile = sourceFile });

            linesParsed = parsedCount;
            return result;
        }

        private static int ParsePriorityOrDefault(string s, int def)
        {
            return int.TryParse(s, out var p) ? p : def;
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
            }
            ParseCsvFile(csvPath);
        }

        private void ConvertHootLogToWpilog(string hootLogPath, string wpilogPath)
        {
            m_stopWatch.Restart();
            progressBar1.Value = 0;

            // Load saved owlet path + sha1 from AppData and verify. If missing/mismatch, prompt.
            if (!TryEnsureOwletPathVerified(out string errorMessage))
            {
                if (!string.IsNullOrEmpty(errorMessage))
                    MessageBox.Show(errorMessage, "Owlet verification", MessageBoxButtons.OK, MessageBoxIcon.Warning);

                using OpenFileDialog openFileDialog = new OpenFileDialog
                {
                    Filter = "Owlet Executable|owlet.exe|All files (*.*)|*.*",
                    Title = "Select Owlet Executable",
                    InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)
                };
                if (openFileDialog.ShowDialog() == DialogResult.OK)
                {
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
                else
                {
                    MessageBox.Show("Please select the Owlet executable.");
                    return;
                }
            }

            // Ensure output directory exists
            Directory.CreateDirectory(Path.GetDirectoryName(wpilogPath)!);

            string arguments = $"-f wpilog -F \"{hootLogPath}\" \"{wpilogPath}\"";
            if (!File.Exists(hootLogPath))
            {
                MessageBox.Show("Provide valid hoot directory");
            }

            var process = new System.Diagnostics.Process
            {
                StartInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = m_owletExecutablePath,
                    Arguments = arguments,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };
            Console.WriteLine($"Executing: {m_owletExecutablePath} {arguments}");

            process.Start();
            string output = process.StandardOutput.ReadToEnd();
            string error = process.StandardError.ReadToEnd();
            process.WaitForExit();
            progressBar1.Value = 50;

            if (process.ExitCode != 0)
            {
                throw new Exception($"Owlet conversion failed: {error}");
            }
            ConvertWpilogToCsv(wpilogPath, wpilogPath.Replace(".wpilog", ".csv"));
        }

        // -------- AppData storage + SHA-1 verification helpers --------

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

        private static string GetOwletConfigPath()
        {
            return Path.Combine(GetAppDataDir(), "owlet_path.txt");
        }

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

        // Loads saved path and verifies SHA-1; sets m_owletExecutablePath if valid.
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

        // --- Save text box output to a text file ---
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
    }
}