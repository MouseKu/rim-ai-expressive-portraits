using System;
using System.IO;
using UnityEngine;
using Verse;

namespace RimAIPortrait
{
    /// <summary>Edits the shared tag extraction instruction for Base or Inpaint.</summary>
    internal sealed class Dialog_TagExtractorPrompt : Window
    {
        private string prompt;
        private string status;
        private Vector2 scroll;
        private readonly bool inpaint;

        public override Vector2 InitialSize => PortraitWindowSizing.Fit(800f, 750f);

        public Dialog_TagExtractorPrompt(bool inpaint = false)
        {
            this.inpaint = inpaint;
            prompt = inpaint ? RimAIPortraitMod.Settings.TagExtractorInpaintPrompt ?? ""
                : RimAIPortraitMod.Settings.TagExtractorBasePrompt ?? "";
            doCloseX = true;
            absorbInputAroundWindow = true;
            closeOnClickedOutside = false;
        }

        public override void DoWindowContents(Rect rect)
        {
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(0f, 0f, rect.width, 35f),
                Localization.T(inpaint ? "RimAIPortrait.Inpaint.TagExtractorPromptTitle"
                    : "RimAIPortrait.Gallery.TagExtractorPromptTitle"));
            Text.Font = GameFont.Small;
            Widgets.Label(new Rect(0f, 40f, rect.width, 50f),
                Localization.T(inpaint ? "RimAIPortrait.Inpaint.TagExtractorPromptScope"
                    : "RimAIPortrait.Gallery.TagExtractorPromptScope"));

            Rect editor = new Rect(0f, 96f, rect.width, rect.height - 163f);
            float viewWidth = editor.width - 18f;
            float viewHeight = Mathf.Max(editor.height, Text.CalcHeight(prompt ?? "", viewWidth - 12f) + 60f);
            Rect view = new Rect(0f, 0f, viewWidth, viewHeight);
            Widgets.BeginScrollView(editor, ref scroll, view);
            prompt = Widgets.TextArea(new Rect(0f, 0f, viewWidth, viewHeight), prompt ?? "");
            Widgets.EndScrollView();

            float buttonY = rect.height - 54f;
            if (Widgets.ButtonText(new Rect(0f, buttonY, 110f, 38f),
                Localization.T("RimAIPortrait.Common.Reset")))
                RestoreDefault();
            if (!status.NullOrEmpty())
                Widgets.Label(new Rect(120f, buttonY + 5f, rect.width - 370f, 30f), status);
            if (Widgets.ButtonText(new Rect(rect.width - 240f, buttonY, 110f, 38f),
                Localization.T("RimAIPortrait.Gallery.CancelTagExtractorPrompt")))
                Close();
            if (Widgets.ButtonText(new Rect(rect.width - 120f, buttonY, 120f, 38f),
                Localization.T("RimAIPortrait.Common.Save")))
                SavePrompt();
        }

        private void RestoreDefault()
        {
            try
            {
                string path = Path.Combine(RimAIPortraitMod.ModPack.RootDir,
                    "Prompts", "Common", inpaint ? "TagExtractorInpaint.md" : "TagExtractor.md");
                string original = File.ReadAllText(path).Trim();
                if (string.IsNullOrWhiteSpace(original)) throw new InvalidDataException(path);
                prompt = original;
                scroll = Vector2.zero;
                status = null;
            }
            catch (Exception)
            {
                status = Localization.T("RimAIPortrait.Settings.PromptDefaultMissing");
            }
        }

        private void SavePrompt()
        {
            if (string.IsNullOrWhiteSpace(prompt))
            {
                status = Localization.T("RimAIPortrait.TagExtractor.PromptMissing");
                return;
            }
            if (inpaint) RimAIPortraitMod.Settings.TagExtractorInpaintPrompt = prompt;
            else RimAIPortraitMod.Settings.TagExtractorBasePrompt = prompt;
            RimAIPortraitMod.SaveSettings();
            Close();
        }
    }
}
