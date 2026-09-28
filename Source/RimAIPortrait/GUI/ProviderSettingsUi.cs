using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using UnityEngine;
using Verse;

namespace RimAIPortrait
{
    /// <summary>Describes the behavior of this type or member.</summary>
    internal static class ProviderSettingsUi
    {
        public static void Draw(Listing_Standard listing, PortraitSettings settings, bool expression, Action<string> report)
        {
            listing.Label(Localization.T(expression ? "RimAIPortrait.Settings.ExpressionProvider" : "RimAIPortrait.Settings.BaseProvider"));
            AiProvider provider = expression ? settings.ExpressionProvider : settings.Provider;
            if (listing.ButtonText(Label(provider)))
            {
                provider = provider == AiProvider.OpenAI ? AiProvider.LocalComfyUI
                    : provider == AiProvider.LocalComfyUI ? AiProvider.Google
                    : provider == AiProvider.Google ? AiProvider.NovelAI
                    : AiProvider.OpenAI;
                if (expression) settings.ExpressionProvider = provider; else settings.Provider = provider;
            }

            if (provider == AiProvider.OpenAI)
                DrawOpenAi(listing, settings, expression);
            else if (provider == AiProvider.Google)
                DrawGoogle(listing, settings, expression);
            else if (provider == AiProvider.LocalComfyUI)
                DrawComfyUi(listing, settings, expression);
            else
                DrawNovelAi(listing, settings, expression);

            if (expression)
            {
                TextArea(listing, Localization.T("RimAIPortrait.Settings.ExpressionPrompt"), ref settings.ExpressionInstructions, 130f);
                DrawStyleFolder(listing, ref settings.ExpressionStyleFolder, provider, true, settings, report);
            }
            else DrawStyleFolder(listing, ref settings.StyleFolder, provider, false, settings, report);
        }

        private static void DrawOpenAi(Listing_Standard listing, PortraitSettings settings, bool expression)
        {
            if (expression)
            {
                Text(listing, Localization.T("RimAIPortrait.Settings.ApiKey"), ref settings.ExpressionApiKey);
                Text(listing, Localization.T("RimAIPortrait.Settings.Model"), ref settings.OpenAiExpressionModel);
                Text(listing, Localization.T("RimAIPortrait.Settings.OptionsJson"), ref settings.OpenAiExpressionOptions);
            }
            else
            {
                Text(listing, Localization.T("RimAIPortrait.Settings.ApiKey"), ref settings.ApiKey);
                Text(listing, Localization.T("RimAIPortrait.Settings.Model"), ref settings.OpenAiModel);
                TextArea(listing, Localization.T("RimAIPortrait.Settings.BasePrompt"), ref settings.BaseInstructions, 130f);
                Text(listing, Localization.T("RimAIPortrait.Settings.OptionsJson"), ref settings.OpenAiOptions);
            }
        }

        private static void DrawGoogle(Listing_Standard listing, PortraitSettings settings, bool expression)
        {
            if (expression)
            {
                Text(listing, Localization.T("RimAIPortrait.Settings.ApiKey"), ref settings.GoogleExpressionApiKey);
                Text(listing, Localization.T("RimAIPortrait.Settings.Model"), ref settings.GoogleExpressionModel);
                Text(listing, Localization.T("RimAIPortrait.Settings.OptionsJson"), ref settings.GoogleExpressionOptions);
            }
            else
            {
                Text(listing, Localization.T("RimAIPortrait.Settings.ApiKey"), ref settings.GoogleApiKey);
                Text(listing, Localization.T("RimAIPortrait.Settings.Model"), ref settings.GoogleModel);
                TextArea(listing, Localization.T("RimAIPortrait.Settings.BasePrompt"), ref settings.GeminiBaseInstructions, 130f);
                Text(listing, Localization.T("RimAIPortrait.Settings.OptionsJson"), ref settings.GoogleOptions);
            }
        }

        private static void DrawComfyUi(Listing_Standard listing, PortraitSettings settings, bool expression)
        {
            if (expression)
            {
                Text(listing, Localization.T("RimAIPortrait.Settings.ComfyUrl"), ref settings.ComfyExpressionUrl);
                Text(listing, Localization.T("RimAIPortrait.Settings.WorkflowPath"), ref settings.ComfyExpressionWorkflowPath);
                TextArea(listing, Localization.T("RimAIPortrait.Settings.NegativePrompt"), ref settings.ComfyExpressionNegativePrompt, 62f);
            }
            else
            {
                Text(listing, Localization.T("RimAIPortrait.Settings.ComfyUrl"), ref settings.ComfyUrl);
                Text(listing, Localization.T("RimAIPortrait.Settings.WorkflowPath"), ref settings.ComfyWorkflowPath);
                TextArea(listing, Localization.T("RimAIPortrait.Settings.BasePrompt"), ref settings.ComfyBaseInstructions, 130f);
                TextArea(listing, Localization.T("RimAIPortrait.Settings.NegativePrompt"), ref settings.ComfyNegativePrompt, 62f);
            }
            listing.Label(Localization.T("RimAIPortrait.Settings.ComfyTimeout", settings.ComfyTimeoutSeconds.ToString("0")));
            settings.ComfyTimeoutSeconds = listing.Slider(settings.ComfyTimeoutSeconds, 60f, 3600f);
            listing.Label(Localization.T("RimAIPortrait.Settings.ComfyRequiredOutput"));
            listing.Label(Localization.T("RimAIPortrait.Settings.ComfyWorkflowTokens"));
        }

        private static void DrawNovelAi(Listing_Standard listing, PortraitSettings settings, bool expression)
        {
            if (expression)
            {
                Text(listing, Localization.T("RimAIPortrait.Settings.NovelAiApiKey"), ref settings.NovelAiExpressionApiKey);
                Text(listing, Localization.T("RimAIPortrait.Settings.Model"), ref settings.NovelAiExpressionModel);
                Text(listing, Localization.T("RimAIPortrait.Settings.OptionsJson"), ref settings.NovelAiExpressionOptions);
                listing.Label(Localization.T("RimAIPortrait.Settings.NovelAiInpaintStrengthDefault", settings.NovelAiInpaintStrength.ToString("0.00")));
                settings.NovelAiInpaintStrength = listing.Slider(settings.NovelAiInpaintStrength, .1f, 1f);
                listing.Label(Localization.T("RimAIPortrait.Settings.NovelAiSleepInpaintStrengthDefault", settings.NovelAiSleepInpaintStrength.ToString("0.00")));
                settings.NovelAiSleepInpaintStrength = listing.Slider(settings.NovelAiSleepInpaintStrength, .1f, 1f);
            }
            else
            {
                Text(listing, Localization.T("RimAIPortrait.Settings.NovelAiApiKey"), ref settings.NovelAiApiKey);
                Text(listing, Localization.T("RimAIPortrait.Settings.Model"), ref settings.NovelAiModel);
                TextArea(listing, Localization.T("RimAIPortrait.Settings.BasePrompt"), ref settings.NovelAiBaseInstructions, 130f);
                Text(listing, Localization.T("RimAIPortrait.Settings.OptionsJson"), ref settings.NovelAiOptions);
            }
            listing.Label(Localization.T("RimAIPortrait.Settings.NovelAiReferenceHelpV45"));
        }

        private static void DrawStyleFolder(Listing_Standard listing, ref string folder, AiProvider provider,
            bool expression, PortraitSettings settings, Action<string> report)
        {
            Text(listing, Localization.T("RimAIPortrait.Settings.StyleFolder"), ref folder);
            listing.Label(Localization.T(provider == AiProvider.OpenAI
                ? "RimAIPortrait.Settings.OpenAiStyleFolderHint"
                : "RimAIPortrait.Settings.StyleFolderHint"));
            bool hasDefault = provider == AiProvider.Google || (provider == AiProvider.NovelAI && !expression);
            bool useDefault = PortraitProviderUtilities.UsesDefaultStyle(folder);
            if (provider == AiProvider.OpenAI && (useDefault || string.IsNullOrWhiteSpace(folder)))
            {
                listing.Label(Localization.T("RimAIPortrait.Settings.OpenAiStyleNotUsed"));
                return;
            }
            string actualFolder = useDefault ? Path.Combine(RimAIPortraitMod.ModPack.RootDir, "Style") : folder;
            bool folderExists = Directory.Exists(actualFolder);
            int configuredCount = 0;
            if (folderExists)
            {
                try { configuredCount = PortraitProviderUtilities.StyleFiles(actualFolder).Count(); }
                catch (Exception) { folderExists = false; }
            }
            string statusKey;
            if (useDefault)
                statusKey = "RimAIPortrait.Settings.StyleDefaultInUse";
            else if (string.IsNullOrWhiteSpace(folder))
                statusKey = "RimAIPortrait.Settings.StyleInvalidNoStyle";
            else if (!folderExists)
                statusKey = "RimAIPortrait.Settings.StyleInvalidNoStyle";
            else if (provider == AiProvider.NovelAI && !expression && configuredCount == 0)
                statusKey = "RimAIPortrait.Settings.StyleDefault";
            else
                statusKey = hasDefault ? "RimAIPortrait.Settings.StyleFolderWithDefault"
                    : "RimAIPortrait.Settings.StyleFolderConfigured";
            listing.Label(Localization.T(statusKey));
            if (folderExists && configuredCount > 0)
            {
                IPortraitGenerationProvider selectedProvider = PortraitProviderRegistry.Get(provider);
                var request = new PortraitGenerationRequest {
                    State = expression ? PortraitState.High : PortraitState.Normal,
                    ImageUse = expression ? PortraitImageUse.Edit : PortraitImageUse.CharacterReference,
                    PrimaryImage = expression ? new byte[0] : null,
                    ReferencePaths = new string[0],
                    Configuration = selectedProvider.CreateConfiguration(settings, expression)
                };
                int capacity = selectedProvider.StyleReferenceCapacity(request);
                if (configuredCount > capacity)
                    listing.Label(Localization.T("RimAIPortrait.Settings.StyleReferenceIgnored", capacity, configuredCount));
            }
            if (!listing.ButtonText(Localization.T("RimAIPortrait.Settings.OpenStyleFolder"))) return;
            try
            {
                if (string.IsNullOrWhiteSpace(folder)) throw new InvalidOperationException(Localization.T("RimAIPortrait.Error.StyleFolderEmpty"));
                if (!useDefault) Directory.CreateDirectory(actualFolder);
                Process.Start(actualFolder);
            }
            catch (Exception exception) { report(Localization.T("RimAIPortrait.Error.OpenStyleFolder", exception.Message)); }
        }

        private static string Label(AiProvider provider)
        {
            return provider == AiProvider.Google ? "Google Gemini"
                : provider == AiProvider.OpenAI ? "OpenAI"
                : provider == AiProvider.NovelAI ? "NovelAI"
                : "ComfyUI";
        }

        private static void Text(Listing_Standard listing, string label, ref string value)
        {
            listing.Label(label);
            value = listing.TextEntry(value ?? "");
        }

        private static void TextArea(Listing_Standard listing, string label, ref string value, float height)
        {
            listing.Label(label);
            value = Widgets.TextArea(listing.GetRect(height), value ?? "");
        }
    }
}
