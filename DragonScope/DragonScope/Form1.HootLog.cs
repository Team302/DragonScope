using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using WpiLogLib;

namespace DragonScope
{
    public partial class Form1
    {
        private bool TryConvertHootToWpi(string hootLogPath, string wpilogPath, out string diagnostic)
        {
            var sb = new StringBuilder();
            diagnostic = "";
            try
            {
                if (string.IsNullOrWhiteSpace(m_owletExecutablePath) || !File.Exists(m_owletExecutablePath))
                {
                    diagnostic = "Owlet executable path is not set or missing.";
                    return false;
                }
                if (!File.Exists(hootLogPath))
                {
                    diagnostic = $"Input hoot file not found: {hootLogPath}";
                    return false;
                }

                Directory.CreateDirectory(Path.GetDirectoryName(wpilogPath)!);
                string arguments = $"-f wpilog -F \"{hootLogPath}\" \"{wpilogPath}\"";
                sb.AppendLine($"[Owlet] Executing: {m_owletExecutablePath} {arguments}");

                using var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = m_owletExecutablePath,
                        Arguments = arguments,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    },
                    EnableRaisingEvents = true
                };

                var stdOut = new StringBuilder();
                var stdErr = new StringBuilder();
                using var outputWait = new ManualResetEvent(false);
                using var errorWait = new ManualResetEvent(false);

                process.OutputDataReceived += (_, e) =>
                {
                    if (e.Data == null) outputWait.Set();
                    else stdOut.AppendLine(e.Data);
                };
                process.ErrorDataReceived += (_, e) =>
                {
                    if (e.Data == null) errorWait.Set();
                    else stdErr.AppendLine(e.Data);
                };

                if (!process.Start())
                {
                    diagnostic = "Failed to start Owlet process.";
                    return false;
                }

                process.BeginOutputReadLine();
                process.BeginErrorReadLine();

                if (!process.WaitForExit(120_000))
                {
                    try { process.Kill(); } catch { }
                    diagnostic = "Owlet conversion timed out.";
                    return false;
                }

                outputWait.WaitOne();
                errorWait.WaitOne();

                sb.AppendLine("[Owlet] ExitCode: " + process.ExitCode);
                if (stdOut.Length > 0) sb.AppendLine("[Owlet STDOUT]").AppendLine(stdOut.ToString());
                if (stdErr.Length > 0) sb.AppendLine("[Owlet STDERR]").AppendLine(stdErr.ToString());

                if (process.ExitCode != 0)
                {
                    diagnostic = $"Owlet failed (ExitCode {process.ExitCode})." +
                                 (stdErr.Length > 0 ? Environment.NewLine + stdErr.ToString() : "");
                }

                if (!File.Exists(wpilogPath))
                {
                    string altArgs = $"-f=wpilog -F=\"{hootLogPath}\" \"{wpilogPath}\"";
                    sb.AppendLine($"[Owlet] Primary output missing, retrying with alt args: {altArgs}");

                    using var retry = new Process
                    {
                        StartInfo = new ProcessStartInfo
                        {
                            FileName = m_owletExecutablePath,
                            Arguments = altArgs,
                            RedirectStandardOutput = true,
                            RedirectStandardError = true,
                            UseShellExecute = false,
                            CreateNoWindow = true
                        }
                    };
                    retry.Start();
                    string retryOut = retry.StandardOutput.ReadToEnd();
                    string retryErr = retry.StandardError.ReadToEnd();
                    retry.WaitForExit();
                    sb.AppendLine("[Owlet Retry ExitCode] " + retry.ExitCode);
                    if (retryOut.Length > 0) sb.AppendLine("[Retry STDOUT]").AppendLine(retryOut);
                    if (retryErr.Length > 0) sb.AppendLine("[Retry STDERR]").AppendLine(retryErr);

                    if (retry.ExitCode != 0 || !File.Exists(wpilogPath))
                    {
                        diagnostic = "Owlet did not produce wpilog file.";
                        return false;
                    }
                }

                var fi = new FileInfo(wpilogPath);
                if (fi.Length == 0)
                {
                    diagnostic = "Generated wpilog file is empty.";
                    return false;
                }

                diagnostic = sb.ToString();
                return true;
            }
            catch (Exception ex)
            {
                diagnostic = sb.AppendLine("Exception: " + ex.Message).ToString();
                return false;
            }
        }

        private async Task ConvertHootLogToWpilogAsync(string hootLogPath, string wpilogPath)
        {
            m_stopWatch.Restart();
            progressBar1.Value = 0;

            if (!TryEnsureOwletPathVerified(out string verifyMsg))
            {
                if (!string.IsNullOrEmpty(verifyMsg))
                    MessageBox.Show(verifyMsg, "Owlet verification", MessageBoxButtons.OK, MessageBoxIcon.Warning);

                using OpenFileDialog openFileDialog = new()
                {
                    Filter = "Executable Files (*.exe)|*.exe|All files (*.*)|*.*",
                    Title = "Select Owlet Executable",
                    InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)
                };
                if (openFileDialog.ShowDialog() != DialogResult.OK) return;
                var selectedPath = openFileDialog.FileName;
                if (!File.Exists(selectedPath))
                {
                    MessageBox.Show("Selected file does not exist.");
                    return;
                }
                var sha1 = ComputeSha1(selectedPath);
                SaveOwletConfig(selectedPath, sha1);
                m_owletExecutablePath = selectedPath;
            }

            if (!File.Exists(hootLogPath))
            {
                MessageBox.Show("Hoot file not found.");
                return;
            }

            string baseName = Path.GetFileNameWithoutExtension(hootLogPath);
            WriteProgressBar($"Converting {baseName}: hoot ? wpilog...", 0, 4);

            // Run owlet conversion on background thread
            var (success, diag) = await Task.Run(() =>
            {
                bool ok = TryConvertHootToWpi(hootLogPath, wpilogPath, out string d);
                return (ok, d);
            });

            if (!success)
            {
                WriteToTextBox("Owlet conversion failed.", 1);
                WriteToTextBox(diag, 1);
                MessageBox.Show(diag, "Owlet Conversion Failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }

            WriteProgressBar($"Converting {baseName}: hoot ? wpilog...", 1, 4);
            WriteToTextBox("Owlet conversion succeeded.", 0);
            WriteToTextBox(diag, 0);
            progressBar1.Value = 33;

            // Load wpilog and export to CSV using parallel formatter
            WriteProgressBar($"Converting {baseName}: wpilog ? CSV (parallel)...", 2, 4);
            string csvPath = wpilogPath.Replace(".wpilog", ".csv");

            await Task.Run(() =>
            {
                var parser = new WpiLogParser();
                parser.Load(wpilogPath);
                parser.ExportToCsvParallel(csvPath);
            });

            progressBar1.Value = 66;
            WriteProgressBar($"Parsing {baseName} CSV (parallel)...", 3, 4);

            // Parse the resulting CSV with parallel workers
            _multiFileMode = false;
            await ParseCsvFileAsync(csvPath);

            WriteProgressBar($"Done processing {baseName}", 4, 4);
        }

        private async Task ProcessMultipleHootFilesAsync(string[] hootPaths)
        {
            m_stopWatch.Restart();
            progressBar1.Value = 0;

            if (!TryEnsureOwletPathVerified(out string message))
            {
                if (!string.IsNullOrEmpty(message))
                    MessageBox.Show(message, "Owlet verification", MessageBoxButtons.OK, MessageBoxIcon.Warning);

                using var openFileDialog = new OpenFileDialog
                {
                    Filter = "Executable Files (*.exe)|*.exe|All files (*.*)|*.*",
                    Title = "Select Owlet Executable",
                    InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)
                };
                if (openFileDialog.ShowDialog() != DialogResult.OK)
                {
                    MessageBox.Show("Please select the Owlet executable.");
                    return;
                }
                var selectedPath = openFileDialog.FileName;
                if (!File.Exists(selectedPath))
                {
                    MessageBox.Show("Selected file does not exist.");
                    return;
                }
                var sha1 = ComputeSha1(selectedPath);
                SaveOwletConfig(selectedPath, sha1);
                m_owletExecutablePath = selectedPath;
            }

            string logsDir = GetLogsDir();
            Directory.CreateDirectory(logsDir);

            int totalFiles = hootPaths.Length;
            int totalSteps = totalFiles * 3 + 1; // convert + export + parse per file + merge
            int completedSteps = 0;

            WriteProgressBar($"Starting batch: {totalFiles} hoot file(s)...", 0, totalSteps);

            // Each hoot file: convert ? load+export ? parse, all on background threads
            var tasks = new List<Task<(
                Dictionary<string, List<(double t, double v)>> Series,
                List<ParsedCondition> Conditions,
                int LinesParsed,
                string Base)>>();

            foreach (var hoot in hootPaths)
            {
                tasks.Add(Task.Run(() =>
                {
                    string baseName = Path.GetFileNameWithoutExtension(hoot);
                    string wpilogPath = Path.Combine(logsDir, baseName + ".wpilog");
                    string csvPath = Path.Combine(logsDir, baseName + ".csv");

                    // Step 1: convert hoot ? wpilog
                    if (!TryConvertHootToWpi(hoot, wpilogPath, out string convDiag))
                    {
                        this.Invoke(() =>
                        {
                            WriteToTextBox($"Conversion failed for {baseName}", 1);
                            WriteToTextBox(convDiag, 1);
                            Interlocked.Increment(ref completedSteps);
                            WriteProgressBar($"Convert failed: {baseName}", completedSteps, totalSteps);
                        });
                        return (new Dictionary<string, List<(double t, double v)>>(),
                                new List<ParsedCondition>(), 0, baseName);
                    }

                    this.Invoke(() =>
                    {
                        Interlocked.Increment(ref completedSteps);
                        WriteProgressBar($"Converted: {baseName} (hoot ? wpilog)", completedSteps, totalSteps);
                    });

                    // Step 2: wpilog ? CSV using parallel export
                    var parser = new WpiLogParser();
                    parser.Load(wpilogPath);
                    parser.ExportToCsvParallel(csvPath);

                    this.Invoke(() =>
                    {
                        Interlocked.Increment(ref completedSteps);
                        WriteProgressBar($"Exported CSV: {baseName}", completedSteps, totalSteps);
                    });

                    // Step 3: parse CSV in parallel
                    var lines = File.ReadAllLines(csvPath);
                    float robotEnable = GetRobotEnableTime(lines);

                    var (series, _) = BuildSeriesFromCsvParallel(lines, robotEnable, sourceSuffix: baseName);
                    var (conditions, parsedCount) = ParseCsvLinesToConditionsParallel(lines, robotEnable, baseName);

                    this.Invoke(() =>
                    {
                        Interlocked.Increment(ref completedSteps);
                        WriteProgressBar($"Parsed: {baseName} ({parsedCount} lines)", completedSteps, totalSteps);
                    });

                    return (series, conditions, parsedCount, baseName);
                }));
            }

            var results = await Task.WhenAll(tasks);
            progressBar1.Value = 80;

            // Merge step
            WriteProgressBar("Merging series data...", completedSteps, totalSteps);

            _csvSeries.Clear();

            int totalLinesParsed = 0;
            
            await Task.Run(() => 
            {
                var allConditions = new List<ParsedCondition>();
                foreach (var r in results)
                {
                    if (r.Series.Count == 0 && r.Conditions.Count == 0) continue;
                    totalLinesParsed += r.LinesParsed;
                    allConditions.AddRange(r.Conditions);

                    foreach (var kvp in r.Series)
                    {
                        if (!_csvSeries.ContainsKey(kvp.Key))
                            _csvSeries[kvp.Key] = kvp.Value;
                    }
                }

                _lastConditions = allConditions
                    .GroupBy(c => new { c.Name, c.Start, c.End, c.Priority, c.Kind })
                    .Select(g => g.First())
                    .OrderBy(c => c.End ?? c.Start)
                    .ThenBy(c => c.Name, StringComparer.OrdinalIgnoreCase)
                    .ToList();
            });

            completedSteps = totalSteps;
            WriteProgressBar($"Done - {totalFiles} file(s), {totalLinesParsed} lines merged", completedSteps, totalSteps);

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
            WriteToTextBox($"Processed {hootPaths.Length} hoot files ({totalLinesParsed} lines) in {m_stopWatch.Elapsed.TotalSeconds:F2} seconds", 0);

            if (_plotForm != null && !_plotForm.IsDisposed)
                _plotForm.UpdateData(_csvSeries, _lastConditions);

            string combinedFiles = string.Join("_", hootPaths.Select(p => Path.GetFileNameWithoutExtension(p)));
            if (combinedFiles.Length > 100) 
            {
                combinedFiles = combinedFiles.Substring(0, 100) + "...";
            }
            
            // Cache the merged multi-file analysis
            await CacheCurrentAnalysisAsync($"Combined_{combinedFiles}_{DateTime.Now:yyyyMMdd_HHmmss}", totalLinesParsed);

            CompactHeap();
        }

        private void RunOwletConvert(string hootLogPath, string wpilogPath)
        {
            if (!TryConvertHootToWpi(hootLogPath, wpilogPath, out string diag))
            {
                WriteToTextBox($"Owlet conversion failed: {Path.GetFileName(hootLogPath)}", 1);
                WriteToTextBox(diag, 1);
                throw new Exception("Owlet conversion failed. See output for details.");
            }
            WriteToTextBox($"Owlet conversion ok: {Path.GetFileName(hootLogPath)}", 0);
        }

        private async Task ProcessBulkLogsAndCacheAsync(string parentFolder)
        {
            m_stopWatch.Restart();
            progressBar1.Value = 0;

            try
            {
                if (!TryEnsureOwletPathVerified(out string message))
                {
                    if (!string.IsNullOrEmpty(message))
                        MessageBox.Show(message, "Owlet verification", MessageBoxButtons.OK, MessageBoxIcon.Warning);

                    using var openFileDialog = new OpenFileDialog
                    {
                        Filter = "Executable Files (*.exe)|*.exe|All files (*.*)|*.*",
                        Title = "Select Owlet Executable",
                        InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)
                    };
                    if (openFileDialog.ShowDialog() != DialogResult.OK)
                    {
                        MessageBox.Show("Please select the Owlet executable.");
                        return;
                    }
                    var selectedPath = openFileDialog.FileName;
                    if (!File.Exists(selectedPath))
                    {
                        MessageBox.Show("Selected file does not exist.");
                        return;
                    }
                    var sha1 = ComputeSha1(selectedPath);
                    SaveOwletConfig(selectedPath, sha1);
                    m_owletExecutablePath = selectedPath;
                }

                // Get all subdirectories (each match)
                var matchFolders = Directory.GetDirectories(parentFolder)
                    .OrderBy(d => Path.GetFileName(d))
                    .ToList();

                if (matchFolders.Count == 0)
                {
                    MessageBox.Show($"No subdirectories found in:\n{parentFolder}", "No Data Found", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                WriteToTextBox($"Found {matchFolders.Count} match folder(s) to process.", 0);

                string logsDir = GetLogsDir();
                Directory.CreateDirectory(logsDir);

                int matchNumber = 1;
                int totalMatches = matchFolders.Count;
                int totalCached = 0;

                foreach (var matchFolder in matchFolders)
                {
                    string matchName = Path.GetFileName(matchFolder);
                    WriteToTextBox($"\n--- Processing Match {matchNumber}/{totalMatches}: {matchName} ---", 0);

                    // Find the largest .hoot or .wpilog file from each CAN bus
                    var hootFiles = Directory.GetFiles(matchFolder, "*.hoot", SearchOption.AllDirectories);
                    var wpilogFiles = Directory.GetFiles(matchFolder, "*.wpilog", SearchOption.AllDirectories);

                    // Combine both types and group by CAN bus identifier (typically in filename)
                    var allLogFiles = hootFiles.Cast<string>().Concat(wpilogFiles).ToList();

                    if (allLogFiles.Count == 0)
                    {
                        WriteToTextBox($"No .hoot or .wpilog files found in {matchName}", 1);
                        continue;
                    }

                    // Group files by CAN bus (assuming format like "CAN0_*.hoot", "CAN1_*.hoot", "CAN2_*.hoot")
                    var filesByBus = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

                    foreach (var file in allLogFiles)
                    {
                        string filename = Path.GetFileName(file);
                        string busId = ExtractCanBusId(filename);

                        if (!filesByBus.ContainsKey(busId))
                            filesByBus[busId] = new List<string>();

                        filesByBus[busId].Add(file);
                    }

                    WriteToTextBox($"Found {filesByBus.Count} CAN bus(es): {string.Join(", ", filesByBus.Keys)}", 0);

                    // For each bus, select the largest file
                    var selectedFiles = new List<string>();
                    foreach (var busFiles in filesByBus.Values)
                    {
                        var largest = busFiles
                            .OrderByDescending(f => new FileInfo(f).Length)
                            .First();
                        selectedFiles.Add(largest);
                        WriteToTextBox($"Selected for {Path.GetFileName(largest)}: {new FileInfo(largest).Length / (1024.0 * 1024.0):F2} MB", 0);
                    }

                    // Convert .wpilog files to .hoot equivalent (skip if already wpilog)
                    var hootFilesToProcess = new List<string>();
                    foreach (var file in selectedFiles)
                    {
                        if (file.EndsWith(".wpilog", StringComparison.OrdinalIgnoreCase))
                        {
                            hootFilesToProcess.Add(file); // Already wpilog, process as-is
                        }
                        else
                        {
                            hootFilesToProcess.Add(file); // .hoot file, will convert
                        }
                    }

                    // Process these files (convert if needed, parse, and cache)
                    if (hootFilesToProcess.Count > 0)
                    {
                        try
                        {
                            progressBar1.Value = 0;
                            await ProcessMultipleHootFilesAsync(hootFilesToProcess.ToArray());

                            // Cache the result with match name
                            string cacheFileName = $"{matchName}_{DateTime.Now:yyyyMMdd_HHmmss}";
                            await CacheCurrentAnalysisAsync(cacheFileName, 0);
                            totalCached++;

                            WriteToTextBox($"✓ Successfully cached match: {matchName}", 0);
                        }
                        catch (Exception ex)
                        {
                            WriteToTextBox($"✗ Failed to process match {matchName}: {ex.Message}", 1);
                        }
                    }

                    matchNumber++;
                }

                progressBar1.Value = 100;
                m_stopWatch.Stop();
                WriteToTextBox($"\n=== BULK PROCESSING COMPLETE ===", 0);
                WriteToTextBox($"Processed {totalMatches} match folder(s), cached {totalCached} successfully in {m_stopWatch.Elapsed.TotalSeconds:F2} seconds", 0);

                MessageBox.Show($"Bulk processing complete.\n\nProcessed: {totalMatches} matches\nSuccessfully cached: {totalCached}", 
                    "Bulk Process Complete", MessageBoxButtons.OK, MessageBoxIcon.Information);

                CompactHeap();
            }
            catch (Exception ex)
            {
                WriteToTextBox($"Bulk processing error: {ex.Message}", 1);
                MessageBox.Show($"An error occurred during bulk processing:\n{ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private string ExtractCanBusId(string filename)
        {
            // Remove extension first
            string nameWithoutExt = Path.GetFileNameWithoutExtension(filename);

            // Try to extract CAN bus identifier from filename
            // Expected formats:
            // - "CAN0_...", "CAN1_...", "CAN2_..." (standard format)
            // - "MICHE_E8_7E177D0C...", "MICHE_E8_rio_..." (Team 302 format)
            // - "TEAM_ROBOT_<BUS_ID>_..." (general underscore-separated format)

            // First try: Look for CAN[0-9]+ pattern
            var match = System.Text.RegularExpressions.Regex.Match(
                nameWithoutExt, 
                @"CAN[0-9]+", 
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);

            if (match.Success)
                return match.Value.ToUpper();

            // Second try: Split by underscore and extract the bus ID
            // For "MICHE_E8_rio_2026-03-08" format, bus ID is at index 2
            var parts = nameWithoutExt.Split('_');
            if (parts.Length >= 3)
            {
                // Return the 3rd segment (index 2) as the bus identifier
                string busId = parts[2];

                // Validate it's not a timestamp or other non-identifier pattern
                // (timestamps typically contain dashes or are date-like)
                if (!busId.Contains('-') && busId.Length > 0)
                {
                    return busId;
                }
            }

            // Fallback: try to extract number pattern
            match = System.Text.RegularExpressions.Regex.Match(nameWithoutExt, @"[0-9]+");
            if (match.Success)
                return $"BUS{match.Value}";

            // Last resort: use full filename minus extension
            return nameWithoutExt;
        }
    }
}