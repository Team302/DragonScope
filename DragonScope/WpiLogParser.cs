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

        public void Load(string path, Action<string>? logCallback = null)
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read);
            using var reader = new BinaryReader(stream);

            int resyncAttempts = 0;
            const int maxResync = 1000;

            while (reader.BaseStream.Position < reader.BaseStream.Length)
            {
                long recordStart = reader.BaseStream.Position;

                if (!TryReadByte(reader, out byte recordType))
                    break;

                try
                {
                    logCallback?.Invoke($"[{recordStart:X}] RecordType=0x{recordType:X2}");

                    switch (recordType)
                    {
                        case 0x00: ReadStartEntry(reader, logCallback); break;
                        case 0x01: reader.ReadUInt16(); reader.ReadUInt64(); break; // Finish Entry
                        case 0x02: reader.ReadUInt16(); reader.ReadUInt64(); _ = ReadString(reader); break; // Metadata
                        default:
                            ReadDataRecord(reader, recordType, logCallback);
                            break;
                    }

                    resyncAttempts = 0; // reset on success
                }
                catch (Exception ex)
                {
                    logCallback?.Invoke($"⚠️ Error at 0x{recordStart:X}: {ex.Message}. Skipping 1 byte...");
                    resyncAttempts++;
                    if (resyncAttempts > maxResync)
                        throw new Exception("Too many resync attempts. File may be corrupt.");
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

        private void ReadStartEntry(BinaryReader reader, Action<string>? logCallback)
        {
            ushort id = reader.ReadUInt16();
            string type = ReadString(reader);
            string name = ReadString(reader);
            string metadata = ReadString(reader);

            Entries[id] = new WpiLogEntry { Id = id, Name = name, Type = type };
            logCallback?.Invoke($"Entry {id}: {type} {name}");
        }

        private void ReadDataRecord(BinaryReader reader, byte recordType, Action<string>? logCallback)
        {
            long recordStart = reader.BaseStream.Position;
            ushort id = reader.ReadUInt16();
            ulong timestamp = reader.ReadUInt64();

            if (!TryReadByte(reader, out byte length))
            {
                logCallback?.Invoke($"⚠️ Could not read length byte at 0x{recordStart:X}");
                return;
            }

            byte[] data = reader.ReadBytes(length);

            if (!Entries.TryGetValue(id, out var entry))
            {
                logCallback?.Invoke($"Unknown entry ID {id} at 0x{recordStart:X}, skipping...");
                return;
            }

            object? value = ParseValue(entry.Type, data);
            if (value != null)
                entry.Values.Add((timestamp, value));
            else
                logCallback?.Invoke($"⚠️ Could not parse value for {entry.Name} (type: {entry.Type}) at 0x{recordStart:X}");
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

        private static string ReadString(BinaryReader reader)
        {
            if (!TryReadByte(reader, out byte len))
                return "";
            byte[] bytes = reader.ReadBytes(len);
            return Encoding.UTF8.GetString(bytes);
        }

        private static bool TryReadByte(BinaryReader reader, out byte value)
        {
            try
            {
                value = reader.ReadByte();
                return true;
            }
            catch (EndOfStreamException)
            {
                value = 0;
                return false;
            }
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
    }
}
