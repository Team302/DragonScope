using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace DragonScope
{
    public partial class MotorStatsForm : Form
    {
        private readonly Dictionary<string, List<(double t, double v)>> _series;

        public MotorStatsForm(Dictionary<string, List<(double t, double v)>> series)
        {
            _series = new Dictionary<string, List<(double t, double v)>>(StringComparer.OrdinalIgnoreCase);
            foreach (var kvp in series)
                _series[kvp.Key] = new List<(double t, double v)>(kvp.Value);

            InitializeComponent();

            // Populate combo box with all available series that contain "current" (case insensitive)
            var motorCurrentSeries = _series.Keys
                .Where(k => k.Contains("StatorCurrent", StringComparison.OrdinalIgnoreCase))
                .OrderBy(k => k, StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (motorCurrentSeries.Count == 0)
            {
                // If no "current" series found, show all available series
                motorCurrentSeries = _series.Keys.OrderBy(k => k, StringComparer.OrdinalIgnoreCase).ToList();
            }

            foreach (var key in motorCurrentSeries)
                seriesComboBox.Items.Add(key);

            if (seriesComboBox.Items.Count > 0)
                seriesComboBox.SelectedIndex = 0;
        }

        private void SeriesComboBox_SelectedIndexChanged(object? sender, EventArgs e)
        {
            CalculateStatistics();
        }

        private void UpdateButton_Click(object? sender, EventArgs e)
        {
            CalculateStatistics();
        }

        private void CalculateStatistics()
        {
            resultsTextBox.Clear();

            if (seriesComboBox.SelectedItem is not string selectedSeries)
            {
                resultsTextBox.SelectionColor = Color.Red;
                resultsTextBox.AppendText("Please select a series.\n");
                return;
            }

            if (!_series.TryGetValue(selectedSeries, out var data) || data.Count == 0)
            {
                resultsTextBox.SelectionColor = Color.Red;
                resultsTextBox.AppendText("No data available for selected series.\n");
                return;
            }

            // Extract values
            var values = data.Select(p => p.v).ToList();

            if (values.Count == 0)
            {
                resultsTextBox.SelectionColor = Color.Red;
                resultsTextBox.AppendText("No values to analyze.\n");
                return;
            }

            // Calculate statistics
            var min = values.Min();
            var max = values.Max();
            var avg = values.Average();
            var median = CalculateMedian(values);
            var stdDev = CalculateStandardDeviation(values, avg);
            var rms = Math.Sqrt(values.Average(v => v * v));

            // Count zero and negative values
            var zeroCount = values.Count(v => v == 0);
            var negativeCount = values.Count(v => v < 0);
            var positiveCount = values.Count(v => v > 0);

            // Time information
            var times = data.Select(p => p.t).ToList();
            var minTime = times.Min();
            var maxTime = times.Max();
            var duration = maxTime - minTime;

            // Find when min/max occurred
            var minIndex = data.FindIndex(p => p.v == min);
            var maxIndex = data.FindIndex(p => p.v == max);
            var minTimeValue = minIndex >= 0 ? data[minIndex].t : 0;
            var maxTimeValue = maxIndex >= 0 ? data[maxIndex].t : 0;

            // Append results
            resultsTextBox.SelectionColor = Color.DarkBlue;
            resultsTextBox.AppendText($"Series: {selectedSeries}\n");
            resultsTextBox.SelectionColor = Color.Black;
            resultsTextBox.AppendText($"Data Points: {values.Count}\n\n");

            resultsTextBox.SelectionColor = Color.DarkGreen;
            resultsTextBox.AppendText("=== Time Information ===\n");
            resultsTextBox.SelectionColor = Color.Black;
            resultsTextBox.AppendText($"Start Time:        {minTime:F3} s\n");
            resultsTextBox.AppendText($"End Time:          {maxTime:F3} s\n");
            resultsTextBox.AppendText($"Duration:          {duration:F3} s\n\n");

            resultsTextBox.SelectionColor = Color.DarkGreen;
            resultsTextBox.AppendText("=== Current Statistics ===\n");
            resultsTextBox.SelectionColor = Color.Black;
            resultsTextBox.AppendText($"Minimum Current:   {min:F6} A (at t={minTimeValue:F3} s)\n");
            resultsTextBox.AppendText($"Maximum Current:   {max:F6} A (at t={maxTimeValue:F3} s)\n");
            resultsTextBox.AppendText($"Average Current:   {avg:F6} A\n");
            resultsTextBox.AppendText($"Median Current:    {median:F6} A\n");
            resultsTextBox.AppendText($"Std Deviation:     {stdDev:F6} A\n");
            resultsTextBox.AppendText($"RMS Current:       {rms:F6} A\n\n");

            resultsTextBox.SelectionColor = Color.DarkGreen;
            resultsTextBox.AppendText("=== Value Distribution ===\n");
            resultsTextBox.SelectionColor = Color.Black;
            resultsTextBox.AppendText($"Positive Values:   {positiveCount} ({(positiveCount * 100.0 / values.Count):F1}%)\n");
            resultsTextBox.AppendText($"Negative Values:   {negativeCount} ({(negativeCount * 100.0 / values.Count):F1}%)\n");
            resultsTextBox.AppendText($"Zero Values:       {zeroCount} ({(zeroCount * 100.0 / values.Count):F1}%)\n\n");

            // Calculate percentiles
            resultsTextBox.SelectionColor = Color.DarkGreen;
            resultsTextBox.AppendText("=== Percentiles ===\n");
            resultsTextBox.SelectionColor = Color.Black;
            var sortedValues = values.OrderBy(v => v).ToList();
            var p25 = GetPercentile(sortedValues, 25);
            var p50 = GetPercentile(sortedValues, 50);
            var p75 = GetPercentile(sortedValues, 75);
            var p90 = GetPercentile(sortedValues, 90);
            var p95 = GetPercentile(sortedValues, 95);
            var p99 = GetPercentile(sortedValues, 99);

            resultsTextBox.AppendText($"25th Percentile:   {p25:F6} A\n");
            resultsTextBox.AppendText($"50th Percentile:   {p50:F6} A\n");
            resultsTextBox.AppendText($"75th Percentile:   {p75:F6} A\n");
            resultsTextBox.AppendText($"90th Percentile:   {p90:F6} A\n");
            resultsTextBox.AppendText($"95th Percentile:   {p95:F6} A\n");
            resultsTextBox.AppendText($"99th Percentile:   {p99:F6} A\n");
        }

        private static double CalculateMedian(List<double> values)
        {
            var sorted = values.OrderBy(v => v).ToList();
            int count = sorted.Count;
            if (count % 2 == 0)
                return (sorted[count / 2 - 1] + sorted[count / 2]) / 2.0;
            return sorted[count / 2];
        }

        private static double CalculateStandardDeviation(List<double> values, double mean)
        {
            if (values.Count <= 1)
                return 0;
            double variance = values.Average(v => Math.Pow(v - mean, 2));
            return Math.Sqrt(variance);
        }

        private static double GetPercentile(List<double> sortedValues, double percentile)
        {
            int count = sortedValues.Count;
            double index = (percentile / 100.0) * (count - 1);
            int lower = (int)Math.Floor(index);
            int upper = (int)Math.Ceiling(index);

            if (lower == upper)
                return sortedValues[lower];

            double weight = index - lower;
            return sortedValues[lower] * (1 - weight) + sortedValues[upper] * weight;
        }
    }
}
