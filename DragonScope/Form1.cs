using System.Reflection;
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

        private enum m_xmlDataType
        {
            TYPE_BOOLEAN = 0,
            TYPE_RANGE = 1,
            TYPE_EXCLUDED = 2,
            TYPE_INVALID = -1
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

            int linesparsed = 0;
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();

            for (int it = 0; it < lines.Length; it++)
            {
                string line = lines[it];
                var values = line.Split(',');

                if (values.Length > 2)
                {
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
                                        WriteToTextBox($"\"{displayName}\" was true from {startTime} to {endTime}".Remove(0, 6), 1);
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
                                                WriteToTextBox($"\"{displayName}\" was out of bounds from {startTime} to {endTime}".Remove(0, 6), rangePriorityInt);
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
                    currentxmlIndex = 0;
                    m_currentxmlType = "";

                    progressBar1.Value = (int)((float)it / lines.Length * 100); // Update progress bar
                    linesparsed = it;
                }
            }

            // Handle any remaining active conditions at the end of the file
            foreach (var condition in activeConditions)
            {
                WriteToTextBox($"\"{condition.Key}\" started at {condition.Value} and did not end.".Remove(0, 6), 4);
            }

            progressBar1.Value = 100; // Ensure progress bar is full at the end
            stopwatch.Stop();
            WriteToTextBox(linesparsed + 1.ToString() + " entries parsed in " + stopwatch.Elapsed.TotalSeconds.ToString() + " seconds", 0);
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
            for (int it = 0; it < lines.Length; it++)
            {
                string line = lines[it];
                var values = line.Split(',');
                if (values.Length > 2 && values[1].Contains("RobotEnable"))
                {
                    if (values[2] == "true")
                    {
                        if (float.TryParse(values[0], out float parsedValue))
                        {
                            return parsedValue;
                        }
                    }
                }
            }
            return 0; // Placeholder return value
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
        private void HootLoad_Click(object sender, EventArgs e)
        {
            try
            {
                string targetPath = "";
                using (OpenFileDialog openFileDialog = new OpenFileDialog())
                {
                    openFileDialog.Filter = "Hoot Files (*.hoot)|*.hoot|All files (*.*)|*.*";
                    openFileDialog.RestoreDirectory = true;
                    if (openFileDialog.ShowDialog() == DialogResult.OK)
                    {
                        string fileNameOnly = Path.GetFileName(openFileDialog.FileName);
                        targetPath = openFileDialog.FileName;
                    }
                }
                using (FolderBrowserDialog folderBrowserDialog = new FolderBrowserDialog())
                {
                    folderBrowserDialog.Description = "Select the target directory for the converted file";
                    if (folderBrowserDialog.ShowDialog() == DialogResult.OK)
                    {
                        ConvertHootLogToWpilog(targetPath, Path.Combine(folderBrowserDialog.SelectedPath, targetPath.Replace(".hoot", ".wpilog")));
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"An error occurred: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
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
            progressBar1.Value = 0;
            if(m_owletExecutablePath != string.Empty)
            {
                MessageBox.Show("Owlet executable already selected, skipping selection dialog.");
            }
            else
            {
                using OpenFileDialog openFileDialog = new OpenFileDialog
                {
                    Filter = "Owlet Executable|owlet.exe|All files (*.*)|*.*",
                    InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)
                };
                if (openFileDialog.ShowDialog() == DialogResult.OK)
                {
                    m_owletExecutablePath = openFileDialog.FileName;
                }
                else
                {
                    MessageBox.Show("Please select the Owlet executable.");
                    return;
                }
            }

            string arguments = $"-f wpilog -F {hootLogPath} {wpilogPath}";
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
    }
}