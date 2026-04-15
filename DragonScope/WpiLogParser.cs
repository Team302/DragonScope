using System.Text;
using System.Globalization;
using System.Collections.Concurrent;
using System.Runtime.InteropServices;

namespace WpiLogLib
{
    public class WpiLogParser
    {
        public Dictionary<ushort, WpiLogEntry> Entries { get; private set; } = new();
        public List<string>? Filters { get; set; } = new();

        private readonly List<WpiLogEntry> _completedEntries = new();
        private int _startCount;
        private int _finishCount;

        // Intern pool for entry type strings — typically only ~5 distinct values
        private readonly Dictionary<string, string> _typePool = new(StringComparer.Ordinal);

        public void Load(string path, Action<string>? logCallback = null)
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize: 65536);
            using var reader = new BinaryReader(stream);

            string header = Encoding.ASCII.GetString(reader.ReadBytes(6));
            if (header != "WPILOG")
                throw new Exception("Invalid wpilog file: Missing WPILOG header.");

            ushort version = reader.ReadUInt16();
            if (version != 0x0100)
                throw new Exception($"Unsupported wpilog version: {version:X}");

            int extraHeaderLength = reader.ReadInt32();
            if (extraHeaderLength > 0)
                reader.ReadBytes(extraHeaderLength); // skip extra header

            while (reader.BaseStream.Position < reader.BaseStream.Length)
            {
                long recordStart = reader.BaseStream.Position;

                try
                {
                    byte headerLengthField = reader.ReadByte();

                    int entryIdLength = (headerLengthField & 0b00000011) + 1;
                    int payloadSizeLength = ((headerLengthField >> 2) & 0b00000011) + 1;
                    int timestampLength = ((headerLengthField >> 4) & 0b00000111) + 1;

                    int entryId = ReadVariableLengthInt(reader, entryIdLength);
                    int payloadSize = ReadVariableLengthInt(reader, payloadSizeLength);
                    long timestamp = ReadVariableLengthLong(reader, timestampLength);

                    if (entryId == 0)
                        ProcessControlRecord(reader, payloadSize, logCallback);
                    else
                        ProcessDataRecord(reader, entryId, timestamp, payloadSize);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"⚠️ Error at 0x{recordStart:X}: {ex.Message}");
                    reader.BaseStream.Position = recordStart + 1;
                }
            }

            if (Entries.Count > 0)
            {
                _completedEntries.AddRange(Entries.Values);
                Entries.Clear();
            }

            logCallback?.Invoke($"Control summary: start={_startCount}, finish={_finishCount}, completed={_completedEntries.Count}");
        }

        /// <summary>
        /// Releases all parsed entry data. Call after export to free memory
        /// when the parser instance is no longer needed for queries.
        /// </summary>
        public void Clear()
        {
            foreach (var entry in _completedEntries)
                entry.Values.Clear();
            _completedEntries.Clear();
            _completedEntries.TrimExcess();

            foreach (var entry in Entries.Values)
                entry.Values.Clear();
            Entries.Clear();

            _typePool.Clear();
            _startCount = 0;
            _finishCount = 0;
        }

        /// <summary>
        /// Exports CSV by streaming formatted lines directly to disk.
        /// Formatting of each entry's values is parallelized, but entries are
        /// flushed and released one at a time to cap peak memory.
        /// </summary>
        public void ExportToCsvParallel(string outputPath, Action<string>? logCallback = null)
        {
            var allEntries = new List<WpiLogEntry>(_completedEntries.Count + Entries.Count);
            allEntries.AddRange(_completedEntries);
            allEntries.AddRange(Entries.Values);

            var filteredEntries = allEntries.Where(e => ShouldInclude(e.Name)).ToList();
            allEntries = null; // release ref

            // Sort entries largest-first so we process big ones first and free their memory earlier
            filteredEntries.Sort((a, b) => b.Values.Count.CompareTo(a.Values.Count));

            using var writer = new StreamWriter(outputPath, false, Encoding.UTF8, bufferSize: 65536);
            writer.WriteLine("Timestamp,Name,Value");
            int totalCount = 0;

            // Process entries in batches to balance parallelism with memory pressure.
            // Each batch formats lines in parallel, writes them, then releases.
            const int batchSize = 32;
            for (int batchStart = 0; batchStart < filteredEntries.Count; batchStart += batchSize)
            {
                int batchEnd = Math.Min(batchStart + batchSize, filteredEntries.Count);
                int batchEntryCount = batchEnd - batchStart;

                // Pre-allocate a flat array for this batch's lines
                int batchLineCount = 0;
                var offsets = new int[batchEntryCount];
                for (int i = 0; i < batchEntryCount; i++)
                {
                    offsets[i] = batchLineCount;
                    batchLineCount += filteredEntries[batchStart + i].Values.Count;
                }

                var batchLines = new string[batchLineCount];

                Parallel.For(0, batchEntryCount, i =>
                {
                    var entry = filteredEntries[batchStart + i];
                    int baseIdx = offsets[i];
                    string name = entry.Name;
                    string type = entry.Type;
                    var values = entry.Values;

                    for (int j = 0; j < values.Count; j++)
                    {
                        var (timestamp, value) = values[j];
                        double seconds = timestamp / 1_000_000.0;
                        batchLines[baseIdx + j] = 
                            $"{seconds.ToString("0.######", CultureInfo.InvariantCulture)},{name},{value.Format(type)}";
                    }
                });

                // Write this batch sequentially
                for (int i = 0; i < batchLineCount; i++)
                    writer.WriteLine(batchLines[i]);

                totalCount += batchLineCount;

                // Release entry value lists for this batch to free memory immediately
                for (int i = batchStart; i < batchEnd; i++)
                {
                    filteredEntries[i].Values.Clear();
                    filteredEntries[i].Values.TrimExcess();
                }

                if (totalCount % 50000 < batchLineCount)
                    logCallback?.Invoke($"Exported {totalCount} records...");
            }

            logCallback?.Invoke($"Export complete. {totalCount} records written.");
        }

        /// <summary>
        /// Returns all parsed data as CSV-formatted lines in memory.
        /// Formatting is parallelized. Call <see cref="Clear"/> after if you don't need the parser anymore.
        /// </summary>
        public string[] ExportToLinesParallel()
        {
            var allEntries = new List<WpiLogEntry>(_completedEntries.Count + Entries.Count);
            allEntries.AddRange(_completedEntries);
            allEntries.AddRange(Entries.Values);

            var filteredEntries = allEntries.Where(e => ShouldInclude(e.Name)).ToList();

            int totalLines = 0;
            var offsets = new int[filteredEntries.Count];
            for (int i = 0; i < filteredEntries.Count; i++)
            {
                offsets[i] = totalLines;
                totalLines += filteredEntries[i].Values.Count;
            }

            var result = new string[totalLines + 1];
            result[0] = "Timestamp,Name,Value";

            Parallel.For(0, filteredEntries.Count, i =>
            {
                var entry = filteredEntries[i];
                int baseIdx = offsets[i] + 1;
                string name = entry.Name;
                string type = entry.Type;
                var values = entry.Values;

                for (int j = 0; j < values.Count; j++)
                {
                    var (timestamp, value) = values[j];
                    double seconds = timestamp / 1_000_000.0;
                    result[baseIdx + j] = string.Create(null, stackalloc char[128],
                        $"{seconds.ToString("0.######", CultureInfo.InvariantCulture)},{name},{value.Format(type)}");
                }
            });

            return result;
        }

        public void ExportToCsv(string outputPath, Action<string>? logCallback = null)
        {
            using var writer = new StreamWriter(outputPath, false, Encoding.UTF8, bufferSize: 65536);
            writer.WriteLine("Timestamp,Name,Value");
            int count = 0;

            foreach (var entry in _completedEntries)
            {
                if (!ShouldInclude(entry.Name)) continue;

                foreach (var (timestamp, value) in entry.Values)
                {
                    double seconds = timestamp / 1_000_000.0;
                    string tsStr = seconds.ToString("0.######", CultureInfo.InvariantCulture);
                    string valStr = value.Format(entry.Type);
                    writer.WriteLine($"{tsStr},{entry.Name},{valStr}");
                    if (++count % 10000 == 0)
                        logCallback?.Invoke($"Exported {count} records...");
                }
            }

            foreach (var entry in Entries.Values)
            {
                if (!ShouldInclude(entry.Name)) continue;

                foreach (var (timestamp, value) in entry.Values)
                {
                    double seconds = timestamp / 1_000_000.0;
                    string tsStr = seconds.ToString("0.######", CultureInfo.InvariantCulture);
                    string valStr = value.Format(entry.Type);
                    writer.WriteLine($"{tsStr},{entry.Name},{valStr}");
                    if (++count % 10000 == 0)
                        logCallback?.Invoke($"Exported {count} records...");
                }
            }

            logCallback?.Invoke($"Export complete. {count} records written.");
        }

        private static int ReadVariableLengthInt(BinaryReader reader, int length)
        {
            int value = 0;
            for (int i = 0; i < length; i++)
                value |= reader.ReadByte() << (8 * i);
            return value;
        }

        private static long ReadVariableLengthLong(BinaryReader reader, int length)
        {
            long value = 0;
            for (int i = 0; i < length; i++)
                value |= (long)reader.ReadByte() << (8 * i);
            return value;
        }

        private void ProcessControlRecord(BinaryReader reader, int payloadSize, Action<string>? logCallback)
        {
            long payloadEnd = reader.BaseStream.Position + payloadSize;

            byte controlType = reader.ReadByte();
            switch (controlType)
            {
                case 0: // Start
                    _startCount++;
                    int entryId = reader.ReadInt32();
                    string entryName = ReadString(reader);
                    string entryType = InternType(ReadString(reader));
                    string metadata = ReadString(reader);

                    if (Entries.TryGetValue((ushort)entryId, out var existing))
                    {
                        _completedEntries.Add(existing);
                        logCallback?.Invoke($"Archived existing entry {entryId} on Start.");
                    }

                    Entries[(ushort)entryId] = new WpiLogEntry
                    {
                        Id = (ushort)entryId,
                        Name = entryName,
                        Type = entryType
                    };
                    break;

                case 1: // Finish
                    _finishCount++;
                    entryId = reader.ReadInt32();

                    if (Entries.Remove((ushort)entryId, out var finishedEntry))
                    {
                        _completedEntries.Add(finishedEntry);
                        logCallback?.Invoke($"Finished entry {entryId}.");
                    }
                    else
                    {
                        logCallback?.Invoke($"Finish for unknown entry {entryId}.");
                    }
                    break;

                case 2: // Set Metadata
                    entryId = reader.ReadInt32();
                    ReadString(reader); // consume metadata
                    break;

                default:
                    throw new Exception($"Unknown control record type: {controlType}");
            }

            reader.BaseStream.Position = payloadEnd;
        }

        /// <summary>
        /// Interns the entry type string so all entries sharing the same type
        /// (e.g. "double", "boolean") reference a single string instance.
        /// </summary>
        private string InternType(string type)
        {
            if (_typePool.TryGetValue(type, out var existing))
                return existing;
            _typePool[type] = type;
            return type;
        }

        private bool ShouldInclude(string name) =>
            Filters == null || Filters.Count == 0 || Filters.Any(f => name.Contains(f, StringComparison.OrdinalIgnoreCase));

        private void ProcessDataRecord(BinaryReader reader, int entryId, long timestamp, int payloadSize)
        {
            if (!Entries.TryGetValue((ushort)entryId, out var entry))
            {
                // Skip unknown entry payload without allocating
                reader.BaseStream.Position += payloadSize;
                return;
            }

            // Parse directly into the non-boxing WpiLogValue struct
            WpiLogValue? value = entry.Type switch
            {
                "double" when payloadSize >= 8 => WpiLogValue.FromDouble(BitConverter.ToDouble(reader.ReadBytes(8), 0)),
                "float" when payloadSize >= 4 => WpiLogValue.FromDouble(BitConverter.ToSingle(reader.ReadBytes(4), 0)),
                "int64" when payloadSize >= 8 => WpiLogValue.FromLong(BitConverter.ToInt64(reader.ReadBytes(8), 0)),
                "int32" when payloadSize >= 4 => WpiLogValue.FromLong(BitConverter.ToInt32(reader.ReadBytes(4), 0)),
                "boolean" when payloadSize >= 1 => WpiLogValue.FromBool(reader.ReadByte() != 0),
                "string" => WpiLogValue.FromString(Encoding.UTF8.GetString(reader.ReadBytes(payloadSize))),
                _ => null
            };

            if (value.HasValue)
            {
                entry.Values.Add(((ulong)timestamp, value.Value));
            }
            else
            {
                // Unknown type — skip remaining payload
                long expectedEnd = reader.BaseStream.Position;
                // Some bytes may have been read by the switch; seek past the rest
                // Since we can't know how many bytes the failed match read,
                // recalculate based on how many bytes were consumed
                reader.BaseStream.Position += payloadSize - (int)(expectedEnd - reader.BaseStream.Position);
            }
        }

        private static string ReadString(BinaryReader reader)
        {
            int byteLength = reader.ReadInt32();
            if (byteLength < 0) throw new Exception("Negative string length");
            if (byteLength == 0) return string.Empty;

            var bytes = reader.ReadBytes(byteLength);
            if (bytes.Length != byteLength)
                throw new EndOfStreamException("Unexpected end of stream while reading string.");

            return Encoding.UTF8.GetString(bytes);
        }
    }
}
