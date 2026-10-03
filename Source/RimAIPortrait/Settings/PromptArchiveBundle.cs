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
        private const int Version = 3;
        private const int MaximumEntryBytes = 2 * 1024 * 1024;
        private const int MaximumTotalBytes = 8 * 1024 * 1024;

        private static readonly string[] ModelFields = {
            nameof(PortraitSettings.OpenAiModel), nameof(PortraitSettings.OpenAiExpressionModel),
            nameof(PortraitSettings.GoogleModel), nameof(PortraitSettings.GoogleExpressionModel),
            nameof(PortraitSettings.NovelAiModel), nameof(PortraitSettings.NovelAiExpressionModel),
            nameof(PortraitSettings.RegenerateOpenAiModel), nameof(PortraitSettings.RegenerateGoogleModel),
            nameof(PortraitSettings.RegenerateNovelAiModel), nameof(PortraitSettings.TagExtractorOpenAiModel),
            nameof(PortraitSettings.TagExtractorGoogleModel), nameof(PortraitSettings.TagExtractorLocalModel)
        };

        private static readonly string[] OptionFiles = {
            "google-ai-img-caption.json", "google-base-options.json", "google-expression-options.json",
            "nai-base-options.json", "nai-expression-options.json", "open-ai-img-caption.json",
            "openai-base-options.json", "openai-expression-options.json"
        };

        [DataContract]
        private sealed class ValuesData
        {
            [DataMember] public int version;
            [DataMember] public string provider;
            [DataMember] public string expressionProvider;
            [DataMember] public string regenerateProvider;
            [DataMember] public Dictionary<string, string> models;
            [DataMember] public bool enableTagExtractor;
            [DataMember] public string tagExtractorBackend;
            [DataMember] public Dictionary<string, bool> inclusion;
            [DataMember] public float novelAiInpaintStrength;
            [DataMember] public float novelAiSleepInpaintStrength;
            [DataMember(EmitDefaultValue = false)] public Dictionary<string, float> inpaintStrengths;
        }

        private sealed class BundleData
        {
            public ValuesData Values;
            public Dictionary<string, string> Prompts;
            public Dictionary<string, string> Options;
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
                foreach (string name in PromptNames())
                    data.Prompts[name] = ReadText(archive, name, ref totalBytes);
                foreach (string fileName in OptionFiles)
                    data.Options[fileName] = ReadText(archive, "options/" + fileName, ref totalBytes);
                using (var stream = new MemoryStream(ReadBytes(archive, ValuesPath, ref totalBytes)))
                    data.Values = (ValuesData)ValuesSerializer().ReadObject(stream);
                if (archive.Entries.Count != data.Prompts.Count + data.Options.Count + 1)
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
                    enableTagExtractor = settings.EnableTagExtractor,
                    tagExtractorBackend = settings.TagExtractorBackend.ToString(),
                    inclusion = new Dictionary<string, bool>(StringComparer.Ordinal),
                    novelAiInpaintStrength = settings.NovelAiInpaintStrength,
                    novelAiSleepInpaintStrength = settings.NovelAiSleepInpaintStrength,
                    inpaintStrengths = new Dictionary<string, float>(StringComparer.Ordinal)
                }
            };
            data.Prompts["Prompt/Additional.txt"] = settings.Prompt;
            data.Prompts["Prompt/Common/TagExtractorBase.txt"] = settings.TagExtractorBasePrompt;
            data.Prompts["Prompt/Common/TagExtractorInpaintBatch.txt"] = settings.TagExtractorInpaintBatchPrompt;
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
            foreach (string name in ModelFields)
                data.Values.models[name] = (string)typeof(PortraitSettings).GetField(name).GetValue(settings);
            foreach (string name in PromptArchive.InclusionFields)
                data.Values.inclusion[name] = (bool)typeof(PortraitSettings).GetField(name).GetValue(settings);
            foreach (PortraitState state in ExpressionStates())
                data.Values.inpaintStrengths[state.ToString()] = settings.GetInpaintStrength(state);
            return data;
        }

        private static IEnumerable<string> PromptNames()
        {
            yield return "Prompt/Additional.txt";
            yield return "Prompt/Common/TagExtractorBase.txt";
            yield return "Prompt/Common/TagExtractorInpaintBatch.txt";
            foreach (AiProvider provider in Enum.GetValues(typeof(AiProvider)))
            {
                string folder = "Prompt/" + PromptLibrary.ProviderFolder(provider) + "/";
                yield return folder + "Base.txt";
                yield return folder + "Expression.txt";
                foreach (PortraitState state in ExpressionStates())
                    yield return folder + state + ".txt";
            }
        }

        private static void Validate(BundleData data)
        {
            ValuesData values = data.Values;
            AiProvider provider;
            AiProvider expressionProvider;
            AiProvider regenerateProvider;
            TagExtractorModel backend;
            if (values == null || values.version != Version || data.Prompts == null || data.Options == null
                || !Enum.TryParse(values.provider, out provider) || !Enum.IsDefined(typeof(AiProvider), provider)
                || !Enum.TryParse(values.expressionProvider, out expressionProvider)
                || !Enum.IsDefined(typeof(AiProvider), expressionProvider)
                || !Enum.TryParse(values.regenerateProvider, out regenerateProvider)
                || !Enum.IsDefined(typeof(AiProvider), regenerateProvider)
                || !Enum.TryParse(values.tagExtractorBackend, out backend)
                || !Enum.IsDefined(typeof(TagExtractorModel), backend)
                || values.models == null || values.models.Count != ModelFields.Length
                || values.inclusion == null || values.inclusion.Count != PromptArchive.InclusionFields.Length
                || float.IsNaN(values.novelAiInpaintStrength) || float.IsInfinity(values.novelAiInpaintStrength)
                || values.novelAiInpaintStrength < .1f || values.novelAiInpaintStrength > 1f
                || float.IsNaN(values.novelAiSleepInpaintStrength) || float.IsInfinity(values.novelAiSleepInpaintStrength)
                || values.novelAiSleepInpaintStrength < .1f || values.novelAiSleepInpaintStrength > 1f)
                throw new InvalidDataException("Invalid prompt archive values.json.");
            foreach (string name in ModelFields)
                if (!values.models.ContainsKey(name)) throw new InvalidDataException("Missing model: " + name);
            foreach (string name in PromptArchive.InclusionFields)
                if (!values.inclusion.ContainsKey(name)) throw new InvalidDataException("Missing prompt choice: " + name);
            if (values.inpaintStrengths != null)
            {
                if (values.inpaintStrengths.Count != Enum.GetValues(typeof(PortraitState)).Length - 1)
                    throw new InvalidDataException("Invalid inpaint strengths.");
                foreach (PortraitState state in ExpressionStates())
                {
                    float strength;
                    if (!values.inpaintStrengths.TryGetValue(state.ToString(), out strength)
                        || float.IsNaN(strength) || float.IsInfinity(strength)
                        || strength < .1f || strength > 1f)
                        throw new InvalidDataException("Invalid inpaint strength: " + state);
                }
            }
            int promptCount = 0;
            foreach (string name in PromptNames())
            {
                if (!data.Prompts.ContainsKey(name)) throw new InvalidDataException("Missing prompt: " + name);
                promptCount++;
            }
            if (data.Prompts.Count != promptCount || data.Options.Count != OptionFiles.Length)
                throw new InvalidDataException("Prompt archive is incomplete.");
            foreach (string name in OptionFiles)
            {
                string json;
                if (!data.Options.TryGetValue(name, out json)) throw new InvalidDataException("Missing options: " + name);
                if (name == "open-ai-img-caption.json" || name == "google-ai-img-caption.json")
                    PortraitImageCaptions.Parse(json, name);
                else PromptArchive.ValidateOptions(json);
            }
        }

        private static void Apply(PortraitSettings settings, BundleData data)
        {
            ValuesData values = data.Values;
            settings.Provider = (AiProvider)Enum.Parse(typeof(AiProvider), values.provider);
            settings.ExpressionProvider = (AiProvider)Enum.Parse(typeof(AiProvider), values.expressionProvider);
            settings.RegenerateProvider = (AiProvider)Enum.Parse(typeof(AiProvider), values.regenerateProvider);
            settings.EnableTagExtractor = values.enableTagExtractor;
            settings.TagExtractorBackend = (TagExtractorModel)Enum.Parse(typeof(TagExtractorModel), values.tagExtractorBackend);
            settings.NovelAiInpaintStrength = values.novelAiInpaintStrength;
            settings.NovelAiSleepInpaintStrength = values.novelAiSleepInpaintStrength;
            foreach (PortraitState state in ExpressionStates())
            {
                float strength = values.inpaintStrengths == null
                    ? (state == PortraitState.Sleep ? values.novelAiSleepInpaintStrength : values.novelAiInpaintStrength)
                    : values.inpaintStrengths[state.ToString()];
                settings.SetInpaintStrength(state, strength);
            }
            foreach (string name in ModelFields)
                typeof(PortraitSettings).GetField(name).SetValue(settings, values.models[name]);
            foreach (string name in PromptArchive.InclusionFields)
                typeof(PortraitSettings).GetField(name).SetValue(settings, values.inclusion[name]);
            settings.Prompt = data.Prompts["Prompt/Additional.txt"];
            settings.TagExtractorBasePrompt = data.Prompts["Prompt/Common/TagExtractorBase.txt"];
            settings.TagExtractorInpaintBatchPrompt = data.Prompts["Prompt/Common/TagExtractorInpaintBatch.txt"];
            foreach (AiProvider provider in Enum.GetValues(typeof(AiProvider)))
            {
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
            settings.GoogleImageCaptionsJson = data.Options["google-ai-img-caption.json"];
            settings.OpenAiImageCaptionsJson = data.Options["open-ai-img-caption.json"];
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
