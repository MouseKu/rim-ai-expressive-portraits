using System;
using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace RimAIPortrait
{
    /// <summary>Describes the behavior of this type or member.</summary>
    internal sealed class PromptSettingsUi
    {
        private PortraitState editedState = PortraitState.Normal;

        public void DrawExpressionSettings(Listing_Standard listing, PortraitSettings settings,
            Action saveSettings, Action<string> report)
        {
            ProviderSettingsUi.Draw(listing, settings, true, report);
            listing.CheckboxLabeled(Localization.T("RimAIPortrait.Settings.EmotionChanges"), ref settings.EnableEmotionChanges,
                Localization.T("RimAIPortrait.Settings.EmotionChangesTip"));
            if (settings.EnableEmotionChanges)
            {
                listing.Gap();
                DrawEmotionSettings(listing, settings, saveSettings, report);
            }
        }

        public void DrawPromptInformation(Listing_Standard listing, PortraitSettings settings)
        {
            listing.Label(Localization.T("RimAIPortrait.Settings.PromptInformation"));
            Check(listing, Localization.T("RimAIPortrait.Settings.CharacterSprite"), ref settings.IncludeSprite);
            if (settings.IncludeSprite)
            {
                Check(listing, Localization.T("RimAIPortrait.Settings.ExcludeHeadgearFromSprite"),
                    ref settings.ExcludeHeadgearFromSprite);
                if (settings.ExcludeHeadgearFromSprite && settings.IncludeApparel)
                {
                    Color previousColor = GUI.color;
                    GUI.color = new Color(.95f, .2f, .2f, 1f);
                    listing.Label(Localization.T("RimAIPortrait.Settings.HeadgearApparelWarning"));
                    GUI.color = previousColor;
                }
            }
            Check(listing, Localization.T("RimAIPortrait.Settings.Gender"), ref settings.IncludeGender);
            Check(listing, Localization.T("RimAIPortrait.Settings.Race"), ref settings.IncludeRace);
            Check(listing, Localization.T("RimAIPortrait.Settings.Xenotype"), ref settings.IncludeXenotype);
            Check(listing, Localization.T("RimAIPortrait.Settings.Gene"), ref settings.IncludeGene);
            if (settings.IncludeGene)
                Check(listing, Localization.T("RimAIPortrait.Settings.AppearanceGenesOnly"), ref settings.OnlyAppearanceGenes);
            Check(listing, Localization.T("RimAIPortrait.Settings.Age"), ref settings.IncludeAge);
            Check(listing, Localization.T("RimAIPortrait.Settings.BodyType"), ref settings.IncludeBody);
            Check(listing, Localization.T("RimAIPortrait.Settings.SkinColor"), ref settings.IncludeSkin);
            Check(listing, Localization.T("RimAIPortrait.Settings.Hair"), ref settings.IncludeHair);
            Check(listing, Localization.T("RimAIPortrait.Settings.EyeColor"), ref settings.IncludeEyes);
            Check(listing, Localization.T("RimAIPortrait.Settings.Apparel"), ref settings.IncludeApparel);
            Check(listing, Localization.T("RimAIPortrait.Settings.Health"), ref settings.IncludeHealth);
            Check(listing, Localization.T("RimAIPortrait.Settings.Traits"), ref settings.IncludeTraits);
        }

        private void DrawEmotionSettings(Listing_Standard listing, PortraitSettings settings, Action saveSettings, Action<string> report)
        {
            listing.Label(Localization.T("RimAIPortrait.Settings.EnabledEmotions"));
            Check(listing, PortraitStateNames.Label(PortraitState.High), ref settings.EnableHigh);
            Check(listing, PortraitStateNames.Label(PortraitState.Low), ref settings.EnableLow);
            Check(listing, PortraitStateNames.Label(PortraitState.Sleep), ref settings.EnableSleep);
            Check(listing, PortraitStateNames.Label(PortraitState.Pain), ref settings.EnablePain);
            Check(listing, PortraitStateNames.Label(PortraitState.Down), ref settings.EnableDown);
            Check(listing, PortraitStateNames.Label(PortraitState.Hot), ref settings.EnableHot);
            Check(listing, PortraitStateNames.Label(PortraitState.Cold), ref settings.EnableCold);
            Check(listing, PortraitStateNames.Label(PortraitState.Angry), ref settings.EnableAngry);
            Check(listing, PortraitStateNames.Label(PortraitState.Distressed), ref settings.EnableDistressed);
            listing.GapLine();
            if (!settings.EnableDeprecatedFeatures || settings.ExpressionProvider != AiProvider.LocalComfyUI)
                DrawEditor(listing, settings, saveSettings, report);
            settings.SwapSeconds = SettingsTooltips.Slider(listing,
                Localization.T("RimAIPortrait.Settings.SwapInterval", settings.SwapSeconds.ToString("0.0")),
                settings.SwapSeconds, .5f, 30f, "RimAIPortrait.Settings.SwapIntervalTip");
            settings.LowMoodThreshold = SettingsTooltips.Slider(listing,
                Localization.T("RimAIPortrait.Settings.WhenLow", settings.LowMoodThreshold.ToString("P0")),
                settings.LowMoodThreshold, 0f, settings.HighMoodThreshold, "RimAIPortrait.Settings.WhenLowTip");
            settings.HighMoodThreshold = SettingsTooltips.Slider(listing,
                Localization.T("RimAIPortrait.Settings.WhenHigh", settings.HighMoodThreshold.ToString("P0")),
                settings.HighMoodThreshold, settings.LowMoodThreshold, 1f, "RimAIPortrait.Settings.WhenHighTip");
        }

        private void DrawEditor(Listing_Standard listing, PortraitSettings settings, Action saveSettings, Action<string> report)
        {
            AiProvider provider = settings.ExpressionProvider;
            if (!settings.IsStateEnabled(editedState)) editedState = PortraitState.Normal;
            listing.Label(Localization.T("RimAIPortrait.Settings.EmotionPrompts")
                + " (" + ProviderSettingsUi.Label(provider) + ")");
            listing.Label(Localization.T("RimAIPortrait.Settings.EmotionPromptScope"));
            Rect row = listing.GetRect(32f);
            if (Widgets.ButtonText(new Rect(row.x, row.y, row.width - 220f, row.height), PortraitStateNames.Label(editedState) + " ▼"))
            {
                var options = new List<FloatMenuOption>();
                foreach (PortraitState state in Enum.GetValues(typeof(PortraitState)))
                {
                    if (state == PortraitState.Base || !settings.IsStateEnabled(state)) continue;
                    PortraitState captured = state;
                    options.Add(new FloatMenuOption(PortraitStateNames.Label(captured), () => editedState = captured));
                }
                Find.WindowStack.Add(new FloatMenu(options));
            }
            if (Widgets.ButtonText(new Rect(row.xMax - 100f, row.y, 100f, row.height), Localization.T("RimAIPortrait.Common.Save")))
            {
                saveSettings();
                report(Localization.T("RimAIPortrait.Settings.PromptSaved",
                    ProviderSettingsUi.Label(provider) + " / " + PortraitStateNames.Label(editedState)));
            }
            if (Widgets.ButtonText(new Rect(row.xMax - 210f, row.y, 100f, row.height),
                Localization.T("RimAIPortrait.Common.Reset")))
            {
                string original = RimAIPortraitMod.PackagedPrompt(provider, editedState + ".md", true);
                if (original == null) report(Localization.T("RimAIPortrait.Settings.PromptDefaultMissing"));
                else
                {
                    settings.SetStatePrompt(provider, editedState, original);
                    saveSettings();
                    report(Localization.T("RimAIPortrait.Settings.PromptReset",
                        ProviderSettingsUi.Label(provider) + " / " + PortraitStateNames.Label(editedState)));
                }
            }
            string prompt = PortraitPrompt.StateOverride(settings, provider, editedState);
            Rect promptArea = listing.GetRect(100f);
            settings.SetStatePrompt(provider, editedState, Widgets.TextArea(promptArea, prompt ?? ""));
            TooltipHandler.TipRegion(promptArea,
                Localization.T("RimAIPortrait.Settings.EmotionPromptScope"));
        }

        private static void Check(Listing_Standard listing, string label, ref bool value)
        {
            string tip = SettingsTooltips.TipForLabel(label)
                ?? Localization.T("RimAIPortrait.Settings.EnabledEmotionTip", label);
            listing.CheckboxLabeled(label, ref value, tip);
        }
    }
}
