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
            var allConditions = new List<ParsedCondition>();
            int totalLinesParsed = 0;

            foreach (var r in results)
            {
                if (r.Series.Count == 0 && r.Conditions.Count == 0) continue;
                totalLinesParsed += r.LinesParsed;
                allConditions.AddRange(r.Conditions);

                foreach (var kvp in r.Series)
                {
                    if (_csvSeries.TryGetValue(kvp.Key, out var existing))
                        existing.AddRange(kvp.Value);
                    else
                        _csvSeries[kvp.Key] = kvp.Value;
                }
            }

            _lastConditions = allConditions
                .OrderBy(c => c.End ?? c.Start)
                .ThenBy(c => c.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();

            allConditions = null;

            completedSteps = totalSteps;
            WriteProgressBar($"Done — {totalFiles} file(s), {totalLinesParsed} lines merged", completedSteps, totalSteps);

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
    }
}