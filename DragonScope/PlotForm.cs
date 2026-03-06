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

            PopulateSelectors();
            RenderPlot(null, EventArgs.Empty);
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
                        var baseColor = PriorityColor(c.Priority, alpha: c.End.HasValue ? 40 : 180);
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

            formsPlot.Plot.Legend.IsVisible = checkedKeys.Count > 0 || (chkShowErrors.Checked && _conditions.Count > 0);
            if (formsPlot.Plot.Legend.IsVisible)
                formsPlot.Plot.Legend.Alignment = Alignment.UpperLeft;

            formsPlot.Refresh();
        }
    }
}