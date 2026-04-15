using System.Collections.Generic;

namespace WpiLogLib
{
    public class WpiLogEntry
    {
        public ushort Id { get; set; }
        public string Name { get; set; } = "";
        public string Type { get; set; } = "";
        public List<(ulong Timestamp, WpiLogValue Value)> Values { get; set; } = new();
    }
}
