using System;
using System.IO;
using UnityEngine;
using Verse;

namespace RimAIPortrait
{
    internal sealed class Dialog_PortraitRegenerate : Window
    {
        private readonly string imagePath;
        private readonly Action<string> confirmed;
        private Texture2D preview;
        private string prompt = Localization.T("RimAIPortrait.Regenerate.PromptPrefix");
        private string error;

        public override Vector2 InitialSize => PortraitWindowSizing.Fit(760f, 840f);

        public Dialog_PortraitRegenerate(string imagePath, Action<string> confirmed)
        {
            this.imagePath = imagePath;
            this.confirmed = confirmed;
            doCloseX = true;
            absorbInputAroundWindow = true;
        }

        public override void PreOpen()
        {
            base.PreOpen();
            try
            {
                preview = new Texture2D(2, 2, TextureFormat.ARGB32, false);
                if (!preview.LoadImage(File.ReadAllBytes(imagePath)))
                    throw new InvalidDataException(Localization.T("RimAIPortrait.Inpaint.InvalidImage"));
            }
            catch (Exception exception)
            {
                if (preview != null) UnityEngine.Object.Destroy(preview);
                preview = null;
                error = exception.Message;
            }
        }

        public override void DoWindowContents(Rect rect)
        {
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(0f, 0f, rect.width, 34f), Localization.T("RimAIPortrait.Regenerate.Title"));
            Text.Font = GameFont.Small;
            Rect area = new Rect(0f, 42f, rect.width, rect.height - 220f);
            Widgets.DrawMenuSection(area);
            if (preview == null)
                Widgets.Label(area.ContractedBy(12f), error ?? Localization.T("RimAIPortrait.Inpaint.InvalidImage"));
            else
            {
                float scale = Mathf.Min(area.width / preview.width, area.height / preview.height);
                float width = preview.width * scale;
                float height = preview.height * scale;
                GUI.DrawTexture(new Rect(area.center.x - width / 2f, area.center.y - height / 2f,
                    width, height), preview, ScaleMode.ScaleToFit);
            }
            Widgets.Label(new Rect(0f, rect.height - 170f, rect.width, 26f),
                Localization.T("RimAIPortrait.Common.Prompt"));
            prompt = Widgets.TextArea(new Rect(0f, rect.height - 142f, rect.width, 90f), prompt ?? "");
            if (Widgets.ButtonText(new Rect(rect.width - 125f, rect.height - 42f, 125f, 38f),
                Localization.T("RimAIPortrait.Inpaint.Confirm"), true, true,
                preview != null && !string.IsNullOrWhiteSpace(prompt) && PortraitRuntime.GenerationButtonReady))
            {
                confirmed(prompt);
                Close();
            }
        }

        public override void PostClose()
        {
            if (preview != null) UnityEngine.Object.Destroy(preview);
            base.PostClose();
        }
    }
}
