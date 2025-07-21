using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

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

        public void Load(string path)
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
                    switch (recordType)
                    {
                        case 0x00: ReadStartEntry(reader); break;
                        case 0x01: reader.ReadUInt16(); reader.ReadUInt64(); break;
                        case 0x02: reader.ReadUInt16(); reader.ReadUInt64(); _ = ReadString(reader); break;

                        default:
                            ReadDataRecord(reader, recordType); // try to parse regardless
                            break;

                    }

                    resyncAttempts = 0; // reset on successful read
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"⚠️ Error at 0x{recordStart:X}: {ex.Message}");

                    resyncAttempts++;
                    if (resyncAttempts > maxResync)
                        throw new Exception("Too many consecutive resync attempts. File may be corrupt.");

                    // Move forward one byte and try again
                    reader.BaseStream.Position = recordStart + 1;
                }
            }
        }


        public void ExportToCsv(string path)
        {
            using var writer = new StreamWriter(path);
            writer.WriteLine("Timestamp,Name,Value");

            foreach (var entry in Entries.Values)
            {
                foreach (var (timestamp, value) in entry.Values)
                {
                    writer.WriteLine($"{timestamp},{entry.Name},{value}");
                }
            }
        }

        private void ReadStartEntry(BinaryReader reader)
        {
            ushort id = reader.ReadUInt16();
            string type = ReadString(reader);
            string name = ReadString(reader);
            string metadata = ReadString(reader);

            Entries[id] = new WpiLogEntry { Id = id, Name = name, Type = type };
        }

        private void ReadDataRecord(BinaryReader reader, byte recordType)
        {
            ushort id = reader.ReadUInt16();
            ulong timestamp = reader.ReadUInt64();
            byte length = reader.ReadByte();
            byte[] data = reader.ReadBytes(length);

            if (!Entries.TryGetValue(id, out var entry))
                return; // unknown entry, skip

            object? value = entry.Type switch
            {
                "double" when data.Length >= 8 => BitConverter.ToDouble(data, 0),
                "int64" when data.Length >= 8 => BitConverter.ToInt64(data, 0),
                "boolean" when data.Length >= 1 => data[0] != 0,
                "string" => Encoding.UTF8.GetString(data),
                _ => null
            };

            if (value != null)
                entry.Values.Add((timestamp, value));
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
    }
}
