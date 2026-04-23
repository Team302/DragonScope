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
        private DataCacheManager _cacheManager = new();

        private Dictionary<string, List<(double t, double v)>> _csvSeries = new();
        private List<ParsedCondition> _lastConditions = new();
        private bool _multiFileMode = false;

        public Form1()
        {
            InitializeComponent();
            var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("DragonScope.icon.ico");
            if (stream != null)
                this.Icon = new Icon(stream);

            this.Shown += Form1_Shown;
        }

        private void Form1_Shown(object? sender, EventArgs e)
        {
            string? cachedPath = LoadConfigPath();
            string defaultPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Documents", "GitHub", "DragonScope", "config.xml");

            string targetPath = !string.IsNullOrWhiteSpace(cachedPath) && File.Exists(cachedPath) ? cachedPath : defaultPath;

            if (File.Exists(targetPath))
            {
                ParseXmlFile(targetPath);
                lblXmlFile.Text = targetPath;
                m_xmlInit = true;
                WriteToTextBox($"Loaded config from: {targetPath}", 0);
                if (targetPath != cachedPath)
                {
                    SaveConfigPath(targetPath);
                }
            }
            else
            {
                MessageBox.Show($"Could not find config.xml in the expected location:\n{targetPath}\n\nPlease select it manually.", "Config Required", MessageBoxButtons.OK, MessageBoxIcon.Information);
                btnOpenXml_Click(this, EventArgs.Empty);
            }
        }

        private string GetConfigPathCacheFile()
        {
            string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DragonScope");
            Directory.CreateDirectory(dir);
            return Path.Combine(dir, "cached_config_path.txt");
        }

        private void SaveConfigPath(string path)
        {
            try
            {
                File.WriteAllText(GetConfigPathCacheFile(), path);
            }
            catch { }
        }

        private string? LoadConfigPath()
        {
            try
            {
                string file = GetConfigPathCacheFile();
                if (File.Exists(file))
                {
                    string path = File.ReadAllText(file).Trim();
                    if (File.Exists(path))
                        return path;
                }
            }
            catch { }
            return null;
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
                
                Task.Run(() =>
                {
                    foreach (var file in Directory.GetFiles(dir))
                    {
                        try { File.Delete(file); filesDeleted++; } catch { errors++; }
                    }
                    foreach (var sub in Directory.GetDirectories(dir))
                    {
                        try { Directory.Delete(sub, true); foldersDeleted++; } catch { errors++; }
                    }
                }).ContinueWith(t =>
                {
                    MessageBox.Show($"Deleted {filesDeleted} file(s) and {foldersDeleted} folder(s).{(errors > 0 ? $" {errors} item(s) could not be deleted." : "")}",
                        "Delete Logs", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }, TaskScheduler.FromCurrentSynchronizationContext());
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
                SaveConfigPath(openFileDialog.FileName);
                WriteToTextBox($"Loaded config from: {openFileDialog.FileName}", 0);
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

        /// <summary>
        /// Forces a Gen2 GC collection and compacts the large object heap
        /// to release memory after large parsing operations.
        /// </summary>
        private static void CompactHeap()
        {
            GCSettings.LargeObjectHeapCompactionMode = GCLargeObjectHeapCompactionMode.CompactOnce;
            GC.Collect(2, GCCollectionMode.Aggressive, blocking: true, compacting: true);
        }

        #region Cache Management

        private void CacheCurrentAnalysis(string fileName, int linesParsed)
        {
            try
            {
                string dataHash = _cacheManager.GenerateDataHash(_csvSeries, _lastConditions);
                string cacheId = Guid.NewGuid().ToString("N");

                var analysis = new CachedAnalysis
                {
                    CacheId = cacheId,
                    FileName = fileName,
                    DataHash = dataHash,
                    CachedAt = DateTime.Now,
                    LinesParsed = linesParsed,
                    CsvSeries = new Dictionary<string, List<(double t, double v)>>(_csvSeries),
                    Conditions = new List<ParsedCondition>(_lastConditions)
                };

                _cacheManager.SaveAnalysis(analysis);
                WriteToTextBox($"Analysis cached: {fileName} (ID: {cacheId})", 0);
            }
            catch (Exception ex)
            {
                WriteToTextBox($"Failed to cache analysis: {ex.Message}", 1);
            }
        }

        private async Task CacheCurrentAnalysisAsync(string fileName, int linesParsed)
        {
            try
            {
                string dataHash = await Task.Run(() => _cacheManager.GenerateDataHash(_csvSeries, _lastConditions));
                string cacheId = Guid.NewGuid().ToString("N");

                var analysis = new CachedAnalysis
                {
                    CacheId = cacheId,
                    FileName = fileName,
                    DataHash = dataHash,
                    CachedAt = DateTime.Now,
                    LinesParsed = linesParsed,
                    CsvSeries = new Dictionary<string, List<(double t, double v)>>(_csvSeries),
                    Conditions = new List<ParsedCondition>(_lastConditions)
                };

                await _cacheManager.SaveAnalysisAsync(analysis);
                WriteToTextBox($"Analysis cached: {fileName} (ID: {cacheId})", 0);
            }
            catch (Exception ex)
            {
                WriteToTextBox($"Failed to cache analysis: {ex.Message}", 1);
            }
        }

        public CachedAnalysis? LoadCachedAnalysis(string cacheId)
        {
            try
            {
                var analysis = _cacheManager.LoadAnalysis(cacheId);
                if (analysis == null)
                {
                    MessageBox.Show("Failed to load cached analysis.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return null;
                }

                // Check for legacy/corrupted cache where points are (0,0) due to previous JSON field serialization bug
                bool corrupted = false;
                foreach (var list in analysis.CsvSeries.Values)
                {
                    if (list.Count > 10 && list.All(p => p.t == 0 && p.v == 0))
                    {
                        corrupted = true;
                        break;
                    }
                }

                if (corrupted)
                {
                    MessageBox.Show("This cached analysis appears to be corrupted (all data points are exactly 0). This is caused by loading a cache made before the latest JSON serialization fix.\n\nPlease clear your caches inside the Cache Browser and re-parse your logs to fix this issue.", "Legacy Cache Detected", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return null;
                }

                _csvSeries.Clear();
                foreach (var kvp in analysis.CsvSeries)
                    _csvSeries[kvp.Key] = new List<(double t, double v)>(kvp.Value);

                _lastConditions = analysis.Conditions
                    .OrderBy(c => c.End ?? c.Start)
                    .ThenBy(c => c.Name, StringComparer.OrdinalIgnoreCase)
                    .ToList();
                
                lblCsvFile.Text = $"[CACHED] {analysis.FileName}";
                progressBar1.Value = 100;

                textBoxOutput.Clear();
                _writtenMessages.Clear();

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

                WriteToTextBox($"Loaded cached analysis: {analysis.FileName} ({analysis.LinesParsed} lines)", 0);
                WriteToTextBox($"Data Hash: {analysis.DataHash}", 0);

                if (_plotForm != null && !_plotForm.IsDisposed)
                    _plotForm.UpdateData(_csvSeries, _lastConditions);

                return analysis;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading cached analysis: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return null;
            }
        }

        public async Task<CachedAnalysis?> LoadCachedAnalysisAsync(string cacheId)
        {
            try
            {
                var analysis = await Task.Run(() => _cacheManager.LoadAnalysis(cacheId));
                if (analysis == null)
                {
                    MessageBox.Show("Failed to load cached analysis.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return null;
                }

                // Check for legacy/corrupted cache where points are (0,0) due to previous JSON field serialization bug
                bool corrupted = false;
                foreach (var list in analysis.CsvSeries.Values)
                {
                    if (list.Count > 10 && list.All(p => p.t == 0 && p.v == 0))
                    {
                        corrupted = true;
                        break;
                    }
                }

                if (corrupted)
                {
                    MessageBox.Show("This cached analysis appears to be corrupted (all data points are exactly 0). This is caused by loading a cache made before the latest JSON serialization fix.\n\nPlease clear your caches inside the Cache Browser and re-parse your logs to fix this issue.", "Legacy Cache Detected", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return null;
                }

                _csvSeries.Clear();

                await Task.Run(() =>
                {
                    foreach (var kvp in analysis.CsvSeries)
                        _csvSeries[kvp.Key] = new List<(double t, double v)>(kvp.Value);
                });

                _lastConditions = await Task.Run(() => analysis.Conditions
                    .OrderBy(c => c.End ?? c.Start)
                    .ThenBy(c => c.Name, StringComparer.OrdinalIgnoreCase)
                    .ToList());
                
                lblCsvFile.Text = $"[CACHED] {analysis.FileName}";
                progressBar1.Value = 100;

                textBoxOutput.Clear();
                _writtenMessages.Clear();

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

                WriteToTextBox($"Loaded cached analysis: {analysis.FileName} ({analysis.LinesParsed} lines)", 0);
                WriteToTextBox($"Data Hash: {analysis.DataHash}", 0);

                if (_plotForm != null && !_plotForm.IsDisposed)
                    _plotForm.UpdateData(_csvSeries, _lastConditions);

                return analysis;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading cached analysis: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return null;
            }
        }

        public List<CachedAnalysisMetadata> SearchCache(string searchTerm)
        {
            return _cacheManager.SearchCachedAnalyses(searchTerm);
        }

        public List<CachedAnalysisMetadata> GetAllCachedAnalyses()
        {
            return _cacheManager.GetAllCachedAnalyses();
        }

        public void DeleteCachedAnalysis(string cacheId)
        {
            try
            {
                _cacheManager.DeleteAnalysis(cacheId);
                WriteToTextBox($"Deleted cached analysis: {cacheId}", 0);
            }
            catch (Exception ex)
            {
                WriteToTextBox($"Failed to delete cached analysis: {ex.Message}", 1);
            }
        }

        public void ClearAllCache()
        {
            try
            {
                _cacheManager.ClearAllCache();
                WriteToTextBox("All cached analyses cleared.", 0);
            }
            catch (Exception ex)
            {
                WriteToTextBox($"Failed to clear cache: {ex.Message}", 1);
            }
        }

        private void BtnCacheBrowser_Click(object? sender, EventArgs e)
        {
            var cacheBrowser = new CacheBrowserForm(this);
            cacheBrowser.ShowDialog(this);
        }

        private void btnMotorStats_Click(object? sender, EventArgs e)
        {
            if (_csvSeries.Count == 0)
            {
                MessageBox.Show("No data loaded. Please load a CSV or cache file first.", "Motor Statistics", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var motorStatsForm = new MotorStatsForm(_csvSeries, _csvSeries);
            motorStatsForm.ShowDialog(this);
        }

        private void btnMultiCacheAnalysis_Click(object? sender, EventArgs e)
        {
            var multiCacheForm = new MultiCacheMotorAnalysisForm(_cacheManager);
            multiCacheForm.Show(this);
        }

        private async void btnBulkProcessCache_Click(object? sender, EventArgs e)
        {
            textBoxOutput.Text = "";
            _writtenMessages.Clear();

            if (!m_xmlInit)
            {
                MessageBox.Show("Please load the XML file first.", "Bulk Process & Cache", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            using var folderDialog = new FolderBrowserDialog
            {
                Description = "Select parent folder containing match subfolders with log files",
                ShowNewFolderButton = false
            };

            if (folderDialog.ShowDialog() != DialogResult.OK)
                return;

            string parentFolder = folderDialog.SelectedPath;
            await ProcessBulkLogsAndCacheAsync(parentFolder);
        }

        #endregion
    }
}