using System;
using Verse;

namespace RimAIPortrait
{
    public sealed partial class PortraitSettings
    {
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
