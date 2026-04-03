using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using ScottPlot;
using ScottPlot.WinForms;

namespace DragonScope
{
    public partial class PlotForm : Form
    {
        private readonly Dictionary<string, List<(double t, double v)>> _series = new(StringComparer.OrdinalIgnoreCase);
        private readonly List<ParsedCondition> _conditions = new();
        private List<string> _plottedKeys = new();
        private ScottPlot.Plottables.Crosshair? _crosshair;

        public PlotForm()
        {
            InitializeComponent();
            formsPlot.Plot.Title("CSV Data");
            formsPlot.Plot.XLabel("Time (s from RobotEnable)");
            formsPlot.Plot.YLabel("Value");
            formsPlot.Plot.HideLegend();
            formsPlot.MouseMove += FormsPlot_MouseMove;
        }

        private void FormsPlot_MouseMove(object? sender, MouseEventArgs e)
        {
            if (_plottedKeys.Count == 0) return;

            Coordinates mouseCoordinates = formsPlot.Plot.GetCoordinates(e.X, e.Y);

            double minXDist = double.MaxValue;
            double snapX = 0;
            double snapY = 0;
            bool found = false;
            string closestKey = "";

            foreach (var key in _plottedKeys)
            {
                if (!_series.TryGetValue(key, out var pts) || pts.Count == 0) continue;

                int idx = pts.BinarySearch(new (mouseCoordinates.X, 0), Comparer<(double t, double v)>.Create((a, b) => a.t.CompareTo(b.t)));
                if (idx < 0) idx = ~idx;

                for (int i = Math.Max(0, idx - 1); i <= Math.Min(pts.Count - 1, idx); i++)
                {
                    double xDist = Math.Abs(pts[i].t - mouseCoordinates.X);
                    if (xDist < minXDist)
                    {
                        minXDist = xDist;
                        snapX = pts[i].t;
                        snapY = pts[i].v;
                        closestKey = key;
                        found = true;
                    }
                }
            }

            if (found && _crosshair != null)
            {
                _crosshair.IsVisible = true;
                _crosshair.Position = new Coordinates(snapX, snapY);
                this.Text = $"DragonScope Data Plot : {closestKey} (Time: {snapX:F3}, Value: {snapY:F5})";
                formsPlot.Refresh();
            }
            else if (_crosshair != null)
            {
                _crosshair.IsVisible = false;
                this.Text = "DragonScope Data Plot";
                formsPlot.Refresh();
            }
        }

        public void UpdateData(Dictionary<string, List<(double t, double v)>> series, IReadOnlyList<ParsedCondition> conditions)
        {
            _series.Clear();
            foreach (var kv in series)
                _series[kv.Key] = kv.Value;

            _conditions.Clear();
            _conditions.AddRange(conditions);

            PopulateSelectors();
            RenderPlot(null, EventArgs.Empty);
            formsPlot.Plot.HideLegend();

        }

        private void PopulateSelectors()
        {
            seriesList.BeginUpdate();
            var prevChecked = seriesList.CheckedItems.Cast<string>().ToHashSet(StringComparer.OrdinalIgnoreCase);
            seriesList.Items.Clear();
            foreach (var key in _series.Keys.OrderBy(k => k, StringComparer.OrdinalIgnoreCase))
            {
                int idx = seriesList.Items.Add(key);
                if (prevChecked.Contains(key))
                    seriesList.SetItemChecked(idx, true);
            }
            if (seriesList.CheckedItems.Count == 0)
            {
                for (int i = 0; i < Math.Min(2, seriesList.Items.Count); i++)
                    seriesList.SetItemChecked(i, true);
            }
            seriesList.EndUpdate();

            cmbSeriesPicker.BeginUpdate();
            var selected = cmbSeriesPicker.SelectedItem as string;
            cmbSeriesPicker.Items.Clear();
            foreach (var key in _series.Keys.OrderBy(k => k, StringComparer.OrdinalIgnoreCase))
                cmbSeriesPicker.Items.Add(key);
            cmbSeriesPicker.EndUpdate();
            if (selected != null && cmbSeriesPicker.Items.Contains(selected))
                cmbSeriesPicker.SelectedItem = selected;
            else if (cmbSeriesPicker.Items.Count > 0 && cmbSeriesPicker.SelectedIndex < 0)
                cmbSeriesPicker.SelectedIndex = 0;
        }

        private void btnPlotSelected_Click(object? sender, EventArgs e)
        {
            if (cmbSeriesPicker.SelectedItem is not string sel) return;
            int idx = seriesList.Items.IndexOf(sel);
            if (idx >= 0 && !seriesList.GetItemChecked(idx))
                seriesList.SetItemChecked(idx, true);
            RenderPlot(sender!, EventArgs.Empty);
        }

        private void seriesList_ItemCheck(object? sender, ItemCheckEventArgs e)
        {
            BeginInvoke((Action)(() => RenderPlot(sender!, EventArgs.Empty)));
        }

        private ScottPlot.Color ToPlotColor(System.Drawing.Color c) => new(c.R, c.G, c.B, c.A);

        // Fully qualify System.Drawing.Color to avoid ambiguity with ScottPlot.Color
        private static System.Drawing.Color PriorityColor(int priority, int alpha = 200) => priority switch
        {
            1 => System.Drawing.Color.FromArgb(alpha, System.Drawing.Color.Red),
            2 => System.Drawing.Color.FromArgb(alpha, System.Drawing.Color.Orange),
            3 => System.Drawing.Color.FromArgb(alpha, System.Drawing.Color.Goldenrod),
            4 => System.Drawing.Color.FromArgb(alpha, System.Drawing.Color.Purple),
            _ => System.Drawing.Color.FromArgb(alpha, System.Drawing.Color.Gray),
        };

        private void RenderPlot(object? sender, EventArgs e)
        {
            formsPlot.Plot.Clear();

            var checkedKeys = seriesList.CheckedItems.Cast<string>().ToList();
            _plottedKeys = checkedKeys;
            
            foreach (var key in checkedKeys)
            {
                if (!_series.TryGetValue(key, out var pts) || pts.Count == 0)
                    continue;

                double[] xs = new double[pts.Count];
                double[] ys = new double[pts.Count];
                for (int i = 0; i < pts.Count; i++)
                {
                    xs[i] = pts[i].t;
                    ys[i] = pts[i].v;
                }

                var scatter = formsPlot.Plot.Add.Scatter(xs, ys);
                scatter.LegendText = key;
                scatter.LineWidth = 1.5f;
            }

            if (chkShowErrors.Checked && _conditions.Count > 0)
            {
                if (chkGroupErrorSpans.Checked)
                {
                    var grouped = _conditions.Where(c => c.End.HasValue).GroupBy(c => c.Priority);
                    foreach (var grp in grouped)
                    {
                        var drawColor = PriorityColor(grp.Key, alpha: 50);
                        foreach (var c in grp)
                        {
                            var span = formsPlot.Plot.Add.VerticalSpan(c.Start, c.End!.Value);
                            span.FillColor = ToPlotColor(drawColor);
                        }
                    }
                    foreach (var c in _conditions.Where(c => !c.End.HasValue))
                    {
                        var vline = formsPlot.Plot.Add.VerticalLine(c.Start);
                        vline.Color = ToPlotColor(PriorityColor(c.Priority));
                    }
                }
                else
                {
                    foreach (var c in _conditions)
                    {
                        var baseColor = PriorityColor(c.Priority, alpha: c.End.HasValue ? 40 : 180);
                        if (c.End.HasValue)
                        {
                            var span = formsPlot.Plot.Add.VerticalSpan(c.Start, c.End.Value);
                            span.FillColor = ToPlotColor(baseColor);
                        }
                        else
                        {
                            var vline = formsPlot.Plot.Add.VerticalLine(c.Start);
                            vline.Color = ToPlotColor(baseColor);
                        }
                    }
                }
            }

            _crosshair = formsPlot.Plot.Add.Crosshair(0, 0);
            _crosshair.IsVisible = false;

            formsPlot.Plot.Legend.IsVisible = checkedKeys.Count > 0;
            if (formsPlot.Plot.Legend.IsVisible)
                formsPlot.Plot.Legend.Alignment = Alignment.UpperLeft;

            formsPlot.Plot.Axes.AutoScale();
            formsPlot.Refresh();
        }
    }
}