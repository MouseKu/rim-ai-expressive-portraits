using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;
using Verse;

namespace RimAIPortrait
{
    /// <summary>The shareable, file-based prompt ZIP format.</summary>
    internal static class PromptArchiveBundle
    {
        private static IEnumerable<PortraitState> ExpressionStates()
        {
            foreach (PortraitState state in Enum.GetValues(typeof(PortraitState)))
                if (state != PortraitState.Base) yield return state;
        }

        private const string ValuesPath = "options/values.json";
        private const int Version = 10;
        private const int PreviousVersion = 9;
        private const int HeadgearVersion = 9;
        private const int XenotypeVersion = 8;
        private const int LocalProviderVersion = 6;
        private const int LocalOptionsVersion = 5;
        private const int TagOptionsVersion = 7;
        private const int LegacyModelFieldCount = 12;
        private const int LegacyOptionFileCount = 8;
        private static readonly string[] TagLimitFields = {
            nameof(PortraitSettings.TagExtractorBasePositiveLimit),
            nameof(PortraitSettings.TagExtractorBaseNegativeLimit),
            nameof(PortraitSettings.TagExtractorInpaintPositiveLimit),
            nameof(PortraitSettings.TagExtractorInpaintNegativeLimit)
        };
        private const string InpaintPromptPath = "Prompt/Common/TagExtractorInpaint.txt";
        private const string LegacyInpaintPromptPath = "Prompt/Common/TagExtractorInpaintBatch.txt";
        private const string XenotypePath = "options/xenotype-provider.json";
        private const int MaximumEntryBytes = 2 * 1024 * 1024;
        private const int MaximumTotalBytes = 8 * 1024 * 1024;

        private static readonly string[] ModelFields = {
            nameof(PortraitSettings.OpenAiModel), nameof(PortraitSettings.OpenAiExpressionModel),
            nameof(PortraitSettings.GoogleModel), nameof(PortraitSettings.GoogleExpressionModel),
            nameof(PortraitSettings.NovelAiModel), nameof(PortraitSettings.NovelAiExpressionModel),
            nameof(PortraitSettings.RegenerateOpenAiModel), nameof(PortraitSettings.RegenerateGoogleModel),
            nameof(PortraitSettings.RegenerateNovelAiModel), nameof(PortraitSettings.TagExtractorOpenAiModel),
            nameof(PortraitSettings.TagExtractorGoogleModel), nameof(PortraitSettings.TagExtractorLocalModel),
            nameof(PortraitSettings.LocalOpenAiModel), nameof(PortraitSettings.LocalOpenAiExpressionModel),
            nameof(PortraitSettings.RegenerateLocalOpenAiModel)
        };

        private static readonly string[] OptionFiles = {
            "google-ai-img-caption.json", "google-base-options.json", "google-expression-options.json",
            "nai-base-options.json", "nai-expression-options.json", "open-ai-img-caption.json",
            "openai-base-options.json", "openai-expression-options.json",
            "local-openai-base-options.json", "local-openai-expression-options.json",
            "local-openai-regenerate-options.json", "local-ai-img-caption.json",
            "tag-extractor-openai-options.json", "tag-extractor-google-options.json",
            "tag-extractor-local-options.json"
        };

        [DataContract]
        private sealed class ValuesData
        {
            [DataMember] public int version;
            [DataMember] public string provider;
            [DataMember] public string expressionProvider;
            [DataMember] public string regenerateProvider;
            [DataMember] public Dictionary<string, string> models;
            [DataMember] public string tagExtractorBackend;
            [DataMember] public Dictionary<string, bool> inclusion;
            [DataMember(EmitDefaultValue = false)] public float? novelAiInpaintStrength;
            [DataMember(EmitDefaultValue = false)] public float? novelAiSleepInpaintStrength;
            [DataMember(EmitDefaultValue = false)] public Dictionary<string, float> inpaintStrengths;
            [DataMember(EmitDefaultValue = false)] public Dictionary<string, int> tagLimits;
        }

        private sealed class BundleData
        {
            public ValuesData Values;
            public Dictionary<string, string> Prompts;
            public Dictionary<string, string> Options;
            public XenotypeData Xenotypes;
        }

        [DataContract]
        private sealed class XenotypeData
        {
            [DataMember] public List<XenotypeEntryData> entries;
        }

        [DataContract]
        private sealed class XenotypeEntryData
        {
            [DataMember] public string key;
            [DataMember] public string value;
            [DataMember] public bool enabled;
        }

        public static bool IsBundle(string path)
        {
            using (var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            using (var archive = new ZipArchive(file, ZipArchiveMode.Read))
                return archive.GetEntry(ValuesPath) != null;
        }

        public static string Export(PortraitSettings settings)
        {
            BundleData data = Capture(settings);
            Validate(data);
            var entries = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (KeyValuePair<string, string> prompt in data.Prompts)
                entries[prompt.Key] = prompt.Value ?? "";
            foreach (KeyValuePair<string, string> option in data.Options)
                entries["options/" + option.Key] = option.Value;
            using (var stream = new MemoryStream())
            {
                ValuesSerializer().WriteObject(stream, data.Values);
                entries[ValuesPath] = Encoding.UTF8.GetString(stream.ToArray());
            }
            using (var stream = new MemoryStream())
            {
                XenotypeSerializer().WriteObject(stream, data.Xenotypes);
                entries[XenotypePath] = Encoding.UTF8.GetString(stream.ToArray());
            }

            var encoded = new Dictionary<string, byte[]>(StringComparer.Ordinal);
            int totalBytes = 0;
            foreach (KeyValuePair<string, string> entry in entries)
            {
                byte[] bytes = Encoding.UTF8.GetBytes(entry.Value);
                totalBytes += bytes.Length;
                if (bytes.Length > MaximumEntryBytes || totalBytes > MaximumTotalBytes)
                    throw new InvalidDataException("Prompt archive is too large.");
                encoded[entry.Key] = bytes;
            }

            Directory.CreateDirectory(PromptArchive.DirectoryPath);
            string stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
            string path = Path.Combine(PromptArchive.DirectoryPath, "RimAIPortrait-Prompts-" + stamp + ".zip");
            for (int suffix = 2; File.Exists(path); suffix++)
                path = Path.Combine(PromptArchive.DirectoryPath, "RimAIPortrait-Prompts-" + stamp + "-" + suffix + ".zip");
            using (var file = new FileStream(path, FileMode.CreateNew, FileAccess.Write))
            using (var archive = new ZipArchive(file, ZipArchiveMode.Create))
            {
                foreach (KeyValuePair<string, byte[]> entry in encoded)
                {
                    using (Stream output = archive.CreateEntry(entry.Key, CompressionLevel.Optimal).Open())
                        output.Write(entry.Value, 0, entry.Value.Length);
                }
            }
            return path;
        }

        public static void Import(PortraitSettings settings, string path)
        {
            var data = new BundleData {
                Prompts = new Dictionary<string, string>(StringComparer.Ordinal),
                Options = new Dictionary<string, string>(StringComparer.Ordinal)
            };
            using (var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            using (var archive = new ZipArchive(file, ZipArchiveMode.Read))
            {
                int totalBytes = 0;
                using (var stream = new MemoryStream(ReadBytes(archive, ValuesPath, ref totalBytes)))
                    data.Values = (ValuesData)ValuesSerializer().ReadObject(stream);
                if (data.Values == null || (data.Values.version != Version
                    && data.Values.version != PreviousVersion && data.Values.version != 8
                    && data.Values.version != 7
                    && data.Values.version != 6 && data.Values.version != 5
                    && data.Values.version != 4 && data.Values.version != 3))
                    throw new InvalidDataException("Unsupported prompt archive version.");
                foreach (string name in PromptNames(data.Values.version))
                {
                    string archiveName = data.Values.version == 3 && name == InpaintPromptPath
                        ? LegacyInpaintPromptPath : name;
                    data.Prompts[name] = ReadText(archive, archiveName, ref totalBytes);
                }
                int optionCount = OptionCount(data.Values.version);
                for (int index = 0; index < optionCount; index++)
                    data.Options[OptionFiles[index]] = ReadText(archive, "options/" + OptionFiles[index], ref totalBytes);
                if (data.Values.version >= XenotypeVersion)
                {
                    using (var stream = new MemoryStream(ReadBytes(archive, XenotypePath, ref totalBytes)))
                        data.Xenotypes = (XenotypeData)XenotypeSerializer().ReadObject(stream);
                }
                if (archive.Entries.Count != data.Prompts.Count + data.Options.Count
                    + (data.Values.version >= XenotypeVersion ? 2 : 1))
                    throw new InvalidDataException("Prompt archive contains unexpected files.");
            }
            Validate(data);
            Apply(settings, data);
        }

        private static BundleData Capture(PortraitSettings settings)
        {
            var data = new BundleData {
                Prompts = new Dictionary<string, string>(StringComparer.Ordinal),
                Options = new Dictionary<string, string>(StringComparer.Ordinal),
                Values = new ValuesData {
                    version = Version,
                    provider = settings.Provider.ToString(),
                    expressionProvider = settings.ExpressionProvider.ToString(),
                    regenerateProvider = settings.RegenerateProvider.ToString(),
                    models = new Dictionary<string, string>(StringComparer.Ordinal),
                    tagExtractorBackend = settings.TagExtractorBackend.ToString(),
                    inclusion = new Dictionary<string, bool>(StringComparer.Ordinal),
                    novelAiInpaintStrength = settings.NovelAiInpaintStrength,
                    novelAiSleepInpaintStrength = settings.NovelAiSleepInpaintStrength,
                    inpaintStrengths = new Dictionary<string, float>(StringComparer.Ordinal),
                    tagLimits = new Dictionary<string, int>(StringComparer.Ordinal)
                },
                Xenotypes = new XenotypeData { entries = new List<XenotypeEntryData>() }
            };
            if (settings.XenotypePromptEntries != null)
            {
                foreach (XenotypePromptEntry entry in settings.XenotypePromptEntries)
                {
                    if (entry == null) continue;
                    data.Xenotypes.entries.Add(new XenotypeEntryData {
                        key = entry.DefName, value = entry.Value, enabled = entry.Enabled
                    });
                }
            }
            var savedKeys = new HashSet<string>(StringComparer.Ordinal);
            foreach (XenotypeEntryData entry in data.Xenotypes.entries)
                if (entry.key != null) savedKeys.Add(entry.key);
            foreach (XenotypePromptEntry entry in XenotypePromptDefaults.PackagedEntries())
            {
                if (!savedKeys.Add(entry.DefName)) continue;
                data.Xenotypes.entries.Add(new XenotypeEntryData {
                    key = entry.DefName, value = entry.Value, enabled = entry.Enabled
                });
            }
            data.Prompts["Prompt/Additional.txt"] = settings.Prompt;
            data.Prompts["Prompt/Common/TagExtractorBase.txt"] = settings.TagExtractorBasePrompt;
            data.Prompts[InpaintPromptPath] = settings.TagExtractorInpaintPrompt;
            foreach (AiProvider provider in Enum.GetValues(typeof(AiProvider)))
            {
                string folder = "Prompt/" + PromptLibrary.ProviderFolder(provider) + "/";
                data.Prompts[folder + "Base.txt"] = PromptArchive.BasePrompt(settings, provider);
                data.Prompts[folder + "Expression.txt"] = settings.GetExpressionInstructions(provider);
                foreach (PortraitState state in ExpressionStates())
                    data.Prompts[folder + state + ".txt"] = settings.GetStatePrompt(provider, state);
            }
            data.Options["google-ai-img-caption.json"] = PortraitImageCaptions.EffectiveJson(AiProvider.Google, settings);
            data.Options["google-base-options.json"] = settings.GoogleOptions ?? ProviderOptionDefaults.GoogleBase;
            data.Options["google-expression-options.json"] = settings.GoogleExpressionOptions ?? ProviderOptionDefaults.GoogleExpression;
            data.Options["nai-base-options.json"] = settings.NovelAiOptions ?? ProviderOptionDefaults.NovelAiBase;
            data.Options["nai-expression-options.json"] = settings.NovelAiExpressionOptions ?? ProviderOptionDefaults.NovelAiExpression;
            data.Options["open-ai-img-caption.json"] = PortraitImageCaptions.EffectiveJson(AiProvider.OpenAI, settings);
            data.Options["openai-base-options.json"] = settings.OpenAiOptions ?? ProviderOptionDefaults.OpenAiBase;
            data.Options["openai-expression-options.json"] = settings.OpenAiExpressionOptions ?? ProviderOptionDefaults.OpenAiExpression;
            data.Options["local-openai-base-options.json"] = settings.LocalOpenAiOptions ?? ProviderOptionDefaults.LocalBase;
            data.Options["local-openai-expression-options.json"] = settings.LocalOpenAiExpressionOptions ?? ProviderOptionDefaults.LocalExpression;
            data.Options["local-openai-regenerate-options.json"] = settings.RegenerateLocalOpenAiOptions ?? ProviderOptionDefaults.LocalBase;
            data.Options["local-ai-img-caption.json"] = PortraitImageCaptions.EffectiveJson(AiProvider.LocalOpenAI, settings);
            data.Options["tag-extractor-openai-options.json"] = settings.TagExtractorOpenAiOptions ?? ProviderOptionDefaults.TagExtractorOpenAi;
            data.Options["tag-extractor-google-options.json"] = settings.TagExtractorGoogleOptions ?? ProviderOptionDefaults.TagExtractorGoogle;
            data.Options["tag-extractor-local-options.json"] = settings.TagExtractorLocalOptions ?? ProviderOptionDefaults.TagExtractorLocal;
            foreach (string name in ModelFields)
                data.Values.models[name] = (string)typeof(PortraitSettings).GetField(name).GetValue(settings);
            foreach (string name in PromptArchive.InclusionFields)
                data.Values.inclusion[name] = (bool)typeof(PortraitSettings).GetField(name).GetValue(settings);
            foreach (PortraitState state in ExpressionStates())
                data.Values.inpaintStrengths[state.ToString()] = settings.GetInpaintStrength(state);
            foreach (string name in TagLimitFields)
                data.Values.tagLimits[name] = (int)typeof(PortraitSettings).GetField(name).GetValue(settings);
            return data;
        }

        private static IEnumerable<string> PromptNames(int version)
        {
            yield return "Prompt/Additional.txt";
            yield return "Prompt/Common/TagExtractorBase.txt";
            yield return InpaintPromptPath;
            foreach (AiProvider provider in Enum.GetValues(typeof(AiProvider)))
            {
                if (provider == AiProvider.LocalOpenAI && version < LocalProviderVersion) continue;
                string folder = "Prompt/" + PromptLibrary.ProviderFolder(provider) + "/";
                yield return folder + "Base.txt";
                yield return folder + "Expression.txt";
                foreach (PortraitState state in ExpressionStates())
                    yield return folder + state + ".txt";
            }
        }

        private static bool ValidStrength(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value) && value >= .1f && value <= 1f;
        }

        private static void Validate(BundleData data)
        {
            ValuesData values = data.Values;
            AiProvider provider;
            AiProvider expressionProvider;
            AiProvider regenerateProvider;
            TagExtractorModel backend;
            int expectedOptions = values != null ? OptionCount(values.version) : LegacyOptionFileCount;
            if (values == null || (values.version != Version && values.version != PreviousVersion
                    && values.version != 8 && values.version != 7
                    && values.version != 6 && values.version != 5
                && values.version != 4 && values.version != 3)
                || data.Prompts == null || data.Options == null
                || (values.provider != null && (!Enum.TryParse(values.provider, out provider)
                    || !Enum.IsDefined(typeof(AiProvider), provider)))
                || (values.expressionProvider != null && (!Enum.TryParse(values.expressionProvider, out expressionProvider)
                    || !Enum.IsDefined(typeof(AiProvider), expressionProvider)))
                || (values.regenerateProvider != null && (!Enum.TryParse(values.regenerateProvider, out regenerateProvider)
                    || !Enum.IsDefined(typeof(AiProvider), regenerateProvider)))
                || (values.tagExtractorBackend != null && (!Enum.TryParse(values.tagExtractorBackend, out backend)
                    || !Enum.IsDefined(typeof(TagExtractorModel), backend)))
                || (values.novelAiInpaintStrength.HasValue && !ValidStrength(values.novelAiInpaintStrength.Value))
                || (values.novelAiSleepInpaintStrength.HasValue && !ValidStrength(values.novelAiSleepInpaintStrength.Value)))
                throw new InvalidDataException("Invalid prompt archive values.json.");
            if (values.inpaintStrengths != null)
            {
                foreach (PortraitState state in ExpressionStates())
                {
                    float strength;
                    if (values.inpaintStrengths.TryGetValue(state.ToString(), out strength)
                        && !ValidStrength(strength))
                        throw new InvalidDataException("Invalid inpaint strength: " + state);
                }
            }
            if (values.tagLimits != null)
            {
                for (int index = 0; index < TagLimitFields.Length; index++)
                {
                    int limit;
                    if (values.tagLimits.TryGetValue(TagLimitFields[index], out limit)
                        && (limit < (index % 2 == 0 ? 1 : 0) || limit > 50))
                        throw new InvalidDataException("Invalid tag limit: " + TagLimitFields[index]);
                }
            }
            int promptCount = 0;
            foreach (string name in PromptNames(values.version))
            {
                if (!data.Prompts.ContainsKey(name)) throw new InvalidDataException("Missing prompt: " + name);
                promptCount++;
            }
            if (data.Prompts.Count != promptCount || data.Options.Count != expectedOptions)
                throw new InvalidDataException("Prompt archive is incomplete.");
            for (int index = 0; index < expectedOptions; index++)
            {
                string name = OptionFiles[index];
                string json;
                if (!data.Options.TryGetValue(name, out json)) throw new InvalidDataException("Missing options: " + name);
                if (name == "open-ai-img-caption.json" || name == "google-ai-img-caption.json"
                    || name == "local-ai-img-caption.json")
                    PortraitImageCaptions.Parse(json, name);
                else PromptArchive.ValidateOptions(json);
            }
            if (values.version >= XenotypeVersion)
            {
                if (data.Xenotypes == null || data.Xenotypes.entries == null)
                    throw new InvalidDataException("Missing xenotype-provider.json entries.");
                var keys = new HashSet<string>(StringComparer.Ordinal);
                foreach (XenotypeEntryData entry in data.Xenotypes.entries)
                {
                    if (entry == null || string.IsNullOrWhiteSpace(entry.key) || !keys.Add(entry.key))
                        throw new InvalidDataException("Invalid xenotype-provider.json entry.");
                }
            }
        }

        private static void Apply(PortraitSettings settings, BundleData data)
        {
            ValuesData values = data.Values;
            if (values.provider != null)
                settings.Provider = (AiProvider)Enum.Parse(typeof(AiProvider), values.provider);
            if (values.expressionProvider != null)
                settings.ExpressionProvider = (AiProvider)Enum.Parse(typeof(AiProvider), values.expressionProvider);
            if (values.regenerateProvider != null)
                settings.RegenerateProvider = (AiProvider)Enum.Parse(typeof(AiProvider), values.regenerateProvider);
            if (values.tagExtractorBackend != null)
                settings.TagExtractorBackend = (TagExtractorModel)Enum.Parse(typeof(TagExtractorModel), values.tagExtractorBackend);
            if (values.novelAiInpaintStrength.HasValue)
                settings.NovelAiInpaintStrength = values.novelAiInpaintStrength.Value;
            if (values.novelAiSleepInpaintStrength.HasValue)
                settings.NovelAiSleepInpaintStrength = values.novelAiSleepInpaintStrength.Value;
            foreach (PortraitState state in ExpressionStates())
            {
                float strength;
                if (values.inpaintStrengths != null
                    && values.inpaintStrengths.TryGetValue(state.ToString(), out strength))
                    settings.SetInpaintStrength(state, strength);
                else if (values.inpaintStrengths == null && values.version < HeadgearVersion)
                {
                    float? legacyStrength = state == PortraitState.Sleep
                        ? values.novelAiSleepInpaintStrength : values.novelAiInpaintStrength;
                    if (legacyStrength.HasValue) settings.SetInpaintStrength(state, legacyStrength.Value);
                }
            }
            int modelCount = values.version >= LocalOptionsVersion ? ModelFields.Length : LegacyModelFieldCount;
            for (int index = 0; index < modelCount; index++)
            {
                string model;
                if (values.models != null && values.models.TryGetValue(ModelFields[index], out model))
                    typeof(PortraitSettings).GetField(ModelFields[index]).SetValue(settings, model);
            }
            foreach (string name in PromptArchive.InclusionFields)
            {
                if (values.version < HeadgearVersion && name == nameof(PortraitSettings.ExcludeHeadgearFromSprite)) continue;
                bool included;
                if (values.inclusion != null && values.inclusion.TryGetValue(name, out included))
                    typeof(PortraitSettings).GetField(name).SetValue(settings, included);
            }
            for (int index = 0; index < TagLimitFields.Length; index++)
            {
                int limit;
                if (values.tagLimits != null && values.tagLimits.TryGetValue(TagLimitFields[index], out limit))
                    typeof(PortraitSettings).GetField(TagLimitFields[index]).SetValue(settings, limit);
            }
            settings.Prompt = data.Prompts["Prompt/Additional.txt"];
            settings.TagExtractorBasePrompt = data.Prompts["Prompt/Common/TagExtractorBase.txt"];
            settings.TagExtractorInpaintPrompt = values.version == 3
                ? PromptArchive.InpaintPromptDefault() : data.Prompts[InpaintPromptPath];
            foreach (AiProvider provider in Enum.GetValues(typeof(AiProvider)))
            {
                if (provider == AiProvider.LocalOpenAI && values.version < LocalProviderVersion) continue;
                string folder = "Prompt/" + PromptLibrary.ProviderFolder(provider) + "/";
                PromptArchive.SetBasePrompt(settings, provider, data.Prompts[folder + "Base.txt"]);
                PromptArchive.SetExpressionPrompt(settings, provider, data.Prompts[folder + "Expression.txt"]);
                foreach (PortraitState state in ExpressionStates())
                    settings.SetStatePrompt(provider, state, data.Prompts[folder + state + ".txt"]);
            }
            settings.GoogleOptions = data.Options["google-base-options.json"];
            settings.GoogleExpressionOptions = data.Options["google-expression-options.json"];
            settings.NovelAiOptions = data.Options["nai-base-options.json"];
            settings.NovelAiExpressionOptions = data.Options["nai-expression-options.json"];
            settings.OpenAiOptions = data.Options["openai-base-options.json"];
            settings.OpenAiExpressionOptions = data.Options["openai-expression-options.json"];
            if (values.version >= LocalOptionsVersion)
            {
                settings.LocalOpenAiOptions = data.Options["local-openai-base-options.json"];
                settings.LocalOpenAiExpressionOptions = data.Options["local-openai-expression-options.json"];
                settings.RegenerateLocalOpenAiOptions = data.Options["local-openai-regenerate-options.json"];
            }
            settings.GoogleImageCaptionsJson = data.Options["google-ai-img-caption.json"];
            settings.OpenAiImageCaptionsJson = data.Options["open-ai-img-caption.json"];
            if (values.version >= LocalProviderVersion)
                settings.LocalOpenAiImageCaptionsJson = data.Options["local-ai-img-caption.json"];
            if (values.version >= TagOptionsVersion)
            {
                settings.TagExtractorOpenAiOptions = data.Options["tag-extractor-openai-options.json"];
                settings.TagExtractorGoogleOptions = data.Options["tag-extractor-google-options.json"];
                settings.TagExtractorLocalOptions = data.Options["tag-extractor-local-options.json"];
            }
            if (values.version >= XenotypeVersion)
            {
                var entries = new List<XenotypePromptEntry>(data.Xenotypes.entries.Count);
                foreach (XenotypeEntryData entry in data.Xenotypes.entries)
                    entries.Add(new XenotypePromptEntry {
                        DefName = entry.key, Value = entry.value, Enabled = entry.enabled
                    });
                settings.XenotypePromptEntries = entries;
            }
        }

        private static int OptionCount(int version)
        {
            return version >= TagOptionsVersion ? OptionFiles.Length
                : version == LocalProviderVersion ? OptionFiles.Length - 3
                : version == LocalOptionsVersion ? OptionFiles.Length - 4 : LegacyOptionFileCount;
        }

        private static DataContractJsonSerializer XenotypeSerializer()
        {
            return new DataContractJsonSerializer(typeof(XenotypeData));
        }

        private static DataContractJsonSerializer ValuesSerializer()
        {
            return new DataContractJsonSerializer(typeof(ValuesData),
                new DataContractJsonSerializerSettings { UseSimpleDictionaryFormat = true });
        }

        private static string ReadText(ZipArchive archive, string name, ref int totalBytes)
        {
            return new UTF8Encoding(false, true).GetString(ReadBytes(archive, name, ref totalBytes));
        }

        private static byte[] ReadBytes(ZipArchive archive, string name, ref int totalBytes)
        {
            ZipArchiveEntry item = archive.GetEntry(name);
            if (item == null || item.Length > MaximumEntryBytes)
                throw new InvalidDataException("Missing or oversized ZIP entry: " + name);
            using (Stream input = item.Open())
            using (var output = new MemoryStream())
            {
                byte[] buffer = new byte[8192];
                int count;
                while ((count = input.Read(buffer, 0, buffer.Length)) > 0)
                {
                    totalBytes += count;
                    if (output.Length + count > MaximumEntryBytes || totalBytes > MaximumTotalBytes)
                        throw new InvalidDataException("Prompt archive is too large.");
                    output.Write(buffer, 0, count);
                }
                return output.ToArray();
            }
        }
    }
}
