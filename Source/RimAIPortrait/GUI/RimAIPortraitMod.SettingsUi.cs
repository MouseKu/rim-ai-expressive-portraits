using System;
using UnityEngine;
using Verse;

namespace RimAIPortrait
{
    public sealed partial class RimAIPortraitMod
    {
        private Vector2 scroll;
        private string status = "";
        private string offsetXBuffer;
        private string offsetYBuffer;
        private bool baseProviderExpanded;
        private bool expressionProviderExpanded;
        private bool regenerateProviderExpanded;
        private bool tagExtractorExpanded;
        private readonly PromptSettingsUi promptSettingsUi = new PromptSettingsUi();

        /// <summary>Describes the behavior of this type or member.</summary>
        /// <param name="inRect">The inRect value.</param>
        public override void DoSettingsWindowContents(Rect inRect)
        {
            // Listing_Standard starts another column when its view height is exceeded.
            // Keep the full settings list in one scrollable column, including expanded sections.
            Rect view = new Rect(0, 0, inRect.width - 18f, 6000f);
            Widgets.BeginScrollView(inRect, ref scroll, view);
            var l = new Listing_Standard(); l.Begin(view);
            if (l.ButtonText(Localization.T("RimAIPortrait.Settings.BaseProvider") + ": "
                + ProviderSettingsUi.Label(Settings.Provider) + (baseProviderExpanded ? " [-]" : " [+]")))
                baseProviderExpanded = !baseProviderExpanded;
            if (baseProviderExpanded)
            {
                ProviderSettingsUi.Draw(l, Settings, false, message => status = message);
                l.GapLine();
                promptSettingsUi.DrawPromptInformation(l, Settings);
            }
            l.GapLine();
            if (l.ButtonText(Localization.T("RimAIPortrait.Settings.ExpressionProvider") + ": "
                + ProviderSettingsUi.Label(Settings.ExpressionProvider) + (expressionProviderExpanded ? " [-]" : " [+]")))
                expressionProviderExpanded = !expressionProviderExpanded;
            if (expressionProviderExpanded)
                promptSettingsUi.DrawExpressionSettings(l, Settings, WriteSettings, message => status = message);
            l.GapLine();
            if (l.ButtonText(Localization.T("RimAIPortrait.Settings.RegenerateProvider") + ": "
                + ProviderSettingsUi.Label(Settings.RegenerateProvider) + (regenerateProviderExpanded ? " [-]" : " [+]")))
                regenerateProviderExpanded = !regenerateProviderExpanded;
            if (regenerateProviderExpanded)
                ProviderSettingsUi.DrawRegenerate(l, Settings, message => status = message);
            l.GapLine();
            if (l.ButtonText(Localization.T("RimAIPortrait.TagExtractor.Model") + ": "
                + Localization.T("RimAIPortrait.TagExtractor.Model." + Settings.TagExtractorBackend)
                + (tagExtractorExpanded ? " [-]" : " [+]")))
                tagExtractorExpanded = !tagExtractorExpanded;
            if (tagExtractorExpanded)
            {
                l.Label(Localization.T("RimAIPortrait.TagExtractor.Scope"));
                if (l.ButtonText(Localization.T("RimAIPortrait.TagExtractor.Model") + ": "
                    + Localization.T("RimAIPortrait.TagExtractor.Model." + Settings.TagExtractorBackend)))
                    Settings.TagExtractorBackend = (TagExtractorModel)(((int)Settings.TagExtractorBackend + 1) % 3);
                if (Settings.TagExtractorBackend == TagExtractorModel.OpenAI)
                {
                    Text(l, Localization.T("RimAIPortrait.Settings.ApiKey"), ref Settings.TagExtractorOpenAiKey);
                    Text(l, Localization.T("RimAIPortrait.Settings.Model"), ref Settings.TagExtractorOpenAiModel);
                    Text(l, Localization.T("RimAIPortrait.Settings.OptionsJson"), ref Settings.TagExtractorOpenAiOptions);
                }
                else if (Settings.TagExtractorBackend == TagExtractorModel.Google)
                {
                    Text(l, Localization.T("RimAIPortrait.Settings.ApiKey"), ref Settings.TagExtractorGoogleKey);
                    Text(l, Localization.T("RimAIPortrait.Settings.Model"), ref Settings.TagExtractorGoogleModel);
                    Text(l, Localization.T("RimAIPortrait.Settings.OptionsJson"), ref Settings.TagExtractorGoogleOptions);
                }
                else
                {
                    Text(l, Localization.T("RimAIPortrait.TagExtractor.LocalUrl"), ref Settings.TagExtractorLocalUrl);
                    Text(l, Localization.T("RimAIPortrait.Settings.Model"), ref Settings.TagExtractorLocalModel);
                    Text(l, Localization.T("RimAIPortrait.Settings.OptionsJson"), ref Settings.TagExtractorLocalOptions);
                    Text(l, Localization.T("RimAIPortrait.Settings.ApiKey"), ref Settings.TagExtractorLocalKey);
                }
            }
            l.GapLine();
            l.GapLine(); l.Label(Localization.T("RimAIPortrait.Settings.DisplayBehavior"));
            Settings.Width = SettingsTooltips.Slider(l,
                Localization.T("RimAIPortrait.Settings.Width", Settings.Width.ToString("0")),
                Settings.Width, 64f, 600f, "RimAIPortrait.Settings.WidthTip");
            Settings.Scale = SettingsTooltips.Slider(l,
                Localization.T("RimAIPortrait.Settings.Scale", Settings.Scale.ToString("0.00")),
                Settings.Scale, .25f, 3f, "RimAIPortrait.Settings.ScaleTip");
            DrawOffset(l, Localization.T("RimAIPortrait.Settings.RightOffset", Settings.OffsetX.ToString("0")),
                ref Settings.OffsetX, ref offsetXBuffer, Mathf.Max(500f, UI.screenWidth),
                "RimAIPortrait.Settings.RightOffsetTip");
            DrawOffset(l, Localization.T("RimAIPortrait.Settings.UpOffset", Settings.OffsetY.ToString("0")),
                ref Settings.OffsetY, ref offsetYBuffer, Mathf.Max(500f, UI.screenHeight),
                "RimAIPortrait.Settings.UpOffsetTip");
            l.GapLine();
            if (l.ButtonText(Localization.T("RimAIPortrait.PromptArchive.Open")))
                Find.WindowStack.Add(new Dialog_PromptArchive(Settings, WriteSettings));
            if (l.ButtonText(Localization.T("RimAIPortrait.Settings.Advanced")))
                Find.WindowStack.Add(new Dialog_AdvancedSettings(this));
            if (l.ButtonText(Localization.T("RimAIPortrait.Settings.RestoreDefaults")))
                Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation(
                    Localization.T("RimAIPortrait.Settings.ConfirmRestoreDefaults"), RestoreDefaults));
            if (!status.NullOrEmpty()) l.Label(status);
            l.End(); Widgets.EndScrollView();
        }

        private void RestoreDefaults()
        {
            Settings.ResetToDefaults();
            offsetXBuffer = null;
            offsetYBuffer = null;
            XenotypePromptDefaults.Load(ModPack);
            XenotypePromptDefaults.Apply(Settings);
            ApplyPromptDefaults(ModPack);
            WriteSettings();
            status = Localization.T("RimAIPortrait.Settings.DefaultsRestored");
        }

        /// <summary>Describes the behavior of this type or member.</summary>
        private static void Text(Listing_Standard l, string label, ref string value)
        {
            SettingsTooltips.Text(l, label, ref value);
        }

        private static void DrawOffset(Listing_Standard listing, string label, ref float value,
            ref string buffer, float maximum, string tipKey)
        {
            Rect row = listing.GetRect(30f);
            Widgets.Label(new Rect(row.x, row.y, row.width - 100f, row.height), label);
            Widgets.TextFieldNumeric<float>(new Rect(row.xMax - 90f, row.y, 90f, row.height),
                ref value, ref buffer, -300f, maximum);
            TooltipHandler.TipRegion(row, Localization.T(tipKey));
            Rect slider = listing.GetRect(22f);
            float sliderValue = Widgets.HorizontalSlider(slider, value, -300f, maximum);
            TooltipHandler.TipRegion(slider, Localization.T(tipKey));
            if (!Mathf.Approximately(sliderValue, value))
            {
                value = sliderValue;
                buffer = value.ToString("0");
            }
        }
    }
}
