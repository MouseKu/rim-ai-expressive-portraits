using System;
using System.Diagnostics;
using System.IO;
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
                provider = provider == AiProvider.Google ? AiProvider.OpenAI : provider == AiProvider.OpenAI ? AiProvider.LocalComfyUI : AiProvider.Google;
                if (expression) settings.ExpressionProvider = provider; else settings.Provider = provider;
            }

            if (provider == AiProvider.OpenAI)
                DrawOpenAi(listing, settings, expression);
            else if (provider == AiProvider.Google)
                DrawGoogle(listing, settings, expression);
            else
                DrawComfyUi(listing, settings, expression);

            if (expression) DrawStyleFolder(listing, ref settings.ExpressionStyleFolder, report);
            else DrawStyleFolder(listing, ref settings.StyleFolder, report);
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
                Text(listing, Localization.T("RimAIPortrait.Settings.ApiKey"), ref settings.ExpressionApiKey);
                Text(listing, Localization.T("RimAIPortrait.Settings.Model"), ref settings.GoogleExpressionModel);
                Text(listing, Localization.T("RimAIPortrait.Settings.OptionsJson"), ref settings.GoogleExpressionOptions);
            }
            else
            {
                Text(listing, Localization.T("RimAIPortrait.Settings.ApiKey"), ref settings.ApiKey);
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
                TextArea(listing, Localization.T("RimAIPortrait.Settings.BasePrompt"), ref settings.BaseInstructions, 130f);
                TextArea(listing, Localization.T("RimAIPortrait.Settings.NegativePrompt"), ref settings.ComfyNegativePrompt, 62f);
            }
            listing.Label(Localization.T("RimAIPortrait.Settings.ComfyTimeout", settings.ComfyTimeoutSeconds.ToString("0")));
            settings.ComfyTimeoutSeconds = listing.Slider(settings.ComfyTimeoutSeconds, 60f, 3600f);
            listing.Label(Localization.T("RimAIPortrait.Settings.ComfyRequiredOutput"));
            listing.Label(Localization.T("RimAIPortrait.Settings.ComfyWorkflowTokens"));
        }

        private static void DrawStyleFolder(Listing_Standard listing, ref string folder, Action<string> report)
        {
            Text(listing, Localization.T("RimAIPortrait.Settings.StyleFolder"), ref folder);
            if (!listing.ButtonText(Localization.T("RimAIPortrait.Settings.OpenStyleFolder"))) return;
            try
            {
                if (folder.NullOrEmpty()) throw new InvalidOperationException(Localization.T("RimAIPortrait.Error.StyleFolderEmpty"));
                Directory.CreateDirectory(folder);
                Process.Start(folder);
            }
            catch (Exception exception) { report(Localization.T("RimAIPortrait.Error.OpenStyleFolder", exception.Message)); }
        }

        private static string Label(AiProvider provider)
        {
            return provider == AiProvider.Google ? "Google Gemini" : provider == AiProvider.OpenAI ? "OpenAI" : "ComfyUI";
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
