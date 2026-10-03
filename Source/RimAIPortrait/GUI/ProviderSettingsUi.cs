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
                DrawOpenAi(listing, settings, expression, report);
            else if (provider == AiProvider.Google)
                DrawGoogle(listing, settings, expression, report);
            else if (provider == AiProvider.LocalComfyUI)
                DrawComfyUi(listing, settings, expression, report);
            else
                DrawNovelAi(listing, settings, expression, report);

            if (expression)
            {
                listing.Label(Localization.T("RimAIPortrait.Settings.ExpressionPrompt") + " (" + Label(provider) + ")");
                Rect promptArea = listing.GetRect(130f);
                if (provider == AiProvider.Google)
                    settings.GoogleExpressionInstructions = Widgets.TextArea(promptArea, settings.GoogleExpressionInstructions ?? "");
                else if (provider == AiProvider.LocalComfyUI)
                    settings.ComfyExpressionInstructions = Widgets.TextArea(promptArea, settings.ComfyExpressionInstructions ?? "");
                else if (provider == AiProvider.NovelAI)
                    settings.NovelAiExpressionInstructions = Widgets.TextArea(promptArea, settings.NovelAiExpressionInstructions ?? "");
                else
                    settings.ExpressionInstructions = Widgets.TextArea(promptArea, settings.ExpressionInstructions ?? "");
                Rect saveRow = listing.GetRect(32f);
                if (Widgets.ButtonText(new Rect(saveRow.xMax - 100f, saveRow.y, 100f, saveRow.height),
                    Localization.T("RimAIPortrait.Common.Save")))
                {
                    RimAIPortraitMod.SaveSettings();
                    report(Localization.T("RimAIPortrait.Settings.ExpressionPromptSaved", Label(provider)));
                }
                if (Widgets.ButtonText(new Rect(saveRow.xMax - 210f, saveRow.y, 100f, saveRow.height),
                    Localization.T("RimAIPortrait.Common.Reset")))
                {
                    string original = RimAIPortraitMod.PackagedPrompt(provider, "ExpressionPrompt.md", false);
                    if (original == null) report(Localization.T("RimAIPortrait.Settings.PromptDefaultMissing"));
                    else
                    {
                        if (provider == AiProvider.Google) settings.GoogleExpressionInstructions = original;
                        else if (provider == AiProvider.LocalComfyUI) settings.ComfyExpressionInstructions = original;
                        else if (provider == AiProvider.NovelAI) settings.NovelAiExpressionInstructions = original;
                        else settings.ExpressionInstructions = original;
                        RimAIPortraitMod.SaveSettings();
                        report(Localization.T("RimAIPortrait.Settings.PromptReset",
                            Label(provider) + " / " + Localization.T("RimAIPortrait.Settings.ExpressionPrompt")));
                    }
                }
            }
            else DrawStyleFolder(listing, ref settings.StyleFolder, provider, settings, report);
        }

        public static void DrawRegenerate(Listing_Standard listing, PortraitSettings settings, Action<string> report)
        {
            listing.Label(Localization.T("RimAIPortrait.Settings.RegenerateProvider"));
            AiProvider provider = settings.RegenerateProvider;
            if (listing.ButtonText(Label(provider)))
                settings.RegenerateProvider = provider = provider == AiProvider.OpenAI ? AiProvider.LocalComfyUI
                    : provider == AiProvider.LocalComfyUI ? AiProvider.Google
                    : provider == AiProvider.Google ? AiProvider.NovelAI : AiProvider.OpenAI;
            if (provider == AiProvider.OpenAI)
            {
                Text(listing, Localization.T("RimAIPortrait.Settings.ApiKey"), ref settings.RegenerateOpenAiApiKey);
                Text(listing, Localization.T("RimAIPortrait.Settings.Model"), ref settings.RegenerateOpenAiModel);
                Text(listing, Localization.T("RimAIPortrait.Settings.OptionsJson"), ref settings.RegenerateOpenAiOptions);
            }
            else if (provider == AiProvider.Google)
            {
                Text(listing, Localization.T("RimAIPortrait.Settings.ApiKey"), ref settings.RegenerateGoogleApiKey);
                Text(listing, Localization.T("RimAIPortrait.Settings.Model"), ref settings.RegenerateGoogleModel);
                Text(listing, Localization.T("RimAIPortrait.Settings.OptionsJson"), ref settings.RegenerateGoogleOptions);
            }
            else if (provider == AiProvider.NovelAI)
            {
                Text(listing, Localization.T("RimAIPortrait.Settings.NovelAiApiKey"), ref settings.RegenerateNovelAiApiKey);
                Text(listing, Localization.T("RimAIPortrait.Settings.Model"), ref settings.RegenerateNovelAiModel);
                Text(listing, Localization.T("RimAIPortrait.Settings.OptionsJson"), ref settings.RegenerateNovelAiOptions);
                if (NovelAiPortraitProvider.IsV5Model(settings.RegenerateNovelAiModel))
                    listing.Label(Localization.T("RimAIPortrait.Settings.NovelAiV5Help"));
            }
            else
            {
                Text(listing, Localization.T("RimAIPortrait.Settings.ComfyUrl"), ref settings.RegenerateComfyUrl);
                Text(listing, Localization.T("RimAIPortrait.Settings.WorkflowPath"), ref settings.RegenerateComfyWorkflowPath);
                PromptArea(listing, Localization.T("RimAIPortrait.Settings.NegativePrompt"),
                    ref settings.RegenerateComfyNegativePrompt, 62f,
                    SettingsDefaults.Values.RegenerateComfyNegativePrompt, report);
                listing.Label(Localization.T("RimAIPortrait.Settings.ComfyTimeout", settings.ComfyTimeoutSeconds.ToString("0")));
                settings.ComfyTimeoutSeconds = listing.Slider(settings.ComfyTimeoutSeconds, 60f, 3600f);
            }
        }

        private static void DrawOpenAi(Listing_Standard listing, PortraitSettings settings, bool expression, Action<string> report)
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
                PromptArea(listing, Localization.T("RimAIPortrait.Settings.BasePrompt"),
                    ref settings.BaseInstructions, 130f,
                    RimAIPortraitMod.PackagedPrompt(AiProvider.OpenAI, "BasePrompt.md", false), report);
                Text(listing, Localization.T("RimAIPortrait.Settings.OptionsJson"), ref settings.OpenAiOptions);
            }
        }

        private static void DrawGoogle(Listing_Standard listing, PortraitSettings settings, bool expression, Action<string> report)
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
                PromptArea(listing, Localization.T("RimAIPortrait.Settings.BasePrompt"),
                    ref settings.GeminiBaseInstructions, 130f,
                    RimAIPortraitMod.PackagedPrompt(AiProvider.Google, "BasePrompt.md", false), report);
                Text(listing, Localization.T("RimAIPortrait.Settings.OptionsJson"), ref settings.GoogleOptions);
            }
        }

        private static void DrawComfyUi(Listing_Standard listing, PortraitSettings settings, bool expression, Action<string> report)
        {
            if (expression)
            {
                Text(listing, Localization.T("RimAIPortrait.Settings.ComfyUrl"), ref settings.ComfyExpressionUrl);
                Text(listing, Localization.T("RimAIPortrait.Settings.WorkflowPath"), ref settings.ComfyExpressionWorkflowPath);
                PromptArea(listing, Localization.T("RimAIPortrait.Settings.NegativePrompt"),
                    ref settings.ComfyExpressionNegativePrompt, 62f,
                    SettingsDefaults.Values.ComfyExpressionNegativePrompt, report);
            }
            else
            {
                Text(listing, Localization.T("RimAIPortrait.Settings.ComfyUrl"), ref settings.ComfyUrl);
                Text(listing, Localization.T("RimAIPortrait.Settings.WorkflowPath"), ref settings.ComfyWorkflowPath);
                PromptArea(listing, Localization.T("RimAIPortrait.Settings.BasePrompt"),
                    ref settings.ComfyBaseInstructions, 130f,
                    RimAIPortraitMod.PackagedPrompt(AiProvider.LocalComfyUI, "BasePrompt.md", false), report);
                PromptArea(listing, Localization.T("RimAIPortrait.Settings.NegativePrompt"),
                    ref settings.ComfyNegativePrompt, 62f,
                    SettingsDefaults.Values.ComfyNegativePrompt, report);
            }
            listing.Label(Localization.T("RimAIPortrait.Settings.ComfyTimeout", settings.ComfyTimeoutSeconds.ToString("0")));
            settings.ComfyTimeoutSeconds = listing.Slider(settings.ComfyTimeoutSeconds, 60f, 3600f);
            listing.Label(Localization.T("RimAIPortrait.Settings.ComfyRequiredOutput"));
            listing.Label(Localization.T("RimAIPortrait.Settings.ComfyWorkflowTokens"));
        }

        private static void DrawNovelAi(Listing_Standard listing, PortraitSettings settings, bool expression, Action<string> report)
        {
            if (expression)
            {
                Text(listing, Localization.T("RimAIPortrait.Settings.NovelAiApiKey"), ref settings.NovelAiExpressionApiKey);
                Text(listing, Localization.T("RimAIPortrait.Settings.Model"), ref settings.NovelAiExpressionModel);
                Text(listing, Localization.T("RimAIPortrait.Settings.OptionsJson"), ref settings.NovelAiExpressionOptions);
            }
            else
            {
                Text(listing, Localization.T("RimAIPortrait.Settings.NovelAiApiKey"), ref settings.NovelAiApiKey);
                Text(listing, Localization.T("RimAIPortrait.Settings.Model"), ref settings.NovelAiModel);
                PromptArea(listing, Localization.T("RimAIPortrait.Settings.BasePrompt"),
                    ref settings.NovelAiBaseInstructions, 130f,
                    RimAIPortraitMod.PackagedPrompt(AiProvider.NovelAI, "BasePrompt.md", false), report);
                Text(listing, Localization.T("RimAIPortrait.Settings.OptionsJson"), ref settings.NovelAiOptions);
            }
            string model = expression ? settings.NovelAiExpressionModel : settings.NovelAiModel;
            listing.Label(Localization.T(NovelAiPortraitProvider.IsV5Model(model)
                ? "RimAIPortrait.Settings.NovelAiV5Help"
                : "RimAIPortrait.Settings.NovelAiReferenceHelpV45"));
        }

        private static void DrawStyleFolder(Listing_Standard listing, ref string folder, AiProvider provider,
            PortraitSettings settings, Action<string> report)
        {
            Text(listing, Localization.T("RimAIPortrait.Settings.StyleFolder"), ref folder);
            listing.Label(Localization.T(provider == AiProvider.OpenAI
                ? "RimAIPortrait.Settings.OpenAiStyleFolderHint"
                : "RimAIPortrait.Settings.StyleFolderHint"));
            string novelAiModel = settings.NovelAiModel;
            if (provider == AiProvider.NovelAI && NovelAiPortraitProvider.IsV5Model(novelAiModel))
                listing.Label(Localization.T("RimAIPortrait.Settings.NovelAiV5StyleVibe"));
            bool hasDefault = provider == AiProvider.Google || provider == AiProvider.NovelAI;
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
            else if (provider == AiProvider.NovelAI && configuredCount == 0)
                statusKey = "RimAIPortrait.Settings.StyleDefault";
            else
                statusKey = hasDefault ? "RimAIPortrait.Settings.StyleFolderWithDefault"
                    : "RimAIPortrait.Settings.StyleFolderConfigured";
            listing.Label(Localization.T(statusKey));
            if (folderExists && configuredCount > 0)
            {
                IPortraitGenerationProvider selectedProvider = PortraitProviderRegistry.Get(provider);
                var request = new PortraitGenerationRequest {
                    State = PortraitState.Base,
                    ImageUse = PortraitImageUse.CharacterReference,
                    PrimaryImage = null,
                    ReferencePaths = new string[0],
                    Configuration = selectedProvider.CreateConfiguration(settings, false)
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

        internal static string Label(AiProvider provider)
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

        private static void PromptArea(Listing_Standard listing, string label, ref string value,
            float height, string original, Action<string> report)
        {
            TextArea(listing, label, ref value, height);
            Rect row = listing.GetRect(32f);
            if (Widgets.ButtonText(new Rect(row.xMax - 210f, row.y, 100f, row.height),
                Localization.T("RimAIPortrait.Common.Reset")))
            {
                if (original == null) report(Localization.T("RimAIPortrait.Settings.PromptDefaultMissing"));
                else
                {
                    value = original;
                    RimAIPortraitMod.SaveSettings();
                    report(Localization.T("RimAIPortrait.Settings.PromptReset", label));
                }
            }
            if (Widgets.ButtonText(new Rect(row.xMax - 100f, row.y, 100f, row.height),
                Localization.T("RimAIPortrait.Common.Save")))
            {
                RimAIPortraitMod.SaveSettings();
                report(Localization.T("RimAIPortrait.Settings.PromptSaved", label));
            }
        }
    }
}
