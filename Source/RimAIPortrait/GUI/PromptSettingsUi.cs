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
        private AiProvider editedProvider;
        private bool providerSelected;

        public void Draw(Listing_Standard listing, PortraitSettings settings, Action saveSettings, Action<string> report)
        {
            listing.GapLine();
            listing.Label(Localization.T("RimAIPortrait.Settings.PromptInformation"));
            Check(listing, Localization.T("RimAIPortrait.Settings.CharacterSprite"), ref settings.IncludeSprite);
            Check(listing, Localization.T("RimAIPortrait.Settings.Gender"), ref settings.IncludeGender);
            Check(listing, Localization.T("RimAIPortrait.Settings.Race"), ref settings.IncludeRace);
            Check(listing, Localization.T("RimAIPortrait.Settings.Xenotype"), ref settings.IncludeXenotype);
            Check(listing, Localization.T("RimAIPortrait.Settings.Age"), ref settings.IncludeAge);
            Check(listing, Localization.T("RimAIPortrait.Settings.BodyType"), ref settings.IncludeBody);
            Check(listing, Localization.T("RimAIPortrait.Settings.SkinColor"), ref settings.IncludeSkin);
            Check(listing, Localization.T("RimAIPortrait.Settings.Hair"), ref settings.IncludeHair);
            Check(listing, Localization.T("RimAIPortrait.Settings.EyeColor"), ref settings.IncludeEyes);
            Check(listing, Localization.T("RimAIPortrait.Settings.Apparel"), ref settings.IncludeApparel);
            Check(listing, Localization.T("RimAIPortrait.Settings.Health"), ref settings.IncludeHealth);
            Check(listing, Localization.T("RimAIPortrait.Settings.Traits"), ref settings.IncludeTraits);
            DrawEmotionSettings(listing, settings, saveSettings, report);
        }

        private void DrawEmotionSettings(Listing_Standard listing, PortraitSettings settings, Action saveSettings, Action<string> report)
        {
            listing.GapLine();
            listing.CheckboxLabeled(Localization.T("RimAIPortrait.Settings.EmotionChanges"), ref settings.EnableEmotionChanges,
                Localization.T("RimAIPortrait.Settings.EmotionChangesTip"));
            if (!settings.EnableEmotionChanges) return;

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
            listing.Gap();
            ProviderSettingsUi.Draw(listing, settings, true, report);
            listing.GapLine();
            DrawEditor(listing, settings, saveSettings, report);
            listing.Label(Localization.T("RimAIPortrait.Settings.SwapInterval", settings.SwapSeconds.ToString("0.0")));
            settings.SwapSeconds = listing.Slider(settings.SwapSeconds, .5f, 30f);
            listing.Label(Localization.T("RimAIPortrait.Settings.WhenLow", settings.LowMoodThreshold.ToString("P0")));
            settings.LowMoodThreshold = listing.Slider(settings.LowMoodThreshold, 0f, settings.HighMoodThreshold);
            listing.Label(Localization.T("RimAIPortrait.Settings.WhenHigh", settings.HighMoodThreshold.ToString("P0")));
            settings.HighMoodThreshold = listing.Slider(settings.HighMoodThreshold, settings.LowMoodThreshold, 1f);
        }

        private void DrawEditor(Listing_Standard listing, PortraitSettings settings, Action saveSettings, Action<string> report)
        {
            if (!providerSelected)
            {
                editedProvider = settings.ExpressionProvider;
                providerSelected = true;
            }
            if (!settings.IsStateEnabled(editedState)) editedState = PortraitState.Normal;
            listing.Label(Localization.T("RimAIPortrait.Settings.EmotionPrompts"));
            if (listing.ButtonText(Localization.T("RimAIPortrait.Settings.ExpressionProvider") + ": "
                + ProviderSettingsUi.Label(editedProvider)))
            {
                var providers = new List<FloatMenuOption>();
                foreach (AiProvider provider in Enum.GetValues(typeof(AiProvider)))
                {
                    AiProvider selected = provider;
                    providers.Add(new FloatMenuOption(ProviderSettingsUi.Label(selected), () => editedProvider = selected));
                }
                Find.WindowStack.Add(new FloatMenu(providers));
            }
            Rect row = listing.GetRect(32f);
            if (Widgets.ButtonText(new Rect(row.x, row.y, row.width - 110f, row.height), PortraitStateNames.Label(editedState) + " ▼"))
            {
                var options = new List<FloatMenuOption>();
                foreach (PortraitState state in Enum.GetValues(typeof(PortraitState)))
                {
                    if (!settings.IsStateEnabled(state)) continue;
                    PortraitState captured = state;
                    options.Add(new FloatMenuOption(PortraitStateNames.Label(captured), () => editedState = captured));
                }
                Find.WindowStack.Add(new FloatMenu(options));
            }
            if (Widgets.ButtonText(new Rect(row.xMax - 100f, row.y, 100f, row.height), Localization.T("RimAIPortrait.Common.Save")))
            {
                saveSettings();
                report(Localization.T("RimAIPortrait.Settings.PromptSaved",
                    ProviderSettingsUi.Label(editedProvider) + " / " + PortraitStateNames.Label(editedState)));
            }
            string prompt = PortraitPrompt.StateOverride(settings, editedProvider, editedState);
            settings.SetStatePrompt(editedProvider, editedState, Widgets.TextArea(listing.GetRect(100f), prompt ?? ""));
        }

        private static void Check(Listing_Standard listing, string label, ref bool value)
        {
            listing.CheckboxLabeled(label, ref value);
        }
    }
}
