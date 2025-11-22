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
        private readonly List<string> _filteredKeys = new();
        private string _lastSearch = "";

        public PlotForm()
        {
            InitializeComponent();
            formsPlot.Plot.Title("CSV Data");
            formsPlot.Plot.XLabel("Time (s from RobotEnable)");
            formsPlot.Plot.YLabel("Value");
            formsPlot.Refresh();
        }

        public void UpdateData(Dictionary<string, List<(double t, double v)>> series, IReadOnlyList<ParsedCondition> conditions)
        {
            _series.Clear();
            foreach (var kv in series)
                _series[kv.Key] = kv.Value;

            _conditions.Clear();
            _conditions.AddRange(conditions);

            RefreshSeriesList(keepSelection: true);
            RenderPlot(null, EventArgs.Empty);
        }

        private void txtSearch_TextChanged(object? sender, EventArgs e)
        {
            _lastSearch = txtSearch.Text ?? "";
            RefreshSeriesList(keepSelection: true);
        }

        private void btnSelectAll_Click(object? sender, EventArgs e)
        {
            lstSeries.BeginUpdate();
            for (int i = 0; i < lstSeries.Items.Count; i++)
                lstSeries.SetSelected(i, true);
            lstSeries.EndUpdate();
            RenderPlot(null, EventArgs.Empty);
        }

        private void btnClearSelection_Click(object? sender, EventArgs e)
        {
            lstSeries.ClearSelected();
            RenderPlot(null, EventArgs.Empty);
        }

        private IEnumerable<string> GetSelectedKeys()
        {
            return lstSeries.SelectedItems.Cast<string>();
        }

        private void lstSeries_SelectedIndexChanged(object? sender, EventArgs e)
        {
            // Live re-render on selection changes
            RenderPlot(null, EventArgs.Empty);
        }

        private void RefreshSeriesList(bool keepSelection)
        {
            var prevSelected = keepSelection
                ? GetSelectedKeys().ToHashSet(StringComparer.OrdinalIgnoreCase)
                : new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            string term = _lastSearch.Trim();
            IEnumerable<string> keys = _series.Keys;

            if (!string.IsNullOrEmpty(term))
                keys = keys.Where(k => k.Contains(term, StringComparison.OrdinalIgnoreCase));

            _filteredKeys.Clear();
            _filteredKeys.AddRange(keys.OrderBy(k => k, StringComparer.OrdinalIgnoreCase));

            lstSeries.BeginUpdate();
            lstSeries.Items.Clear();
            foreach (var k in _filteredKeys)
            {
                int index = lstSeries.Items.Add(k);
                if (prevSelected.Contains(k))
                    lstSeries.SetSelected(index, true);
            }
            lstSeries.EndUpdate();

            // Auto-select first couple if none selected
            if (lstSeries.SelectedItems.Count == 0 && lstSeries.Items.Count > 0)
            {
                for (int i = 0; i < Math.Min(2, lstSeries.Items.Count); i++)
                    lstSeries.SetSelected(i, true);
            }
        }

        private ScottPlot.Color ToPlotColor(System.Drawing.Color c) => new(c.R, c.G, c.B, c.A);

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

            var selectedKeys = GetSelectedKeys().ToList();
            double? xMin = null, xMax = null, yMin = null, yMax = null;

            foreach (var key in selectedKeys)
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

                double lxMin = xs.Min();
                double lxMax = xs.Max();
                double lyMin = ys.Min();
                double lyMax = ys.Max();

                xMin = xMin.HasValue ? Math.Min(xMin.Value, lxMin) : lxMin;
                xMax = xMax.HasValue ? Math.Max(xMax.Value, lxMax) : lxMax;
                yMin = yMin.HasValue ? Math.Min(yMin.Value, lyMin) : lyMin;
                yMax = yMax.HasValue ? Math.Max(yMax.Value, lyMax) : lyMax;
            }

            if (chkShowErrors.Checked && _conditions.Count > 0)
            {
                foreach (var c in _conditions)
                {
                    double start = c.Start;
                    double end = c.End ?? c.Start;
                    xMin = xMin.HasValue ? Math.Min(xMin.Value, start) : start;
                    xMax = xMax.HasValue ? Math.Max(xMax.Value, end) : end;
                }

                if (chkGroupErrorSpans.Checked)
                {
                    var grouped = _conditions.Where(c => c.End.HasValue).GroupBy(c => c.Priority);
                    foreach (var grp in grouped)
                    {
                        var drawColor = PriorityColor(grp.Key, 50);
                        foreach (var c in grp)
                        {
                            var span = formsPlot.Plot.Add.VerticalSpan(c.Start, c.End!.Value);
                            span.FillColor = ToPlotColor(drawColor);
                        }
                        var legendLine = formsPlot.Plot.Add.Line(0, 0, 0, 0);
                        legendLine.Color = ToPlotColor(drawColor);
                        legendLine.LineWidth = 0;
                        legendLine.LegendText = $"Priority {grp.Key} interval(s)";
                    }
                    foreach (var c in _conditions.Where(c => !c.End.HasValue))
                    {
                        var vline = formsPlot.Plot.Add.VerticalLine(c.Start);
                        vline.Color = ToPlotColor(PriorityColor(c.Priority));
                        vline.LegendText = $"{c.Name} (open)";
                    }
                }
                else
                {
                    foreach (var c in _conditions)
                    {
                        var baseColor = PriorityColor(c.Priority, c.End.HasValue ? 40 : 180);
                        if (c.End.HasValue)
                        {
                            var span = formsPlot.Plot.Add.VerticalSpan(c.Start, c.End.Value);
                            span.FillColor = ToPlotColor(baseColor);
                            span.LegendText = c.Name;
                        }
                        else
                        {
                            var vline = formsPlot.Plot.Add.VerticalLine(c.Start);
                            vline.Color = ToPlotColor(baseColor);
                            vline.LegendText = $"{c.Name} (open)";
                        }
                    }
                }
            }

            if (xMin.HasValue && xMax.HasValue && yMin.HasValue && yMax.HasValue)
            {
                if (xMin.Value == xMax.Value)
                {
                    xMin -= 0.5;
                    xMax += 0.5;
                }
                if (yMin.Value == yMax.Value)
                {
                    double padSingle = (yMin.Value == 0 && yMax.Value == 1) ? 0.05 : 0.5;
                    yMin -= padSingle;
                    yMax += padSingle;
                }

                double xr = xMax.Value - xMin.Value;
                double yr = yMax.Value - yMin.Value;
                double xp = xr * 0.05;
                double yp = yr * 0.05;

                formsPlot.Plot.Axes.SetLimits(
                    xMin.Value - xp,
                    xMax.Value + xp,
                    yMin.Value - yp,
                    yMax.Value + yp);
            }

            formsPlot.Plot.Legend.IsVisible = selectedKeys.Count > 0 || (chkShowErrors.Checked && _conditions.Count > 0);
            if (formsPlot.Plot.Legend.IsVisible)
                formsPlot.Plot.Legend.Alignment = Alignment.UpperLeft;

            formsPlot.Refresh();
        }
    }
}