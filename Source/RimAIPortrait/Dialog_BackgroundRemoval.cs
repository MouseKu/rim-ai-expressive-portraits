using System;
using System.IO;
using UnityEngine;
using Verse;

namespace RimAIPortrait
{
    /// <summary>Previews and confirms non-destructive background removal for a gallery image.</summary>
    public sealed class Dialog_BackgroundRemoval : Window
    {
        private readonly string path;
        private readonly Action confirmed;
        private byte[] originalData;
        private byte[] previewData;
        private Texture2D previewTexture;
        private string colorHex;
        private float sensitivity;
        private int erosionPixels;
        private bool previewCurrent;
        private bool pickingColor;
        private string status;

        public override Vector2 InitialSize => new Vector2(900f, 900f);

        public Dialog_BackgroundRemoval(string path, Action confirmed)
        {
            this.path = path;
            this.confirmed = confirmed;
            PortraitSettings settings = RimAIPortraitMod.Settings;
            colorHex = settings?.BackgroundColorHex ?? "#FFFFFF";
            sensitivity = settings?.BackgroundRemovalSensitivity ?? .3f;
            erosionPixels = settings?.BackgroundErosionPixels ?? 2;
            doCloseX = true;
            absorbInputAroundWindow = true;
            closeOnClickedOutside = false;
        }

        public override void PreOpen()
        {
            base.PreOpen();
            try
            {
                originalData = File.ReadAllBytes(path);
                LoadPreviewTexture(originalData);
                status = Localization.T("RimAIPortrait.BackgroundRemoval.OriginalPreview");
            }
            catch (Exception exception)
            {
                status = Localization.T("RimAIPortrait.Preview.LoadFailed", exception.Message);
            }
        }

        public override void DoWindowContents(Rect rect)
        {
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(0f, 0f, rect.width, 34f), Localization.T("RimAIPortrait.BackgroundRemoval.Title"));
            Text.Font = GameFont.Small;
            Widgets.Label(new Rect(0f, 38f, rect.width, 26f), Path.GetFileName(path));

            Widgets.Label(new Rect(0f, 70f, 190f, 28f), Localization.T("RimAIPortrait.Settings.BackgroundColor"));
            string previousColor = colorHex;
            colorHex = Widgets.TextField(new Rect(195f, 66f, 180f, 32f), colorHex ?? "");
            Color32 color;
            bool validColor = BackgroundRemoval.TryParseHex(colorHex, out color);
            Rect swatch = new Rect(385f, 69f, 26f, 26f);
            Widgets.DrawBoxSolid(swatch, validColor ? (Color)color : Color.gray);
            Widgets.DrawBox(swatch);
            if (!validColor) TooltipHandler.TipRegion(swatch, Localization.T("RimAIPortrait.Settings.BackgroundColorInvalid"));

            float previousSensitivity = sensitivity;
            Widgets.Label(new Rect(430f, 70f, rect.width - 430f, 28f),
                Localization.T("RimAIPortrait.Settings.BackgroundRemovalSensitivity", Mathf.RoundToInt(sensitivity * 100f)));
            sensitivity = Widgets.HorizontalSlider(new Rect(430f, 98f, rect.width - 430f, 24f), sensitivity, 0f, 1f);
            int previousErosion = erosionPixels;
            Widgets.Label(new Rect(0f, 116f, 190f, 28f),
                Localization.T("RimAIPortrait.BackgroundRemoval.Erosion", erosionPixels));
            erosionPixels = Mathf.RoundToInt(Widgets.HorizontalSlider(new Rect(195f, 116f, 216f, 24f), erosionPixels, 0f, 10f));
            if (previousColor != colorHex || !Mathf.Approximately(previousSensitivity, sensitivity) || previousErosion != erosionPixels)
                previewCurrent = false;

            if (Widgets.ButtonText(new Rect(430f, 112f, 140f, 34f), Localization.T("RimAIPortrait.BackgroundRemoval.Preview")))
            {
                if (!validColor)
                    status = Localization.T("RimAIPortrait.Settings.BackgroundColorInvalid");
                else
                {
                    previewData = BackgroundRemoval.Remove(originalData, colorHex, sensitivity, erosionPixels);
                    LoadPreviewTexture(previewData);
                    previewCurrent = true;
                    status = Localization.T("RimAIPortrait.BackgroundRemoval.PreviewReady");
                }
            }
            if (Widgets.ButtonText(new Rect(580f, 112f, 140f, 34f), Localization.T("RimAIPortrait.BackgroundRemoval.Eyedropper")))
            {
                pickingColor = !pickingColor;
                status = Localization.T(pickingColor
                    ? "RimAIPortrait.BackgroundRemoval.EyedropperActive"
                    : "RimAIPortrait.BackgroundRemoval.EyedropperCancelled");
            }
            Rect alignRect = new Rect(730f, 112f, rect.width - 730f, 34f);
            if (Widgets.ButtonText(alignRect, Localization.T("RimAIPortrait.BackgroundRemoval.Align"), true, true, previewCurrent))
            {
                previewData = BackgroundRemoval.AlignBottom(previewData);
                LoadPreviewTexture(previewData);
                status = Localization.T("RimAIPortrait.BackgroundRemoval.Aligned");
            }
            TooltipHandler.TipRegion(alignRect, Localization.T("RimAIPortrait.BackgroundRemoval.AlignTip"));

            Widgets.Label(new Rect(0f, 154f, rect.width, 28f), status ?? "");
            Rect imageArea = new Rect(0f, 184f, rect.width, rect.height - 234f);
            Widgets.DrawMenuSection(imageArea);
            Rect imageBounds = imageArea.ContractedBy(8f);
            if (previewTexture != null)
            {
                GUI.DrawTexture(imageBounds, previewTexture, ScaleMode.ScaleToFit, true);
                HandleEyedropper(imageBounds);
            }

            if (Widgets.ButtonText(new Rect(rect.width - 320f, rect.height - 40f, 150f, 36f),
                Localization.T("RimAIPortrait.BackgroundRemoval.Confirm"), true, true, previewCurrent))
            {
                try
                {
                    File.WriteAllBytes(path, previewData);
                    confirmed?.Invoke();
                    Close();
                }
                catch (Exception exception)
                {
                    status = Localization.T("RimAIPortrait.BackgroundRemoval.SaveFailed", exception.Message);
                }
            }
            if (Widgets.ButtonText(new Rect(rect.width - 160f, rect.height - 40f, 150f, 36f), Localization.T("RimAIPortrait.BackgroundRemoval.Cancel"))) Close();
        }

        private void LoadPreviewTexture(byte[] data)
        {
            if (previewTexture != null) UnityEngine.Object.Destroy(previewTexture);
            previewTexture = new Texture2D(2, 2, TextureFormat.ARGB32, false);
            if (!previewTexture.LoadImage(data)) throw new InvalidDataException(Localization.T("RimAIPortrait.Preview.UnsupportedImage"));
            previewTexture.wrapMode = TextureWrapMode.Clamp;
        }

        private void HandleEyedropper(Rect bounds)
        {
            if (!pickingColor || Event.current.type != EventType.MouseDown || Event.current.button != 0) return;
            Rect drawnRect = ScaleToFitRect(bounds, previewTexture.width, previewTexture.height);
            if (!drawnRect.Contains(Event.current.mousePosition)) return;

            float normalizedX = Mathf.Clamp01((Event.current.mousePosition.x - drawnRect.x) / drawnRect.width);
            float normalizedY = 1f - Mathf.Clamp01((Event.current.mousePosition.y - drawnRect.y) / drawnRect.height);
            int pixelX = Mathf.Clamp(Mathf.FloorToInt(normalizedX * previewTexture.width), 0, previewTexture.width - 1);
            int pixelY = Mathf.Clamp(Mathf.FloorToInt(normalizedY * previewTexture.height), 0, previewTexture.height - 1);
            Color sampled = previewTexture.GetPixel(pixelX, pixelY);
            colorHex = "#" + ColorUtility.ToHtmlStringRGB(sampled);
            previewCurrent = false;
            pickingColor = false;
            status = Localization.T("RimAIPortrait.BackgroundRemoval.ColorSampled", colorHex);
            Event.current.Use();
        }

        private static Rect ScaleToFitRect(Rect bounds, int textureWidth, int textureHeight)
        {
            float textureAspect = (float)textureWidth / textureHeight;
            float boundsAspect = bounds.width / bounds.height;
            if (textureAspect > boundsAspect)
            {
                float height = bounds.width / textureAspect;
                return new Rect(bounds.x, bounds.center.y - height * .5f, bounds.width, height);
            }
            float width = bounds.height * textureAspect;
            return new Rect(bounds.center.x - width * .5f, bounds.y, width, bounds.height);
        }

        public override void PostClose()
        {
            Color32 parsedColor;
            if (RimAIPortraitMod.Settings != null)
            {
                if (BackgroundRemoval.TryParseHex(colorHex, out parsedColor))
                    RimAIPortraitMod.Settings.BackgroundColorHex = colorHex;
                RimAIPortraitMod.Settings.BackgroundRemovalSensitivity = sensitivity;
                RimAIPortraitMod.Settings.BackgroundErosionPixels = erosionPixels;
                RimAIPortraitMod.SaveSettings();
            }
            if (previewTexture != null) UnityEngine.Object.Destroy(previewTexture);
            previewTexture = null;
            base.PostClose();
        }
    }
}
