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
        private readonly int lastPageIndex;
        private int pageIndex;

        public override Vector2 InitialSize => new Vector2(1030f, 430f);

        public Dialog_PortraitHelp(int firstPageIndex, int lastPageIndex)
        {
            pageIndex = firstPageIndex;
            this.lastPageIndex = lastPageIndex;
            doCloseX = true;
            absorbInputAroundWindow = true;
            closeOnClickedOutside = false;
        }

        public override void PreOpen()
        {
            base.PreOpen();
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
            Widgets.Label(new Rect(0f, 0f, rect.width - 100f, 34f), Localization.T("RimAIPortrait.Help.Title"));
            Text.Font = GameFont.Small;
            Widgets.Label(new Rect(rect.width - 85f, 4f, 85f, 28f), (pageIndex + 1) + " / " + PageCount);

            Rect imageArea = new Rect(0f, 42f, rect.width, rect.height - 100f);
            Widgets.DrawMenuSection(imageArea);
            Texture2D image = pages[pageIndex];
            if (image != null)
                GUI.DrawTexture(imageArea.ContractedBy(8f), image, ScaleMode.ScaleToFit, true);
            else
                Widgets.Label(imageArea.ContractedBy(12f), Localization.T("RimAIPortrait.Help.ImageUnavailable"));

            float buttonY = rect.height - 46f;
            if (Widgets.ButtonText(new Rect(0f, buttonY, 120f, 38f),
                Localization.T("RimAIPortrait.Help.Prev"), true, true, pageIndex > 0))
                pageIndex--;
            if (Widgets.ButtonText(new Rect(rect.width - 250f, buttonY, 120f, 38f),
                Localization.T("RimAIPortrait.Help.Next"), true, true, pageIndex < lastPageIndex))
                pageIndex++;
            if (Widgets.ButtonText(new Rect(rect.width - 120f, buttonY, 120f, 38f),
                Localization.T("RimAIPortrait.Help.Ok")))
                Close();
        }

        public override void PostClose()
        {
            foreach (Texture2D image in pages)
                if (image != null) UnityEngine.Object.Destroy(image);
            base.PostClose();
        }
    }
}
