using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace DragonScope
{
    // Custom converter for (double t, double v) tuples
    public class DataPointConverter : JsonConverter<(double t, double v)>
    {
        public override (double t, double v) Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            double t = 0, v = 0;

            if (reader.TokenType == JsonTokenType.StartArray)
            {
                // New format: [t, v]
                reader.Read();
                if (!reader.TryGetDouble(out t))
                    throw new JsonException("Failed to read time value");

                reader.Read();
                if (!reader.TryGetDouble(out v))
                    throw new JsonException("Failed to read value");

                reader.Read();
                if (reader.TokenType != JsonTokenType.EndArray)
                    throw new JsonException("Expected end of array");
            }
            else if (reader.TokenType == JsonTokenType.StartObject)
            {
                // Old format: {t: value, v: value}
                while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
                {
                    if (reader.TokenType == JsonTokenType.PropertyName)
                    {
                        string? propertyName = reader.GetString();
                        reader.Read();

                        if (propertyName == "t" && reader.TryGetDouble(out var tValue))
                            t = tValue;
                        else if (propertyName == "v" && reader.TryGetDouble(out var vValue))
                            v = vValue;
                    }
                }
            }
            else
            {
                throw new JsonException("Expected array or object for data point tuple");
            }

            return (t, v);
        }

        public override void Write(Utf8JsonWriter writer, (double t, double v) value, JsonSerializerOptions options)
        {
            writer.WriteStartArray();
            writer.WriteNumberValue(value.t);
            writer.WriteNumberValue(value.v);
            writer.WriteEndArray();
        }
    }

    public class CachedAnalysis
    {
        public string CacheId { get; set; } = "";
        public string FileName { get; set; } = "";
        public string DataHash { get; set; } = "";
        public DateTime CachedAt { get; set; }
        public int LinesParsed { get; set; }
        public Dictionary<string, List<(double t, double v)>> CsvSeries { get; set; } = new();
        public List<ParsedCondition> Conditions { get; set; } = new();
    }

    public class CachedAnalysisMetadata
    {
        public string CacheId { get; set; } = "";
        public string FileName { get; set; } = "";
        public string DataHash { get; set; } = "";
        public DateTime CachedAt { get; set; }
        public int LinesParsed { get; set; }
        public List<string> SeriesKeys { get; set; } = new();
    }

    public class DataCacheManager
    {
        private readonly string _cacheDirectory;
        private const string METADATA_FILENAME = "cache_metadata.json";
        private List<CachedAnalysisMetadata> _metadataCache = new();
        private readonly JsonSerializerOptions _serializationOptions;

        public DataCacheManager()
        {
            _cacheDirectory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "DragonScope",
                "DataCache");
            Directory.CreateDirectory(_cacheDirectory);
            
            _serializationOptions = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = false,
                Converters = { new DataPointConverter() }
            };
            
            LoadMetadata();
        }

        public string GenerateDataHash(Dictionary<string, List<(double t, double v)>> csvSeries,
            List<ParsedCondition> conditions)
        {
            using var sha256 = SHA256.Create();
            var bytes = new byte[8192];
            int pointer = 0;

            void Flush()
            {
                if (pointer > 0)
                {
                    sha256.TransformBlock(bytes, 0, pointer, null, 0);
                    pointer = 0;
                }
            }

            Span<char> charBuf = stackalloc char[256];
            var utf8 = System.Text.Encoding.UTF8;

            void AppendSpan(ReadOnlySpan<char> s)
            {
                int maxBytes = utf8.GetMaxByteCount(s.Length);
                if (pointer + maxBytes > bytes.Length)
                    Flush();

                if (maxBytes > bytes.Length)
                {
                    var bigBuffer = new byte[maxBytes];
                    int w = utf8.GetBytes(s, bigBuffer);
                    sha256.TransformBlock(bigBuffer, 0, w, null, 0);
                }
                else
                {
                    pointer += utf8.GetBytes(s, bytes.AsSpan(pointer));
                }
            }

            foreach (var kvp in csvSeries.OrderBy(x => x.Key))
            {
                AppendSpan(kvp.Key.AsSpan());
                
                // Sort in-place to prevent allocating huge arrays
                kvp.Value.Sort((a,b) => a.t.CompareTo(b.t));
                
                foreach (var (t, v) in kvp.Value)
                {
                    AppendSpan("|");
                    if (t.TryFormat(charBuf, out int charsWritten, "G17", System.Globalization.CultureInfo.InvariantCulture))
                        AppendSpan(charBuf.Slice(0, charsWritten));
                    
                    AppendSpan(":");
                    if (v.TryFormat(charBuf, out charsWritten, "G17", System.Globalization.CultureInfo.InvariantCulture))
                        AppendSpan(charBuf.Slice(0, charsWritten));
                }
                AppendSpan(";");
            }

            AppendSpan("---");

            conditions.Sort((a, b) => 
            {
                int cmp = string.Compare(a.Name, b.Name, StringComparison.Ordinal);
                if (cmp != 0) return cmp;
                return a.Start.CompareTo(b.Start);
            });

            foreach (var cond in conditions)
            {
                AppendSpan(cond.Name.AsSpan());
                AppendSpan("|");
                if (cond.Start.TryFormat(charBuf, out int charsWritten, "G17", System.Globalization.CultureInfo.InvariantCulture))
                    AppendSpan(charBuf.Slice(0, charsWritten));
                AppendSpan("|");
                
                if (cond.End.HasValue)
                {
                    if (cond.End.Value.TryFormat(charBuf, out charsWritten, "G17", System.Globalization.CultureInfo.InvariantCulture))
                        AppendSpan(charBuf.Slice(0, charsWritten));
                }
                
                AppendSpan($"|{cond.Priority}|{cond.Kind}|{cond.SourceFile}:");
            }

            Flush();
            sha256.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
            return BitConverter.ToString(sha256.Hash!).Replace("-", "").ToUpperInvariant();
        }

        public void SaveAnalysis(CachedAnalysis analysis)
        {
            var cacheId = analysis.CacheId;
            var cachePath = Path.Combine(_cacheDirectory, $"{cacheId}.json");

            using (var fs = File.Create(cachePath))
            {
                JsonSerializer.Serialize(fs, analysis, _serializationOptions);
            }

            var metadata = new CachedAnalysisMetadata
            {
                CacheId = analysis.CacheId,
                FileName = analysis.FileName,
                DataHash = analysis.DataHash,
                CachedAt = analysis.CachedAt,
                LinesParsed = analysis.LinesParsed,
                SeriesKeys = analysis.CsvSeries.Keys.ToList()
            };

            var existingMetadata = _metadataCache.FirstOrDefault(m => m.CacheId == cacheId);
            if (existingMetadata != null)
                _metadataCache.Remove(existingMetadata);

            _metadataCache.Add(metadata);
            SaveMetadata();
        }

        public CachedAnalysis? LoadAnalysis(string cacheId)
        {
            var cachePath = Path.Combine(_cacheDirectory, $"{cacheId}.json");
            if (!File.Exists(cachePath))
                return null;

            try
            {
                using var fs = File.OpenRead(cachePath);
                return JsonSerializer.Deserialize<CachedAnalysis>(fs, _serializationOptions);
            }
            catch
            {
                return null;
            }
        }

        public List<CachedAnalysisMetadata> GetAllCachedAnalyses() => new(_metadataCache);

        public List<CachedAnalysisMetadata> SearchCachedAnalyses(string searchTerm)
        {
            var term = searchTerm.ToLower();
            return _metadataCache
                .Where(m => m.FileName.ToLower().Contains(term) || m.CacheId.ToLower().Contains(term))
                .ToList();
        }

        public void DeleteAnalysis(string cacheId)
        {
            var cachePath = Path.Combine(_cacheDirectory, $"{cacheId}.json");
            if (File.Exists(cachePath))
                File.Delete(cachePath);

            var metadata = _metadataCache.FirstOrDefault(m => m.CacheId == cacheId);
            if (metadata != null)
                _metadataCache.Remove(metadata);

            SaveMetadata();
        }

        public void ClearAllCache()
        {
            foreach (var file in Directory.GetFiles(_cacheDirectory, "*.json"))
            {
                if (!file.EndsWith(METADATA_FILENAME, StringComparison.OrdinalIgnoreCase))
                    File.Delete(file);
            }
            _metadataCache.Clear();
            SaveMetadata();
        }

        private void SaveMetadata()
        {
            var metadataPath = Path.Combine(_cacheDirectory, METADATA_FILENAME);
            var serializationOptions = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = true
            };
            var json = JsonSerializer.Serialize(_metadataCache, serializationOptions);
            File.WriteAllText(metadataPath, json);
        }

        private void LoadMetadata()
        {
            var metadataPath = Path.Combine(_cacheDirectory, METADATA_FILENAME);
            if (!File.Exists(metadataPath))
                return;

            try
            {
                var json = File.ReadAllText(metadataPath);
                var serializationOptions = new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                };
                _metadataCache = JsonSerializer.Deserialize<List<CachedAnalysisMetadata>>(json, serializationOptions) ?? new();
            }
            catch
            {
                _metadataCache = new();
            }
        }
    }
}
