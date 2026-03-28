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