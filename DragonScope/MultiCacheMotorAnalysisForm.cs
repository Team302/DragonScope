using ScottPlot.WinForms;

namespace DragonScope
{
    public partial class MultiCacheMotorAnalysisForm : Form
    {
        private readonly DataCacheManager _cacheManager;
        private readonly Dictionary<string, (CachedAnalysis analysis, System.Drawing.Color color)> _selectedCaches = new();
        private readonly Dictionary<string, List<(double t, double v)>> _filteredData = new();

        public MultiCacheMotorAnalysisForm(DataCacheManager cacheManager)
        {
            _cacheManager = cacheManager ?? throw new ArgumentNullException(nameof(cacheManager));
            InitializeComponent();
            LoadCacheList();
        }

        private void InitializeComponent()
        {
            topPanel = new Panel();
            lblSeries = new Label();
            btnAnalyze = new Button();
            btnExport = new Button();
            lblColors = new Label();
            colorPanel = new FlowLayoutPanel();
            lblCaches = new Label();
            cacheListBox = new CheckedListBox();
            seriesCombo = new ComboBox();
            formsPlot = new FormsPlot();
            SuspendLayout();
            // 
            // topPanel
            // 
            topPanel.Location = new Point(12, 168);
            topPanel.Name = "topPanel";
            topPanel.Size = new Size(200, 123);
            topPanel.TabIndex = 0;
            // 
            // lblSeries
            // 
            lblSeries.Location = new Point(156, 125);
            lblSeries.Name = "lblSeries";
            lblSeries.Size = new Size(100, 23);
            lblSeries.TabIndex = 2;
            // 
            // btnAnalyze
            // 
            btnAnalyze.Location = new Point(313, 105);
            btnAnalyze.Name = "btnAnalyze";
            btnAnalyze.Size = new Size(75, 23);
            btnAnalyze.TabIndex = 4;
            btnAnalyze.Text = "Analyze";
            btnAnalyze.Click += BtnAnalyze_Click;
            // 
            // btnExport
            // 
            btnExport.Location = new Point(394, 105);
            btnExport.Name = "btnExport";
            btnExport.Size = new Size(75, 23);
            btnExport.TabIndex = 5;
            btnExport.Text = "Export";
            btnExport.Click += BtnExport_Click;
            // 
            // lblColors
            // 
            lblColors.Location = new Point(185, 102);
            lblColors.Name = "lblColors";
            lblColors.Size = new Size(100, 23);
            lblColors.TabIndex = 6;
            // 
            // colorPanel
            // 
            colorPanel.Location = new Point(227, 168);
            colorPanel.Name = "colorPanel";
            colorPanel.Size = new Size(200, 123);
            colorPanel.TabIndex = 7;
            // 
            // lblCaches
            // 
            lblCaches.Location = new Point(207, 102);
            lblCaches.Name = "lblCaches";
            lblCaches.Size = new Size(100, 23);
            lblCaches.TabIndex = 0;
            // 
            // cacheListBox
            // 
            cacheListBox.Location = new Point(317, 41);
            cacheListBox.Name = "cacheListBox";
            cacheListBox.Size = new Size(134, 58);
            cacheListBox.TabIndex = 1;
            cacheListBox.ItemCheck += CacheListBox_ItemCheck;
            // 
            // seriesCombo
            // 
            seriesCombo.Location = new Point(317, 12);
            seriesCombo.Name = "seriesCombo";
            seriesCombo.Size = new Size(121, 23);
            seriesCombo.TabIndex = 3;
            seriesCombo.SelectedIndexChanged += SeriesCombo_SelectedIndexChanged;
            // 
            // formsPlot
            // 
            formsPlot.DisplayScale = 1F;
            formsPlot.Location = new Point(0, 12);
            formsPlot.Name = "formsPlot";
            formsPlot.Size = new Size(150, 150);
            formsPlot.TabIndex = 1;
            // 
            // MultiCacheMotorAnalysisForm
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(512, 303);
            Controls.Add(lblColors);
            Controls.Add(btnExport);
            Controls.Add(colorPanel);
            Controls.Add(btnAnalyze);
            Controls.Add(lblSeries);
            Controls.Add(lblCaches);
            Controls.Add(topPanel);
            Controls.Add(cacheListBox);
            Controls.Add(seriesCombo);
            Controls.Add(formsPlot);
            Name = "MultiCacheMotorAnalysisForm";
            StartPosition = FormStartPosition.CenterParent;
            Text = "Multi-Cache Motor Analysis";
            ResumeLayout(false);
        }

        private void LoadCacheList()
        {
            var cacheListBox = this.Controls.Find("cacheListBox", true).FirstOrDefault() as CheckedListBox;
            if (cacheListBox == null) return;

            var allCaches = _cacheManager.GetAllCachedAnalyses();
            cacheListBox.Items.Clear();

            foreach (var cache in allCaches.OrderByDescending(c => c.CachedAt))
            {
                cacheListBox.Items.Add($"{cache.FileName} ({cache.CachedAt:g})", false);
            }

            PopulateSeriesList();
        }

        private void PopulateSeriesList()
        {
            var seriesCombo = this.Controls.Find("seriesCombo", true).FirstOrDefault() as ComboBox;
            if (seriesCombo == null) return;

            var allMotorSeries = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            // Collect all motor current series from all caches
            var allCaches = _cacheManager.GetAllCachedAnalyses();
            foreach (var cache in allCaches)
            {
                var analysis = _cacheManager.LoadAnalysis(cache.CacheId);
                if (analysis?.CsvSeries != null)
                {
                    foreach (var seriesName in analysis.CsvSeries.Keys)
                    {
                        if (seriesName.Contains("Current", StringComparison.OrdinalIgnoreCase) &&
                            seriesName.Contains("Stator", StringComparison.OrdinalIgnoreCase))
                        {
                            allMotorSeries.Add(seriesName);
                        }
                    }
                }
            }

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

            var allCaches = _cacheManager.GetAllCachedAnalyses();
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

        private void BtnAnalyze_Click(object? sender, EventArgs e)
        {
            var cacheListBox = this.Controls.Find("cacheListBox", true).FirstOrDefault() as CheckedListBox;
            var seriesCombo = this.Controls.Find("seriesCombo", true).FirstOrDefault() as ComboBox;

            if (cacheListBox == null || seriesCombo == null) return;

            if (seriesCombo.SelectedItem is not string motorCurrentSignal)
            {
                MessageBox.Show("Please select a motor current signal.", "Selection Required", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            _selectedCaches.Clear();
            _filteredData.Clear();

            var allCaches = _cacheManager.GetAllCachedAnalyses();
            var defaultColors = GetDefaultColors();
            var colorIndex = 0;

            for (int i = 0; i < cacheListBox.Items.Count; i++)
            {
                if (!cacheListBox.GetItemChecked(i)) continue;

                var cacheMetadata = allCaches[i];
                var analysis = _cacheManager.LoadAnalysis(cacheMetadata.CacheId);

                if (analysis?.CsvSeries == null) continue;

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
                    MessageBox.Show($"Warning: Cache '{cacheMetadata.FileName}' appears to be corrupted (all data points are exactly 0). This is caused by loading a cache made before the latest JSON serialization fix.\n\nPlease clear your caches and re-parse your logs to fix this issue.", "Legacy Cache Detected", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                // Try to find exact match or case-insensitive match
                var currentSeries = analysis.CsvSeries.FirstOrDefault(kvp =>
                    kvp.Key.Equals(motorCurrentSignal, StringComparison.OrdinalIgnoreCase)).Value;

                if (currentSeries == null || currentSeries.Count == 0)
                {
                    continue;
                }

                var cacheKey = $"{cacheMetadata.FileName}_{cacheMetadata.CacheId}";
                _selectedCaches[cacheKey] = (analysis, defaultColors[colorIndex % defaultColors.Count]);
                colorIndex++;
            }

            if (_selectedCaches.Count == 0)
            {
                MessageBox.Show("No valid data found for selected signal in chosen caches.", "No Data", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            LoadAndFilterData(motorCurrentSignal);
            RefreshPlot();
            UpdateColorPanel();
        }

        private void LoadAndFilterData(string motorCurrentSignal)
        {
            _filteredData.Clear();

            foreach (var kvp in _selectedCaches)
            {
                var cacheKey = kvp.Key;
                var (analysis, _) = kvp.Value;

                var currentSeries = analysis.CsvSeries.FirstOrDefault(s =>
                    s.Key.Equals(motorCurrentSignal, StringComparison.OrdinalIgnoreCase)).Value;

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

        private System.ComponentModel.IContainer components;

        private Panel topPanel;
        private System.Windows.Forms.Label lblCaches;
        private CheckedListBox cacheListBox;
        private System.Windows.Forms.Label lblSeries;
        private ComboBox seriesCombo;
        private Button btnAnalyze;
        private Button btnExport;
        private System.Windows.Forms.Label lblColors;
        private FlowLayoutPanel colorPanel;
        private FormsPlot formsPlot;
    }
}
