using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;

namespace DragonScope
{
    public partial class Form1
    {
        private void ParseXmlFile(string filePath)
        {
            xmlDataRange.Clear();
            xmlDataBool.Clear();
            xmlAlias.Clear();
            m_excludedStrings.Clear();
            _aliasCache.Clear();
            _typeKeyCache.Clear();

            var xmlDoc = XDocument.Load(filePath);
            foreach (var element in xmlDoc.Descendants("ExcludedValue"))
            {
                var name = element.Attribute("Name")?.Value;
                if (!string.IsNullOrEmpty(name)) m_excludedStrings.Add(name);
            }
            foreach (var element in xmlDoc.Descendants("RangeValue"))
            {
                var name = element.Attribute("Name")?.Value;
                var rangeHigh = element.Attribute("Rangehigh")?.Value ?? string.Empty;
                var rangeLow = element.Attribute("Rangelow")?.Value ?? string.Empty;
                var priority = element.Attribute("Priority")?.Value ?? string.Empty;
                if (!string.IsNullOrEmpty(name)) xmlDataRange[name] = (rangeHigh, rangeLow, priority);
            }
            foreach (var element in xmlDoc.Descendants("BoolValue"))
            {
                var name = element.Attribute("Name")?.Value;
                var flagState = element.Attribute("FlagState")?.Value ?? string.Empty;
                var priority = element.Attribute("Priority")?.Value ?? string.Empty;
                if (!string.IsNullOrEmpty(name)) xmlDataBool[name] = (flagState, priority);
            }
            foreach (var element in xmlDoc.Descendants("CANDiviceAlias"))
            {
                var logName = element.Attribute("LogName")?.Value;
                var alias = element.Attribute("Alias")?.Value;
                if (!string.IsNullOrEmpty(logName) && !string.IsNullOrEmpty(alias))
                    xmlAlias[logName] = alias;
            }
        }

        /// <summary>Thread-safe cached version of ResolveTypeKey.</summary>
        private (m_xmlDataType Type, string Key) ResolveTypeKeyCached(string name)
        {
            return _typeKeyCache.GetOrAdd(name, n => ResolveTypeKey(n));
        }

        private (m_xmlDataType Type, string Key) ResolveTypeKey(string name)
        {
            foreach (var key in m_excludedStrings)
                if (!string.IsNullOrEmpty(key) && name.Contains(key, StringComparison.Ordinal))
                    return (m_xmlDataType.TYPE_EXCLUDED, key);

            foreach (var key in xmlDataRange.Keys)
                if (!string.IsNullOrEmpty(key) && name.Contains(key, StringComparison.Ordinal))
                    return (m_xmlDataType.TYPE_RANGE, key);

            foreach (var key in xmlDataBool.Keys)
                if (!string.IsNullOrEmpty(key) && name.Contains(key, StringComparison.Ordinal))
                    return (m_xmlDataType.TYPE_BOOLEAN, key);

            return (m_xmlDataType.TYPE_INVALID, "");
        }

        /// <summary>Thread-safe cached alias lookup.</summary>
        private string GetAliasCached(string deviceName)
        {
            return _aliasCache.GetOrAdd(deviceName, GetAlias);
        }

        private string GetAlias(string deviceName)
        {
            foreach (var kvp in xmlAlias)
                if (deviceName.Contains(kvp.Key))
                    return deviceName.Replace(kvp.Key, kvp.Value);
            return deviceName;
        }
    }
}