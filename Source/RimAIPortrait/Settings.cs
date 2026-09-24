// Contains the implementation for Settings.

using Verse;

namespace RimAIPortrait
{
    /// <summary>Describes the behavior of this type or member.</summary>
    public enum AiProvider { OpenAI, Google, LocalComfyUI }

    /// <summary>Describes the behavior of this type or member.</summary>
    public sealed class PortraitSettings : ModSettings
    {
        public AiProvider Provider = AiProvider.OpenAI;
        public AiProvider ExpressionProvider = AiProvider.OpenAI;
        public string ApiKey = "";
        public string ExpressionApiKey = "";
        public string OpenAiModel = "gpt-image-2.5-sunburst";
        public string OpenAiExpressionModel = "gpt-image-2.5-sunburst";
        public string OpenAiOptions = "{\"quality\":\"low\",\"size\":\"1024x1024\",\"background\":\"transparent\"}";
        public string OpenAiExpressionOptions = "{\"quality\":\"low\",\"size\":\"1024x1024\",\"background\":\"transparent\"}";
        public string GoogleModel = "gemini-2.5-flash-image";
        public string GoogleExpressionModel = "gemini-2.5-flash-image";
        public string GoogleOptions = "{\"aspect_ratio\":\"1:1\",\"image_size\":\"1K\"}";
        public string GoogleExpressionOptions = "{\"aspect_ratio\":\"1:1\",\"image_size\":\"1K\"}";
        public string ComfyUrl = "http://127.0.0.1:8188";
        public string ComfyExpressionUrl = "http://127.0.0.1:8188";
        public string ComfyWorkflowPath = "";
        public string ComfyExpressionWorkflowPath = "";
        public string ComfyNegativePrompt = "pixel art, photorealistic, 3d render, text, watermark, background";
        public string ComfyExpressionNegativePrompt = "pixel art, photorealistic, 3d render, text, watermark, background";
        public float ComfyTimeoutSeconds = 900f;
        public string Prompt = "";
        public string StyleFolder = "";
        public string ExpressionStyleFolder = "";
        public string BaseInstructions = PromptLibrary.DefaultBase;
        public string GeminiBaseInstructions = "";
        public float Width = 150f;
        public float Height = 250f;
        public float Scale = 1f;
        public float OffsetX = 15f;
        public float OffsetY = 214f;
        public float SwapSeconds = 3f;
        public float LowMoodThreshold = 0.20f;
        public float HighMoodThreshold = 0.65f;
        public bool GenerateStates = true;
        public bool EnableEmotionChanges = true;
        public bool EnableHigh = true, EnableLow = true, EnableSleep = false,
            EnablePain = false, EnableDown = false, EnableHot = false,
            EnableCold = false, EnableAngry = false, EnableDistressed = false;
        public bool DebugMode = false;
        public bool EnableFrame = false;
        public float FrameScale = 1f;
        public string FrameImagePath = "Textures/AIExpressivePortraits/default_frame.png";
        public float BackgroundRemovalSensitivity = .3f;
        public string BackgroundColorHex = "#FFFFFF";
        public int BackgroundErosionPixels = 2;
        public bool IncludeSprite = true, IncludeGender = true, IncludeRace = true,
            IncludeXenotype = true, IncludeAge = true, IncludeBody = true,
            IncludeSkin = true, IncludeHair = true, IncludeEyes = true,
            IncludeApparel = true, IncludeApparelQuality = true,
            IncludeHealth = true, IncludeTraits = false, IncludeBackstory = false;
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
        private bool ModelsSplitMigrated;
        private bool ProviderSettingsSplitMigrated;
        private bool PositionDefaultsMigrated;

        /// <summary>Describes the behavior of this type or member.</summary>
        public override void ExposeData()
        {
            Scribe_Values.Look(ref Provider, "provider", AiProvider.OpenAI);
            Scribe_Values.Look(ref ExpressionProvider, "expressionProvider", AiProvider.OpenAI);
            Scribe_Values.Look(ref ApiKey, "apiKey", "");
            Scribe_Values.Look(ref ExpressionApiKey, "expressionApiKey", "");
            Scribe_Values.Look(ref OpenAiModel, "openAiModel", "gpt-image-2.5-sunburst");
            Scribe_Values.Look(ref OpenAiExpressionModel, "openAiExpressionModel", "gpt-image-2.5-sunburst");
            Scribe_Values.Look(ref OpenAiOptions, "openAiOptions", "{\"quality\":\"low\",\"size\":\"1024x1024\",\"background\":\"transparent\"}");
            Scribe_Values.Look(ref OpenAiExpressionOptions, "openAiExpressionOptions", "{\"quality\":\"low\",\"size\":\"1024x1024\",\"background\":\"transparent\"}");
            Scribe_Values.Look(ref GoogleModel, "googleModel", "gemini-2.5-flash-image");
            Scribe_Values.Look(ref GoogleExpressionModel, "googleExpressionModel", "gemini-2.5-flash-image");
            Scribe_Values.Look(ref GoogleOptions, "googleOptions", "{\"aspect_ratio\":\"1:1\",\"image_size\":\"1K\"}");
            Scribe_Values.Look(ref GoogleExpressionOptions, "googleExpressionOptions", "{\"aspect_ratio\":\"1:1\",\"image_size\":\"1K\"}");
            Scribe_Values.Look(ref ComfyUrl, "comfyUrl", "http://127.0.0.1:8188");
            Scribe_Values.Look(ref ComfyExpressionUrl, "comfyExpressionUrl", "http://127.0.0.1:8188");
            Scribe_Values.Look(ref ComfyWorkflowPath, "comfyWorkflowPath", "");
            Scribe_Values.Look(ref ComfyExpressionWorkflowPath, "comfyExpressionWorkflowPath", "");
            Scribe_Values.Look(ref ComfyNegativePrompt, "comfyNegativePrompt", "pixel art, photorealistic, 3d render, text, watermark, background");
            Scribe_Values.Look(ref ComfyExpressionNegativePrompt, "comfyExpressionNegativePrompt", "pixel art, photorealistic, 3d render, text, watermark, background");
            Scribe_Values.Look(ref ComfyTimeoutSeconds, "comfyTimeoutSeconds", 900f);
            Scribe_Values.Look(ref Prompt, "prompt", "");
            Scribe_Values.Look(ref StyleFolder, "styleFolder", "");
            Scribe_Values.Look(ref ExpressionStyleFolder, "expressionStyleFolder", "");
            Scribe_Values.Look(ref BaseInstructions, "baseInstructions", PromptLibrary.DefaultBase);
            Scribe_Values.Look(ref GeminiBaseInstructions, "geminiBaseInstructions", "");
            Scribe_Values.Look(ref Width, "width", 150f); Scribe_Values.Look(ref Height, "height", 250f);
            Scribe_Values.Look(ref Scale, "scale", 1f); Scribe_Values.Look(ref OffsetX, "offsetX", 15f);
            Scribe_Values.Look(ref OffsetY, "offsetY", 214f); Scribe_Values.Look(ref SwapSeconds, "swapSeconds", 3f);
            Scribe_Values.Look(ref LowMoodThreshold, "lowMoodThreshold", 0.20f);
            Scribe_Values.Look(ref HighMoodThreshold, "highMoodThreshold", 0.65f);
            Scribe_Values.Look(ref GenerateStates, "generateStates", true);
            Scribe_Values.Look(ref EnableEmotionChanges, "enableEmotionChanges", true);
            Scribe_Values.Look(ref EnableHigh, "enableHigh", true); Scribe_Values.Look(ref EnableLow, "enableLow", true);
            Scribe_Values.Look(ref EnableSleep, "enableSleep", false); Scribe_Values.Look(ref EnablePain, "enablePain", false);
            Scribe_Values.Look(ref EnableDown, "enableDown", false); Scribe_Values.Look(ref EnableHot, "enableHot", false);
            Scribe_Values.Look(ref EnableCold, "enableCold", false); Scribe_Values.Look(ref EnableAngry, "enableAngry", false);
            Scribe_Values.Look(ref EnableDistressed, "enableDistressed", false);
            Scribe_Values.Look(ref DebugMode, "debugMode", false);
            Scribe_Values.Look(ref EnableFrame, "enableFrame", false);
            Scribe_Values.Look(ref FrameScale, "frameScale", 1f);
            Scribe_Values.Look(ref FrameImagePath, "frameImagePath", "Textures/AIExpressivePortraits/default_frame.png");
            Scribe_Values.Look(ref BackgroundRemovalSensitivity, "backgroundRemovalSensitivity", .3f);
            Scribe_Values.Look(ref BackgroundColorHex, "backgroundColorHex", "#FFFFFF");
            Scribe_Values.Look(ref BackgroundErosionPixels, "backgroundErosionPixels", 2);
            Scribe_Values.Look(ref IncludeSprite, "includeSprite", true); Scribe_Values.Look(ref IncludeGender, "includeGender", true);
            Scribe_Values.Look(ref IncludeRace, "includeRace", true); Scribe_Values.Look(ref IncludeXenotype, "includeXenotype", true);
            Scribe_Values.Look(ref IncludeAge, "includeAge", true); Scribe_Values.Look(ref IncludeBody, "includeBody", true);
            Scribe_Values.Look(ref IncludeSkin, "includeSkin", true); Scribe_Values.Look(ref IncludeHair, "includeHair", true);
            Scribe_Values.Look(ref IncludeEyes, "includeEyes", true); Scribe_Values.Look(ref IncludeApparel, "includeApparel", true);
            Scribe_Values.Look(ref IncludeApparelQuality, "includeApparelQuality", true); Scribe_Values.Look(ref IncludeTraits, "includeTraits", false);
            Scribe_Values.Look(ref IncludeHealth, "includeHealth", true);
            Scribe_Values.Look(ref IncludeBackstory, "includeBackstory", false);
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
            Scribe_Values.Look(ref ModelsSplitMigrated, "modelsSplitMigrated", false);
            Scribe_Values.Look(ref ProviderSettingsSplitMigrated, "providerSettingsSplitMigrated", false);
            Scribe_Values.Look(ref PositionDefaultsMigrated, "positionDefaultsMigrated", false);
            if (Scribe.mode == LoadSaveMode.LoadingVars)
            {
                MigrateSplitModels();
                MigrateProviderSettings();
                MigratePositionDefaults();
                MigrateOpenAiOptionDefaults();
            }
        }

        /// <summary>Describes the behavior of this type or member.</summary>
        /// <param name="state">The state value.</param>
        /// <returns>The resulting value.</returns>
        public bool IsStateEnabled(PortraitState state)
        {
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

        /// <summary>Describes the behavior of this type or member.</summary>
        private void MigrateOpenAiOptionDefaults()
        {
            const string previousDefault = "{\"quality\":\"low\",\"size\":\"1024x1024\"}";
            const string transparentDefault = "{\"quality\":\"low\",\"size\":\"1024x1024\",\"background\":\"transparent\"}";
            if (OpenAiOptions == previousDefault) OpenAiOptions = transparentDefault;
            if (OpenAiExpressionOptions == previousDefault) OpenAiExpressionOptions = transparentDefault;
        }

        /// <summary>Describes the behavior of this type or member.</summary>
        private void MigratePositionDefaults()
        {
            if (PositionDefaultsMigrated) return;
            if (OffsetX == 15f && OffsetY == 25f) OffsetY = 214f;
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
