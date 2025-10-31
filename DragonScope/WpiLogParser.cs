using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace WpiLogLib
{
    public class WpiLogEntry
    {
        public ushort Id { get; set; }
        public string Name { get; set; } = "";
        public string Type { get; set; } = "";
        public List<(ulong Timestamp, object Value)> Values { get; set; } = new();
    }

    public class WpiLogParser
    {
        public Dictionary<ushort, WpiLogEntry> Entries { get; private set; } = new();
        public List<string>? Filters { get; set; } = new();

        private readonly List<WpiLogEntry> _completedEntries = new();
        private int _startCount;
        private int _finishCount;

        public void Load(string path, Action<string>? logCallback = null)
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read);
            using var reader = new BinaryReader(stream);

            // Validate the header (read bytes, not chars)
            string header = Encoding.ASCII.GetString(reader.ReadBytes(6));
            if (header != "WPILOG")
            {
                throw new Exception("Invalid wpilog file: Missing WPILOG header.");
            }

            ushort version = reader.ReadUInt16();
            if (version != 0x0100)
            {
                throw new Exception($"Unsupported wpilog version: {version:X}");
            }

            int extraHeaderLength = reader.ReadInt32();
            if (extraHeaderLength > 0)
            {
                // Extra header length is in bytes; decode from bytes
                string extraHeader = Encoding.UTF8.GetString(reader.ReadBytes(extraHeaderLength));
                // Process extra header if needed
            }

            // Process records
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
                    {
                        ProcessControlRecord(reader, payloadSize, logCallback);
                    }
                    else
                    {
                        ProcessDataRecord(reader, entryId, timestamp, payloadSize);
                    }
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

        public void ExportToCsv(string outputPath, Action<string>? logCallback = null)
        {
            using var writer = new StreamWriter(outputPath);
            writer.WriteLine("Timestamp,Name,Value");
            int count = 0;

            foreach (var entry in _completedEntries)
            {
                if (!ShouldInclude(entry.Name)) continue;

                foreach (var (timestamp, value) in entry.Values)
                {
                    string valStr = FormatValue(entry.Type, value);
                    writer.WriteLine($"{timestamp},{entry.Name},{valStr}");
                    if (++count % 10000 == 0)
                        logCallback?.Invoke($"Exported {count} records...");
                }
            }

            foreach (var entry in Entries.Values)
            {
                if (!ShouldInclude(entry.Name)) continue;

                foreach (var (timestamp, value) in entry.Values)
                {
                    string valStr = FormatValue(entry.Type, value);
                    writer.WriteLine($"{timestamp},{entry.Name},{valStr}");
                    if (++count % 10000 == 0)
                        logCallback?.Invoke($"Exported {count} records...");
                }
            }

            logCallback?.Invoke($"Export complete. {count} records written.");
        }

        private void ReadStartEntry(BinaryReader reader, Action<string>? logCallback)
        {
            ushort id = reader.ReadUInt16();
            string type = ReadString(reader);
            string name = ReadString(reader);
            string metadata = ReadString(reader);

            Entries[id] = new WpiLogEntry { Id = id, Name = name, Type = type };
            logCallback?.Invoke($"Entry {id}: {type} {name}");
        }

        private object? ParseValue(string type, byte[] data)
        {
            try
            {
                return type switch
                {
                    "boolean" => data[0] != 0,
                    "int64" => BitConverter.ToInt64(data, 0),
                    "int32" => BitConverter.ToInt32(data, 0),
                    "float" => BitConverter.ToSingle(data, 0),
                    "double" => BitConverter.ToDouble(data, 0),
                    "string" => Encoding.UTF8.GetString(data),
                    "raw" => BitConverter.ToString(data).Replace("-", ""),
                    _ => null
                };
            }
            catch
            {
                return null;
            }
        }

        private int ReadVariableLengthInt(BinaryReader reader, int length)
        {
            int value = 0;
            for (int i = 0; i < length; i++)
            {
                value |= reader.ReadByte() << (8 * i);
            }
            return value;
        }

        private long ReadVariableLengthLong(BinaryReader reader, int length)
        {
            long value = 0;
            for (int i = 0; i < length; i++)
            {
                value |= (long)reader.ReadByte() << (8 * i);
            }
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
                    string entryType = ReadString(reader);
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
                    string newMetadata = ReadString(reader);
                    if (Entries.TryGetValue((ushort)entryId, out var entry))
                    {
                        // Metadata can be stored or processed as needed
                    }
                    break;

                default:
                    throw new Exception($"Unknown control record type: {controlType}");
            }

            // Align to payload end to avoid desync if we mis-read any field inside
            reader.BaseStream.Position = payloadEnd;
        }

        private bool ShouldInclude(string name) =>
            Filters == null || Filters.Count == 0 || Filters.Any(f => name.Contains(f, StringComparison.OrdinalIgnoreCase));

        private static string FormatValue(string type, object value) =>
            type switch
            {
                "double" => ((double)value).ToString("G17"),
                "float" => ((float)value).ToString("G9"),
                "int64" => value.ToString(),
                "int32" => value.ToString(),
                "boolean" => (bool)value ? "1" : "0",
                "string" => "\"" + value.ToString()?.Replace("\"", "\"\"") + "\"",
                "raw" => value.ToString() ?? "",
                _ => value.ToString() ?? ""
            };

        private void ProcessDataRecord(BinaryReader reader, int entryId, long timestamp, int payloadSize)
        {
            byte[] payload = reader.ReadBytes(payloadSize);

            if (!Entries.TryGetValue((ushort)entryId, out var entry))
                return;

            object? value = entry.Type switch
            {
                "double" when payload.Length >= 8 => BitConverter.ToDouble(payload, 0),
                "int64"  when payload.Length >= 8 => BitConverter.ToInt64(payload, 0),
                "boolean" when payload.Length >= 1 => payload[0] != 0,
                "string" => Encoding.UTF8.GetString(payload),
                _ => null
            };

            if (value != null)
            {
                entry.Values.Add(((ulong)timestamp, value));
            }
        }

        // IMPORTANT: length prefix is in BYTES; decode from bytes with UTF-8
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
