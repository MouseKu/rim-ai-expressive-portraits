using System;
using System.IO;
using UnityEngine;
using Verse;

namespace RimAIPortrait
{
    /// <summary>Shows the packaged portrait generation guide.</summary>
    internal sealed class Dialog_PortraitHelp : Window
    {
        private static readonly string[] PageFiles = { "1-1.jpg", "1-2.jpg", "1-3.jpg", "2.jpg", "3.jpg", "4.jpg", "5-1.jpg", "5-2.jpg" };
        private const int InpaintPageIndex = 5;
        internal const int TagExtractorFirstPageIndex = 6;
        internal const int TagExtractorLastPageIndex = 7;
        private const int PageCount = 8;
        private readonly Texture2D[] pages = new Texture2D[PageCount];
        private readonly int firstPageIndex;
        private readonly int lastPageIndex;
        private readonly string warning;
        private readonly Action<bool> closed;
        private int pageIndex;
        private bool doNotShowAgain;

        public override Vector2 InitialSize => pageIndex < 0 ? PortraitWindowSizing.Fit(760f, 360f)
            : pageIndex == InpaintPageIndex ? PortraitWindowSizing.Fit(800f, 900f) : PortraitWindowSizing.Fit(1030f, 430f);

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
            for (int index = firstPageIndex; index <= lastPageIndex; index++)
            {
                Texture2D texture = null;
                try
                {
                    string path = Path.Combine(RimAIPortraitMod.ModPack.RootDir, "Help", PageFiles[index]);
                    texture = new Texture2D(2, 2, TextureFormat.ARGB32, false);
                    if (!texture.LoadImage(File.ReadAllBytes(path)))
                        throw new InvalidDataException(path);
                    texture.wrapMode = TextureWrapMode.Clamp;
                    pages[index] = texture;
                }
                catch (Exception exception)
                {
                    if (texture != null) UnityEngine.Object.Destroy(texture);
                    Log.Warning("[RimAIPortrait] Could not load portrait help image " + PageFiles[index] + ": " + exception.Message);
                }
            }
        }

        public override void DoWindowContents(Rect rect)
        {
            Text.Font = GameFont.Medium;
            Color titleColor = GUI.color;
            if (pageIndex < 0) GUI.color = new Color(.95f, .2f, .2f, 1f);
            Widgets.Label(new Rect(0f, 0f, rect.width - 100f, 34f), Localization.T(pageIndex < 0
                ? "RimAIPortrait.Help.NovelAiV5WarningTitle"
                : pageIndex == InpaintPageIndex ? "RimAIPortrait.Help.InpaintTitle"
                : pageIndex >= TagExtractorFirstPageIndex ? "RimAIPortrait.Help.TagExtractorTitle"
                : "RimAIPortrait.Help.Title"));
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
            if (pageIndex != InpaintPageIndex)
                Widgets.Label(new Rect(rect.width - 85f, 4f, 85f, 28f),
                    (pageIndex - firstPageIndex + 1) + " / " + (lastPageIndex - firstPageIndex + 1));

            float imageTop = 42f;
            if (!string.IsNullOrEmpty(warning))
            {
                Color previousColor = GUI.color;
                GUI.color = new Color(.95f, .2f, .2f, 1f);
                Widgets.Label(new Rect(0f, 42f, rect.width, 64f), warning);
                GUI.color = previousColor;
                imageTop = 112f;
            }
            string instructions = pageIndex == InpaintPageIndex
                ? Localization.T("RimAIPortrait.Help.InpaintInstructions")
                : pageIndex == TagExtractorLastPageIndex
                ? Localization.T("RimAIPortrait.Help.TagExtractorInstructions") : null;
            float instructionsHeight = instructions == null ? 0f : Text.CalcHeight(instructions, rect.width) + 12f;
            Rect imageArea = new Rect(0f, imageTop, rect.width,
                rect.height - imageTop - 58f - instructionsHeight);
            Widgets.DrawMenuSection(imageArea);
            Texture2D image = pages[pageIndex];
            if (image != null)
                GUI.DrawTexture(imageArea.ContractedBy(8f), image, ScaleMode.ScaleToFit, true);
            else
                Widgets.Label(imageArea.ContractedBy(12f), Localization.T("RimAIPortrait.Help.ImageUnavailable"));
            if (instructions != null)
                Widgets.Label(new Rect(0f, imageArea.yMax + 10f, rect.width, instructionsHeight - 10f), instructions);

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
