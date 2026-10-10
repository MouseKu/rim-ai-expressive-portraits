using System;
using System.Globalization;
using System.Text.RegularExpressions;
using Verse;

namespace RimAIPortrait
{
    public sealed partial class PortraitSettings
    {
        /// <summary>Describes the behavior of this type or member.</summary>
        private void MigrateOpenAiOptionDefaults()
        {
            const string previousDefault = "{\"quality\":\"low\",\"size\":\"1024x1024\"}";
            const string currentDefault = "{\"quality\":\"low\",\"size\":\"1024x1024\",\"background\":\"transparent\"}";
            if (OpenAiOptions == previousDefault || OpenAiOptions == currentDefault) OpenAiOptions = ProviderOptionDefaults.OpenAiBase;
            if (OpenAiExpressionOptions == previousDefault || OpenAiExpressionOptions == currentDefault) OpenAiExpressionOptions = ProviderOptionDefaults.OpenAiExpression;
        }

        private void MigrateLocalOpenAiOptionDefaults()
        {
            if (LocalOpenAiOptions == ProviderOptionDefaults.OpenAiBase)
                LocalOpenAiOptions = ProviderOptionDefaults.LocalBase;
            if (LocalOpenAiExpressionOptions == ProviderOptionDefaults.OpenAiExpression)
                LocalOpenAiExpressionOptions = ProviderOptionDefaults.LocalExpression;
            if (RegenerateLocalOpenAiOptions == ProviderOptionDefaults.OpenAiBase)
                RegenerateLocalOpenAiOptions = ProviderOptionDefaults.LocalRegenerate;
        }

        private void MigrateGoogleOptionDefaults()
        {
            const string currentDefault = "{\"aspect_ratio\":\"1:1\",\"image_size\":\"1K\"}";
            if (GoogleOptions == currentDefault) GoogleOptions = ProviderOptionDefaults.GoogleBase;
            if (GoogleExpressionOptions == currentDefault) GoogleExpressionOptions = ProviderOptionDefaults.GoogleExpression;
        }

        private void MigrateNovelAiOptions(float legacyCharacterStrength, float legacyCharacterFidelity)
        {
            const string previousPackagedBaseDefault = "{\"width\":832,\"height\":832,\"steps\":23,\"scale\":5,\"sampler\":\"k_euler_ancestral\",\"noise_schedule\":\"karras\",\"qualityToggle\":true,\"character_reference_strength\":0.55,\"character_reference_fidelity\":0.7,\"positive_prompt\":\"best quality, very aesthetic, clean anime illustration, anime character portrait, three-quarter view, looking slightly to the side, angled face, clean dark brown lineart, smooth lineart, medium detail, layered hair, defined hair locks, expressive eyes, clean two-tone cel shading, subtle shadows, subtle highlights, clean clothing details, warm muted colors\",\"negative_prompt\":\"worst quality, low quality, lowres, blurry, rough sketch, messy lineart, unfinished, crude drawing, overly simplified, minimalist, flat icon, vector art, front view, straight-on, symmetrical face, symmetrical pose, passport photo, featureless hair, featureless clothing, overly detailed, hyper-detailed, intricate linework, dense linework, crosshatching, hatching, stippling, engraving, etching, manga screentone, realistic hair strands, excessive hair strands, complex shading, excessive shading, photorealistic, 3d render, text, watermark\"}";
            const string previousBaseDefault = "{\"width\":832,\"height\":832,\"steps\":23,\"scale\":5,\"sampler\":\"k_euler_ancestral\",\"noise_schedule\":\"karras\",\"negative_prompt\":\"lowres, bad anatomy, bad hands, text, watermark\",\"qualityToggle\":true}";
            const string previousDefault = "{\"width\":832,\"height\":1216,\"steps\":23,\"scale\":5,\"sampler\":\"k_euler_ancestral\",\"noise_schedule\":\"karras\",\"negative_prompt\":\"lowres, bad anatomy, bad hands, text, watermark\",\"strength\":0.6,\"noise\":0.1}";
            const string currentExpressionDefault = "{\"width\":832,\"height\":832,\"steps\":23,\"scale\":5,\"sampler\":\"k_euler_ancestral\",\"noise_schedule\":\"karras\",\"negative_prompt\":\"lowres, bad anatomy, bad hands, text, watermark\",\"strength\":0.6,\"noise\":0.1}";
            bool useV5Base = !NovelAiModel.NullOrEmpty()
                && NovelAiModel.StartsWith("nai-diffusion-5", StringComparison.OrdinalIgnoreCase);
            bool replacedBaseDefault = NovelAiOptions == previousBaseDefault || NovelAiOptions == previousDefault;
            if (replacedBaseDefault)
                NovelAiOptions = useV5Base ? ProviderOptionDefaults.NovelAiBase : previousPackagedBaseDefault;
            else if (useV5Base && NovelAiOptions == previousPackagedBaseDefault)
                NovelAiOptions = ProviderOptionDefaults.NovelAiBase;
            if (NovelAiExpressionOptions == previousDefault || NovelAiExpressionOptions == currentExpressionDefault)
                NovelAiExpressionOptions = ProviderOptionDefaults.NovelAiExpression;
            if (!useV5Base)
            {
                NovelAiOptions = SetNovelAiReferenceOption(NovelAiOptions, "character_reference_strength", legacyCharacterStrength, replacedBaseDefault);
                NovelAiOptions = SetNovelAiReferenceOption(NovelAiOptions, "character_reference_fidelity", legacyCharacterFidelity, replacedBaseDefault);
            }
        }

        private static string SetNovelAiReferenceOption(string json, string key, float value, bool overwrite)
        {
            if (json.NullOrEmpty()) json = "{}";
            if (float.IsNaN(value) || float.IsInfinity(value))
                value = key == "character_reference_strength" ? .55f : .7f;
            string number = Math.Max(0f, Math.Min(1f, value)).ToString("R", CultureInfo.InvariantCulture);
            string pattern = "(\"" + Regex.Escape(key) + "\"\\s*:\\s*)(?:\"[^\"]*\"|[^,}\\s]+)";
            Match match = Regex.Match(json, pattern, RegexOptions.IgnoreCase);
            if (match.Success)
                return overwrite ? json.Substring(0, match.Index) + match.Groups[1].Value + number + json.Substring(match.Index + match.Length) : json;
            int end = json.LastIndexOf('}');
            if (end < 0) return json;
            string separator = json.Substring(0, end).TrimEnd().EndsWith("{", StringComparison.Ordinal) ? "" : ",";
            return json.Insert(end, separator + "\"" + key + "\":" + number);
        }

        /// <summary>Describes the behavior of this type or member.</summary>
        private void MigratePositionDefaults()
        {
            if (PositionDefaultsMigrated) return;
            if (OffsetX == 15f && OffsetY == 25f) OffsetY = SettingsDefaults.Values.OffsetY;
            PositionDefaultsMigrated = true;
        }

        /// <summary>Describes the behavior of this type or member.</summary>
        private void MigrateProviderSettings()
        {
            if (ProviderSettingsSplitMigrated) return;
            ExpressionProvider = Provider;
            ExpressionApiKey = ApiKey;
            ComfyExpressionUrl = ComfyUrl;
            ComfyExpressionNegativePrompt = ComfyNegativePrompt;
            ExpressionStyleFolder = StyleFolder;
            ProviderSettingsSplitMigrated = true;
        }

        private void MigrateProviderApiKeys()
        {
            if (ProviderApiKeysSplitMigrated) return;
            MigrateProviderApiKey(ref ApiKey, ref GoogleApiKey, Provider);
            MigrateProviderApiKey(ref ExpressionApiKey, ref GoogleExpressionApiKey, ExpressionProvider);
            ProviderApiKeysSplitMigrated = true;
        }

        private void MigrateRegenerateSettings()
        {
            if (RegenerateSettingsMigrated) return;
            RegenerateProvider = Provider;
            RegenerateOpenAiApiKey = ApiKey;
            RegenerateOpenAiModel = OpenAiModel;
            RegenerateOpenAiOptions = OpenAiOptions;
            RegenerateGoogleApiKey = GoogleApiKey;
            RegenerateGoogleModel = GoogleModel;
            RegenerateGoogleOptions = GoogleOptions;
            RegenerateNovelAiApiKey = NovelAiApiKey;
            RegenerateNovelAiModel = NovelAiModel;
            RegenerateNovelAiOptions = NovelAiExpressionOptions;
            RegenerateComfyUrl = ComfyUrl;
            RegenerateComfyWorkflowPath = ComfyWorkflowPath;
            RegenerateComfyNegativePrompt = ComfyNegativePrompt;
            RegenerateStyleFolder = StyleFolder;
            RegenerateSettingsMigrated = true;
        }

        private static void MigrateProviderApiKey(ref string sharedKey, ref string googleKey, AiProvider provider)
        {
            if (string.IsNullOrWhiteSpace(sharedKey)) return;
            bool googleKeyFormat = sharedKey.StartsWith("AIza", StringComparison.Ordinal);
            bool openAiKeyFormat = sharedKey.StartsWith("sk-", StringComparison.Ordinal);
            if (!googleKeyFormat && (openAiKeyFormat || provider != AiProvider.Google)) return;
            if (string.IsNullOrWhiteSpace(googleKey)) googleKey = sharedKey;
            sharedKey = null;
        }

        /// <summary>Describes the behavior of this type or member.</summary>
        private void MigrateSplitModels()
        {
            if (ModelsSplitMigrated) return;
            OpenAiExpressionModel = OpenAiModel;
            GoogleExpressionModel = GoogleModel;
            ComfyExpressionWorkflowPath = ComfyWorkflowPath;
            ModelsSplitMigrated = true;
        }

    }
}
