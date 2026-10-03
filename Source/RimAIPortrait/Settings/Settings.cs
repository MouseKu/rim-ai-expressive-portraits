// Contains the implementation for Settings.

using System;
using System.Globalization;
using System.Reflection;
using System.Text.RegularExpressions;
using Verse;

namespace RimAIPortrait
{
    /// <summary>Describes the behavior of this type or member.</summary>
    public enum AiProvider { OpenAI, Google, LocalComfyUI, NovelAI }
    public enum TagExtractorModel { OpenAI, Google, LocalModel }

    /// <summary>Describes the behavior of this type or member.</summary>
    [Serializable]
    public sealed class PortraitSettings : ModSettings
    {
        public AiProvider Provider;
        public AiProvider ExpressionProvider;
        public AiProvider RegenerateProvider;
        public string ApiKey;
        public string ExpressionApiKey;
        public string GoogleApiKey;
        public string GoogleExpressionApiKey;
        public string OpenAiModel;
        public string OpenAiExpressionModel;
        public string OpenAiOptions = ProviderOptionDefaults.OpenAiBase;
        public string OpenAiExpressionOptions = ProviderOptionDefaults.OpenAiExpression;
        public string OpenAiImageCaptionsJson;
        public string GoogleModel;
        public string GoogleExpressionModel;
        public string GoogleOptions = ProviderOptionDefaults.GoogleBase;
        public string GoogleExpressionOptions = ProviderOptionDefaults.GoogleExpression;
        public string GoogleImageCaptionsJson;
        public string RegenerateOpenAiApiKey;
        public string RegenerateOpenAiModel;
        public string RegenerateOpenAiOptions = ProviderOptionDefaults.OpenAiBase;
        public string RegenerateGoogleApiKey;
        public string RegenerateGoogleModel;
        public string RegenerateGoogleOptions = ProviderOptionDefaults.GoogleBase;
        public string RegenerateNovelAiApiKey;
        public string RegenerateNovelAiModel;
        public string RegenerateNovelAiOptions = ProviderOptionDefaults.NovelAiExpression;
        public string RegenerateComfyUrl;
        public string RegenerateComfyWorkflowPath;
        public string RegenerateComfyNegativePrompt;
        public string RegenerateStyleFolder;
        public string NovelAiApiKey;
        public string NovelAiExpressionApiKey;
        public string NovelAiModel;
        public string NovelAiExpressionModel;
        public string NovelAiOptions = ProviderOptionDefaults.NovelAiBase;
        public string NovelAiExpressionOptions = ProviderOptionDefaults.NovelAiExpression;
        public float NovelAiInpaintStrength;
        public float NovelAiSleepInpaintStrength;
        public float InpaintHighStrength;
        public float InpaintLowStrength;
        public float InpaintPainStrength;
        public float InpaintDownStrength;
        public float InpaintHotStrength;
        public float InpaintColdStrength;
        public float InpaintAngryStrength;
        public float InpaintDistressedStrength;
        public string ComfyUrl;
        public string ComfyExpressionUrl;
        public string ComfyWorkflowPath;
        public string ComfyExpressionWorkflowPath;
        public string ComfyNegativePrompt;
        public string ComfyExpressionNegativePrompt;
        public float ComfyTimeoutSeconds;
        public bool EnableTagExtractor;
        public TagExtractorModel TagExtractorBackend;
        public string TagExtractorOpenAiKey;
        public string TagExtractorOpenAiModel;
        public string TagExtractorGoogleKey;
        public string TagExtractorGoogleModel;
        public string TagExtractorLocalUrl;
        public string TagExtractorLocalModel;
        public string TagExtractorLocalKey;
        public string TagExtractorBasePrompt;
        public string TagExtractorInpaintBatchPrompt;
        public string Prompt;
        public string PortraitRootFolder;
        public string StyleFolder;
        public string ExpressionStyleFolder;
        public string BaseInstructions = PromptLibrary.DefaultBase;
        public string ComfyBaseInstructions;
        public string ExpressionInstructions = PromptLibrary.DefaultExpression;
        public string GoogleExpressionInstructions;
        public string ComfyExpressionInstructions;
        public string NovelAiExpressionInstructions;
        public string GeminiBaseInstructions;
        public string NovelAiBaseInstructions;
        public float Width;
        public float Height;
        public float Scale;
        public float OffsetX;
        public float OffsetY;
        public float SwapSeconds;
        public float LowMoodThreshold;
        public float HighMoodThreshold;
        public bool GenerateStates;
        public bool EnableEmotionChanges;
        public bool PortraitHelpShown;
        public bool PortraitHelpAfterImageShown;
        public bool PortraitHelpDismissed;
        public bool PortraitHelpAfterImageDismissed;
        public bool NovelAiV5HelpDismissed;
        public bool EnableHigh, EnableLow, EnableSleep,
            EnablePain, EnableDown, EnableHot,
            EnableCold, EnableAngry, EnableDistressed;
        public bool DebugMode;
        public bool ShowApiRequest;
        public bool EnableFrame;
        public float FrameScale;
        public string FrameImagePath;
        public float BackgroundRemovalSensitivity;
        public string BackgroundColorHex;
        public int BackgroundErosionPixels;
        public bool RemoveEnclosedBackground;
        public float EnclosedCleanup;
        public bool BackgroundAlignBottom;
        public bool IncludeSprite, IncludeGender, IncludeRace,
            IncludeXenotype, IncludeGene, OnlyAppearanceGenes, IncludeAge, IncludeBody,
            IncludeSkin, IncludeHair, IncludeEyes,
            IncludeApparel, IncludeApparelQuality,
            IncludeHealth, IncludeTraits, IncludeBackstory;
        public string NormalPrompt = PromptLibrary.State[PortraitState.Normal];
        public string HighPrompt = PromptLibrary.State[PortraitState.High];
        public string LowPrompt = PromptLibrary.State[PortraitState.Low];
        public string SleepPrompt = PromptLibrary.State[PortraitState.Sleep];
        public string PainPrompt = PromptLibrary.State[PortraitState.Pain];
        public string DownPrompt = PromptLibrary.State[PortraitState.Down];
        public string HotPrompt = PromptLibrary.State[PortraitState.Hot];
        public string ColdPrompt = PromptLibrary.State[PortraitState.Cold];
        public string AngryPrompt = PromptLibrary.State[PortraitState.Angry];
        public string DistressedPrompt = PromptLibrary.State[PortraitState.Distressed];
        public ProviderStatePrompts GoogleStatePrompts = new ProviderStatePrompts();
        public ProviderStatePrompts ComfyStatePrompts = new ProviderStatePrompts();
        public ProviderStatePrompts NovelAiStatePrompts = new ProviderStatePrompts();
        private bool ModelsSplitMigrated;
        private bool ProviderSettingsSplitMigrated;
        private bool ProviderApiKeysSplitMigrated;
        private bool PositionDefaultsMigrated;
        private bool ProviderPromptsSplitMigrated = true;
        private bool RegenerateSettingsMigrated;
        internal bool ProviderPromptMigrationPending;

        public PortraitSettings()
        {
            SettingsDefaults.Apply(this);
        }

        /// <summary>Restores every persisted option to its packaged default value.</summary>
        public void ResetToDefaults()
        {
            bool portraitHelpShown = PortraitHelpShown;
            bool portraitHelpAfterImageShown = PortraitHelpAfterImageShown;
            bool portraitHelpDismissed = PortraitHelpDismissed;
            bool portraitHelpAfterImageDismissed = PortraitHelpAfterImageDismissed;
            bool novelAiV5HelpDismissed = NovelAiV5HelpDismissed;
            var defaults = new PortraitSettings();
            foreach (FieldInfo field in typeof(PortraitSettings).GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
            {
                if (!field.IsInitOnly) field.SetValue(this, field.GetValue(defaults));
            }
            PortraitHelpShown = portraitHelpShown;
            PortraitHelpAfterImageShown = portraitHelpAfterImageShown;
            PortraitHelpDismissed = portraitHelpDismissed;
            PortraitHelpAfterImageDismissed = portraitHelpAfterImageDismissed;
            NovelAiV5HelpDismissed = novelAiV5HelpDismissed;
            RegenerateSettingsMigrated = true;
        }

        /// <summary>Describes the behavior of this type or member.</summary>
        public override void ExposeData()
        {
            PortraitSettings defaults = SettingsDefaults.Values;
            float legacyCharacterStrength = .55f;
            float legacyCharacterFidelity = .7f;
            Scribe_Values.Look(ref Provider, "provider", defaults.Provider);
            Scribe_Values.Look(ref ExpressionProvider, "expressionProvider", defaults.ExpressionProvider);
            Scribe_Values.Look(ref RegenerateProvider, "regenerateProvider", defaults.RegenerateProvider);
            Scribe_Values.Look(ref RegenerateOpenAiApiKey, "regenerateOpenAiApiKey", defaults.RegenerateOpenAiApiKey);
            Scribe_Values.Look(ref RegenerateOpenAiModel, "regenerateOpenAiModel", defaults.RegenerateOpenAiModel);
            Scribe_Values.Look(ref RegenerateOpenAiOptions, "regenerateOpenAiOptions", ProviderOptionDefaults.OpenAiBase);
            Scribe_Values.Look(ref RegenerateGoogleApiKey, "regenerateGoogleApiKey", defaults.RegenerateGoogleApiKey);
            Scribe_Values.Look(ref RegenerateGoogleModel, "regenerateGoogleModel", defaults.RegenerateGoogleModel);
            Scribe_Values.Look(ref RegenerateGoogleOptions, "regenerateGoogleOptions", ProviderOptionDefaults.GoogleBase);
            Scribe_Values.Look(ref RegenerateNovelAiApiKey, "regenerateNovelAiApiKey", defaults.RegenerateNovelAiApiKey);
            Scribe_Values.Look(ref RegenerateNovelAiModel, "regenerateNovelAiModel", defaults.RegenerateNovelAiModel);
            Scribe_Values.Look(ref RegenerateNovelAiOptions, "regenerateNovelAiOptions", ProviderOptionDefaults.NovelAiExpression);
            Scribe_Values.Look(ref RegenerateComfyUrl, "regenerateComfyUrl", defaults.RegenerateComfyUrl);
            Scribe_Values.Look(ref RegenerateComfyWorkflowPath, "regenerateComfyWorkflowPath", defaults.RegenerateComfyWorkflowPath);
            Scribe_Values.Look(ref RegenerateComfyNegativePrompt, "regenerateComfyNegativePrompt", defaults.RegenerateComfyNegativePrompt);
            Scribe_Values.Look(ref RegenerateStyleFolder, "regenerateStyleFolder", defaults.RegenerateStyleFolder);
            Scribe_Values.Look(ref RegenerateSettingsMigrated, "regenerateSettingsMigrated", false);
            Scribe_Values.Look(ref ApiKey, "apiKey", defaults.ApiKey);
            Scribe_Values.Look(ref ExpressionApiKey, "expressionApiKey", defaults.ExpressionApiKey);
            Scribe_Values.Look(ref GoogleApiKey, "googleApiKey", defaults.GoogleApiKey);
            Scribe_Values.Look(ref GoogleExpressionApiKey, "googleExpressionApiKey", defaults.GoogleExpressionApiKey);
            Scribe_Values.Look(ref OpenAiModel, "openAiModel", defaults.OpenAiModel);
            Scribe_Values.Look(ref OpenAiExpressionModel, "openAiExpressionModel", defaults.OpenAiExpressionModel);
            Scribe_Values.Look(ref OpenAiOptions, "openAiOptions", ProviderOptionDefaults.OpenAiBase);
            Scribe_Values.Look(ref OpenAiExpressionOptions, "openAiExpressionOptions", ProviderOptionDefaults.OpenAiExpression);
            Scribe_Values.Look(ref OpenAiImageCaptionsJson, "openAiImageCaptionsJson");
            Scribe_Values.Look(ref GoogleModel, "googleModel", defaults.GoogleModel);
            Scribe_Values.Look(ref GoogleExpressionModel, "googleExpressionModel", defaults.GoogleExpressionModel);
            Scribe_Values.Look(ref GoogleOptions, "googleOptions", ProviderOptionDefaults.GoogleBase);
            Scribe_Values.Look(ref GoogleExpressionOptions, "googleExpressionOptions", ProviderOptionDefaults.GoogleExpression);
            Scribe_Values.Look(ref GoogleImageCaptionsJson, "googleImageCaptionsJson");
            Scribe_Values.Look(ref NovelAiApiKey, "novelAiApiKey", defaults.NovelAiApiKey);
            Scribe_Values.Look(ref NovelAiExpressionApiKey, "novelAiExpressionApiKey", defaults.NovelAiExpressionApiKey);
            Scribe_Values.Look(ref NovelAiModel, "novelAiModel", defaults.NovelAiModel);
            Scribe_Values.Look(ref NovelAiExpressionModel, "novelAiExpressionModel", defaults.NovelAiExpressionModel);
            Scribe_Values.Look(ref NovelAiOptions, "novelAiOptions", ProviderOptionDefaults.NovelAiBase);
            Scribe_Values.Look(ref NovelAiExpressionOptions, "novelAiExpressionOptions", ProviderOptionDefaults.NovelAiExpression);
            Scribe_Values.Look(ref NovelAiInpaintStrength, "novelAiInpaintStrength", defaults.NovelAiInpaintStrength);
            Scribe_Values.Look(ref NovelAiSleepInpaintStrength, "novelAiSleepInpaintStrength", defaults.NovelAiSleepInpaintStrength);
            Scribe_Values.Look(ref InpaintHighStrength, "inpaintHighStrength", NovelAiInpaintStrength);
            Scribe_Values.Look(ref InpaintLowStrength, "inpaintLowStrength", NovelAiInpaintStrength);
            Scribe_Values.Look(ref InpaintPainStrength, "inpaintPainStrength", NovelAiInpaintStrength);
            Scribe_Values.Look(ref InpaintDownStrength, "inpaintDownStrength", NovelAiInpaintStrength);
            Scribe_Values.Look(ref InpaintHotStrength, "inpaintHotStrength", NovelAiInpaintStrength);
            Scribe_Values.Look(ref InpaintColdStrength, "inpaintColdStrength", NovelAiInpaintStrength);
            Scribe_Values.Look(ref InpaintAngryStrength, "inpaintAngryStrength", NovelAiInpaintStrength);
            Scribe_Values.Look(ref InpaintDistressedStrength, "inpaintDistressedStrength", NovelAiInpaintStrength);
            if (Scribe.mode == LoadSaveMode.LoadingVars)
            {
                Scribe_Values.Look(ref legacyCharacterStrength, "novelAiCharacterReferenceStrength", .55f);
                Scribe_Values.Look(ref legacyCharacterFidelity, "novelAiCharacterReferenceFidelity", .7f);
            }
            Scribe_Values.Look(ref ComfyUrl, "comfyUrl", defaults.ComfyUrl);
            Scribe_Values.Look(ref ComfyExpressionUrl, "comfyExpressionUrl", defaults.ComfyExpressionUrl);
            Scribe_Values.Look(ref ComfyWorkflowPath, "comfyWorkflowPath", defaults.ComfyWorkflowPath);
            Scribe_Values.Look(ref ComfyExpressionWorkflowPath, "comfyExpressionWorkflowPath", defaults.ComfyExpressionWorkflowPath);
            Scribe_Values.Look(ref ComfyNegativePrompt, "comfyNegativePrompt", defaults.ComfyNegativePrompt);
            Scribe_Values.Look(ref ComfyExpressionNegativePrompt, "comfyExpressionNegativePrompt", defaults.ComfyExpressionNegativePrompt);
            Scribe_Values.Look(ref ComfyTimeoutSeconds, "comfyTimeoutSeconds", defaults.ComfyTimeoutSeconds);
            Scribe_Values.Look(ref EnableTagExtractor, "enableTagExtractor", defaults.EnableTagExtractor);
            Scribe_Values.Look(ref TagExtractorBackend, "tagExtractorModel", defaults.TagExtractorBackend);
            Scribe_Values.Look(ref TagExtractorOpenAiKey, "tagExtractorOpenAiKey", defaults.TagExtractorOpenAiKey);
            Scribe_Values.Look(ref TagExtractorOpenAiModel, "tagExtractorOpenAiModel", defaults.TagExtractorOpenAiModel);
            Scribe_Values.Look(ref TagExtractorGoogleKey, "tagExtractorGoogleKey", defaults.TagExtractorGoogleKey);
            Scribe_Values.Look(ref TagExtractorGoogleModel, "tagExtractorGoogleModel", defaults.TagExtractorGoogleModel);
            Scribe_Values.Look(ref TagExtractorLocalUrl, "tagExtractorLocalUrl", defaults.TagExtractorLocalUrl);
            Scribe_Values.Look(ref TagExtractorLocalModel, "tagExtractorLocalModel", defaults.TagExtractorLocalModel);
            Scribe_Values.Look(ref TagExtractorLocalKey, "tagExtractorLocalKey", defaults.TagExtractorLocalKey);
            Scribe_Values.Look(ref TagExtractorBasePrompt, "tagExtractorBasePrompt");
            Scribe_Values.Look(ref TagExtractorInpaintBatchPrompt, "tagExtractorInpaintBatchPrompt");
            Scribe_Values.Look(ref Prompt, "prompt", defaults.Prompt);
            Scribe_Values.Look(ref PortraitRootFolder, "portraitRootFolder", "");
            Scribe_Values.Look(ref StyleFolder, "styleFolder", "");
            Scribe_Values.Look(ref ExpressionStyleFolder, "expressionStyleFolder", "");
            Scribe_Values.Look(ref BaseInstructions, "baseInstructions", PromptLibrary.DefaultBase);
            Scribe_Values.Look(ref ComfyBaseInstructions, "comfyBaseInstructions", defaults.ComfyBaseInstructions);
            Scribe_Values.Look(ref ExpressionInstructions, "expressionInstructions", PromptLibrary.DefaultExpression);
            Scribe_Values.Look(ref GoogleExpressionInstructions, "googleExpressionInstructions");
            Scribe_Values.Look(ref ComfyExpressionInstructions, "comfyExpressionInstructions");
            Scribe_Values.Look(ref NovelAiExpressionInstructions, "novelAiExpressionInstructions");
            Scribe_Values.Look(ref GeminiBaseInstructions, "geminiBaseInstructions", defaults.GeminiBaseInstructions);
            Scribe_Values.Look(ref NovelAiBaseInstructions, "novelAiBaseInstructions", defaults.NovelAiBaseInstructions);
            Scribe_Values.Look(ref Width, "width", defaults.Width); Scribe_Values.Look(ref Height, "height", defaults.Height);
            Scribe_Values.Look(ref Scale, "scale", defaults.Scale); Scribe_Values.Look(ref OffsetX, "offsetX", defaults.OffsetX);
            Scribe_Values.Look(ref OffsetY, "offsetY", defaults.OffsetY); Scribe_Values.Look(ref SwapSeconds, "swapSeconds", defaults.SwapSeconds);
            Scribe_Values.Look(ref LowMoodThreshold, "lowMoodThreshold", defaults.LowMoodThreshold);
            Scribe_Values.Look(ref HighMoodThreshold, "highMoodThreshold", defaults.HighMoodThreshold);
            Scribe_Values.Look(ref GenerateStates, "generateStates", defaults.GenerateStates);
            Scribe_Values.Look(ref EnableEmotionChanges, "enableEmotionChanges", defaults.EnableEmotionChanges);
            Scribe_Values.Look(ref PortraitHelpShown, "portraitHelpShown", false);
            Scribe_Values.Look(ref PortraitHelpAfterImageShown, "portraitHelpAfterImageShown", PortraitHelpShown);
            Scribe_Values.Look(ref PortraitHelpDismissed, "portraitHelpDismissed", false);
            Scribe_Values.Look(ref PortraitHelpAfterImageDismissed, "portraitHelpAfterImageDismissed", false);
            Scribe_Values.Look(ref NovelAiV5HelpDismissed, "novelAiV5HelpDismissed", false);
            Scribe_Values.Look(ref EnableHigh, "enableHigh", defaults.EnableHigh); Scribe_Values.Look(ref EnableLow, "enableLow", defaults.EnableLow);
            Scribe_Values.Look(ref EnableSleep, "enableSleep", defaults.EnableSleep); Scribe_Values.Look(ref EnablePain, "enablePain", defaults.EnablePain);
            Scribe_Values.Look(ref EnableDown, "enableDown", defaults.EnableDown); Scribe_Values.Look(ref EnableHot, "enableHot", defaults.EnableHot);
            Scribe_Values.Look(ref EnableCold, "enableCold", defaults.EnableCold); Scribe_Values.Look(ref EnableAngry, "enableAngry", defaults.EnableAngry);
            Scribe_Values.Look(ref EnableDistressed, "enableDistressed", defaults.EnableDistressed);
            Scribe_Values.Look(ref DebugMode, "debugMode", defaults.DebugMode);
            Scribe_Values.Look(ref ShowApiRequest, "showApiRequest", false);
            Scribe_Values.Look(ref EnableFrame, "enableFrame", defaults.EnableFrame);
            Scribe_Values.Look(ref FrameScale, "frameScale", defaults.FrameScale);
            Scribe_Values.Look(ref FrameImagePath, "frameImagePath", defaults.FrameImagePath);
            Scribe_Values.Look(ref BackgroundRemovalSensitivity, "backgroundRemovalSensitivity", defaults.BackgroundRemovalSensitivity);
            Scribe_Values.Look(ref BackgroundColorHex, "backgroundColorHex", defaults.BackgroundColorHex);
            Scribe_Values.Look(ref BackgroundErosionPixels, "backgroundErosionPixels", defaults.BackgroundErosionPixels);
            Scribe_Values.Look(ref RemoveEnclosedBackground, "removeEnclosedBackground", defaults.RemoveEnclosedBackground);
            Scribe_Values.Look(ref EnclosedCleanup, "enclosedCleanup", defaults.EnclosedCleanup);
            Scribe_Values.Look(ref BackgroundAlignBottom, "backgroundAlignBottom", defaults.BackgroundAlignBottom);
            Scribe_Values.Look(ref IncludeSprite, "includeSprite", defaults.IncludeSprite); Scribe_Values.Look(ref IncludeGender, "includeGender", defaults.IncludeGender);
            Scribe_Values.Look(ref IncludeRace, "includeRace", defaults.IncludeRace); Scribe_Values.Look(ref IncludeXenotype, "includeXenotype", defaults.IncludeXenotype);
            Scribe_Values.Look(ref IncludeGene, "includeGene", defaults.IncludeGene);
            Scribe_Values.Look(ref OnlyAppearanceGenes, "onlyAppearanceGenes", defaults.OnlyAppearanceGenes);
            Scribe_Values.Look(ref IncludeAge, "includeAge", defaults.IncludeAge); Scribe_Values.Look(ref IncludeBody, "includeBody", defaults.IncludeBody);
            Scribe_Values.Look(ref IncludeSkin, "includeSkin", defaults.IncludeSkin); Scribe_Values.Look(ref IncludeHair, "includeHair", defaults.IncludeHair);
            Scribe_Values.Look(ref IncludeEyes, "includeEyes", defaults.IncludeEyes); Scribe_Values.Look(ref IncludeApparel, "includeApparel", defaults.IncludeApparel);
            Scribe_Values.Look(ref IncludeApparelQuality, "includeApparelQuality", defaults.IncludeApparelQuality); Scribe_Values.Look(ref IncludeTraits, "includeTraits", defaults.IncludeTraits);
            Scribe_Values.Look(ref IncludeHealth, "includeHealth", defaults.IncludeHealth);
            Scribe_Values.Look(ref IncludeBackstory, "includeBackstory", defaults.IncludeBackstory);
            Scribe_Values.Look(ref NormalPrompt, "normalPrompt", PromptLibrary.State[PortraitState.Normal]);
            Scribe_Values.Look(ref HighPrompt, "highPrompt", PromptLibrary.State[PortraitState.High]);
            Scribe_Values.Look(ref LowPrompt, "lowPrompt", PromptLibrary.State[PortraitState.Low]);
            Scribe_Values.Look(ref SleepPrompt, "sleepPrompt", PromptLibrary.State[PortraitState.Sleep]);
            Scribe_Values.Look(ref PainPrompt, "painPrompt", PromptLibrary.State[PortraitState.Pain]);
            Scribe_Values.Look(ref DownPrompt, "downPrompt", PromptLibrary.State[PortraitState.Down]);
            Scribe_Values.Look(ref HotPrompt, "hotPrompt", PromptLibrary.State[PortraitState.Hot]);
            Scribe_Values.Look(ref ColdPrompt, "coldPrompt", PromptLibrary.State[PortraitState.Cold]);
            Scribe_Values.Look(ref AngryPrompt, "angryPrompt", PromptLibrary.State[PortraitState.Angry]);
            Scribe_Values.Look(ref DistressedPrompt, "distressedPrompt", PromptLibrary.State[PortraitState.Distressed]);
            Scribe_Deep.Look(ref GoogleStatePrompts, "googleStatePrompts");
            Scribe_Deep.Look(ref ComfyStatePrompts, "comfyStatePrompts");
            Scribe_Deep.Look(ref NovelAiStatePrompts, "novelAiStatePrompts");
            Scribe_Values.Look(ref ProviderPromptsSplitMigrated, "providerPromptsSplitMigrated", false);
            Scribe_Values.Look(ref ModelsSplitMigrated, "modelsSplitMigrated", false);
            Scribe_Values.Look(ref ProviderSettingsSplitMigrated, "providerSettingsSplitMigrated", false);
            Scribe_Values.Look(ref ProviderApiKeysSplitMigrated, "providerApiKeysSplitMigrated", false);
            Scribe_Values.Look(ref PositionDefaultsMigrated, "positionDefaultsMigrated", false);
            if (Scribe.mode == LoadSaveMode.LoadingVars)
            {
                MigrateSplitModels();
                MigrateProviderSettings();
                MigrateProviderApiKeys();
                MigratePositionDefaults();
                MigrateOpenAiOptionDefaults();
                MigrateGoogleOptionDefaults();
                MigrateNovelAiOptions(legacyCharacterStrength, legacyCharacterFidelity);
                MigrateRegenerateSettings();
            }
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (!ProviderPromptsSplitMigrated)
                {
                    ProviderPromptMigrationPending = true;
                    GoogleStatePrompts = LegacyStatePrompts();
                    ComfyStatePrompts = LegacyStatePrompts();
                    NovelAiStatePrompts = LegacyStatePrompts();
                    if (ExpressionInstructions != PromptLibrary.DefaultExpression)
                    {
                        GoogleExpressionInstructions = ExpressionInstructions;
                        ComfyExpressionInstructions = ExpressionInstructions;
                        NovelAiExpressionInstructions = ExpressionInstructions;
                    }
                    ProviderPromptsSplitMigrated = true;
                }
                if (GoogleStatePrompts == null) GoogleStatePrompts = new ProviderStatePrompts();
                if (ComfyStatePrompts == null) ComfyStatePrompts = new ProviderStatePrompts();
                if (NovelAiStatePrompts == null) NovelAiStatePrompts = new ProviderStatePrompts();
            }
        }

        private ProviderStatePrompts LegacyStatePrompts()
        {
            var prompts = new ProviderStatePrompts();
            foreach (PortraitState state in Enum.GetValues(typeof(PortraitState)))
                if (state != PortraitState.Base) prompts.Set(state, LegacyStatePrompt(state));
            return prompts;
        }

        private string LegacyStatePrompt(PortraitState state)
        {
            switch (state)
            {
                case PortraitState.Normal: return NormalPrompt;
                case PortraitState.High: return HighPrompt;
                case PortraitState.Low: return LowPrompt;
                case PortraitState.Sleep: return SleepPrompt;
                case PortraitState.Pain: return PainPrompt;
                case PortraitState.Down: return DownPrompt;
                case PortraitState.Hot: return HotPrompt;
                case PortraitState.Cold: return ColdPrompt;
                case PortraitState.Angry: return AngryPrompt;
                default: return DistressedPrompt;
            }
        }

        public string GetStatePrompt(AiProvider provider, PortraitState state)
        {
            ProviderStatePrompts prompts = provider == AiProvider.Google ? GoogleStatePrompts
                : provider == AiProvider.LocalComfyUI ? ComfyStatePrompts
                : provider == AiProvider.NovelAI ? NovelAiStatePrompts : null;
            return prompts == null ? LegacyStatePrompt(state) : prompts.Get(state);
        }

        public void SetStatePrompt(AiProvider provider, PortraitState state, string value)
        {
            if (provider != AiProvider.OpenAI)
            {
                ProviderStatePrompts prompts = provider == AiProvider.Google ? GoogleStatePrompts
                    : provider == AiProvider.LocalComfyUI ? ComfyStatePrompts : NovelAiStatePrompts;
                if (prompts == null)
                {
                    prompts = new ProviderStatePrompts();
                    if (provider == AiProvider.Google) GoogleStatePrompts = prompts;
                    else if (provider == AiProvider.LocalComfyUI) ComfyStatePrompts = prompts;
                    else NovelAiStatePrompts = prompts;
                }
                prompts.Set(state, value);
                return;
            }
            switch (state)
            {
                case PortraitState.Normal: NormalPrompt = value; break;
                case PortraitState.High: HighPrompt = value; break;
                case PortraitState.Low: LowPrompt = value; break;
                case PortraitState.Sleep: SleepPrompt = value; break;
                case PortraitState.Pain: PainPrompt = value; break;
                case PortraitState.Down: DownPrompt = value; break;
                case PortraitState.Hot: HotPrompt = value; break;
                case PortraitState.Cold: ColdPrompt = value; break;
                case PortraitState.Angry: AngryPrompt = value; break;
                default: DistressedPrompt = value; break;
            }
        }

        public string GetExpressionInstructions(AiProvider provider)
        {
            return provider == AiProvider.Google ? GoogleExpressionInstructions
                : provider == AiProvider.LocalComfyUI ? ComfyExpressionInstructions
                : provider == AiProvider.NovelAI ? NovelAiExpressionInstructions : ExpressionInstructions;
        }

        /// <summary>Describes the behavior of this type or member.</summary>
        /// <param name="state">The state value.</param>
        /// <returns>The resulting value.</returns>
        public bool IsStateEnabled(PortraitState state)
        {
            if (state == PortraitState.Base) return true;
            if (state == PortraitState.Normal) return true;
            if (state == PortraitState.High) return EnableHigh;
            if (state == PortraitState.Low) return EnableLow;
            if (state == PortraitState.Sleep) return EnableSleep;
            if (state == PortraitState.Pain) return EnablePain;
            if (state == PortraitState.Down) return EnableDown;
            if (state == PortraitState.Hot) return EnableHot;
            if (state == PortraitState.Cold) return EnableCold;
            if (state == PortraitState.Angry) return EnableAngry;
            return EnableDistressed;
        }

        public float GetInpaintStrength(PortraitState state)
        {
            switch (state)
            {
                case PortraitState.High: return InpaintHighStrength;
                case PortraitState.Low: return InpaintLowStrength;
                case PortraitState.Sleep: return NovelAiSleepInpaintStrength;
                case PortraitState.Pain: return InpaintPainStrength;
                case PortraitState.Down: return InpaintDownStrength;
                case PortraitState.Hot: return InpaintHotStrength;
                case PortraitState.Cold: return InpaintColdStrength;
                case PortraitState.Angry: return InpaintAngryStrength;
                case PortraitState.Distressed: return InpaintDistressedStrength;
                default: return NovelAiInpaintStrength;
            }
        }

        public void SetInpaintStrength(PortraitState state, float strength)
        {
            switch (state)
            {
                case PortraitState.High: InpaintHighStrength = strength; break;
                case PortraitState.Low: InpaintLowStrength = strength; break;
                case PortraitState.Sleep: NovelAiSleepInpaintStrength = strength; break;
                case PortraitState.Pain: InpaintPainStrength = strength; break;
                case PortraitState.Down: InpaintDownStrength = strength; break;
                case PortraitState.Hot: InpaintHotStrength = strength; break;
                case PortraitState.Cold: InpaintColdStrength = strength; break;
                case PortraitState.Angry: InpaintAngryStrength = strength; break;
                case PortraitState.Distressed: InpaintDistressedStrength = strength; break;
                default: NovelAiInpaintStrength = strength; break;
            }
        }

        /// <summary>Describes the behavior of this type or member.</summary>
        private void MigrateOpenAiOptionDefaults()
        {
            const string previousDefault = "{\"quality\":\"low\",\"size\":\"1024x1024\"}";
            const string currentDefault = "{\"quality\":\"low\",\"size\":\"1024x1024\",\"background\":\"transparent\"}";
            if (OpenAiOptions == previousDefault || OpenAiOptions == currentDefault) OpenAiOptions = ProviderOptionDefaults.OpenAiBase;
            if (OpenAiExpressionOptions == previousDefault || OpenAiExpressionOptions == currentDefault) OpenAiExpressionOptions = ProviderOptionDefaults.OpenAiExpression;
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

        internal PortraitProviderConfiguration RegenerateConfiguration()
        {
            switch (RegenerateProvider)
            {
                case AiProvider.OpenAI:
                    return new OpenAiProviderConfiguration { ApiKey = RegenerateOpenAiApiKey,
                        Model = RegenerateOpenAiModel, OptionsJson = RegenerateOpenAiOptions };
                case AiProvider.Google:
                    return new GoogleProviderConfiguration { ApiKey = RegenerateGoogleApiKey,
                        Model = RegenerateGoogleModel, OptionsJson = RegenerateGoogleOptions };
                case AiProvider.NovelAI:
                    return new NovelAiProviderConfiguration { ApiKey = RegenerateNovelAiApiKey,
                        Model = RegenerateNovelAiModel, OptionsJson = RegenerateNovelAiOptions };
                default:
                    return new ComfyUiProviderConfiguration { ServerUrl = RegenerateComfyUrl,
                        WorkflowPath = RegenerateComfyWorkflowPath, NegativePrompt = RegenerateComfyNegativePrompt,
                        TimeoutSeconds = ComfyTimeoutSeconds };
            }
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
