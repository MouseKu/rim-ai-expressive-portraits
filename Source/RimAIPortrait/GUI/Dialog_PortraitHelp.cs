using System;
using System.IO;
using UnityEngine;
using Verse;

namespace RimAIPortrait
{
    /// <summary>Shows the packaged portrait generation guide.</summary>
    internal sealed class Dialog_PortraitHelp : Window
    {
        private const int PageCount = 3;
        private readonly Texture2D[] pages = new Texture2D[PageCount];
        private readonly int firstPageIndex;
        private readonly int lastPageIndex;
        private readonly string warning;
        private readonly Action<bool> closed;
        private int pageIndex;
        private bool doNotShowAgain;

        public override Vector2 InitialSize => pageIndex < 0 ? new Vector2(760f, 360f) : new Vector2(1030f, 430f);

        public Dialog_PortraitHelp(string warning, Action<bool> closed)
            : this(-1, -1, warning, closed)
        {
        }

        public Dialog_PortraitHelp(int firstPageIndex, int lastPageIndex, string warning = null,
            Action<bool> closed = null)
        {
            pageIndex = firstPageIndex;
            this.firstPageIndex = firstPageIndex;
            this.lastPageIndex = lastPageIndex;
            this.warning = warning;
            this.closed = closed;
            doCloseX = true;
            absorbInputAroundWindow = true;
            closeOnClickedOutside = false;
        }

        public override void PreOpen()
        {
            base.PreOpen();
            if (pageIndex < 0) return;
            for (int index = 0; index < PageCount; index++)
            {
                Texture2D texture = null;
                try
                {
                    string path = Path.Combine(RimAIPortraitMod.ModPack.RootDir, "Help", (index + 1) + ".jpg");
                    texture = new Texture2D(2, 2, TextureFormat.ARGB32, false);
                    if (!texture.LoadImage(File.ReadAllBytes(path)))
                        throw new InvalidDataException(path);
                    texture.wrapMode = TextureWrapMode.Clamp;
                    pages[index] = texture;
                }
                catch (Exception exception)
                {
                    if (texture != null) UnityEngine.Object.Destroy(texture);
                    Log.Warning("[RimAIPortrait] Could not load portrait help image " + (index + 1) + ": " + exception.Message);
                }
            }
        }

        public override void DoWindowContents(Rect rect)
        {
            Text.Font = GameFont.Medium;
            Color titleColor = GUI.color;
            if (pageIndex < 0) GUI.color = new Color(.95f, .2f, .2f, 1f);
            Widgets.Label(new Rect(0f, 0f, rect.width - 100f, 34f), Localization.T(pageIndex < 0
                ? "RimAIPortrait.Help.NovelAiV5WarningTitle" : "RimAIPortrait.Help.Title"));
            GUI.color = titleColor;
            Text.Font = GameFont.Small;
            if (pageIndex < 0)
            {
                Color previousColor = GUI.color;
                GUI.color = new Color(.95f, .2f, .2f, 1f);
                Widgets.Label(new Rect(0f, 42f, rect.width, rect.height - 105f), warning);
                GUI.color = previousColor;
                float warningButtonY = rect.height - 46f;
                Widgets.CheckboxLabeled(new Rect(0f, warningButtonY + 5f, rect.width - 140f, 28f),
                    Localization.T("RimAIPortrait.Help.DoNotShowAgain"), ref doNotShowAgain);
                if (Widgets.ButtonText(new Rect(rect.width - 120f, warningButtonY, 120f, 38f),
                    Localization.T("RimAIPortrait.Help.Ok")))
                    Close();
                return;
            }
            Widgets.Label(new Rect(rect.width - 85f, 4f, 85f, 28f), (pageIndex + 1) + " / " + PageCount);

            float imageTop = 42f;
            if (!string.IsNullOrEmpty(warning))
            {
                Color previousColor = GUI.color;
                GUI.color = new Color(.95f, .2f, .2f, 1f);
                Widgets.Label(new Rect(0f, 42f, rect.width, 64f), warning);
                GUI.color = previousColor;
                imageTop = 112f;
            }
            Rect imageArea = new Rect(0f, imageTop, rect.width, rect.height - imageTop - 58f);
            Widgets.DrawMenuSection(imageArea);
            Texture2D image = pages[pageIndex];
            if (image != null)
                GUI.DrawTexture(imageArea.ContractedBy(8f), image, ScaleMode.ScaleToFit, true);
            else
                Widgets.Label(imageArea.ContractedBy(12f), Localization.T("RimAIPortrait.Help.ImageUnavailable"));

            float buttonY = rect.height - 46f;
            if (Widgets.ButtonText(new Rect(0f, buttonY, 120f, 38f),
                Localization.T("RimAIPortrait.Help.Prev"), true, true, pageIndex > firstPageIndex))
                pageIndex--;
            Widgets.CheckboxLabeled(new Rect(135f, buttonY + 5f, rect.width - 400f, 28f),
                Localization.T("RimAIPortrait.Help.DoNotShowAgain"), ref doNotShowAgain);
            if (Widgets.ButtonText(new Rect(rect.width - 250f, buttonY, 120f, 38f),
                Localization.T("RimAIPortrait.Help.Next"), true, true, pageIndex < lastPageIndex))
                pageIndex++;
            if (Widgets.ButtonText(new Rect(rect.width - 120f, buttonY, 120f, 38f),
                Localization.T("RimAIPortrait.Help.Ok")))
                Close();
        }

        public override void PostClose()
        {
            closed?.Invoke(doNotShowAgain);
            foreach (Texture2D image in pages)
                if (image != null) UnityEngine.Object.Destroy(image);
            base.PostClose();
        }
    }
}
