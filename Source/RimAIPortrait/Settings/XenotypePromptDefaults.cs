using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using Verse;

namespace RimAIPortrait
{
    internal static class XenotypePromptDefaults
    {
        [DataContract]
        private sealed class Entry
        {
            [DataMember]
            public string key = "";
            [DataMember]
            public string value = "";
        }

        [DataContract]
        private sealed class Document
        {
            [DataMember]
            public Entry[] entries = new Entry[0];
        }

        private static Entry[] entries = new Entry[0];

        internal static void Load(ModContentPack content)
        {
            try
            {
                string path = Path.Combine(content.RootDir, "Json", "xenotype-provider.json");
                Document document;
                using (var stream = File.OpenRead(path))
                    document = (Document)new DataContractJsonSerializer(typeof(Document)).ReadObject(stream);
                if (document == null || document.entries == null || document.entries.Length == 0)
                    throw new FormatException("Missing or empty entries array.");
                entries = document.entries;
            }
            catch (Exception exception)
            {
                entries = new Entry[0];
                Log.Warning("[Rim AI Expressive Portraits] Could not load Json/xenotype-provider.json: "
                    + exception.Message);
            }
        }

        internal static string ValueFor(string defName, string fallback)
        {
            Entry entry = entries.FirstOrDefault(item => item != null && item.key == defName);
            return entry != null && !string.IsNullOrWhiteSpace(entry.value) ? entry.value : fallback;
        }

        internal static bool HasValueFor(string defName)
        {
            return entries.Any(item => item != null && item.key == defName
                && !string.IsNullOrWhiteSpace(item.value));
        }

        internal static void Apply(PortraitSettings settings)
        {
            if (settings.XenotypePromptEntries == null)
                settings.XenotypePromptEntries = new List<XenotypePromptEntry>();
            foreach (XenotypePromptEntry entry in PackagedEntries())
            {
                XenotypePromptEntry saved = settings.XenotypePromptEntries.FirstOrDefault(item =>
                    item != null && item.DefName == entry.DefName);
                if (saved == null)
                {
                    settings.XenotypePromptEntries.Add(entry);
                    continue;
                }
                if (saved.DefaultValue != null && saved.Value == saved.DefaultValue)
                {
                    if (!saved.Enabled && saved.DefaultValue != entry.Value)
                        saved.Enabled = true;
                    saved.Value = entry.Value;
                }
                else if (saved.DefaultValue == null &&
                    (string.IsNullOrWhiteSpace(saved.Value) ||
                    string.Equals(saved.Value, saved.DefName, StringComparison.OrdinalIgnoreCase)))
                    saved.Value = entry.Value;
                saved.DefaultValue = entry.Value;
            }
        }

        internal static IEnumerable<XenotypePromptEntry> PackagedEntries()
        {
            foreach (Entry entry in entries)
            {
                if (entry == null || string.IsNullOrWhiteSpace(entry.key)
                    || string.IsNullOrWhiteSpace(entry.value)) continue;
                yield return new XenotypePromptEntry {
                    DefName = entry.key, Value = entry.value, DefaultValue = entry.value, Enabled = true
                };
            }
        }
    }
}
