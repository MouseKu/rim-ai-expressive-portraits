using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Verse;

namespace RimAIPortrait
{
    public sealed partial class RimAIPortraitMod
    {
        /// <summary>
        /// Provides additional behavior and compatibility details.
        /// Provides additional behavior and compatibility details.
        /// </summary>
        private static void ApplyImagePromptDefault(ModContentPack content)
        {
            try
            {
                string path = Path.Combine(content.RootDir, "Prompts", "BasePrompt.md");
                if (!File.Exists(path)) return;
                string imagePrompt = File.ReadAllText(path).Trim();
                if (!imagePrompt.NullOrEmpty() && (Settings.BaseInstructions.NullOrEmpty() || Settings.BaseInstructions == PromptLibrary.DefaultBase))
                    Settings.BaseInstructions = imagePrompt;
            }
            catch (Exception e) { Log.Warning("[Rim AI Expressive Portraits] Could not load Prompts/BasePrompt.md: " + e.Message); }
            if (Settings.ComfyBaseInstructions.NullOrEmpty())
                Settings.ComfyBaseInstructions = Settings.BaseInstructions;
        }

        private static void ApplyExpressionPromptDefault(ModContentPack content)
        {
            try
            {
                string path = Path.Combine(content.RootDir, "Prompts", "ExpressionPrompt.md");
                if (!File.Exists(path)) return;
                string prompt = File.ReadAllText(path).Trim();
                if (!prompt.NullOrEmpty() && (Settings.ExpressionInstructions.NullOrEmpty()
                    || Settings.ExpressionInstructions == PromptLibrary.DefaultExpression))
                    Settings.ExpressionInstructions = prompt;
            }
            catch (Exception e) { Log.Warning("[Rim AI Expressive Portraits] Could not load Prompts/ExpressionPrompt.md: " + e.Message); }
        }

        /// <summary>Loads the provider-specific default Base prompt used by Gemini.</summary>
        private static void ApplyGeminiImagePromptDefault(ModContentPack content)
        {
            try
            {
                string path = Path.Combine(content.RootDir, "Prompts", "BasePrompt(Gemini).md");
                if (!File.Exists(path)) return;
                string imagePrompt = File.ReadAllText(path).Trim();
                if (!imagePrompt.NullOrEmpty() && Settings.GeminiBaseInstructions.NullOrEmpty())
                    Settings.GeminiBaseInstructions = imagePrompt;
            }
            catch (Exception e) { Log.Warning("[Rim AI Expressive Portraits] Could not load Prompts/BasePrompt(Gemini).md: " + e.Message); }
        }

        private static void ApplyNovelAiDefaults(ModContentPack content)
        {
            try
            {
                string promptPath = Path.Combine(content.RootDir, "Prompts", "BasePrompt(NAI).md");
                const string previousView = "Show the character as a bust portrait from a nearly frontal view with only a gentle turn.";
                const string currentView = "Show the character as a bust portrait from a three-quarter view with a gentle head and shoulder turn.";
                string currentOptions = ProviderOptionDefaults.NovelAiBase;
                string previousOptions = currentOptions.Replace("three-quarter view, gentle head and shoulder turn", "nearly frontal view");
                if (OptionsMatch(Settings.NovelAiOptions, previousOptions))
                    Settings.NovelAiOptions = currentOptions;
                if (File.Exists(promptPath))
                {
                    string currentPrompt = File.ReadAllText(promptPath).Trim();
                    string previousPrompt = currentPrompt.Replace(currentView, previousView);
                    if (Settings.NovelAiBaseInstructions == previousPrompt)
                        Settings.NovelAiBaseInstructions = currentPrompt;
                    else if (Settings.NovelAiBaseInstructions.NullOrEmpty())
                    {
                        string sharedDefaultPath = Path.Combine(content.RootDir, "Prompts", "BasePrompt.md");
                        string sharedDefault = File.Exists(sharedDefaultPath) ? File.ReadAllText(sharedDefaultPath).Trim() : PromptLibrary.DefaultBase;
                        bool customLegacyPrompt = Settings.Provider == AiProvider.NovelAI
                            && !Settings.BaseInstructions.NullOrEmpty()
                            && Settings.BaseInstructions != PromptLibrary.DefaultBase
                            && Settings.BaseInstructions != sharedDefault;
                        Settings.NovelAiBaseInstructions = customLegacyPrompt
                            ? Settings.BaseInstructions
                            : currentPrompt;
                    }
                }
            }
            catch (Exception e) { Log.Warning("[Rim AI Expressive Portraits] Could not load NovelAI defaults: " + e.Message); }
        }

        private static bool OptionsMatch(string actual, string expected)
        {
            Dictionary<string, string> actualOptions = PortraitProviderUtilities.ParseOptions(actual);
            Dictionary<string, string> expectedOptions = PortraitProviderUtilities.ParseOptions(expected);
            return actualOptions.Count == expectedOptions.Count
                && expectedOptions.All(option => actualOptions.ContainsKey(option.Key)
                    && actualOptions[option.Key] == option.Value);
        }

        /// <summary>
        /// Provides additional behavior and compatibility details.
        /// Provides additional behavior and compatibility details.
        /// </summary>
        private static void ApplyEmotionPromptDefaults(ModContentPack content)
        {
            foreach (PortraitState state in Enum.GetValues(typeof(PortraitState)))
            {
                try
                {
                    string path = Path.Combine(content.RootDir, "Prompts", state + ".md");
                    if (!File.Exists(path)) continue;
                    string prompt = StripMarkdownHeading(File.ReadAllText(path));
                    if (prompt.NullOrEmpty()) continue;
                    string compiledDefault = PromptLibrary.State[state];
                    if (GetStatePrompt(state) == compiledDefault) SetStatePrompt(state, prompt);
                    PromptLibrary.State[state] = prompt;
                }
                catch (Exception e) { Log.Warning("[Rim AI Expressive Portraits] Could not load " + state + " prompt: " + e.Message); }
            }
        }

        /// <summary>Describes the behavior of this type or member.</summary>
        private static string StripMarkdownHeading(string value)
        {
            string[] lines = (value ?? "").Replace("\r", "").Split('\n');
            int start = 0;
            while (start < lines.Length && string.IsNullOrWhiteSpace(lines[start])) start++;
            if (start < lines.Length && lines[start].TrimStart().StartsWith("#")) start++;
            while (start < lines.Length && string.IsNullOrWhiteSpace(lines[start])) start++;
            return string.Join("\n", lines, start, lines.Length - start).Trim();
        }

        /// <summary>Describes the behavior of this type or member.</summary>
        private static string GetStatePrompt(PortraitState state) => PortraitPrompt.StateOverride(Settings, state);
        /// <summary>Describes the behavior of this type or member.</summary>
        private static void SetStatePrompt(PortraitState state, string value)
        {
            if (state == PortraitState.Normal) Settings.NormalPrompt = value; else if (state == PortraitState.High) Settings.HighPrompt = value;
            else if (state == PortraitState.Low) Settings.LowPrompt = value; else if (state == PortraitState.Sleep) Settings.SleepPrompt = value;
            else if (state == PortraitState.Pain) Settings.PainPrompt = value; else if (state == PortraitState.Down) Settings.DownPrompt = value;
            else if (state == PortraitState.Hot) Settings.HotPrompt = value; else if (state == PortraitState.Cold) Settings.ColdPrompt = value;
            else if (state == PortraitState.Angry) Settings.AngryPrompt = value; else Settings.DistressedPrompt = value;
        }

    }
}
