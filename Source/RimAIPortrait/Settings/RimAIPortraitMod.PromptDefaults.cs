using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Verse;

namespace RimAIPortrait
{
    public sealed partial class RimAIPortraitMod
    {
        private static readonly Dictionary<string, string> packagedPrompts = new Dictionary<string, string>();

        private static void ApplyPromptDefaults(ModContentPack content)
        {
            if (Settings.TagExtractorBasePrompt.NullOrEmpty())
                Settings.TagExtractorBasePrompt = ReadCommonPrompt(content, "TagExtractor.md");
            if (Settings.TagExtractorInpaintPrompt.NullOrEmpty())
                Settings.TagExtractorInpaintPrompt = ReadCommonPrompt(content, "TagExtractorInpaint.md");
            var previousStateDefaults = new Dictionary<PortraitState, string>(PromptLibrary.State);
            string previousBase = Settings.BaseInstructions;
            string openAiBase = ReadPrompt(content, AiProvider.OpenAI, "BasePrompt.md", false);
            if (!openAiBase.NullOrEmpty()
                && (Settings.BaseInstructions.NullOrEmpty() || Settings.BaseInstructions == PromptLibrary.DefaultBase))
                Settings.BaseInstructions = openAiBase;

            string localBase = ReadPrompt(content, AiProvider.LocalOpenAI, "BasePrompt.md", false);
            if (Settings.LocalOpenAiBaseInstructions.NullOrEmpty())
                Settings.LocalOpenAiBaseInstructions = !previousBase.NullOrEmpty()
                    && previousBase != PromptLibrary.DefaultBase && previousBase != openAiBase
                    ? previousBase : localBase;

            string googleBase = ReadPrompt(content, AiProvider.Google, "BasePrompt.md", false);
            if (Settings.GeminiBaseInstructions.NullOrEmpty() && !googleBase.NullOrEmpty())
                Settings.GeminiBaseInstructions = googleBase;

            string comfyBase = ReadPrompt(content, AiProvider.LocalComfyUI, "BasePrompt.md", false);
            if (Settings.ComfyBaseInstructions.NullOrEmpty())
                Settings.ComfyBaseInstructions = comfyBase.NullOrEmpty() ? Settings.BaseInstructions : comfyBase;

            string novelAiBase = ReadPrompt(content, AiProvider.NovelAI, "BasePrompt.md", false);
            if (Settings.NovelAiBaseInstructions.NullOrEmpty() && !novelAiBase.NullOrEmpty())
            {
                bool customLegacyPrompt = Settings.Provider == AiProvider.NovelAI
                    && !previousBase.NullOrEmpty()
                    && previousBase != PromptLibrary.DefaultBase
                    && previousBase != openAiBase;
                Settings.NovelAiBaseInstructions = customLegacyPrompt ? previousBase : novelAiBase;
            }

            string currentOptions = ProviderOptionDefaults.NovelAiBase;
            string previousOptions = currentOptions.Replace("three-quarter view, gentle head and shoulder turn", "nearly frontal view");
            if (OptionsMatch(Settings.NovelAiOptions, previousOptions))
                Settings.NovelAiOptions = currentOptions;

            string openAiExpression = ReadPrompt(content, AiProvider.OpenAI, "ExpressionPrompt.md", false);
            if (Settings.LocalOpenAiExpressionInstructions.NullOrEmpty()
                && !Settings.ExpressionInstructions.NullOrEmpty()
                && Settings.ExpressionInstructions != PromptLibrary.DefaultExpression
                && Settings.ExpressionInstructions != openAiExpression)
                Settings.LocalOpenAiExpressionInstructions = Settings.ExpressionInstructions;
            foreach (AiProvider provider in Enum.GetValues(typeof(AiProvider)))
            {
                string expression = ReadPrompt(content, provider, "ExpressionPrompt.md", false);
                if (provider == AiProvider.OpenAI)
                {
                    if (!expression.NullOrEmpty() && (Settings.ExpressionInstructions.NullOrEmpty()
                        || Settings.ExpressionInstructions == PromptLibrary.DefaultExpression))
                        Settings.ExpressionInstructions = expression;
                }
                else if (!expression.NullOrEmpty()
                    && (Settings.GetExpressionInstructions(provider).NullOrEmpty()
                        || (Settings.ProviderPromptMigrationPending
                            && Settings.GetExpressionInstructions(provider) == openAiExpression)))
                {
                    if (provider == AiProvider.Google) Settings.GoogleExpressionInstructions = expression;
                    else if (provider == AiProvider.LocalComfyUI) Settings.ComfyExpressionInstructions = expression;
                    else if (provider == AiProvider.LocalOpenAI) Settings.LocalOpenAiExpressionInstructions = expression;
                    else Settings.NovelAiExpressionInstructions = expression;
                }

                foreach (PortraitState state in Enum.GetValues(typeof(PortraitState)))
                {
                    if (state == PortraitState.Base) continue;
                    string prompt = ReadPrompt(content, provider, state + ".md", true);
                    if (prompt.NullOrEmpty()) continue;
                    string current = Settings.GetStatePrompt(provider, state);
                    if (provider == AiProvider.LocalOpenAI && current.NullOrEmpty())
                    {
                        string sharedPrompt = Settings.GetStatePrompt(AiProvider.OpenAI, state);
                        string openAiState = ReadPrompt(content, AiProvider.OpenAI, state + ".md", true);
                        if (!sharedPrompt.NullOrEmpty() && sharedPrompt != openAiState
                            && sharedPrompt != previousStateDefaults[state])
                        {
                            current = sharedPrompt;
                            Settings.SetStatePrompt(provider, state, current);
                        }
                    }
                    if (current.NullOrEmpty() || current == previousStateDefaults[state]
                        || (Settings.ProviderPromptMigrationPending && provider != AiProvider.OpenAI
                            && current == PromptLibrary.State[state]))
                        Settings.SetStatePrompt(provider, state, prompt);
                    if (provider == AiProvider.OpenAI) PromptLibrary.State[state] = prompt;
                }
            }
            Settings.ProviderPromptMigrationPending = false;
        }

        private static string ReadCommonPrompt(ModContentPack content, string name)
        {
            string relative = Path.Combine("Prompts", "Common", name);
            try
            {
                return File.ReadAllText(Path.Combine(content.RootDir, relative)).Trim();
            }
            catch (Exception e)
            {
                Log.Warning("[Rim AI Expressive Portraits] Could not load " + relative + ": " + e.Message);
                return "";
            }
        }

        internal static string PackagedPrompt(AiProvider provider, string name, bool stripHeading)
        {
            string key = provider + "/" + name + "/" + stripHeading;
            string value;
            if (!packagedPrompts.TryGetValue(key, out value))
            {
                value = ReadPrompt(ModPack, provider, name, stripHeading);
                packagedPrompts[key] = value;
            }
            return value;
        }

        private static string ReadPrompt(ModContentPack content, AiProvider provider, string name, bool stripHeading)
        {
            string relative = Path.Combine("Prompts", PromptLibrary.ProviderFolder(provider), name);
            try
            {
                string path = Path.Combine(content.RootDir, relative);
                if (!File.Exists(path)) return null;
                string value = File.ReadAllText(path);
                return stripHeading ? StripMarkdownHeading(value) : value.Trim();
            }
            catch (Exception e)
            {
                Log.Warning("[Rim AI Expressive Portraits] Could not load " + relative + ": " + e.Message);
                return null;
            }
        }

        private static string StripMarkdownHeading(string value)
        {
            string[] lines = (value ?? "").Replace("\r", "").Split('\n');
            int start = 0;
            while (start < lines.Length && string.IsNullOrWhiteSpace(lines[start])) start++;
            if (start < lines.Length && lines[start].TrimStart().StartsWith("#")) start++;
            while (start < lines.Length && string.IsNullOrWhiteSpace(lines[start])) start++;
            return string.Join("\n", lines, start, lines.Length - start).Trim();
        }

        private static bool OptionsMatch(string actual, string expected)
        {
            Dictionary<string, string> actualOptions = PortraitProviderUtilities.ParseOptions(actual);
            Dictionary<string, string> expectedOptions = PortraitProviderUtilities.ParseOptions(expected);
            return actualOptions.Count == expectedOptions.Count
                && expectedOptions.All(option => actualOptions.ContainsKey(option.Key)
                    && actualOptions[option.Key] == option.Value);
        }
    }
}
