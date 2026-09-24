// Contains the implementation for RimAIPortraitMod.

using System;
using System.Diagnostics;
using System.IO;
using HarmonyLib;
using UnityEngine;
using Verse;

namespace RimAIPortrait
{
    /// <summary>Describes the behavior of this type or member.</summary>
    public sealed class RimAIPortraitMod : Mod
    {
        public static PortraitSettings Settings;
        public static ModContentPack ModPack;
        private static RimAIPortraitMod instance;
        private Vector2 scroll;
        private string status = "";
        private readonly PromptSettingsUi promptSettingsUi = new PromptSettingsUi();

        /// <summary>Describes the behavior of this type or member.</summary>
        /// <param name="content">The content value.</param>
        public RimAIPortraitMod(ModContentPack content) : base(content)
        {
            instance = this;
            Settings = GetSettings<PortraitSettings>();
            ModPack = content;
            ApplyImagePromptDefault(content);
            ApplyGeminiImagePromptDefault(content);
            ApplyEmotionPromptDefaults(content);
            new Harmony("mint.rimaiportrait").PatchAll();
            LongEventHandler.ExecuteWhenFinished(PortraitRuntime.EnsureCreated);
        }

        /// <summary>Describes the behavior of this type or member.</summary>
        public override string SettingsCategory() => Localization.T("RimAIPortrait.Settings.Category");

        /// <summary>Immediately persists values changed outside the main settings window.</summary>
        public static void SaveSettings()
        {
            instance?.WriteSettings();
        }

        /// <summary>Describes the behavior of this type or member.</summary>
        /// <param name="inRect">The inRect value.</param>
        public override void DoSettingsWindowContents(Rect inRect)
        {
            Rect view = new Rect(0, 0, inRect.width - 18f, 3300f);
            Widgets.BeginScrollView(inRect, ref scroll, view);
            var l = new Listing_Standard(); l.Begin(view);
            ProviderSettingsUi.Draw(l, Settings, false, message => status = message);
            promptSettingsUi.Draw(l, Settings, WriteSettings, message => status = message);
            l.GapLine(); l.Label(Localization.T("RimAIPortrait.Settings.DisplayBehavior"));
            l.Label(Localization.T("RimAIPortrait.Settings.Width", Settings.Width.ToString("0"))); Settings.Width = l.Slider(Settings.Width, 64f, 600f);
            l.Label(Localization.T("RimAIPortrait.Settings.Scale", Settings.Scale.ToString("0.00"))); Settings.Scale = l.Slider(Settings.Scale, .25f, 3f);
            l.Label(Localization.T("RimAIPortrait.Settings.RightOffset", Settings.OffsetX.ToString("0"))); Settings.OffsetX = l.Slider(Settings.OffsetX, -300f, 500f);
            l.Label(Localization.T("RimAIPortrait.Settings.UpOffset", Settings.OffsetY.ToString("0"))); Settings.OffsetY = l.Slider(Settings.OffsetY, -300f, 500f);
            l.GapLine();
            l.CheckboxLabeled(Localization.T("RimAIPortrait.Settings.EnableFrame"), ref Settings.EnableFrame,
                Localization.T("RimAIPortrait.Settings.EnableFrameTip"));
            if (Settings.EnableFrame)
            {
                l.Label(Localization.T("RimAIPortrait.Settings.FrameScale", Settings.FrameScale.ToString("0.00")));
                Settings.FrameScale = l.Slider(Settings.FrameScale, .5f, 1.5f);
                Text(l, Localization.T("RimAIPortrait.Settings.FramePath"), ref Settings.FrameImagePath);
                if (l.ButtonText(Localization.T("RimAIPortrait.Settings.OpenFrameFolder")))
                {
                    try
                    {
                        string folder = Path.GetDirectoryName(Settings.FrameImagePath);
                        if (folder.NullOrEmpty() || !Directory.Exists(folder)) throw new DirectoryNotFoundException(Localization.T("RimAIPortrait.Error.FrameFolderMissing"));
                        Process.Start(folder);
                    }
                    catch (Exception e) { status = Localization.T("RimAIPortrait.Error.OpenFrameFolder", e.Message); }
                }
            }
            if (l.ButtonText(Localization.T("RimAIPortrait.Settings.GenerateSelected")))
            {
                var pawn = Find.Selector.SingleSelectedThing as Pawn;
                if (pawn == null) status = Localization.T("RimAIPortrait.Error.SelectPawnFirst");
                else { status = Localization.T("RimAIPortrait.Status.GenerationStarted"); PortraitRuntime.Instance.Generate(pawn, s => status = s); }
            }
            l.GapLine();
            l.CheckboxLabeled(Localization.T("RimAIPortrait.Settings.DebugMode"), ref Settings.DebugMode,
                Localization.T("RimAIPortrait.Settings.DebugModeTip"));
            if (!status.NullOrEmpty()) l.Label(status);
            l.End(); Widgets.EndScrollView();
        }

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

        /// <summary>Describes the behavior of this type or member.</summary>
        private static void Text(Listing_Standard l, string label, ref string value) { l.Label(label); value = l.TextEntry(value ?? ""); }
    }
}
