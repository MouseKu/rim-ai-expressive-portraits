using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using Verse;

namespace RimAIPortrait
{
    /// <summary>Applies background removal options to selected gallery images after confirmation.</summary>
    public sealed class Dialog_BackgroundRemoval : Window
    {
        private readonly string[] paths;
        private readonly Action<int, int> confirmed;
        private int currentIndex;
        private byte[] originalData;
        private byte[] previewData;
        private Texture2D workingTexture;
        private Texture2D previewTexture;
        private int alignmentOffset;
        private string colorHex;
        private float sensitivity;
        private int erosionPixels;
        private bool previewCurrent;
        private bool alignBottom;
        private bool removeEnclosed;
        private float enclosedCleanup;
        private bool saving;
        private bool closed;
        private bool imagePressHandled;
        private string status;
        private readonly List<BackgroundRemovalStep> steps = new List<BackgroundRemovalStep>();
        private readonly List<BackgroundRemovalStep> redoHistory = new List<BackgroundRemovalStep>();
        private string CurrentPath => paths[currentIndex];

        public override Vector2 InitialSize => new Vector2(900f, 900f);

        public Dialog_BackgroundRemoval(IEnumerable<string> paths, Action<int, int> confirmed)
        {
            this.paths = new List<string>(paths).ToArray();
            this.confirmed = confirmed;
            PortraitSettings settings = RimAIPortraitMod.Settings;
            colorHex = settings?.BackgroundColorHex ?? "#FFFFFF";
            sensitivity = settings?.BackgroundRemovalSensitivity ?? .3f;
            erosionPixels = settings?.BackgroundErosionPixels ?? 2;
            removeEnclosed = settings?.RemoveEnclosedBackground ?? true;
            enclosedCleanup = settings?.EnclosedCleanup ?? .5f;
            alignBottom = settings?.BackgroundAlignBottom ?? false;
            doCloseX = true;
            absorbInputAroundWindow = true;
            closeOnClickedOutside = false;
        }

        public override void PreOpen()
        {
            base.PreOpen();
            try
            {
                LoadCurrentImage();
            }
            catch (Exception exception)
            {
                status = Localization.T("RimAIPortrait.Preview.LoadFailed", exception.Message);
            }
        }

        public override void DoWindowContents(Rect rect)
        {
            if (!saving && Event.current.type == EventType.KeyDown && Event.current.control
                && (Event.current.keyCode == KeyCode.Z || Event.current.keyCode == KeyCode.Y))
            {
                if (Event.current.keyCode == KeyCode.Y || Event.current.shift) Redo();
                else Undo();
                Event.current.Use();
            }
            bool previousGuiEnabled = GUI.enabled;
            GUI.enabled = previousGuiEnabled && !saving;
            Rect imageArea = new Rect(0f, 230f, rect.width, rect.height - 280f);
            Rect imageBounds = imageArea.ContractedBy(8f);
            if (previewTexture != null) HandleImageClick(imageBounds);
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(0f, 0f, rect.width, 34f), Localization.T("RimAIPortrait.BackgroundRemoval.Title"));
            Text.Font = GameFont.Small;
            if (Widgets.ButtonText(new Rect(0f, 36f, 42f, 30f), "<", true, true, currentIndex > 0))
                ChangeImage(-1);
            Widgets.Label(new Rect(52f, 38f, rect.width - 104f, 26f),
                Localization.T("RimAIPortrait.BackgroundRemoval.ImagePosition", currentIndex + 1, paths.Length, Path.GetFileName(CurrentPath)));
            if (Widgets.ButtonText(new Rect(rect.width - 42f, 36f, 42f, 30f), ">", true, true, currentIndex < paths.Length - 1))
                ChangeImage(1);

            Widgets.Label(new Rect(0f, 76f, 120f, 28f), Localization.T("RimAIPortrait.Settings.BackgroundColor"));
            string previousColorHex = colorHex;
            float previousSensitivity = sensitivity;
            int previousErosionPixels = erosionPixels;
            bool previousRemoveEnclosed = removeEnclosed;
            float previousEnclosedCleanup = enclosedCleanup;
            bool previousAlignBottom = alignBottom;
            colorHex = Widgets.TextField(new Rect(130f, 72f, 140f, 32f), colorHex ?? "");
            TooltipHandler.TipRegion(new Rect(0f, 72f, 270f, 32f),
                Localization.T("RimAIPortrait.BackgroundRemoval.Tip.Color"));
            Color32 color;
            bool validColor = BackgroundRemoval.TryParseHex(colorHex, out color);
            Rect swatch = new Rect(280f, 75f, 26f, 26f);
            Widgets.DrawBoxSolid(swatch, validColor ? (Color)color : Color.gray);
            Widgets.DrawBox(swatch);
            if (!validColor) TooltipHandler.TipRegion(swatch, Localization.T("RimAIPortrait.Settings.BackgroundColorInvalid"));

            Widgets.Label(new Rect(320f, 76f, 220f, 28f),
                Localization.T("RimAIPortrait.Settings.BackgroundRemovalSensitivity", Mathf.RoundToInt(sensitivity * 100f)));
            sensitivity = Widgets.HorizontalSlider(new Rect(550f, 77f, rect.width - 550f, 24f), sensitivity, 0f, 1f);
            TooltipHandler.TipRegion(new Rect(320f, 76f, rect.width - 320f, 28f),
                Localization.T("RimAIPortrait.BackgroundRemoval.Tip.Sensitivity"));
            Widgets.Label(new Rect(0f, 118f, 195f, 28f),
                Localization.T("RimAIPortrait.BackgroundRemoval.Erosion", erosionPixels));
            erosionPixels = Mathf.RoundToInt(Widgets.HorizontalSlider(new Rect(200f, 119f, 200f, 24f), erosionPixels, 0f, 10f));
            TooltipHandler.TipRegion(new Rect(0f, 118f, 400f, 28f),
                Localization.T("RimAIPortrait.BackgroundRemoval.Tip.Erosion"));
            bool historyChanged = false;
            Rect undoButton = new Rect(410f, 112f, 105f, 34f);
            if (Widgets.ButtonText(undoButton,
                Localization.T("RimAIPortrait.BackgroundRemoval.Undo"), true, true, steps.Count > 0))
            {
                Undo();
                historyChanged = true;
            }
            TooltipHandler.TipRegion(undoButton, Localization.T("RimAIPortrait.BackgroundRemoval.Tip.Undo"));
            Rect redoButton = new Rect(525f, 112f, 105f, 34f);
            if (Widgets.ButtonText(redoButton,
                Localization.T("RimAIPortrait.BackgroundRemoval.Redo"), true, true, redoHistory.Count > 0))
            {
                Redo();
                historyChanged = true;
            }
            TooltipHandler.TipRegion(redoButton, Localization.T("RimAIPortrait.BackgroundRemoval.Tip.Redo"));
            Rect alignRect = new Rect(640f, 112f, rect.width - 640f, 34f);
            if (Widgets.ButtonText(alignRect, (alignBottom ? "[x] " : "[ ] ") + Localization.T("RimAIPortrait.BackgroundRemoval.Align")))
            {
                alignBottom = !alignBottom;
            }
            TooltipHandler.TipRegion(alignRect, Localization.T("RimAIPortrait.BackgroundRemoval.AlignTip"));

            Rect removeEnclosedRect = new Rect(0f, 157f, 245f, 28f);
            Widgets.CheckboxLabeled(removeEnclosedRect,
                Localization.T("RimAIPortrait.BackgroundRemoval.RemoveEnclosed"), ref removeEnclosed);
            TooltipHandler.TipRegion(removeEnclosedRect,
                Localization.T("RimAIPortrait.BackgroundRemoval.Tip.RemoveEnclosed"));
            if (removeEnclosed)
            {
                Widgets.Label(new Rect(300f, 157f, 220f, 28f),
                    Localization.T("RimAIPortrait.BackgroundRemoval.EnclosedCleanup", Mathf.RoundToInt(enclosedCleanup * 100f)));
                enclosedCleanup = Widgets.HorizontalSlider(new Rect(530f, 160f, rect.width - 530f, 24f), enclosedCleanup, 0f, 1f);
                TooltipHandler.TipRegion(new Rect(300f, 157f, rect.width - 300f, 28f),
                    Localization.T("RimAIPortrait.BackgroundRemoval.Tip.EnclosedCleanup"));
            }
            if (!historyChanged && (previousColorHex != colorHex || !Mathf.Approximately(previousSensitivity, sensitivity)
                || previousErosionPixels != erosionPixels || previousRemoveEnclosed != removeEnclosed
                || !Mathf.Approximately(previousEnclosedCleanup, enclosedCleanup)
                || previousAlignBottom != alignBottom))
            {
                if (steps.Count > 0)
                {
                    UpdateLastStep();
                    RefreshPreview();
                }
            }
            validColor = BackgroundRemoval.TryParseHex(colorHex, out color);
            Widgets.Label(new Rect(0f, 200f, rect.width, 28f), status ?? "");
            Widgets.DrawMenuSection(imageArea);
            if (previewTexture != null)
            {
                GUI.DrawTexture(imageBounds, previewTexture, ScaleMode.ScaleToFit, true);
                TooltipHandler.TipRegion(imageBounds, Localization.T("RimAIPortrait.BackgroundRemoval.Tip.Image"));
            }

            if (Widgets.ButtonText(new Rect(rect.width - 320f, rect.height - 40f, 150f, 36f),
                Localization.T("RimAIPortrait.BackgroundRemoval.Confirm"), true, true, !saving && previewCurrent && validColor))
            {
                saving = true;
                status = Localization.T("RimAIPortrait.BackgroundRemoval.Saving");
                PortraitRuntime.EnsureCreated();
                PortraitRuntime.Instance.RemoveBackgroundFromGallery(paths, steps.ToArray(), alignBottom, OnSaveCompleted);
                PortraitSettings settings = RimAIPortraitMod.Settings;
                if (settings != null)
                {
                    settings.BackgroundColorHex = colorHex;
                    settings.BackgroundRemovalSensitivity = sensitivity;
                    settings.BackgroundErosionPixels = erosionPixels;
                    settings.RemoveEnclosedBackground = removeEnclosed;
                    settings.EnclosedCleanup = enclosedCleanup;
                    settings.BackgroundAlignBottom = alignBottom;
                    RimAIPortraitMod.SaveSettings();
                }
            }
            GUI.enabled = previousGuiEnabled;
            if (Widgets.ButtonText(new Rect(rect.width - 160f, rect.height - 40f, 150f, 36f),
                Localization.T("RimAIPortrait.BackgroundRemoval.Exit"), true, true, !saving)) Close();
        }

        private void OnSaveCompleted(int succeeded, int failed)
        {
            confirmed?.Invoke(succeeded, failed);
            saving = false;
            if (closed) return;
            if (failed == 0)
            {
                Close();
                return;
            }

            steps.Clear();
            redoHistory.Clear();
            try
            {
                LoadCurrentImage();
                status = Localization.T("RimAIPortrait.BackgroundRemoval.Saved", succeeded, failed);
            }
            catch (Exception exception)
            {
                ClearLoadedImage();
                status = Localization.T("RimAIPortrait.Preview.LoadFailed", exception.Message);
            }
        }

        private void LoadPreviewTexture(byte[] data)
        {
            if (previewTexture != null) UnityEngine.Object.Destroy(previewTexture);
            previewTexture = new Texture2D(2, 2, TextureFormat.ARGB32, false);
            if (!previewTexture.LoadImage(data))
            {
                UnityEngine.Object.Destroy(previewTexture);
                previewTexture = null;
                throw new InvalidDataException(Localization.T("RimAIPortrait.Preview.UnsupportedImage"));
            }
            previewTexture.wrapMode = TextureWrapMode.Clamp;
        }

        private void LoadCurrentImage()
        {
            originalData = File.ReadAllBytes(CurrentPath);
            RefreshPreview();
        }

        private void ChangeImage(int offset)
        {
            currentIndex += offset;
            try { LoadCurrentImage(); }
            catch (Exception exception)
            {
                ClearLoadedImage();
                status = Localization.T("RimAIPortrait.Preview.LoadFailed", exception.Message);
            }
        }

        private void ClearLoadedImage()
        {
            originalData = null;
            previewData = null;
            previewCurrent = false;
            if (workingTexture != null) UnityEngine.Object.Destroy(workingTexture);
            workingTexture = null;
            if (previewTexture != null) UnityEngine.Object.Destroy(previewTexture);
            previewTexture = null;
        }

        private void RefreshPreview()
        {
            if (originalData == null)
            {
                previewData = null;
                previewCurrent = false;
                return;
            }
            previewData = steps.Count == 0 ? originalData
                : BackgroundRemoval.ApplySteps(originalData, steps, CurrentPath);
            RenderPreview();
            status = Localization.T(steps.Count == 0
                ? "RimAIPortrait.BackgroundRemoval.OriginalPreview"
                : "RimAIPortrait.BackgroundRemoval.PreviewReady");
        }

        private void RenderPreview()
        {
            if (workingTexture != null) UnityEngine.Object.Destroy(workingTexture);
            workingTexture = new Texture2D(2, 2, TextureFormat.ARGB32, false);
            if (!workingTexture.LoadImage(previewData))
            {
                UnityEngine.Object.Destroy(workingTexture);
                workingTexture = null;
                throw new InvalidDataException(Localization.T("RimAIPortrait.Preview.UnsupportedImage"));
            }
            workingTexture.wrapMode = TextureWrapMode.Clamp;
            alignmentOffset = steps.Count > 0 && alignBottom ? BottomAlignmentOffset(workingTexture) : 0;
            LoadPreviewTexture(alignmentOffset > 0 ? BackgroundRemoval.AlignBottom(previewData) : previewData);
            previewCurrent = steps.Count > 0;
        }

        private void HandleImageClick(Rect bounds)
        {
            if (saving || Event.current.button != 0) return;
            if (Event.current.type == EventType.MouseUp)
            {
                if (imagePressHandled)
                {
                    imagePressHandled = false;
                    return;
                }
            }
            else if (Event.current.type == EventType.MouseDown)
            {
                imagePressHandled = false;
            }
            else return;
            if (workingTexture == null) return;
            Rect drawnRect = ScaleToFitRect(bounds, workingTexture.width, workingTexture.height);
            if (!drawnRect.Contains(Event.current.mousePosition)) return;

            float normalizedX = Mathf.Clamp01((Event.current.mousePosition.x - drawnRect.x) / drawnRect.width);
            float normalizedY = 1f - Mathf.Clamp01((Event.current.mousePosition.y - drawnRect.y) / drawnRect.height);
            int pixelX = Mathf.Clamp(Mathf.FloorToInt(normalizedX * workingTexture.width), 0, workingTexture.width - 1);
            int pixelY = Mathf.Clamp(Mathf.FloorToInt(normalizedY * workingTexture.height) + alignmentOffset,
                0, workingTexture.height - 1);
            Color sampled = workingTexture.GetPixel(pixelX, pixelY);
            if (sampled.a <= 0f) return;
            colorHex = "#" + ColorUtility.ToHtmlStringRGB(sampled);
            var step = new BackgroundRemovalStep
            {
                ColorHex = colorHex,
                Sensitivity = sensitivity,
                ErosionPixels = erosionPixels,
                RemoveEnclosed = removeEnclosed,
                EnclosedCleanup = enclosedCleanup,
                PickedPath = CurrentPath,
                PickedX = pixelX,
                PickedY = pixelY
            };
            steps.Add(step);
            redoHistory.Clear();
            previewData = BackgroundRemoval.ApplyStep(previewData, step, CurrentPath);
            RenderPreview();
            status = Localization.T("RimAIPortrait.BackgroundRemoval.ColorSampled", colorHex);
            imagePressHandled = Event.current.type == EventType.MouseDown;
            Event.current.Use();
        }

        private void UpdateLastStep()
        {
            int last = steps.Count - 1;
            BackgroundRemovalStep step = steps[last];
            Color32 color;
            if (BackgroundRemoval.TryParseHex(colorHex, out color)) step.ColorHex = colorHex;
            step.Sensitivity = sensitivity;
            step.ErosionPixels = erosionPixels;
            step.RemoveEnclosed = removeEnclosed;
            step.EnclosedCleanup = enclosedCleanup;
            steps[last] = step;
            redoHistory.Clear();
        }

        private void RestoreOptions(BackgroundRemovalStep step)
        {
            colorHex = step.ColorHex;
            sensitivity = step.Sensitivity;
            erosionPixels = step.ErosionPixels;
            removeEnclosed = step.RemoveEnclosed;
            enclosedCleanup = step.EnclosedCleanup;
        }

        private void Undo()
        {
            if (steps.Count == 0) return;
            int last = steps.Count - 1;
            redoHistory.Add(steps[last]);
            steps.RemoveAt(last);
            if (steps.Count > 0) RestoreOptions(steps[steps.Count - 1]);
            RefreshPreview();
            status = Localization.T("RimAIPortrait.BackgroundRemoval.Undone");
        }

        private void Redo()
        {
            if (redoHistory.Count == 0) return;
            int last = redoHistory.Count - 1;
            BackgroundRemovalStep step = redoHistory[last];
            redoHistory.RemoveAt(last);
            steps.Add(step);
            RestoreOptions(step);
            RefreshPreview();
            status = Localization.T("RimAIPortrait.BackgroundRemoval.Redone");
        }

        private static int BottomAlignmentOffset(Texture2D texture)
        {
            Color32[] pixels = texture.GetPixels32();
            for (int y = 0; y < texture.height; y++)
                for (int x = 0; x < texture.width; x++)
                    if (pixels[y * texture.width + x].a != 0) return y;
            return 0;
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
            closed = true;
            ClearLoadedImage();
            base.PostClose();
        }
    }
}
