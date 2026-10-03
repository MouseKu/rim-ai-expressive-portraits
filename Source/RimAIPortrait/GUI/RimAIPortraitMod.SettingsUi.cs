using System;
using System.Diagnostics;
using System.IO;
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
        private readonly PromptSettingsUi promptSettingsUi = new PromptSettingsUi();

        /// <summary>Describes the behavior of this type or member.</summary>
        /// <param name="inRect">The inRect value.</param>
        public override void DoSettingsWindowContents(Rect inRect)
        {
            Rect view = new Rect(0, 0, inRect.width - 18f, 4400f);
            Widgets.BeginScrollView(inRect, ref scroll, view);
            var l = new Listing_Standard(); l.Begin(view);
            ProviderSettingsUi.Draw(l, Settings, false, message => status = message);
            promptSettingsUi.DrawMain(l, Settings, message => status = message);
            l.GapLine();
            ProviderSettingsUi.DrawRegenerate(l, Settings, message => status = message);
            l.GapLine();
            l.CheckboxLabeled(Localization.T("RimAIPortrait.TagExtractor.Enabled"), ref Settings.EnableTagExtractor);
            if (Settings.EnableTagExtractor)
            {
                l.Label(Localization.T("RimAIPortrait.TagExtractor.Scope"));
                if (!TagExtraction.IsConfigured(Settings))
                    l.Label(Localization.T("RimAIPortrait.TagExtractor.SetupWarning"));
                if (l.ButtonText(Localization.T("RimAIPortrait.TagExtractor.Model") + ": "
                    + Localization.T("RimAIPortrait.TagExtractor.Model." + Settings.TagExtractorBackend)))
                    Settings.TagExtractorBackend = (TagExtractorModel)(((int)Settings.TagExtractorBackend + 1) % 3);
                if (Settings.TagExtractorBackend == TagExtractorModel.OpenAI)
                {
                    Text(l, Localization.T("RimAIPortrait.Settings.ApiKey"), ref Settings.TagExtractorOpenAiKey);
                    Text(l, Localization.T("RimAIPortrait.Settings.Model"), ref Settings.TagExtractorOpenAiModel);
                }
                else if (Settings.TagExtractorBackend == TagExtractorModel.Google)
                {
                    Text(l, Localization.T("RimAIPortrait.Settings.ApiKey"), ref Settings.TagExtractorGoogleKey);
                    Text(l, Localization.T("RimAIPortrait.Settings.Model"), ref Settings.TagExtractorGoogleModel);
                }
                else
                {
                    Text(l, Localization.T("RimAIPortrait.TagExtractor.LocalUrl"), ref Settings.TagExtractorLocalUrl);
                    Text(l, Localization.T("RimAIPortrait.Settings.Model"), ref Settings.TagExtractorLocalModel);
                    Text(l, Localization.T("RimAIPortrait.Settings.ApiKey"), ref Settings.TagExtractorLocalKey);
                }
            }
            l.GapLine();
            promptSettingsUi.DrawDetails(l, Settings, WriteSettings, message => status = message);
            l.GapLine();
            Text(l, Localization.T("RimAIPortrait.Settings.PortraitRootFolder"), ref Settings.PortraitRootFolder);
            PortraitData.RecheckRoot();
            string portraitRoot;
            if (string.IsNullOrWhiteSpace(Settings.PortraitRootFolder))
                l.Label(Localization.T("RimAIPortrait.Settings.PortraitRootDefault", PortraitData.DefaultRoot));
            else if (PortraitData.TryGetCustomRoot(Settings.PortraitRootFolder, out portraitRoot))
                l.Label(Localization.T("RimAIPortrait.Settings.PortraitRootConfigured"));
            else
                l.Label(Localization.T("RimAIPortrait.Settings.PortraitRootInvalid"));
            if (l.ButtonText(Localization.T("RimAIPortrait.Settings.OpenPortraitRoot")))
            {
                try
                {
                    string folder;
                    if (string.IsNullOrWhiteSpace(Settings.PortraitRootFolder)) folder = PortraitData.DefaultRoot;
                    else if (!PortraitData.TryNormalizeCustomRoot(Settings.PortraitRootFolder, out folder))
                        throw new InvalidOperationException(Localization.T("RimAIPortrait.Settings.PortraitRootInvalid"));
                    Directory.CreateDirectory(folder);
                    PortraitData.RecheckRoot();
                    Process.Start(folder);
                }
                catch (Exception e) { status = Localization.T("RimAIPortrait.Error.OpenPortraitRoot", e.Message); }
            }
            l.GapLine(); l.Label(Localization.T("RimAIPortrait.Settings.DisplayBehavior"));
            l.Label(Localization.T("RimAIPortrait.Settings.Width", Settings.Width.ToString("0"))); Settings.Width = l.Slider(Settings.Width, 64f, 600f);
            l.Label(Localization.T("RimAIPortrait.Settings.Scale", Settings.Scale.ToString("0.00"))); Settings.Scale = l.Slider(Settings.Scale, .25f, 3f);
            DrawOffset(l, Localization.T("RimAIPortrait.Settings.RightOffset", Settings.OffsetX.ToString("0")),
                ref Settings.OffsetX, ref offsetXBuffer, Mathf.Max(500f, UI.screenWidth));
            DrawOffset(l, Localization.T("RimAIPortrait.Settings.UpOffset", Settings.OffsetY.ToString("0")),
                ref Settings.OffsetY, ref offsetYBuffer, Mathf.Max(500f, UI.screenHeight));
            l.GapLine();
            if (l.ButtonText(Localization.T("RimAIPortrait.Settings.RestoreDefaults")))
                Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation(
                    Localization.T("RimAIPortrait.Settings.ConfirmRestoreDefaults"), RestoreDefaults));
            l.Gap();
            if (l.ButtonText(Localization.T("RimAIPortrait.Settings.Advanced")))
                Find.WindowStack.Add(new Dialog_AdvancedSettings(this));
            if (!status.NullOrEmpty()) l.Label(status);
            l.End(); Widgets.EndScrollView();
        }

        private void RestoreDefaults()
        {
            Settings.ResetToDefaults();
            offsetXBuffer = null;
            offsetYBuffer = null;
            ApplyPromptDefaults(ModPack);
            WriteSettings();
            status = Localization.T("RimAIPortrait.Settings.DefaultsRestored");
        }

        /// <summary>Describes the behavior of this type or member.</summary>
        private static void Text(Listing_Standard l, string label, ref string value) { l.Label(label); value = l.TextEntry(value ?? ""); }

        private static void DrawOffset(Listing_Standard listing, string label, ref float value, ref string buffer, float maximum)
        {
            Rect row = listing.GetRect(30f);
            Widgets.Label(new Rect(row.x, row.y, row.width - 100f, row.height), label);
            Widgets.TextFieldNumeric<float>(new Rect(row.xMax - 90f, row.y, 90f, row.height),
                ref value, ref buffer, -300f, maximum);
            float sliderValue = listing.Slider(value, -300f, maximum);
            if (!Mathf.Approximately(sliderValue, value))
            {
                value = sliderValue;
                buffer = value.ToString("0");
            }
        }
    }
}
