using System;
using System.IO;
using UnityEngine;
using Verse;

namespace RimAIPortrait
{
    public sealed partial class Dialog_PortraitGallery
    {
        // Increase this number when a new numbered image and text pair is packaged in Notice/.
        private const int CurrentNoticeNumber = 0;

        private bool ShowNoticeOnGalleryOpen()
        {
            if (galleryClosed || helpOpen ||
                RimAIPortraitMod.Settings.DismissedNoticeNumber >= CurrentNoticeNumber)
                return false;

            helpOpen = true;
            Find.WindowStack.Add(new Dialog_PortraitNotice(CurrentNoticeNumber, dismissed =>
            {
                if (dismissed)
                {
                    RimAIPortraitMod.Settings.DismissedNoticeNumber = CurrentNoticeNumber;
                    RimAIPortraitMod.SaveSettings();
                }
                helpOpen = false;
                ShowNextHelp();
            }));
            return true;
        }
    }

    internal sealed class Dialog_PortraitNotice : Window
    {
        private readonly int noticeNumber;
        private readonly Action<bool> closed;
        private Texture2D image;
        private string noticeText;
        private Vector2 scrollPosition;
        private bool doNotShowAgain;

        public override Vector2 InitialSize => PortraitWindowSizing.Fit(790f, 650f);

        public Dialog_PortraitNotice(int noticeNumber, Action<bool> closed)
        {
            this.noticeNumber = noticeNumber;
            this.closed = closed;
            doCloseX = true;
            absorbInputAroundWindow = true;
            closeOnClickedOutside = false;
        }

        public override void PreOpen()
        {
            base.PreOpen();
            string folder = Path.Combine(RimAIPortraitMod.ModPack.RootDir, "Notice");
            string language = LanguageDatabase.activeLanguage != null &&
                LanguageDatabase.activeLanguage.folderName == "Korean" ? "kr" : "eng";
            string textPath = Path.Combine(folder, language + noticeNumber + ".txt");
            try
            {
                noticeText = File.ReadAllText(textPath).Trim();
            }
            catch (Exception exception)
            {
                Log.Warning("[RimAIPortrait] Could not load notice text " + textPath + ": " + exception.Message);
                noticeText = Localization.T("RimAIPortrait.Notice.TextUnavailable");
            }

            string imagePath = Path.Combine(folder, noticeNumber + ".jpg");
            Texture2D loadedImage = null;
            try
            {
                loadedImage = new Texture2D(2, 2, TextureFormat.ARGB32, false);
                if (!loadedImage.LoadImage(File.ReadAllBytes(imagePath)))
                    throw new InvalidDataException(imagePath);
                loadedImage.wrapMode = TextureWrapMode.Clamp;
                image = loadedImage;
            }
            catch (Exception exception)
            {
                if (loadedImage != null) UnityEngine.Object.Destroy(loadedImage);
                Log.Warning("[RimAIPortrait] Could not load notice image " + imagePath + ": " + exception.Message);
            }
        }

        public override void DoWindowContents(Rect rect)
        {
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(0f, 0f, rect.width, 34f), Localization.T("RimAIPortrait.Notice.Title"));
            Text.Font = GameFont.Small;

            float bodyTop = 42f;
            float bodyHeight = rect.height - bodyTop - 58f;
            float contentWidth = Mathf.Max(1f, rect.width - 20f);
            float imageHeight = contentWidth * 296f / 531f;
            float textHeight = Text.CalcHeight(noticeText, contentWidth - 16f);
            float contentHeight = imageHeight + textHeight + 30f;
            Rect viewport = new Rect(0f, bodyTop, rect.width, bodyHeight);
            Widgets.BeginScrollView(viewport, ref scrollPosition,
                new Rect(0f, 0f, contentWidth, contentHeight));
            Rect imageArea = new Rect(0f, 0f, contentWidth, imageHeight);
            Widgets.DrawMenuSection(imageArea);
            if (image != null)
                GUI.DrawTexture(imageArea.ContractedBy(8f), image, ScaleMode.ScaleToFit, true);
            else
                Widgets.Label(imageArea.ContractedBy(12f),
                    Localization.T("RimAIPortrait.Notice.ImageUnavailable"));
            Widgets.Label(new Rect(8f, imageHeight + 12f, contentWidth - 16f, textHeight), noticeText);
            Widgets.EndScrollView();

            float buttonY = rect.height - 46f;
            Widgets.CheckboxLabeled(new Rect(0f, buttonY + 5f, rect.width - 140f, 28f),
                Localization.T("RimAIPortrait.Help.DoNotShowAgain"), ref doNotShowAgain);
            if (Widgets.ButtonText(new Rect(rect.width - 120f, buttonY, 120f, 38f),
                Localization.T("RimAIPortrait.Help.Ok")))
                Close();
        }

        public override void PostClose()
        {
            closed?.Invoke(doNotShowAgain);
            if (image != null) UnityEngine.Object.Destroy(image);
            base.PostClose();
        }
    }
}
