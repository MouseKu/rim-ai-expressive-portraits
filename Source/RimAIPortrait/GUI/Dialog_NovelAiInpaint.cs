using System;
using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace RimAIPortrait
{
    /// <summary>Selects an expression inpaint mask for the active provider.</summary>
    internal sealed class Dialog_NovelAiInpaint : Window
    {
        private struct MaskOperation
        {
            public Rect Area;
            public bool Exclude;
        }

        private readonly byte[] imageBytes;
        private readonly PortraitState state;
        private readonly AiProvider provider;
        private readonly Action<byte[], byte[], float, int, string> completed;
        private readonly List<MaskOperation> selections = new List<MaskOperation>();
        private float strength;
        private string extraTag;
        private Texture2D preview;
        private Texture2D maskOverlay;
        private Vector2 dragStart;
        private Rect dragging;
        private bool isDragging;
        private int dragButton;
        private bool hasSelectedPixels;
        private bool confirmed;
        private string error;

        public override Vector2 InitialSize => new Vector2(880f, 900f);

        public Dialog_NovelAiInpaint(byte[] imageBytes, PortraitState state, AiProvider provider, float initialStrength, string initialExtraTag,
            Action<byte[], byte[], float, int, string> completed)
        {
            this.imageBytes = imageBytes;
            this.state = state;
            this.provider = provider;
            strength = Mathf.Clamp(initialStrength, .1f, 1f);
            extraTag = initialExtraTag ?? "";
            this.completed = completed;
            doCloseX = true;
            absorbInputAroundWindow = true;
            closeOnClickedOutside = false;
        }

        public override void PreOpen()
        {
            base.PreOpen();
            try
            {
                preview = new Texture2D(2, 2, TextureFormat.ARGB32, false);
                if (!preview.LoadImage(imageBytes)) throw new ArgumentException(Localization.T("RimAIPortrait.Inpaint.InvalidImage"));
                maskOverlay = new Texture2D(preview.width, preview.height, TextureFormat.ARGB32, false);
                RefreshSelection();
            }
            catch (Exception exception)
            {
                if (preview != null) UnityEngine.Object.Destroy(preview);
                if (maskOverlay != null) UnityEngine.Object.Destroy(maskOverlay);
                preview = null;
                maskOverlay = null;
                error = exception.Message;
            }
        }

        public override void DoWindowContents(Rect rect)
        {
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(0f, 0f, rect.width, 34f), Localization.T("RimAIPortrait.Inpaint.Title",
                provider == AiProvider.LocalComfyUI ? "ComfyUI" : provider.ToString()));
            Text.Font = GameFont.Small;
            Widgets.Label(new Rect(0f, 38f, rect.width, 28f), Localization.T("RimAIPortrait.Inpaint.State", PortraitStateNames.Label(state)));
            if (provider == AiProvider.NovelAI)
            {
                Widgets.Label(new Rect(0f, 66f, rect.width, 28f), Localization.T("RimAIPortrait.Inpaint.Strength", strength.ToString("0.00")));
                strength = Widgets.HorizontalSlider(new Rect(0f, 94f, rect.width, 24f), strength, .1f, 1f);
                TooltipHandler.TipRegion(new Rect(0f, 66f, rect.width, 52f),
                    Localization.T("RimAIPortrait.Inpaint.StrengthTip"));
            }
            if (provider == AiProvider.NovelAI || provider == AiProvider.LocalComfyUI)
            {
                Widgets.Label(new Rect(0f, 122f, rect.width, 24f), Localization.T("RimAIPortrait.Inpaint.ExtraTag"));
                extraTag = Widgets.TextField(new Rect(0f, 148f, rect.width, 30f), extraTag);
                if (string.IsNullOrEmpty(extraTag) && Event.current.type == EventType.Repaint)
                {
                    Color previousColor = GUI.color;
                    GUI.color = new Color(.55f, .55f, .55f, 1f);
                    Widgets.Label(new Rect(6f, 153f, rect.width - 12f, 24f),
                        Localization.T("RimAIPortrait.Inpaint.ExtraTagPlaceholder"));
                    GUI.color = previousColor;
                }
                TooltipHandler.TipRegion(new Rect(0f, 122f, rect.width, 56f),
                    Localization.T("RimAIPortrait.Inpaint.ExtraTagTip"));
            }
            Widgets.Label(new Rect(0f, 184f, rect.width, 44f), Localization.T(provider == AiProvider.LocalComfyUI
                ? "RimAIPortrait.Inpaint.ComfyInstructions"
                : state == PortraitState.Sleep ? "RimAIPortrait.Inpaint.SleepInstructions" : "RimAIPortrait.Inpaint.Instructions"));
            Rect area = new Rect(0f, 232f, rect.width, rect.height - 294f);
            Widgets.DrawMenuSection(area);
            if (preview == null)
                Widgets.Label(area.ContractedBy(12f), error ?? Localization.T("RimAIPortrait.Inpaint.InvalidImage"));
            else
            {
                Rect imageRect = Fit(area.ContractedBy(8f), preview.width, preview.height);
                GUI.DrawTexture(imageRect, preview);
                GUI.DrawTexture(imageRect, maskOverlay);
                HandleDrag(imageRect);
                foreach (MaskOperation selection in selections)
                    DrawSelection(ToScreen(imageRect, selection.Area), selection.Exclude, false);
                if (isDragging) DrawSelection(dragging, dragButton == 1, true);
            }
            float buttonY = rect.height - 48f;
            if (Widgets.ButtonText(new Rect(rect.width - 365f, buttonY, 110f, 38f), Localization.T("RimAIPortrait.Inpaint.Undo"), true, true, selections.Count > 0))
            {
                selections.RemoveAt(selections.Count - 1);
                RefreshSelection();
            }
            if (Widgets.ButtonText(new Rect(rect.width - 250f, buttonY, 115f, 38f), Localization.T("RimAIPortrait.Inpaint.Clear"), true, true, selections.Count > 0))
            {
                selections.Clear();
                RefreshSelection();
            }
            if (Widgets.ButtonText(new Rect(rect.width - 125f, buttonY, 125f, 38f), Localization.T("RimAIPortrait.Inpaint.Confirm"), true, true,
                preview != null && (hasSelectedPixels || provider == AiProvider.LocalComfyUI)))
            {
                byte[] mask = hasSelectedPixels ? BuildMask() : null;
                byte[] requestMask = null;
                if (mask != null && provider == AiProvider.NovelAI)
                    requestMask = selections.Exists(selection => selection.Exclude)
                        ? BuildMask(true) : NovelAiInpaintComposite.ExpandMask(mask);
                confirmed = true;
                completed(mask, requestMask, strength, selections.Count, extraTag);
                Close();
            }
        }

        private void HandleDrag(Rect imageRect)
        {
            Event current = Event.current;
            if (current.type == EventType.MouseDown && (current.button == 0 || current.button == 1)
                && imageRect.Contains(current.mousePosition))
            {
                dragStart = current.mousePosition;
                dragButton = current.button;
                dragging = new Rect(dragStart.x, dragStart.y, 0f, 0f);
                isDragging = true;
                current.Use();
            }
            else if (isDragging && current.type == EventType.MouseDrag)
            {
                dragging = Between(dragStart, Clamp(current.mousePosition, imageRect));
                current.Use();
            }
            else if (isDragging && current.type == EventType.MouseUp && current.button == dragButton)
            {
                dragging = Between(dragStart, Clamp(current.mousePosition, imageRect));
                isDragging = false;
                if (dragging.width >= 3f && dragging.height >= 3f)
                {
                    selections.Add(new MaskOperation {
                        Area = new Rect((dragging.x - imageRect.x) / imageRect.width,
                            (dragging.y - imageRect.y) / imageRect.height,
                            dragging.width / imageRect.width, dragging.height / imageRect.height),
                        Exclude = dragButton == 1
                    });
                    RefreshSelection();
                }
                current.Use();
            }
        }

        private void RefreshSelection()
        {
            if (preview == null) { hasSelectedPixels = false; return; }
            Color32[] pixels = MaskPixels(false);
            hasSelectedPixels = false;
            for (int index = 0; index < pixels.Length; index++)
            {
                bool selected = pixels[index].r >= 128;
                if (selected) hasSelectedPixels = true;
                pixels[index] = selected ? new Color32(76, 179, 255, 85) : new Color32(0, 0, 0, 0);
            }
            maskOverlay.SetPixels32(pixels);
            maskOverlay.Apply(false, false);
        }

        private Color32[] MaskPixels(bool expandIncludedAreas)
        {
            int width = preview.width;
            int height = preview.height;
            int padding = expandIncludedAreas ? Mathf.Clamp(Mathf.RoundToInt(Mathf.Min(width, height) * .05f), 24, 64) : 0;
            var pixels = new Color32[width * height];
            for (int i = 0; i < pixels.Length; i++) pixels[i] = new Color32(0, 0, 0, 255);
            foreach (MaskOperation operation in selections)
            {
                Rect selected = operation.Area;
                int grow = operation.Exclude ? 0 : padding;
                int left = Mathf.Clamp(Mathf.FloorToInt(selected.xMin * width) - grow, 0, width);
                int right = Mathf.Clamp(Mathf.CeilToInt(selected.xMax * width) + grow, 0, width);
                int bottom = Mathf.Clamp(height - Mathf.CeilToInt(selected.yMax * height) - grow, 0, height);
                int top = Mathf.Clamp(height - Mathf.FloorToInt(selected.yMin * height) + grow, 0, height);
                for (int y = bottom; y < top; y++)
                    for (int x = left; x < right; x++)
                        pixels[y * width + x] = operation.Exclude
                            ? new Color32(0, 0, 0, 255) : new Color32(255, 255, 255, 255);
            }
            return pixels;
        }

        private byte[] BuildMask(bool expandIncludedAreas = false)
        {
            int width = preview.width;
            int height = preview.height;
            Color32[] pixels = MaskPixels(expandIncludedAreas);
            var texture = new Texture2D(width, height, TextureFormat.ARGB32, false);
            try
            {
                texture.SetPixels32(pixels);
                texture.Apply(false, false);
                return texture.EncodeToPNG();
            }
            finally { UnityEngine.Object.Destroy(texture); }
        }

        private static Rect Fit(Rect area, int width, int height)
        {
            float scale = Mathf.Min(area.width / width, area.height / height);
            float fittedWidth = width * scale;
            float fittedHeight = height * scale;
            return new Rect(area.x + (area.width - fittedWidth) / 2f,
                area.y + (area.height - fittedHeight) / 2f, fittedWidth, fittedHeight);
        }

        private static Vector2 Clamp(Vector2 point, Rect area)
        {
            return new Vector2(Mathf.Clamp(point.x, area.xMin, area.xMax), Mathf.Clamp(point.y, area.yMin, area.yMax));
        }

        private static Rect Between(Vector2 first, Vector2 second)
        {
            return Rect.MinMaxRect(Mathf.Min(first.x, second.x), Mathf.Min(first.y, second.y),
                Mathf.Max(first.x, second.x), Mathf.Max(first.y, second.y));
        }

        private static Rect ToScreen(Rect area, Rect selection)
        {
            return new Rect(area.x + selection.x * area.width, area.y + selection.y * area.height,
                selection.width * area.width, selection.height * area.height);
        }

        private static void DrawSelection(Rect area, bool exclude, bool dragging)
        {
            Color previous = GUI.color;
            Color color = exclude ? new Color(1f, 0.35f, 0.3f, 1f) : new Color(0.3f, 0.7f, 1f, 1f);
            if (dragging)
            {
                GUI.color = new Color(color.r, color.g, color.b, 0.25f);
                GUI.DrawTexture(area, Texture2D.whiteTexture);
            }
            GUI.color = color;
            Widgets.DrawBox(area, 2);
            GUI.color = previous;
        }

        public override void PostClose()
        {
            if (preview != null) UnityEngine.Object.Destroy(preview);
            if (maskOverlay != null) UnityEngine.Object.Destroy(maskOverlay);
            preview = null;
            maskOverlay = null;
            if (!confirmed) completed(null, null, strength, 0, null);
            base.PostClose();
        }
    }
}
