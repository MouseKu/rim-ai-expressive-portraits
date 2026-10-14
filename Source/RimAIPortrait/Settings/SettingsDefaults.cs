using System;
using System.IO;
#if RIMWORLD_1_7
using System.Reflection;
using System.Text.Json;
#else
using UnityEngine;
#endif
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
#if RIMWORLD_1_7
            using (JsonDocument document = JsonDocument.Parse(json))
            {
                foreach (JsonProperty property in document.RootElement.EnumerateObject())
                {
                    FieldInfo field = typeof(PortraitSettings).GetField(property.Name);
                    if (field == null) throw new InvalidDataException("Unknown default setting: " + property.Name);
                    Type type = field.FieldType;
                    object value = type == typeof(string) ? (object)property.Value.GetString()
                        : type == typeof(bool) ? property.Value.GetBoolean()
                        : type == typeof(int) ? property.Value.GetInt32()
                        : type == typeof(float) ? property.Value.GetSingle()
                        : type.IsEnum ? Enum.ToObject(type, property.Value.GetInt32())
                        : throw new InvalidDataException("Unsupported default setting: " + property.Name);
                    field.SetValue(settings, value);
                }
            }
#else
            JsonUtility.FromJsonOverwrite(json, settings);
#endif
        }
    }
}
