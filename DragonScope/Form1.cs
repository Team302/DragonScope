using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using System.Xml.Linq;

namespace DragonScope
{
    public partial class Form1 : Form
    {
        private Dictionary<string, (string Type, string RangeHigh, string RangeLow, string priority)> xmlData;
        private bool xmlinit = false;

        public Form1()
        {
            InitializeComponent();
        }

        private void btnOpenCsv_Click(object sender, EventArgs e)
        {
            if(!xmlinit)
            {
                MessageBox.Show("Please load the XML file first.");
                return;
            }
            textBoxOutput.Text = "";
            using (OpenFileDialog openFileDialog = new OpenFileDialog())
            {
                openFileDialog.Filter = "CSV files (*.csv)|*.csv|All files (*.*)|*.*";
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
                if (openFileDialog.ShowDialog() == DialogResult.OK)
                {
                    ParseXmlFile(openFileDialog.FileName);
                    lblXmlFile.Text = openFileDialog.FileName;
                    xmlinit = true;
                }
            }
        }

        private void ParseCsvFile(string filePath)
        {
            var errors = new HashSet<string>();
            var lines = File.ReadAllLines(filePath);
            float robotenable = 0;
            bool robotenablelatch = false;
            for (int it = 0; it < lines.Length; it++)
            {
                string line = lines[it];
                var values = line.Split(',');
                if (values.Length > 2)
                {
                    if (xmlData.Keys.Any(k => k == values[1])) //TODO fix this to accept values that contain string in xml
                    {
                        var (type, rangeHigh, rangeLow, priority) = xmlData[values[1]];

                        if (type == "bool" && values[2] == "1")
                        {
                            if (errors.Add(values[1]))
                            {
                                if (float.TryParse(values[0], out float timeValue))
                                {
                                    WriteToTextBox("\"" + values[1] + "\"" + " has value: " + values[2] + " at time: " + (timeValue - robotenable), 1);
                                }
                            }
                        }
                        else if (type == "range" && float.TryParse(values[2], out float intValue))
                        {

                            if (float.TryParse(rangeLow, out float low) && float.TryParse(rangeHigh, out float high))
                            {
                                    if (intValue <= low || intValue >= high)
                                    {
                                        if (errors.Add(values[1]))
                                        {
                                            if (float.TryParse(values[0], out float timeValue))
                                            {
                                            WriteToTextBox("\"" + values[1] + "\"" + " has value: " + values[2] + " at time: " + (timeValue - robotenable), 1);
                                            }
                                        }
                                    }
                            }
                        }
                    }
                    else if (values[1].Contains("/Fault_") && values[2] == "1")
                    {
                        if (errors.Add(values[1]))
                        {
                            if (float.TryParse(values[0], out float timeValue))
                            {
                                WriteToTextBox("\"" + values[1] + "\"" + " has value: " + values[2] + " at time: " + (timeValue - robotenable), 1);
                            }
                        }
                    }
                    else if (values[1].Contains("/StickyFault_") && values[2] == "1")
                    {
                        if (errors.Add(values[1]))
                        {
                            if (float.TryParse(values[0], out float timeValue))
                            {
                                WriteToTextBox("\"" + values[1] + "\"" + " has value: " + values[2] + " at time: " + (timeValue - robotenable), 2);
                            }
                        }
                    }
                    else if (values[1].Contains("RobotEnable"))
                    {
                        if (values[2] == "true")
                        {
                            if (float.TryParse(values[0], out float parsedValue))
                            {
                                if (robotenablelatch == false)
                                {
                                    robotenable = parsedValue;
                                    robotenablelatch = true;
                                }
                            }
                        }
                    }

                    progressBar1.Value = (int)((float)it / lines.Length * 100); // Update progress bar

                }
                
            }
            progressBar1.Value = 100; // Ensure progress bar is full at the end
        }

        private void ParseXmlFile(string filePath)
        {
            xmlData = new Dictionary<string, (string Type, string RangeHigh, string RangeLow, string priority)>();
            var xmlDoc = XDocument.Load(filePath);
            foreach (var element in xmlDoc.Descendants("Value"))
            {
                var name = element.Attribute("Name")?.Value;
                var type = element.Attribute("Type")?.Value;
                var rangeHigh = element.Attribute("Rangehigh")?.Value;
                var rangeLow = element.Attribute("Rangelow")?.Value;
                var priority = element.Attribute("Priority")?.Value;

                if (!string.IsNullOrEmpty(name) && !string.IsNullOrEmpty(type))
                {
                    xmlData[name] = (type, rangeHigh, rangeLow, priority);
                }
            }
        }

        private void WriteToTextBox(string text, int priority)
        {
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
                default:
                    textBoxOutput.SelectionColor = System.Drawing.Color.Black;
                    break;
            }

            textBoxOutput.AppendText(text + $"{Environment.NewLine}");
            textBoxOutput.SelectionColor = System.Drawing.Color.Black; // Reset color to default
        }
    }
}