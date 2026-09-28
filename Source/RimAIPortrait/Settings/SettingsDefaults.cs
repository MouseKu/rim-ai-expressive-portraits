using System;
using System.IO;
using UnityEngine;
using Verse;

namespace RimAIPortrait
{
    /// <summary>Loads packaged scalar settings before RimWorld deserializes saved settings.</summary>
    internal static class SettingsDefaults
    {
        private static string json;
        internal static PortraitSettings Values { get; private set; }

        internal static void Load(ModContentPack content)
        {
            string path = Path.Combine(content.RootDir, "Json", "default-values.json");
            json = File.ReadAllText(path);
            if (string.IsNullOrWhiteSpace(json))
                throw new InvalidDataException("Json/default-values.json is empty.");
            Values = new PortraitSettings();
        }

        internal static void Apply(PortraitSettings settings)
        {
            if (json == null)
                throw new InvalidOperationException("Settings defaults must be loaded before settings are created.");
            JsonUtility.FromJsonOverwrite(json, settings);
        }
    }
}
