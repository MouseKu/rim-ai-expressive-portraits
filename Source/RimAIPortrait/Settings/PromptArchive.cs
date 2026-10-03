using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;
using Verse;

namespace RimAIPortrait
{
    /// <summary>Transfers prompt text, prompt inclusion choices, and selected JSON settings.</summary>
    internal static class PromptArchive
    {
        private const string EntryName = "prompt-settings.json";
        private const string NovelAiBaseEntry = "Json/nai-base-options.json";
        private const string NovelAiExpressionEntry = "Json/nai-expression-options.json";
        private const string OpenAiCaptionEntry = "Json/open-ai-img-caption.json";
        private const string GoogleCaptionEntry = "Json/google-ai-img-caption.json";
        private const int FormatVersion = 2;
        private const int MaximumJsonBytes = 2 * 1024 * 1024;

        internal static readonly string[] InclusionFields = {
            nameof(PortraitSettings.IncludeSprite), nameof(PortraitSettings.IncludeGender),
            nameof(PortraitSettings.IncludeRace), nameof(PortraitSettings.IncludeXenotype),
            nameof(PortraitSettings.IncludeGene), nameof(PortraitSettings.OnlyAppearanceGenes),
            nameof(PortraitSettings.IncludeAge), nameof(PortraitSettings.IncludeBody),
            nameof(PortraitSettings.IncludeSkin), nameof(PortraitSettings.IncludeHair),
            nameof(PortraitSettings.IncludeEyes), nameof(PortraitSettings.IncludeApparel),
            nameof(PortraitSettings.IncludeApparelQuality), nameof(PortraitSettings.IncludeHealth),
            nameof(PortraitSettings.IncludeTraits), nameof(PortraitSettings.IncludeBackstory)
        };

        [DataContract]
        private sealed class ArchiveData
        {
            [DataMember] public int Version { get; set; }
            [DataMember] public Dictionary<string, string> Prompts { get; set; }
            [DataMember] public Dictionary<string, bool> Inclusion { get; set; }
        }

        public static string DirectoryPath => Path.Combine(GenFilePaths.SaveDataFolderPath,
            "RimAIPortrait", "PromptArchives");

        public static string Export(PortraitSettings settings)
        {
            return PromptArchiveBundle.Export(settings);
        }

        public static void Import(PortraitSettings settings, string path)
        {
            if (PromptArchiveBundle.IsBundle(path))
            {
                PromptArchiveBundle.Import(settings, path);
                return;
            }
            ArchiveData data;
            string novelAiBase = null;
            string novelAiExpression = null;
            string openAiCaptions = null;
            string googleCaptions = null;
            using (var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            using (var archive = new ZipArchive(file, ZipArchiveMode.Read))
            {
                using (var json = new MemoryStream(ReadEntry(archive, EntryName)))
                    data = (ArchiveData)Serializer().ReadObject(json);
                Validate(data);
                if (archive.Entries.Count != (data.Version == 1 ? 1 : 5))
                    throw new InvalidDataException("This is not a supported prompt archive.");
                if (data.Version == 2)
                {
                    novelAiBase = ReadTextEntry(archive, NovelAiBaseEntry);
                    novelAiExpression = ReadTextEntry(archive, NovelAiExpressionEntry);
                    openAiCaptions = ReadTextEntry(archive, OpenAiCaptionEntry);
                    googleCaptions = ReadTextEntry(archive, GoogleCaptionEntry);
                }
            }
            if (data.Version == 2)
            {
                ValidateOptions(novelAiBase);
                ValidateOptions(novelAiExpression);
                PortraitImageCaptions.Parse(openAiCaptions, "open-ai-img-caption.json");
                PortraitImageCaptions.Parse(googleCaptions, "google-ai-img-caption.json");
            }
            Apply(settings, data);
            if (data.Version == 2)
            {
                settings.NovelAiOptions = novelAiBase;
                settings.NovelAiExpressionOptions = novelAiExpression;
                settings.OpenAiImageCaptionsJson = openAiCaptions;
                settings.GoogleImageCaptionsJson = googleCaptions;
            }
        }

        private static string ReadTextEntry(ZipArchive archive, string name)
        {
            return new UTF8Encoding(false, true).GetString(ReadEntry(archive, name));
        }

        private static byte[] ReadEntry(ZipArchive archive, string name)
        {
            ZipArchiveEntry item = archive.GetEntry(name);
            if (item == null || item.Length > MaximumJsonBytes)
                throw new InvalidDataException("Prompt archive is missing or has an oversized " + name + ".");
            using (Stream entry = item.Open())
            using (var output = new MemoryStream())
            {
                byte[] buffer = new byte[8192];
                int count;
                while ((count = entry.Read(buffer, 0, buffer.Length)) > 0)
                {
                    if (output.Length + count > MaximumJsonBytes)
                        throw new InvalidDataException("Prompt archive entry is too large.");
                    output.Write(buffer, 0, count);
                }
                return output.ToArray();
            }
        }

        internal static void ValidateOptions(string json)
        {
            using (var input = new MemoryStream(Encoding.UTF8.GetBytes(json)))
            {
                var serializer = new DataContractJsonSerializer(typeof(Dictionary<string, object>),
                    new DataContractJsonSerializerSettings { UseSimpleDictionaryFormat = true });
                if (serializer.ReadObject(input) == null)
                    throw new InvalidDataException("NovelAI options must be a JSON object.");
            }
        }

        private static DataContractJsonSerializer Serializer()
        {
            return new DataContractJsonSerializer(typeof(ArchiveData),
                new DataContractJsonSerializerSettings { UseSimpleDictionaryFormat = true });
        }

        private static void Validate(ArchiveData data)
        {
            if (data == null || (data.Version != 1 && data.Version != FormatVersion)
                || data.Prompts == null || data.Inclusion == null)
                throw new InvalidDataException("Unsupported prompt archive version or format.");
            if (data.Prompts.Count != 3 + 4 * (1 + Enum.GetValues(typeof(PortraitState)).Length)
                || data.Inclusion.Count != InclusionFields.Length)
                throw new InvalidDataException("Prompt archive is incomplete.");
            RequirePrompt(data, "Prompt");
            RequirePrompt(data, "TagExtractorBasePrompt");
            RequirePrompt(data, "TagExtractorInpaintBatchPrompt");
            foreach (AiProvider provider in Enum.GetValues(typeof(AiProvider)))
            {
                string prefix = provider + "/";
                RequirePrompt(data, prefix + "Base");
                RequirePrompt(data, prefix + "Expression");
                foreach (PortraitState state in Enum.GetValues(typeof(PortraitState)))
                    if (state != PortraitState.Base) RequirePrompt(data, prefix + state);
            }
            foreach (string name in InclusionFields)
                if (!data.Inclusion.ContainsKey(name))
                    throw new InvalidDataException("Prompt archive is missing " + name + ".");
        }

        private static void RequirePrompt(ArchiveData data, string name)
        {
            if (!data.Prompts.ContainsKey(name))
                throw new InvalidDataException("Prompt archive is missing " + name + ".");
        }

        private static void Apply(PortraitSettings settings, ArchiveData data)
        {
            settings.Prompt = data.Prompts["Prompt"];
            settings.TagExtractorBasePrompt = data.Prompts["TagExtractorBasePrompt"];
            settings.TagExtractorInpaintBatchPrompt = data.Prompts["TagExtractorInpaintBatchPrompt"];
            foreach (AiProvider provider in Enum.GetValues(typeof(AiProvider)))
            {
                string prefix = provider + "/";
                SetBasePrompt(settings, provider, data.Prompts[prefix + "Base"]);
                SetExpressionPrompt(settings, provider, data.Prompts[prefix + "Expression"]);
                foreach (PortraitState state in Enum.GetValues(typeof(PortraitState)))
                    if (state != PortraitState.Base) settings.SetStatePrompt(provider, state, data.Prompts[prefix + state]);
            }
            foreach (string name in InclusionFields)
                typeof(PortraitSettings).GetField(name).SetValue(settings, data.Inclusion[name]);
        }

        internal static string BasePrompt(PortraitSettings settings, AiProvider provider)
        {
            return provider == AiProvider.Google ? settings.GeminiBaseInstructions
                : provider == AiProvider.LocalComfyUI ? settings.ComfyBaseInstructions
                : provider == AiProvider.NovelAI ? settings.NovelAiBaseInstructions
                : settings.BaseInstructions;
        }

        internal static void SetBasePrompt(PortraitSettings settings, AiProvider provider, string value)
        {
            if (provider == AiProvider.Google) settings.GeminiBaseInstructions = value;
            else if (provider == AiProvider.LocalComfyUI) settings.ComfyBaseInstructions = value;
            else if (provider == AiProvider.NovelAI) settings.NovelAiBaseInstructions = value;
            else settings.BaseInstructions = value;
        }

        internal static void SetExpressionPrompt(PortraitSettings settings, AiProvider provider, string value)
        {
            if (provider == AiProvider.Google) settings.GoogleExpressionInstructions = value;
            else if (provider == AiProvider.LocalComfyUI) settings.ComfyExpressionInstructions = value;
            else if (provider == AiProvider.NovelAI) settings.NovelAiExpressionInstructions = value;
            else settings.ExpressionInstructions = value;
        }
    }
}
