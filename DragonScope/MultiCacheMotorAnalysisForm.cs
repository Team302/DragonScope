using System.Collections.Concurrent;
using ScottPlot.WinForms;

namespace DragonScope
{
    public partial class MultiCacheMotorAnalysisForm : Form
    {
        private readonly DataCacheManager _cacheManager;
        private readonly Dictionary<string, (CachedAnalysis analysis, System.Drawing.Color color)> _selectedCaches = new();
        private readonly Dictionary<string, List<(double t, double v)>> _filteredData = new();

        private void LogStatus(string message)
        {
            if (this.InvokeRequired)
            {
                this.Invoke(new Action(() => LogStatus(message)));
                return;
            }

            if (this.Controls.Find("statusTextBox", true).FirstOrDefault() is RichTextBox txt)
            {
                txt.AppendText($"[{DateTime.Now:HH:mm:ss}] {message}{Environment.NewLine}");
                txt.ScrollToCaret();
            }
        }

        public MultiCacheMotorAnalysisForm(DataCacheManager cacheManager)
        {
            _cacheManager = cacheManager ?? throw new ArgumentNullException(nameof(cacheManager));
            InitializeComponent();
        }

        protected override async void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            
            if (this.Controls.Find("seriesCombo", true).FirstOrDefault() is ComboBox seriesCombo)
            {
                seriesCombo.Items.Add("Loading series...");
                seriesCombo.SelectedIndex = 0;
                seriesCombo.Enabled = false;
            }

            LogStatus("Initializing Multi-Cache Analysis...");
            await LoadCacheListAsync();

            if (this.Controls.Find("seriesCombo", true).FirstOrDefault() is ComboBox seriesComboEnd)
            {
                seriesComboEnd.Enabled = true;
            }
            LogStatus("Ready. Select caches and a signal to analyze.");
        }

        private async Task LoadCacheListAsync()
        {
            var cacheListBox = this.Controls.Find("cacheListBox", true).FirstOrDefault() as CheckedListBox;
            if (cacheListBox == null) return;

            LogStatus("Loading cached logs into memory...");
            var allCaches = _cacheManager.GetAllCachedAnalyses();
            cacheListBox.Items.Clear();

            foreach (var cache in allCaches.OrderByDescending(c => c.CachedAt))
            {
                cacheListBox.Items.Add($"{cache.FileName} ({cache.CachedAt:g})", false);
            }

            LogStatus($"Found {allCaches.Count} caches. Searching for common motor signals (multi-threaded)...");
            await PopulateSeriesListAsync();
        }

        private string GetCleanSeriesName(string name)
        {
            if (string.IsNullOrEmpty(name)) return string.Empty;
            int idx = name.LastIndexOf(" [");
            if (idx > 0 && name.EndsWith("]"))
            {
                return name.Substring(0, idx);
            }
            return name;
        }

        private async Task PopulateSeriesListAsync()
        {
            var seriesCombo = this.Controls.Find("seriesCombo", true).FirstOrDefault() as ComboBox;
            if (seriesCombo == null) return;

            var allCaches = _cacheManager.GetAllCachedAnalyses();

            var allMotorSeries = await Task.Run(() => 
            {
                var motorSeries = new ConcurrentDictionary<string, byte>(StringComparer.OrdinalIgnoreCase);

                Parallel.ForEach(allCaches, cache =>
                {
                    if (cache.SeriesKeys != null && cache.SeriesKeys.Count > 0)
                    {
                        foreach (var seriesName in cache.SeriesKeys)
                        {
                            if (seriesName.Contains("Current", StringComparison.OrdinalIgnoreCase) &&
                                seriesName.Contains("Stator", StringComparison.OrdinalIgnoreCase))
                            {
                                motorSeries.TryAdd(GetCleanSeriesName(seriesName), 0);
                            }
                        }
                    }
                    else
                    {
                        // Fallback for older caches that don't have SeriesKeys populated
                        var analysis = _cacheManager.LoadAnalysis(cache.CacheId);
                        if (analysis?.CsvSeries != null)
                        {
                            foreach (var seriesName in analysis.CsvSeries.Keys)
                            {
                                if (seriesName.Contains("Current", StringComparison.OrdinalIgnoreCase) &&
                                    seriesName.Contains("Stator", StringComparison.OrdinalIgnoreCase))
                                {
                                    motorSeries.TryAdd(GetCleanSeriesName(seriesName), 0);
                                }
                            }
                        }
                    }
                });

                return motorSeries.Keys.ToList();
            });

            seriesCombo.Items.Clear();
            foreach (var series in allMotorSeries.OrderBy(s => s))
            {
                seriesCombo.Items.Add(series);
            }

            if (seriesCombo.Items.Count > 0)
                seriesCombo.SelectedIndex = 0;
        }

        private void CacheListBox_ItemCheck(object? sender, ItemCheckEventArgs e)
        {
            // Will refresh series list and plot when cache selection changes
            BeginInvoke((Action)(() =>
            {
                if (this.Controls.Find("seriesCombo", true).FirstOrDefault() is ComboBox seriesCombo &&
                    seriesCombo.SelectedItem != null)
                {
                    UpdateColorPanel();
                }
            }));
        }

        private void SeriesCombo_SelectedIndexChanged(object? sender, EventArgs e)
        {
            UpdateColorPanel();
        }

        private void UpdateColorPanel()
        {
            var colorPanel = this.Controls.Find("colorPanel", true).FirstOrDefault() as FlowLayoutPanel;
            if (colorPanel == null) return;

            var cacheListBox = this.Controls.Find("cacheListBox", true).FirstOrDefault() as CheckedListBox;
            if (cacheListBox == null) return;

            colorPanel.Controls.Clear();

            var allCaches = _cacheManager.GetAllCachedAnalyses().OrderByDescending(c => c.CachedAt).ToList();
            var defaultColors = GetDefaultColors();
            var colorIndex = 0;

            for (int i = 0; i < cacheListBox.Items.Count; i++)
            {
                if (!cacheListBox.GetItemChecked(i)) continue;

                var cache = allCaches[i];
                var btnColor = new Button
                {
                    Width = 25,
                    Height = 25,
                    Tag = cache.CacheId,
                    BackColor = defaultColors[colorIndex % defaultColors.Count],
                    FlatStyle = FlatStyle.Flat
                };
                btnColor.Click += (s, e) => ChangeColor(btnColor, cache.CacheId);

                var label = new System.Windows.Forms.Label
                {
                    Text = cache.FileName,
                    AutoSize = true,
                    TextAlign = ContentAlignment.MiddleLeft
                };

                colorPanel.Controls.Add(btnColor);
                colorPanel.Controls.Add(label);

                colorIndex++;
            }
        }

        private void ChangeColor(Button colorButton, string cacheId)
        {
            using (var colorDialog = new ColorDialog { Color = colorButton.BackColor, FullOpen = true })
            {
                if (colorDialog.ShowDialog() == DialogResult.OK)
                {
                    colorButton.BackColor = colorDialog.Color;

                    // Update selected cache color
                    if (_selectedCaches.ContainsKey(cacheId))
                    {
                        var cache = _selectedCaches[cacheId].analysis;
                        _selectedCaches[cacheId] = (cache, colorDialog.Color);
                        RefreshPlot();
                    }
                }
            }
        }

        private async void BtnAnalyze_Click(object? sender, EventArgs e)
        {
            var cacheListBox = this.Controls.Find("cacheListBox", true).FirstOrDefault() as CheckedListBox;
            var seriesCombo = this.Controls.Find("seriesCombo", true).FirstOrDefault() as ComboBox;

            if (cacheListBox == null || seriesCombo == null) return;

            if (seriesCombo.SelectedItem is not string motorCurrentSignal)
            {
                MessageBox.Show("Please select a motor current signal.", "Selection Required", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            // Disable UI while analyzing
            btnAnalyze.Enabled = false;
            btnAnalyze.Text = "Loading...";
            
            LogStatus($"Pre-filtering caches for signal: {motorCurrentSignal}...");

            _selectedCaches.Clear();
            _filteredData.Clear();

            var allCaches = _cacheManager.GetAllCachedAnalyses().OrderByDescending(c => c.CachedAt).ToList();
            var defaultColors = GetDefaultColors();
            
            var selectedIndices = new List<int>();
            for (int i = 0; i < cacheListBox.Items.Count; i++)
            {
                if (cacheListBox.GetItemChecked(i))
                    selectedIndices.Add(i);
            }
            
            LogStatus($"Attempting to load {selectedIndices.Count} selected cache(s) in parallel.");

            // Load analysis payloads in parallel
            var loadTasks = selectedIndices.Select(async (index, colorIndex) =>
            {
                var cacheMetadata = allCaches[index];
                
                // Fast path: Check if metadata says it even has the key before loading massive JSON
                // If SeriesKeys is completely empty, it might be an older cache format, so we fall through and load it just in case.
                if (cacheMetadata.SeriesKeys != null && cacheMetadata.SeriesKeys.Count > 0 && 
                    !cacheMetadata.SeriesKeys.Any(k => GetCleanSeriesName(k).Equals(motorCurrentSignal, StringComparison.OrdinalIgnoreCase)))
                {
                    return null; 
                }

                var analysis = await Task.Run(() => _cacheManager.LoadAnalysis(cacheMetadata.CacheId));
                if (analysis?.CsvSeries == null) return null;

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
                    MessageBox.Show($"Warning: Cache '{cacheMetadata.FileName}' appears to be corrupted (all data points are exactly 0). This is caused by loading a cache made before the latest JSON serialization fix.\n\nPlease clear your caches and re-parse your logs to fix this issue.", "Legacy Cache Detected", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return null;
                }

                var currentSeries = analysis.CsvSeries.FirstOrDefault(kvp =>
                    GetCleanSeriesName(kvp.Key).Equals(motorCurrentSignal, StringComparison.OrdinalIgnoreCase)).Value;

                if (currentSeries == null || currentSeries.Count == 0)
                    return null;

                var cacheKey = $"{cacheMetadata.FileName}_{cacheMetadata.CacheId}";
                return new { CacheKey = cacheKey, Analysis = analysis, Color = defaultColors[colorIndex % defaultColors.Count] };
            });

            var results = (await Task.WhenAll(loadTasks)).Where(r => r != null).ToList();

            foreach (var r in results)
            {
                if (r != null)
                {
                    _selectedCaches[r.CacheKey] = (r.Analysis, r.Color);
                }
            }

            if (_selectedCaches.Count == 0)
            {
                btnAnalyze.Enabled = true;
                btnAnalyze.Text = "Analyze";
                MessageBox.Show("No valid data found for selected signal in chosen caches.", "No Data", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                LogStatus("Analysis failed: No valid data found.");
                return;
            }

            LogStatus($"Processing and filtering time data for {_selectedCaches.Count} matching log(s)...");
            await Task.Run(() => LoadAndFilterData(motorCurrentSignal));
            
            LogStatus("Redrawing Plot interface...");
            RefreshPlot();
            UpdateColorPanel();

            LogStatus("Analysis visual plot generated successfully.");

            btnAnalyze.Enabled = true;
            btnAnalyze.Text = "Analyze";
        }

        private void LoadAndFilterData(string motorCurrentSignal)
        {
            _filteredData.Clear();

            foreach (var kvp in _selectedCaches)
            {
                var cacheKey = kvp.Key;
                var (analysis, _) = kvp.Value;

                var currentSeries = analysis.CsvSeries.FirstOrDefault(s =>
                    GetCleanSeriesName(s.Key).Equals(motorCurrentSignal, StringComparison.OrdinalIgnoreCase)).Value;

                if (currentSeries == null) continue;

                // Time is already normalized to RobotEnable in the parser, so point.t >= 0 is enabled
                var filtered = new List<(double t, double v)>();
                foreach (var point in currentSeries)
                {
                    if (point.t >= 0)
                    {
                        filtered.Add(point);
                    }
                }

                if (filtered.Count == 0)
                {
                    // Fallback to all data if there's no data past enable
                    filtered = currentSeries;
                }

                _filteredData[cacheKey] = filtered;
            }
        }

        private void RefreshPlot()
        {
            var formsPlot = this.Controls.Find("formsPlot", true).FirstOrDefault() as FormsPlot;
            if (formsPlot == null) return;

            formsPlot.Plot.Clear();

            foreach (var kvp in _filteredData)
            {
                var cacheKey = kvp.Key;
                var data = kvp.Value;

                if (data.Count == 0) continue;

                if (!_selectedCaches.TryGetValue(cacheKey, out var cacheInfo))
                    continue;

                var (_, color) = cacheInfo;

                double[] xs = new double[data.Count];
                double[] ys = new double[data.Count];

                for (int i = 0; i < data.Count; i++)
                {
                    xs[i] = data[i].t;
                    ys[i] = data[i].v;
                }

                var scatter = formsPlot.Plot.Add.Scatter(xs, ys);
                scatter.LegendText = cacheKey.Split('_')[0]; // Use filename part for legend
                scatter.LineWidth = 1.5f;
                scatter.Color = ToPlotColor(color);
            }

            formsPlot.Plot.Legend.IsVisible = true;
            formsPlot.Plot.XLabel("Time (s from First Enable)");
            formsPlot.Plot.YLabel("Current (A)");
            formsPlot.Plot.Title("Motor Current Analysis (Robot Enabled Only)");
            formsPlot.Plot.Axes.AutoScale();
            formsPlot.Refresh();
        }

        private void BtnExport_Click(object? sender, EventArgs e)
        {
            if (_filteredData.Count == 0)
            {
                MessageBox.Show("No data to export. Please analyze first.", "No Data", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            using (var saveDialog = new SaveFileDialog
            {
                Filter = "CSV Files (*.csv)|*.csv",
                DefaultExt = "csv",
                FileName = "motor_analysis.csv"
            })
            {
                if (saveDialog.ShowDialog() == DialogResult.OK)
                {
                    ExportToCSV(saveDialog.FileName);
                    MessageBox.Show("Data exported successfully.", "Export Complete", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
        }

        private void ExportToCSV(string filePath)
        {
            using (var writer = new System.IO.StreamWriter(filePath))
            {
                // Header
                writer.Write("Time (s)");
                foreach (var cacheKey in _filteredData.Keys.OrderBy(k => k))
                {
                    writer.Write($",{cacheKey.Split('_')[0]}");
                }
                writer.WriteLine();

                // Find max number of points
                int maxPoints = _filteredData.Values.Max(v => v.Count);

                // Data rows
                for (int i = 0; i < maxPoints; i++)
                {
                    double currentTime = 0;
                    bool first = true;

                    foreach (var cacheKey in _filteredData.Keys.OrderBy(k => k))
                    {
                        var data = _filteredData[cacheKey];
                        if (i < data.Count)
                        {
                            if (first)
                            {
                                currentTime = data[i].t;
                                writer.Write(currentTime.ToString("G17"));
                                first = false;
                            }
                            writer.Write($",{data[i].v:G17}");
                        }
                        else
                        {
                            writer.Write(",");
                        }
                    }
                    writer.WriteLine();
                }
            }
        }

        private ScottPlot.Color ToPlotColor(System.Drawing.Color c) => new(c.R, c.G, c.B, c.A);

        private static List<System.Drawing.Color> GetDefaultColors()
        {
            return new List<System.Drawing.Color>
            {
                System.Drawing.Color.Blue,
                System.Drawing.Color.Red,
                System.Drawing.Color.Green,
                System.Drawing.Color.Orange,
                System.Drawing.Color.Purple,
                System.Drawing.Color.Brown,
                System.Drawing.Color.Pink,
                System.Drawing.Color.Olive,
                System.Drawing.Color.Cyan,
                System.Drawing.Color.Magenta
            };
        }
    }
}
