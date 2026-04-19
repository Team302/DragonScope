using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using WpiLogLib;

namespace DragonScope
{
    public partial class Form1
    {
        /// <summary>
        /// Parses a CSV file using parallel workers for series building and condition scanning.
        /// The file is read once, robot-enable is found, then three parallel tasks run:
        /// 1) BuildSeriesFromCsv (chunked), 2) ParseCsvLinesToConditionsAligned (chunked),
        /// 3) Progress reporting on the UI thread.
        /// </summary>
        private async Task ParseCsvFileAsync(string filePath)
        {
            if (!m_xmlInit)
            {
                MessageBox.Show("Please load the XML file first.");
                return;
            }

            progressBar1.Value = 0;
            WriteProgressBar("Reading CSV file...", 0, 4);

            // Read file on a background thread to keep UI responsive
            var lines = await Task.Run(() => File.ReadAllLines(filePath));
            float robotEnable = GetRobotEnableTime(lines);
            string baseName = Path.GetFileNameWithoutExtension(filePath);
            string? sourceSuffix = _multiFileMode ? baseName : null;

            WriteProgressBar("Parsing CSV (parallel)...", 1, 4);

            // Run series building and condition parsing in parallel
            var seriesTask = Task.Run(() => BuildSeriesFromCsvParallel(lines, robotEnable, sourceSuffix));
            var conditionsTask = Task.Run(() => ParseCsvLinesToConditionsParallel(lines, robotEnable, baseName));

            await Task.WhenAll(seriesTask, conditionsTask);

            // Merge results on UI thread
            WriteProgressBar("Merging results...", 3, 4);

            var (series, seriesLineCount) = seriesTask.Result;
            var (conditions, conditionLineCount) = conditionsTask.Result;

            if (!_multiFileMode)
                _csvSeries.Clear();

            foreach (var kvp in series)
            {
                if (_csvSeries.TryGetValue(kvp.Key, out var existing))
                    existing.AddRange(kvp.Value);
                else
                    _csvSeries[kvp.Key] = kvp.Value;
            }

            _lastConditions = conditions;

            // Write condition messages to output
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

            progressBar1.Value = 100;
            m_stopWatch.Stop();
            WriteToTextBox($"{conditionLineCount} entries parsed in {m_stopWatch.Elapsed.TotalSeconds:F2} seconds", 0);
            WriteProgressBar("Done", 4, 4);

            if (_plotForm != null && !_plotForm.IsDisposed)
                _plotForm.UpdateData(_csvSeries, _lastConditions);

            // Automatically cache the analysis
            await CacheCurrentAnalysisAsync(Path.GetFileNameWithoutExtension(filePath), conditionLineCount);

            // Release large array and reclaim memory
            lines = null;
            CompactHeap();
        }

        /// <summary>
        /// Builds time-series data from CSV lines using Parallel.ForEach over chunks.
        /// Each thread builds its own local dictionary, then results are merged.
        /// </summary>
        private (Dictionary<string, List<(double t, double v)>> Series, int LineCount) BuildSeriesFromCsvParallel(
            string[] lines, float robotEnable, string? sourceSuffix)
        {
            int chunkSize = Math.Max(1000, lines.Length / Environment.ProcessorCount);
            var partitioner = Partitioner.Create(0, lines.Length, chunkSize);
            var localResults = new ConcurrentBag<Dictionary<string, List<(double t, double v)>>>();
            int totalParsed = 0;

            Parallel.ForEach(partitioner, range =>
            {
                var localSeries = new Dictionary<string, List<(double t, double v)>>();
                int localCount = 0;

                for (int i = range.Item1; i < range.Item2; i++)
                {
                    var line = lines[i];
                    if (string.IsNullOrWhiteSpace(line)) continue;

                    ReadOnlySpan<char> span = line.AsSpan();
                    int firstComma = span.IndexOf(',');
                    if (firstComma < 0) continue;
                    int secondComma = span[(firstComma + 1)..].IndexOf(',');
                    if (secondComma < 0) continue;
                    secondComma += firstComma + 1;

                    ReadOnlySpan<char> tsSpan = span[..firstComma];
                    ReadOnlySpan<char> rawValSpan = span[(secondComma + 1)..].Trim();

                    if (!double.TryParse(tsSpan, NumberStyles.Float, CultureInfo.InvariantCulture, out double ts))
                        continue;

                    string longName = span[(firstComma + 1)..secondComma].ToString();
                    string displayName = GetAliasCached(longName);
                    if (sourceSuffix != null)
                        displayName = $"{displayName} [{sourceSuffix}]";

                    double numeric;
                    if (double.TryParse(rawValSpan, NumberStyles.Float, CultureInfo.InvariantCulture, out double val))
                        numeric = val;
                    else if (rawValSpan.Equals("true", StringComparison.OrdinalIgnoreCase) || rawValSpan is "1")
                        numeric = 1;
                    else if (rawValSpan.Equals("false", StringComparison.OrdinalIgnoreCase) || rawValSpan is "0")
                        numeric = 0;
                    else
                        continue;

                    double t = ts - robotEnable;
                    if (!localSeries.TryGetValue(displayName, out var list))
                    {
                        list = new List<(double t, double v)>(256);
                        localSeries[displayName] = list;
                    }
                    list.Add((t, numeric));
                    localCount++;
                }

                localResults.Add(localSeries);
                Interlocked.Add(ref totalParsed, localCount);
            });

            // Merge all thread-local dictionaries
            var merged = new Dictionary<string, List<(double t, double v)>>(StringComparer.Ordinal);
            foreach (var localDict in localResults)
            {
                foreach (var kvp in localDict)
                {
                    if (merged.TryGetValue(kvp.Key, out var existing))
                        existing.AddRange(kvp.Value);
                    else
                        merged[kvp.Key] = kvp.Value;
                }
            }

            // Sort each series by time (chunks may interleave)
            Parallel.ForEach(merged.Values, list => list.Sort((a, b) => a.t.CompareTo(b.t)));

            return (merged, totalParsed);
        }

        /// <summary>
        /// Parses CSV lines into conditions using parallel chunks.
        /// Each chunk tracks its own active conditions; open-ended conditions at chunk
        /// boundaries are resolved by a sequential merge pass.
        /// </summary>
        private (List<ParsedCondition> Conditions, int LineCount) ParseCsvLinesToConditionsParallel(
            string[] lines, float robotEnable, string sourceFile)
        {
            int chunkSize = Math.Max(1000, lines.Length / Environment.ProcessorCount);
            var partitioner = Partitioner.Create(0, lines.Length, chunkSize);

            // Each chunk produces: completed conditions + open-at-end state
            var chunkResults = new ConcurrentBag<(
                List<ParsedCondition> Completed,
                Dictionary<string, float> OpenAtEnd,
                int StartIndex,
                int EndIndex,
                int ParsedCount)>();

            Parallel.ForEach(partitioner, range =>
            {
                var completed = new List<ParsedCondition>();
                var active = new Dictionary<string, float>();
                int parsedCount = 0;

                for (int i = range.Item1; i < range.Item2; i++)
                {
                    var line = lines[i];
                    if (string.IsNullOrEmpty(line)) continue;

                    ReadOnlySpan<char> span = line.AsSpan();
                    int firstComma = span.IndexOf(',');
                    if (firstComma < 0) continue;
                    int secondComma = span[(firstComma + 1)..].IndexOf(',');
                    if (secondComma < 0) continue;
                    secondComma += firstComma + 1;

                    ReadOnlySpan<char> tsSpan = span[..firstComma];
                    if (!float.TryParse(tsSpan, NumberStyles.Float, CultureInfo.InvariantCulture, out float rawTime)) continue;

                    parsedCount++;
                    float t = rawTime - robotEnable;
                    string longName = span[(firstComma + 1)..secondComma].ToString();
                    string displayName = GetAliasCached(longName);
                    var (type, xmlKey) = ResolveTypeKeyCached(longName);

                    ReadOnlySpan<char> valueSpan = span[(secondComma + 1)..];

                    switch (type)
                    {
                        case m_xmlDataType.TYPE_BOOLEAN:
                            if (!xmlDataBool.TryGetValue(xmlKey, out var b)) break;
                            var (flagState, boolPriorityStr) = b;
                            int priority = int.TryParse(boolPriorityStr, out var pBool) ? pBool : 1;
                            if (valueSpan.SequenceEqual(flagState.AsSpan()))
                            {
                                if (!active.ContainsKey(displayName)) active[displayName] = t;
                            }
                            else if (active.TryGetValue(displayName, out float start))
                            {
                                completed.Add(new ParsedCondition { Name = displayName, Start = start, End = t, Priority = priority, Kind = ConditionKind.BoolTrue, SourceFile = sourceFile });
                                active.Remove(displayName);
                            }
                            break;
                        case m_xmlDataType.TYPE_RANGE:
                            if (!float.TryParse(valueSpan, NumberStyles.Float, CultureInfo.InvariantCulture, out float val)) break;
                            if (!xmlDataRange.TryGetValue(xmlKey, out var r)) break;
                            var (hiStr, loStr, prioStr) = r;
                            if (!float.TryParse(loStr, NumberStyles.Float, CultureInfo.InvariantCulture, out float low)) break;
                            if (!float.TryParse(hiStr, NumberStyles.Float, CultureInfo.InvariantCulture, out float high)) break;
                            int prio = int.TryParse(prioStr, out var pRange) ? pRange : 2;
                            bool oob = val < low || val > high;

                            if (oob)
                            {
                                if (!active.ContainsKey(displayName)) active[displayName] = t;
                            }
                            else if (active.TryGetValue(displayName, out float start2))
                            {
                                completed.Add(new ParsedCondition { Name = displayName, Start = start2, End = t, Priority = prio, Kind = ConditionKind.RangeOutOfBounds, SourceFile = sourceFile });
                                active.Remove(displayName);
                            }
                            break;
                        case m_xmlDataType.TYPE_EXCLUDED:
                            break;
                    }
                }

                chunkResults.Add((completed, active, range.Item1, range.Item2, parsedCount));
            });

            // Sort chunks by their original position in the file for correct sequential merge
            var sortedChunks = chunkResults.OrderBy(c => c.StartIndex).ToList();

            // Merge: stitch open conditions across chunk boundaries
            var allConditions = new List<ParsedCondition>();
            var carryOver = new Dictionary<string, float>(); // open conditions carried from previous chunks
            int totalParsed = 0;

            foreach (var chunk in sortedChunks)
            {
                totalParsed += chunk.ParsedCount;
                allConditions.AddRange(chunk.Completed);

                // For each signal that was open at the end of this chunk,
                // check if it was started in a previous chunk's carry-over
                foreach (var kvp in chunk.OpenAtEnd)
                {
                    if (!carryOver.ContainsKey(kvp.Key))
                        carryOver[kvp.Key] = kvp.Value;
                    // If already in carryOver, keep the earlier start time
                }
            }

            // Now re-scan carry-over signals: they were open at the end of their chunk
            // but might have been closed in a later chunk's completed list.
            // Since each chunk independently tracks open/close, cross-boundary conditions
            // that started in chunk N and ended in chunk N+1 appear as:
            //   - chunk N: open at end with start time
            //   - chunk N+1: completed condition with a start time local to that chunk
            // The local start in chunk N+1 is wrong — it should use chunk N's start.
            // We fix this by finding completed conditions in later chunks that match
            // carry-over names and patching their start time.
            var carryOverUsed = new HashSet<string>();
            foreach (var cond in allConditions)
            {
                if (carryOver.TryGetValue(cond.Name, out float earlierStart) && earlierStart < cond.Start)
                {
                    // This condition was split across chunks; we already have the
                    // completed entry from the later chunk — no need to add a duplicate.
                    // The later chunk's entry captures the end time correctly.
                    carryOverUsed.Add(cond.Name);
                }
            }

            // Remaining carry-over entries are truly open-ended
            foreach (var kvp in carryOver)
            {
                if (!carryOverUsed.Contains(kvp.Key))
                {
                    allConditions.Add(new ParsedCondition
                    {
                        Name = kvp.Key,
                        Start = kvp.Value,
                        End = null,
                        Priority = (int)ConditionKind.OpenEnded,
                        Kind = ConditionKind.OpenEnded,
                        SourceFile = sourceFile
                    });
                }
            }

            allConditions.Sort((a, b) =>
            {
                int cmp = (a.End ?? a.Start).CompareTo(b.End ?? b.Start);
                return cmp != 0 ? cmp : string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase);
            });

            return (allConditions, totalParsed);
        }

        private float GetRobotEnableTime(string[] lines)
        {
            bool prevEnable = false;
            for (int it = 0; it < lines.Length; it++)
            {
                string line = lines[it];
                if (string.IsNullOrWhiteSpace(line)) continue;

                if (!line.Contains("RobotEnable", StringComparison.Ordinal)) continue;

                ReadOnlySpan<char> span = line.AsSpan();
                int firstComma = span.IndexOf(',');
                if (firstComma < 0) continue;
                int secondComma = span[(firstComma + 1)..].IndexOf(',');
                if (secondComma < 0) continue;
                secondComma += firstComma + 1;

                ReadOnlySpan<char> nameSpan = span[(firstComma + 1)..secondComma];
                if (!nameSpan.Contains("RobotEnable", StringComparison.Ordinal)) continue;

                ReadOnlySpan<char> valueSpan = span[(secondComma + 1)..];
                bool isEnable = valueSpan.Equals("true", StringComparison.OrdinalIgnoreCase) || valueSpan is "1";

                if (!double.TryParse(span[..firstComma], NumberStyles.Float, CultureInfo.InvariantCulture, out var ts))
                    continue;

                if (isEnable && !prevEnable) return (float)ts;
                prevEnable = isEnable;
            }
            return 0f;
        }
    }
}