using System;
using System.Globalization;
using System.Runtime.InteropServices;

namespace WpiLogLib
{
    /// <summary>
    /// Discriminated union that stores a parsed wpilog value without boxing.
    /// Uses an explicit layout to overlap the numeric fields (only one is valid at a time,
    /// determined by <see cref="Kind"/>).
    /// </summary>
    [StructLayout(LayoutKind.Auto)]
    public readonly struct WpiLogValue
    {
        public enum ValueKind : byte { Double, Long, Bool, String }

        public ValueKind Kind { get; }

        // Numeric storage — only one is meaningful per Kind
        private readonly double _doubleVal;
        private readonly long _longVal;
        private readonly bool _boolVal;

        // String storage — null unless Kind == String
        private readonly string? _stringVal;

        private WpiLogValue(ValueKind kind, double d, long l, bool b, string? s)
        {
            Kind = kind;
            _doubleVal = d;
            _longVal = l;
            _boolVal = b;
            _stringVal = s;
        }

        public static WpiLogValue FromDouble(double v) => new(ValueKind.Double, v, 0, false, null);
        public static WpiLogValue FromLong(long v) => new(ValueKind.Long, 0, v, false, null);
        public static WpiLogValue FromBool(bool v) => new(ValueKind.Bool, 0, 0, v, null);
        public static WpiLogValue FromString(string v) => new(ValueKind.String, 0, 0, false, v);

        public double AsDouble => _doubleVal;
        public long AsLong => _longVal;
        public bool AsBool => _boolVal;
        public string? AsString => _stringVal;

        /// <summary>Formats the value to a CSV-safe string without boxing.</summary>
        public string Format(string entryType) => entryType switch
        {
            "double" => _doubleVal.ToString("G17", CultureInfo.InvariantCulture),
            "float" => ((float)_doubleVal).ToString("G9", CultureInfo.InvariantCulture),
            "int64" => _longVal.ToString(CultureInfo.InvariantCulture),
            "int32" => ((int)_longVal).ToString(CultureInfo.InvariantCulture),
            "boolean" => _boolVal ? "1" : "0",
            "string" => "\"" + (_stringVal?.Replace("\"", "\"\"") ?? "") + "\"",
            _ => Kind switch
            {
                ValueKind.Double => _doubleVal.ToString("G17", CultureInfo.InvariantCulture),
                ValueKind.Long => _longVal.ToString(CultureInfo.InvariantCulture),
                ValueKind.Bool => _boolVal ? "1" : "0",
                ValueKind.String => _stringVal ?? "",
                _ => ""
            }
        };
    }
}
