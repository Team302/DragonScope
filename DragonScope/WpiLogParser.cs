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
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public string Type { get; set; } = "";
        public List<(ulong Timestamp, object Value)> Values { get; set; } = new();
    }

    public class WpiLogParser
    {
        public Dictionary<int, WpiLogEntry> Entries { get; private set; } = new();
        public List<string>? Filters { get; set; } = new();

        public void Load(string path, Action<string>? logCallback = null)
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read);
            using var reader = new BinaryReader(stream);

            // Validate the header
            string header = new string(reader.ReadChars(6));
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
                string extraHeader = new string(reader.ReadChars(extraHeaderLength));
                // Process extra header if needed
            }

            // Process records
            while (reader.BaseStream.Position < reader.BaseStream.Length)
            {
                long recordStart = reader.BaseStream.Position;

                try
                {
                    byte headerLengthField = reader.ReadByte();

                    // Decode header length field
                    int entryIdLength = (headerLengthField & 0b00000011) + 1;
                    int payloadSizeLength = ((headerLengthField >> 2) & 0b00000011) + 1;
                    int timestampLength = ((headerLengthField >> 4) & 0b00000111) + 1;

                    // Read entry ID
                    int entryId = ReadVariableLengthInt(reader, entryIdLength);

                    // Read payload size
                    int payloadSize = ReadVariableLengthInt(reader, payloadSizeLength);

                    // Read timestamp
                    long timestamp = ReadVariableLengthLong(reader, timestampLength);

                    // Process control or data record
                    if (entryId == 0)
                    {
                        // Control record
                        ProcessControlRecord(reader, payloadSize);
                    }
                    else
                    {
                        // Data record
                        ProcessDataRecord(reader, entryId, timestamp, payloadSize);
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"⚠️ Error at 0x{recordStart:X}: {ex.Message}");
                    // Skip to the next byte and try again
                    reader.BaseStream.Position = recordStart + 1;
                }
            }
        }

        public void ExportToCsv(string outputPath, Action<string>? logCallback = null)
        {
            using var writer = new StreamWriter(outputPath);
            writer.WriteLine("Timestamp,Name,Value");
            int count = 0;

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

        private void ProcessControlRecord(BinaryReader reader, int payloadSize)
        {
            byte controlType = reader.ReadByte();
            switch (controlType)
            {
                case 0: // Start
                    int entryId = ReadVariableLengthInt(reader, 4); // Fixed: use variable length for entryId
                    string entryName = ReadString(reader);
                    string entryType = ReadString(reader);
                    string metadata = ReadString(reader);

                    Entries[entryId] = new WpiLogEntry
                    {
                        Id = entryId,
                        Name = entryName,
                        Type = entryType
                    };
                    break;

                case 1: // Finish
                    entryId = ReadVariableLengthInt(reader, 4); // Fixed: use variable length for entryId
                    Entries.Remove(entryId);
                    break;

                case 2: // Set Metadata
                    entryId = ReadVariableLengthInt(reader, 4); // Fixed: use variable length for entryId
                    string newMetadata = ReadString(reader);
                    if (Entries.TryGetValue(entryId, out var entry))
                    {
                        // Metadata can be stored or processed as needed
                    }
                    break;

                default:
                    throw new Exception($"Unknown control record type: {controlType}");
            }
        }

        private bool ShouldInclude(string name)
        {
            //Filters == null || Filters.Count == 0 || Filters.Any(f => name.Contains(f, StringComparison.OrdinalIgnoreCase));
            return true;
        }

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

            if (!Entries.TryGetValue(entryId, out var entry))
                return;

            object? value = entry.Type switch
            {
                "double" when payload.Length >= 8 => BitConverter.ToDouble(payload, 0),
                "int64" when payload.Length >= 8 => BitConverter.ToInt64(payload, 0),
                "boolean" when payload.Length >= 1 => payload[0] != 0,
                "string" => Encoding.UTF8.GetString(payload),
                _ => null
            };

            if (value != null)
            {
                entry.Values.Add(((ulong)timestamp, value));
            }
        }

        private static string ReadString(BinaryReader reader)
        {
            int length = reader.ReadInt32();
            return new string(reader.ReadChars(length));
        }
    }
}
