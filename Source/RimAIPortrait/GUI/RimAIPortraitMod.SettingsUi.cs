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
        private readonly PromptSettingsUi promptSettingsUi = new PromptSettingsUi();

        /// <summary>Describes the behavior of this type or member.</summary>
        /// <param name="inRect">The inRect value.</param>
        public override void DoSettingsWindowContents(Rect inRect)
        {
            Rect view = new Rect(0, 0, inRect.width - 18f, 4400f);
            Widgets.BeginScrollView(inRect, ref scroll, view);
            var l = new Listing_Standard(); l.Begin(view);
            ProviderSettingsUi.Draw(l, Settings, false, message => status = message);
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
            promptSettingsUi.Draw(l, Settings, WriteSettings, message => status = message);
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
            l.GapLine();
            l.CheckboxLabeled(Localization.T("RimAIPortrait.Settings.DebugMode"), ref Settings.DebugMode,
                Localization.T("RimAIPortrait.Settings.DebugModeTip"));
            if (!status.NullOrEmpty()) l.Label(status);
            l.GapLine();
            if (l.ButtonText(Localization.T("RimAIPortrait.Settings.RestoreDefaults")))
            {
                Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation(Localization.T("RimAIPortrait.Settings.ConfirmRestoreDefaults"), RestoreDefaults));
            }
            l.Gap();
            if (l.ButtonText(Localization.T("RimAIPortrait.Settings.ResetPortraitHelp")))
            {
                Settings.PortraitHelpShown = false;
                Settings.PortraitHelpAfterImageShown = false;
                WriteSettings();
                status = Localization.T("RimAIPortrait.Settings.PortraitHelpReset");
            }
            l.End(); Widgets.EndScrollView();
        }

        private void RestoreDefaults()
        {
            Settings.ResetToDefaults();
            ApplyPromptDefaults(ModPack);
            WriteSettings();
            status = Localization.T("RimAIPortrait.Settings.DefaultsRestored");
        }

        /// <summary>Describes the behavior of this type or member.</summary>
        private static void Text(Listing_Standard l, string label, ref string value) { l.Label(label); value = l.TextEntry(value ?? ""); }
    }
}
