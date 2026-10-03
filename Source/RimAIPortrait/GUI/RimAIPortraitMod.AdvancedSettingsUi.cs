using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using UnityEngine;
using Verse;

namespace RimAIPortrait
{
    public sealed partial class RimAIPortraitMod
    {
        private AiProvider captionProvider = AiProvider.OpenAI;
        private AiProvider loadedCaptionProvider;
        private bool captionLoaded;
        private string captionEditorText;
        private bool inpaintStrengthsExpanded;

        private void DrawAdvancedSettings(Listing_Standard l)
        {
            DrawTagExtractorPrompt(l, Localization.T("RimAIPortrait.TagExtractor.BasePrompt"),
                ref Settings.TagExtractorBasePrompt, "TagExtractor.md");
            DrawTagExtractorPrompt(l, Localization.T("RimAIPortrait.TagExtractor.InpaintBatchPrompt"),
                ref Settings.TagExtractorInpaintBatchPrompt, "TagExtractorInpaintBatch.md");
            l.GapLine();
            DrawImageCaptionSettings(l);
            l.GapLine();
            if (l.ButtonText(Localization.T("RimAIPortrait.Settings.InpaintStrength")
                + (inpaintStrengthsExpanded ? " ▲" : " ▼")))
                inpaintStrengthsExpanded = !inpaintStrengthsExpanded;
            if (inpaintStrengthsExpanded)
            {
                l.Label(Localization.T("RimAIPortrait.Settings.InpaintStrengthHelp"));
                foreach (PortraitState state in Enum.GetValues(typeof(PortraitState)))
                {
                    if (state == PortraitState.Base) continue;
                    float strength = Settings.GetInpaintStrength(state);
                    l.Label(Localization.T("RimAIPortrait.Settings.InpaintStrengthState",
                        PortraitStateNames.Label(state), strength.ToString("0.00")));
                    Settings.SetInpaintStrength(state, l.Slider(strength, .1f, 1f));
                }
            }
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
            l.CheckboxLabeled(Localization.T("RimAIPortrait.Settings.ShowApiRequest"), ref Settings.ShowApiRequest,
                Localization.T("RimAIPortrait.Settings.ShowApiRequestTip"));
            l.GapLine();
            if (l.ButtonText(Localization.T("RimAIPortrait.Settings.ResetPortraitHelp")))
            {
                Settings.PortraitHelpShown = false;
                Settings.PortraitHelpAfterImageShown = false;
                Settings.PortraitHelpDismissed = false;
                Settings.PortraitHelpAfterImageDismissed = false;
                Settings.NovelAiV5HelpDismissed = false;
                WriteSettings();
                status = Localization.T("RimAIPortrait.Settings.PortraitHelpReset");
            }
            if (!status.NullOrEmpty()) l.Label(status);
        }

        private void DrawImageCaptionSettings(Listing_Standard listing)
        {
            listing.Label(Localization.T("RimAIPortrait.Settings.ImageCaptions"));
            if (listing.ButtonText(Localization.T("RimAIPortrait.Settings.ImageCaptionProvider")
                + ": " + ProviderSettingsUi.Label(captionProvider)))
            {
                var choices = new List<FloatMenuOption> {
                    new FloatMenuOption(ProviderSettingsUi.Label(AiProvider.OpenAI),
                        () => { captionProvider = AiProvider.OpenAI; captionLoaded = false; }),
                    new FloatMenuOption(ProviderSettingsUi.Label(AiProvider.Google),
                        () => { captionProvider = AiProvider.Google; captionLoaded = false; })
                };
                Find.WindowStack.Add(new FloatMenu(choices));
            }
            if (!captionLoaded || loadedCaptionProvider != captionProvider)
            {
                try
                {
                    captionEditorText = PortraitImageCaptions.EffectiveJson(captionProvider, Settings);
                    loadedCaptionProvider = captionProvider;
                    captionLoaded = true;
                }
                catch (Exception exception)
                {
                    status = Localization.T("RimAIPortrait.Settings.ImageCaptionFailed", exception.Message);
                    captionEditorText = "";
                    loadedCaptionProvider = captionProvider;
                    captionLoaded = true;
                }
            }
            captionEditorText = Widgets.TextArea(listing.GetRect(240f), captionEditorText ?? "");
            Rect row = listing.GetRect(32f);
            if (Widgets.ButtonText(new Rect(row.x, row.y, 170f, row.height),
                Localization.T("RimAIPortrait.Settings.ImageCaptionUsePackaged")))
            {
                if (captionProvider == AiProvider.OpenAI) Settings.OpenAiImageCaptionsJson = null;
                else Settings.GoogleImageCaptionsJson = null;
                captionLoaded = false;
                WriteSettings();
                status = Localization.T("RimAIPortrait.Settings.ImageCaptionPackaged");
            }
            if (Widgets.ButtonText(new Rect(row.xMax - 100f, row.y, 100f, row.height),
                Localization.T("RimAIPortrait.Common.Save")))
            {
                try
                {
                    string name = captionProvider == AiProvider.OpenAI
                        ? "open-ai-img-caption.json" : "google-ai-img-caption.json";
                    PortraitImageCaptions.Parse(captionEditorText, name);
                    if (captionProvider == AiProvider.OpenAI) Settings.OpenAiImageCaptionsJson = captionEditorText;
                    else Settings.GoogleImageCaptionsJson = captionEditorText;
                    WriteSettings();
                    status = Localization.T("RimAIPortrait.Settings.ImageCaptionSaved");
                }
                catch (Exception exception)
                {
                    status = Localization.T("RimAIPortrait.Settings.ImageCaptionFailed", exception.Message);
                }
            }
        }

        private void DrawTagExtractorPrompt(Listing_Standard listing, string label, ref string value,
            string defaultName)
        {
            listing.Label(label);
            value = Widgets.TextArea(listing.GetRect(160f), value ?? "");
            Rect row = listing.GetRect(32f);
            if (Widgets.ButtonText(new Rect(row.xMax - 210f, row.y, 100f, row.height),
                Localization.T("RimAIPortrait.Common.Reset")))
            {
                string original = ReadCommonPrompt(ModPack, defaultName);
                if (string.IsNullOrWhiteSpace(original))
                    status = Localization.T("RimAIPortrait.Settings.PromptDefaultMissing");
                else
                {
                    value = original;
                    WriteSettings();
                    status = Localization.T("RimAIPortrait.Settings.PromptReset", label);
                }
            }
            if (Widgets.ButtonText(new Rect(row.xMax - 100f, row.y, 100f, row.height),
                Localization.T("RimAIPortrait.Common.Save")))
            {
                if (string.IsNullOrWhiteSpace(value))
                    status = Localization.T("RimAIPortrait.TagExtractor.PromptMissing");
                else
                {
                    WriteSettings();
                    status = Localization.T("RimAIPortrait.Settings.PromptSaved", label);
                }
            }
        }

        private sealed class Dialog_AdvancedSettings : Window
        {
            private readonly RimAIPortraitMod owner;
            private Vector2 scroll;

            public override Vector2 InitialSize => PortraitWindowSizing.Fit(850f, 900f);

            public Dialog_AdvancedSettings(RimAIPortraitMod owner)
            {
                this.owner = owner;
                doCloseX = true;
                absorbInputAroundWindow = true;
                closeOnClickedOutside = false;
            }

            public override void DoWindowContents(Rect rect)
            {
                Verse.Text.Font = GameFont.Medium;
                Widgets.Label(new Rect(0f, 0f, rect.width, 35f), Localization.T("RimAIPortrait.Settings.Advanced"));
                Verse.Text.Font = GameFont.Small;
                Rect content = new Rect(0f, 40f, rect.width, rect.height - 40f);
                Rect view = new Rect(0f, 0f, content.width - 18f, 4400f);
                Widgets.BeginScrollView(content, ref scroll, view);
                var listing = new Listing_Standard();
                listing.Begin(view);
                owner.DrawAdvancedSettings(listing);
                listing.End();
                Widgets.EndScrollView();
            }

            public override void PostClose()
            {
                owner.WriteSettings();
                base.PostClose();
            }
        }
    }
}
