using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using ScottPlot;
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
            this.components = new System.ComponentModel.Container();
            this.AutoScaleMode = AutoScaleMode.Font;
            this.ClientSize = new Size(1200, 700);
            this.Text = "Multi-Cache Motor Analysis";
            this.Name = "MultiCacheMotorAnalysisForm";
            this.StartPosition = FormStartPosition.CenterParent;

            // Top panel for controls
            var topPanel = new Panel
            {
                Dock = DockStyle.Top,
                Height = 120,
                BorderStyle = BorderStyle.FixedSingle
            };
            this.Controls.Add(topPanel);

            // Row 1: Cache selection
            var lblCaches = new System.Windows.Forms.Label
            {
                Text = "Select Caches:",
                Location = new Point(12, 12),
                AutoSize = true
            };
            topPanel.Controls.Add(lblCaches);

            var cacheListBox = new CheckedListBox
            {
                Name = "cacheListBox",
                Location = new Point(120, 12),
                Width = 350,
                Height = 95
            };
            cacheListBox.ItemCheck += CacheListBox_ItemCheck;
            topPanel.Controls.Add(cacheListBox);

            // Row 1: Series selection
            var lblSeries = new System.Windows.Forms.Label
            {
                Text = "Motor Current Signal:",
                Location = new Point(480, 12),
                AutoSize = true
            };
            topPanel.Controls.Add(lblSeries);

            var seriesCombo = new ComboBox
            {
                Name = "seriesCombo",
                Location = new Point(620, 12),
                Width = 300,
                Height = 23,
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            seriesCombo.SelectedIndexChanged += SeriesCombo_SelectedIndexChanged;
            topPanel.Controls.Add(seriesCombo);

            // Row 2: Analyze button
            var btnAnalyze = new Button
            {
                Text = "Analyze & Plot",
                Location = new Point(480, 45),
                Width = 100,
                Height = 23
            };
            btnAnalyze.Click += BtnAnalyze_Click;
            topPanel.Controls.Add(btnAnalyze);

            var btnExport = new Button
            {
                Text = "Export Data",
                Location = new Point(590, 45),
                Width = 100,
                Height = 23
            };
            btnExport.Click += BtnExport_Click;
            topPanel.Controls.Add(btnExport);

            // Row 2: Color customization
            var lblColors = new System.Windows.Forms.Label
            {
                Text = "Colors:",
                Location = new Point(480, 75),
                AutoSize = true
            };
            topPanel.Controls.Add(lblColors);

            var colorPanel = new FlowLayoutPanel
            {
                Name = "colorPanel",
                Location = new Point(540, 70),
                Width = 380,
                Height = 35,
                AutoScroll = true
            };
            topPanel.Controls.Add(colorPanel);

            // Main plot area
            var formsPlot = new FormsPlot
            {
                Name = "formsPlot",
                Dock = DockStyle.Fill
            };
            formsPlot.Plot.Title("Motor Current Analysis (Robot Enabled Only)");
            formsPlot.Plot.XLabel("Time (s from First Enable)");
            formsPlot.Plot.YLabel("Current (A)");
            this.Controls.Add(formsPlot);
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

            // Find the earliest robot enable time across all caches
            double? globalMinTime = null;

            foreach (var kvp in _selectedCaches)
            {
                var cacheKey = kvp.Key;
                var (analysis, _) = kvp.Value;

                var currentSeries = analysis.CsvSeries.FirstOrDefault(s =>
                    s.Key.Equals(motorCurrentSignal, StringComparison.OrdinalIgnoreCase)).Value;

                if (currentSeries == null) continue;

                // Extract RobotEnable data
                var robotEnableData = new List<(double t, double v)>();
                var enableKey = analysis.CsvSeries.Keys.FirstOrDefault(k =>
                    k.Contains("RobotEnable", StringComparison.OrdinalIgnoreCase));

                if (enableKey != null && analysis.CsvSeries.TryGetValue(enableKey, out var enableData))
                {
                    robotEnableData = new List<(double t, double v)>(enableData);
                }

                // Filter current data by robot-enabled state and normalize time
                var filtered = new List<(double t, double v)>();
                double? minEnabledTime = null;

                foreach (var point in currentSeries)
                {
                    if (IsRobotEnabled(point.t, robotEnableData))
                    {
                        if (minEnabledTime == null)
                            minEnabledTime = point.t;

                        filtered.Add(point);
                    }
                }

                if (minEnabledTime.HasValue)
                {
                    if (globalMinTime == null || minEnabledTime < globalMinTime)
                        globalMinTime = minEnabledTime;

                    _filteredData[cacheKey] = filtered;
                }
            }

            // Normalize all times to start from global minimum
            if (globalMinTime.HasValue)
            {
                var normalized = new Dictionary<string, List<(double t, double v)>>();
                foreach (var kvp in _filteredData)
                {
                    normalized[kvp.Key] = kvp.Value.Select(p => (p.t - globalMinTime.Value, p.v)).ToList();
                }
                _filteredData.Clear();
                foreach (var kvp in normalized)
                {
                    _filteredData[kvp.Key] = kvp.Value;
                }
            }
        }

        private bool IsRobotEnabled(double time, List<(double t, double v)> robotEnableData)
        {
            if (robotEnableData.Count == 0)
                return true;

            var enableAtTime = robotEnableData.LastOrDefault(e => e.t <= time);
            if (enableAtTime == default)
                return false;

            return enableAtTime.v > 0.5;
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
    }
}
