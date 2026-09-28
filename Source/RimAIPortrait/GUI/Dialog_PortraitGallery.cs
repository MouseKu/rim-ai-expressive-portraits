// Contains the implementation for Dialog_PortraitGallery.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Diagnostics;
using UnityEngine;
using Verse;

namespace RimAIPortrait
{
    /// <summary>Describes the behavior of this type or member.</summary>
    public sealed class Dialog_PortraitGallery : Window
    {
        private readonly Pawn pawn;
        private readonly HashSet<string> selected = new HashSet<string>();
        private string selectionAnchor;
        private readonly Dictionary<string, Texture2D> textures = new Dictionary<string, Texture2D>();
        private readonly Dictionary<string, int> textureUse = new Dictionary<string, int>();
        private int textureUseClock;
        private const int TextureCacheLimit = 64;
        private Vector2 scroll;
        private string[] files;
        private string galleryRoot;
        private string extraFeatures;
        private string basePromptOverride;
        private AiProvider basePromptProvider;
        private Texture2D debugSprite;
        private byte[] debugSpriteSource;
        private PortraitState targetState = PortraitState.Normal;
        private string status = Localization.T("RimAIPortrait.Gallery.InitialStatus");
        private bool checkedFirstHelp;
        private const string BasePromptControl = "RimAIPortrait.Gallery.BasePrompt";
        private const string ExtraFeaturesControl = "RimAIPortrait.Gallery.ExtraFeatures";
        public override Vector2 InitialSize => new Vector2(900f, 980f);
        /// <summary>Describes the behavior of this type or member.</summary>
        public Dialog_PortraitGallery(Pawn pawn) { this.pawn = pawn; extraFeatures = PortraitData.ExtraFeatures(pawn); basePromptProvider = RimAIPortraitMod.Settings.Provider; LoadBasePrompt(); doCloseX = true; absorbInputAroundWindow = true; RefreshFiles(); }

        /// <summary>Describes the behavior of this type or member.</summary>
        public override void DoWindowContents(Rect rect)
        {
            if (!string.Equals(galleryRoot, PortraitData.Root, StringComparison.OrdinalIgnoreCase))
            {
                selected.Clear();
                selectionAnchor = null;
                RefreshFiles(true);
            }
            if (!checkedFirstHelp)
            {
                checkedFirstHelp = true;
                if (!RimAIPortraitMod.Settings.PortraitHelpShown)
                {
                    RimAIPortraitMod.Settings.PortraitHelpShown = true;
                    RimAIPortraitMod.SaveSettings();
                    Find.WindowStack.Add(new Dialog_PortraitHelp());
                }
            }
            if (basePromptProvider != RimAIPortraitMod.Settings.Provider)
            {
                basePromptProvider = RimAIPortraitMod.Settings.Provider;
                LoadBasePrompt();
            }
            bool emotionChangesAvailable = RimAIPortraitMod.Settings.EnableEmotionChanges && !pawn.RaceProps.Animal;
            if (!emotionChangesAvailable || !RimAIPortraitMod.Settings.IsStateEnabled(targetState))
                targetState = PortraitState.Normal;
            HandleShortcuts(emotionChangesAvailable);

            Rect openFolderButton = new Rect(0f, 0f, 95f, 32f);
            if (Widgets.ButtonText(openFolderButton, Localization.T("RimAIPortrait.Common.OpenFolder")))
            {
                try { string folder = PortraitData.PawnFolder(pawn); Directory.CreateDirectory(folder); Process.Start(folder); }
                catch (Exception exception) { status = Localization.T("RimAIPortrait.Error.OpenFolder", exception.Message); }
            }
            Tip(openFolderButton, "OpenFolder");
            Rect refreshButton = new Rect(105f, 0f, 95f, 32f);
            if (Widgets.ButtonText(refreshButton, Localization.T("RimAIPortrait.Common.Refresh")))
            {
                RefreshFiles(true);
                status = Localization.T("RimAIPortrait.Gallery.Refreshed");
            }
            Tip(refreshButton, "Refresh");
            Widgets.Label(new Rect(212f, 4f, rect.width - 212f, 28f),
                Localization.T("RimAIPortrait.Gallery.SaveFolder", PortraitData.PawnFolder(pawn)));

            float firstWidth = (rect.width - 30f) / 4f;
            Rect generateBaseButton = new Rect(0f, 45f, firstWidth, 34f);
            if (Widgets.ButtonText(generateBaseButton, Localization.T("RimAIPortrait.Gallery.GenerateBase")))
            {
                SaveRequestFields();
                PortraitRuntime.EnsureCreated();
                status = Localization.T("RimAIPortrait.Status.GeneratingBase");
                PortraitRuntime.Instance.GenerateNormal(pawn, x => { status = x; RefreshFiles(); });
            }
            Tip(generateBaseButton, "GenerateBase");
            Rect regenerateButton = new Rect(firstWidth + 10f, 45f, firstWidth, 34f);
            if (Widgets.ButtonText(regenerateButton,
                Localization.T("RimAIPortrait.Gallery.RegenerateSelected")))
            {
                if (selected.Count != 1) status = Localization.T("RimAIPortrait.Gallery.SelectOneToRegenerate");
                else
                {
                    SaveRequestFields();
                    PortraitRuntime.EnsureCreated();
                    status = Localization.T("RimAIPortrait.Status.RegeneratingSelected");
                    PortraitRuntime.Instance.GenerateNormalFromReference(pawn, selected.First(),
                        x => { status = x; RefreshFiles(); });
                }
            }
            Tip(regenerateButton, "RegenerateSelected");
            Rect emotionRect = new Rect((firstWidth + 10f) * 2f, 45f, firstWidth, 34f);
            if (Widgets.ButtonText(emotionRect, Localization.T("RimAIPortrait.Gallery.GenerateEmotionsFromBase"),
                true, true, emotionChangesAvailable))
            {
                string basePath = PortraitData.ExistingPathFor(pawn, PortraitState.Normal);
                if (!File.Exists(basePath)) status = Localization.T("RimAIPortrait.Gallery.BaseRequired");
                else
                {
                    SaveRequestFields();
                    PortraitRuntime.EnsureCreated();
                    Find.WindowStack.Add(new Dialog_EmotionGeneration(pawn, new[] { basePath }, RefreshFiles));
                }
            }
            if (!emotionChangesAvailable && RimAIPortraitMod.Settings.EnableEmotionChanges)
                TooltipHandler.TipRegion(emotionRect, Localization.T("RimAIPortrait.Gallery.UnavailableForAnimals"));
            else Tip(emotionRect, "GenerateEmotions");
            Rect removeBackgroundButton = new Rect((firstWidth + 10f) * 3f, 45f, firstWidth, 34f);
            if (Widgets.ButtonText(removeBackgroundButton,
                Localization.T("RimAIPortrait.Gallery.RemoveBackground")))
            {
                if (selected.Count == 0)
                    status = Localization.T("RimAIPortrait.Gallery.SelectImagesToRemoveBackground");
                else
                {
                    string[] selectedPaths = selected.OrderBy(path => path, StringComparer.OrdinalIgnoreCase).ToArray();
                    Find.WindowStack.Add(new Dialog_BackgroundRemoval(selectedPaths, (succeeded, failed) =>
                    {
                        foreach (string path in selectedPaths) DropTexture(path);
                        RefreshFiles();
                        status = Localization.T("RimAIPortrait.Gallery.BackgroundRemovalComplete", succeeded, failed);
                    }));
                }
            }
            Tip(removeBackgroundButton, "RemoveBackground");

            float actionWidth = (rect.width - 40f) / 5f;
            Rect confirmButton = new Rect(0f, 88f, actionWidth, 32f);
            if (Widgets.ButtonText(confirmButton,
                Localization.T("RimAIPortrait.Gallery.AssignEmotion")))
                AssignSelected();
            Tip(confirmButton, "Confirm");
            Rect clearSelectionButton = new Rect(actionWidth + 10f, 88f, actionWidth, 32f);
            if (Widgets.ButtonText(clearSelectionButton,
                Localization.T("RimAIPortrait.Common.ClearSelection")))
            {
                selected.Clear();
                selectionAnchor = null;
            }
            Tip(clearSelectionButton, "ClearSelection");
            Rect resetPortraitsButton = new Rect((actionWidth + 10f) * 2f, 88f, actionWidth, 32f);
            if (Widgets.ButtonText(resetPortraitsButton,
                Localization.T("RimAIPortrait.Common.ResetPortraits")))
            {
                Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation(
                    Localization.T("RimAIPortrait.Gallery.ConfirmResetPortraits"), () =>
                {
                    foreach (PortraitState state in Enum.GetValues(typeof(PortraitState)))
                        DropTexture(PortraitData.PathFor(pawn, state));
                    PortraitData.ResetPortraits(pawn);
                    InspectPanePatch.ShowStateImmediately(pawn, PortraitState.Normal);
                    status = Localization.T("RimAIPortrait.Gallery.PortraitsReset");
                }));
            }
            Tip(resetPortraitsButton, "ResetPortraits");
            Rect deleteButton = new Rect((actionWidth + 10f) * 3f, 88f, actionWidth, 32f);
            if (Widgets.ButtonText(deleteButton,
                Localization.T("RimAIPortrait.Common.DeleteImage")))
                DeleteSelected();
            Tip(deleteButton, "DeleteImage");
            Rect previewButton = new Rect((actionWidth + 10f) * 4f, 88f, actionWidth, 32f);
            if (Widgets.ButtonText(previewButton,
                Localization.T("RimAIPortrait.Gallery.LargePreview")))
                PreviewSelected();
            Tip(previewButton, "LargePreview");

            Widgets.Label(new Rect(0f, 128f, 85f, 26f), Localization.T("RimAIPortrait.Gallery.BaseLabel"));
            string providerName = basePromptProvider == AiProvider.Google ? "Google"
                : basePromptProvider == AiProvider.NovelAI ? "NovelAI"
                : basePromptProvider == AiProvider.LocalComfyUI ? "ComfyUI" : "OpenAI";
            Widgets.Label(new Rect(0f, 153f, 85f, 26f),
                Localization.T("RimAIPortrait.Gallery.BaseProviderLabel", providerName));
            Rect baseArea = new Rect(90f, 128f, rect.width - 215f, 54f);
            GUI.SetNextControlName(BasePromptControl);
            basePromptOverride = Widgets.TextArea(baseArea, basePromptOverride ?? "");
            TooltipHandler.TipRegion(baseArea, Localization.T("RimAIPortrait.Gallery.BasePromptTip"));
            Rect saveBaseButton = new Rect(rect.width - 115f, 128f, 115f, 26f);
            if (Widgets.ButtonText(saveBaseButton,
                Localization.T("RimAIPortrait.Gallery.SaveBase")))
                status = Localization.T(PortraitData.SaveBasePrompt(pawn, basePromptProvider, basePromptOverride)
                    ? "RimAIPortrait.Gallery.BaseSaved" : "RimAIPortrait.Gallery.BaseSaveFailed");
            Tip(saveBaseButton, "SaveBase");
            Rect resetBaseButton = new Rect(rect.width - 115f, 156f, 115f, 26f);
            if (Widgets.ButtonText(resetBaseButton,
                Localization.T("RimAIPortrait.Gallery.ResetBase")))
                ResetBasePrompt();
            Tip(resetBaseButton, "ResetBase");

            Widgets.Label(new Rect(0f, 200f, 85f, 28f), Localization.T("RimAIPortrait.Gallery.ExtraLabel"));
            Rect extraArea = new Rect(90f, 191f, rect.width - 215f, 54f);
            GUI.SetNextControlName(ExtraFeaturesControl);
            extraFeatures = Widgets.TextArea(extraArea, extraFeatures ?? "");
            TooltipHandler.TipRegion(extraArea, Localization.T("RimAIPortrait.Gallery.ExtraFeaturesTip"));
            Rect saveExtraButton = new Rect(rect.width - 115f, 198f, 115f, 34f);
            if (Widgets.ButtonText(saveExtraButton,
                Localization.T("RimAIPortrait.Common.Save")))
                status = Localization.T(SaveExtraFeatures()
                    ? "RimAIPortrait.Gallery.ExtraSaved" : "RimAIPortrait.Gallery.ExtraSaveFailed");
            Tip(saveExtraButton, "SaveExtra");

            Widgets.Label(new Rect(0f, 256f, rect.width, 42f), status ?? "");
            float panelTop = 302f;
            float panelHeight = rect.height - panelTop;
            float assignedWidth = Mathf.Min(365f, rect.width * .42f);
            DrawAssignedState(new Rect(0f, panelTop, assignedWidth, panelHeight), emotionChangesAvailable);
            DrawGallery(new Rect(assignedWidth + 10f, panelTop,
                rect.width - assignedWidth - 10f, panelHeight));
        }

        private void HandleShortcuts(bool emotionChangesAvailable)
        {
            Event current = Event.current;
            if (current.type != EventType.KeyDown || current.alt || current.control || current.command || current.shift)
                return;
            string focusedControl = GUI.GetNameOfFocusedControl();
            if (focusedControl == BasePromptControl || focusedControl == ExtraFeaturesControl)
                return;

            switch (current.keyCode)
            {
                case KeyCode.Delete:
                    DeleteSelected();
                    break;
                case KeyCode.P:
                    PreviewSelected();
                    break;
                case KeyCode.LeftArrow:
                    ChangeState(-1, emotionChangesAvailable);
                    break;
                case KeyCode.RightArrow:
                    ChangeState(1, emotionChangesAvailable);
                    break;
                default:
                    return;
            }
            current.Use();
        }

        private void AssignSelected()
        {
            if (selected.Count != 1)
            {
                status = Localization.T("RimAIPortrait.Gallery.SelectOneToAssign");
                return;
            }
            PortraitData.SetActive(pawn, targetState, selected.First());
            DropTexture(PortraitData.PathFor(pawn, targetState));
            InspectPanePatch.ShowStateImmediately(pawn, targetState);
            status = Localization.T("RimAIPortrait.Gallery.Assigned", StateLabel(targetState));
        }

        private void DeleteSelected()
        {
            if (selected.Count == 0) return;
            string[] deleting = selected.ToArray();
            Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation(
                Localization.T("RimAIPortrait.Gallery.ConfirmDelete", deleting.Length), () =>
            {
                PortraitData.Delete(pawn, deleting);
                selected.Clear();
                selectionAnchor = null;
                RefreshFiles(true);
            }));
        }

        private void PreviewSelected()
        {
            if (selected.Count != 1) status = Localization.T("RimAIPortrait.Gallery.SelectOneToPreview");
            else Find.WindowStack.Add(new Dialog_ImagePreview(selected.First()));
        }

        private PortraitState[] AvailableStates(bool emotionChangesAvailable)
        {
            return emotionChangesAvailable
                ? Enum.GetValues(typeof(PortraitState)).Cast<PortraitState>()
                    .Where(RimAIPortraitMod.Settings.IsStateEnabled).ToArray()
                : new[] { PortraitState.Normal };
        }

        private void ChangeState(int direction, bool emotionChangesAvailable)
        {
            PortraitState[] states = AvailableStates(emotionChangesAvailable);
            int nextIndex = Array.IndexOf(states, targetState) + direction;
            if (nextIndex >= 0 && nextIndex < states.Length)
                targetState = states[nextIndex];
        }

        private static void Tip(Rect area, string name)
        {
            TooltipHandler.TipRegion(area, Localization.T("RimAIPortrait.Gallery.Tip." + name));
        }

        private void DrawAssignedState(Rect area, bool emotionChangesAvailable)
        {
            Widgets.DrawMenuSection(area);
            string path = PortraitData.ExistingPathFor(pawn, targetState);
            Rect imageArea = new Rect(area.x + 8f, area.y + 8f, area.width - 16f, area.height - 55f);
            if (RimAIPortraitMod.Settings.DebugMode)
                imageArea = new Rect(imageArea.x, imageArea.y + 94f, imageArea.width, imageArea.height - 94f);
            if (File.Exists(path))
            {
                Texture2D texture = Load(path);
                if (texture != null)
                {
                    GUI.DrawTexture(imageArea, texture, ScaleMode.ScaleToFit, true);
                    TooltipHandler.TipRegion(imageArea,
                        Localization.T("RimAIPortrait.Gallery.Tip.AssignedPortrait", PortraitStateNames.Display(targetState)));
                }
            }
            if (RimAIPortraitMod.Settings.DebugMode)
                DrawDebugSprite(new Rect(area.x + 8f, area.y + 8f, 200f, 84f));
            PortraitState[] states = AvailableStates(emotionChangesAvailable);
            int stateIndex = Array.IndexOf(states, targetState);
            Rect previousButton = new Rect(area.x + 8f, area.yMax - 39f, 42f, 32f);
            if (Widgets.ButtonText(previousButton, "<", true, true, stateIndex > 0))
                ChangeState(-1, emotionChangesAvailable);
            Tip(previousButton, "PreviousState");
            Text.Anchor = TextAnchor.MiddleCenter;
            Widgets.Label(new Rect(area.x + 55f, area.yMax - 39f, area.width - 110f, 32f),
                PortraitStateNames.Display(targetState));
            Text.Anchor = TextAnchor.UpperLeft;
            Rect nextButton = new Rect(area.xMax - 50f, area.yMax - 39f, 42f, 32f);
            if (Widgets.ButtonText(nextButton, ">", true, true,
                stateIndex >= 0 && stateIndex < states.Length - 1))
                ChangeState(1, emotionChangesAvailable);
            Tip(nextButton, "NextState");
        }

        private void DrawGallery(Rect area)
        {
            Widgets.DrawMenuSection(area);
            float cell = 140f;
            float gap = 10f;
            int columns = Math.Max(1, (int)((area.width - 24f + gap) / (cell + gap)));
            float rowHeight = cell + gap;
            float contentHeight = Math.Max(area.height, ((files.Length + columns - 1) / columns) * rowHeight + 8f);
            Rect view = new Rect(0f, 0f, area.width - 20f, contentHeight);
            Widgets.BeginScrollView(area.ContractedBy(4f), ref scroll, view);
            int firstRow = Math.Max(0, (int)(scroll.y / rowHeight));
            int lastRow = Math.Min((files.Length + columns - 1) / columns,
                (int)Math.Ceiling((scroll.y + area.height) / rowHeight) + 1);
            for (int i = firstRow * columns; i < Math.Min(files.Length, lastRow * columns); i++)
            {
                string file = files[i];
                float x = (i % columns) * (cell + gap) + 4f;
                float y = (i / columns) * rowHeight + 4f;
                Rect box = new Rect(x, y, cell, cell);
                if (selected.Contains(file))
                    Widgets.DrawBoxSolid(box.ExpandedBy(4f), new Color(.25f, .65f, 1f, .9f));
                Texture2D texture = Load(file);
                if (texture != null) GUI.DrawTexture(box, texture, ScaleMode.ScaleToFit, true);
                Event current = Event.current;
                if (current.type == EventType.MouseDown && current.button == 0 && current.clickCount == 2
                    && !current.shift && !current.control && !current.alt && !current.command
                    && box.Contains(current.mousePosition))
                {
                    SelectGalleryImage(i, false, false);
                    AssignSelected();
                    current.Use();
                }
                bool shift = Event.current.shift;
                bool control = Event.current.control;
                if (Widgets.ButtonInvisible(box))
                    SelectGalleryImage(i, shift, control);
                TooltipHandler.TipRegion(box, Localization.T("RimAIPortrait.Gallery.Tip.GalleryImage") + "\n" + file);
            }
            Widgets.EndScrollView();
        }
        private void SelectGalleryImage(int index, bool shift, bool control)
        {
            string file = files[index];
            int anchorIndex = selectionAnchor == null ? -1 : Array.IndexOf(files, selectionAnchor);
            if (shift && anchorIndex >= 0)
            {
                if (!control) selected.Clear();
                int start = Math.Min(anchorIndex, index);
                int end = Math.Max(anchorIndex, index);
                for (int i = start; i <= end; i++) selected.Add(files[i]);
                return;
            }

            if (control)
            {
                if (!selected.Add(file)) selected.Remove(file);
            }
            else
            {
                selected.Clear();
                selected.Add(file);
            }
            selectionAnchor = file;
        }
        /// <summary>Describes the behavior of this type or member.</summary>
        private void DrawDebugSprite(Rect area)
        {
            Widgets.Label(new Rect(area.x, area.y, area.width, 22), Localization.T("RimAIPortrait.Debug.PawnSprite"));
            if (!RimAIPortraitMod.Settings.IncludeSprite)
            {
                Widgets.Label(new Rect(area.x, area.y + 27, area.width, 45), Localization.T("RimAIPortrait.Debug.SpriteDisabled"));
                return;
            }
            if (PawnCapture.LastPawnThingId != pawn.ThingID || PawnCapture.LastCapture == null) PawnCapture.Capture(pawn);
            byte[] bytes = PawnCapture.LastCapture;
            if (bytes == null) { Widgets.Label(new Rect(area.x, area.y + 27, area.width, 45), Localization.T("RimAIPortrait.Debug.CaptureFailed")); return; }
            if (!ReferenceEquals(debugSpriteSource, bytes))
            {
                if (debugSprite != null) UnityEngine.Object.Destroy(debugSprite);
                debugSprite = new Texture2D(2, 2, TextureFormat.ARGB32, false);
                if (!debugSprite.LoadImage(bytes)) { UnityEngine.Object.Destroy(debugSprite); debugSprite = null; }
                debugSpriteSource = bytes;
            }
            Rect image = new Rect(area.x + 105, area.y, 84, 84);
            if (debugSprite != null) GUI.DrawTexture(image, debugSprite, ScaleMode.ScaleToFit, true);
            TooltipHandler.TipRegion(image, Localization.T("RimAIPortrait.Debug.LastPawnPng"));
        }
        /// <summary>Describes the behavior of this type or member.</summary>
        private static string StateLabel(PortraitState state)
        {
            return PortraitStateNames.Label(state);
        }
        /// <summary>Describes the behavior of this type or member.</summary>
        private Texture2D Load(string path)
        {
            textureUse[path] = ++textureUseClock;
            if (textures.TryGetValue(path, out var t)) return t;
            try
            {
                t = new Texture2D(2, 2, TextureFormat.ARGB32, false);
                if (!t.LoadImage(File.ReadAllBytes(path))) { UnityEngine.Object.Destroy(t); return null; }
                textures[path] = t;
                TrimTextureCache(path);
                return t;
            }
            catch { return null; }
        }
        private void TrimTextureCache(string keepPath)
        {
            while (textures.Count > TextureCacheLimit)
            {
                string oldestPath = null;
                int oldestUse = int.MaxValue;
                foreach (KeyValuePair<string, Texture2D> item in textures)
                {
                    int used;
                    if (item.Key != keepPath && (!textureUse.TryGetValue(item.Key, out used) || used < oldestUse))
                    {
                        oldestPath = item.Key;
                        oldestUse = used;
                    }
                }
                if (oldestPath == null) break;
                DropTexture(oldestPath);
            }
        }
        /// <summary>Describes the behavior of this type or member.</summary>
        private void DropTexture(string path) { if (textures.TryGetValue(path, out var t)) UnityEngine.Object.Destroy(t); textures.Remove(path); textureUse.Remove(path); }
        /// <summary>Describes the behavior of this type or member.</summary>
        private bool SaveExtraFeatures() => PortraitData.SaveExtraFeatures(pawn, extraFeatures);
        private void ResetBasePrompt()
        {
            AiProvider provider = basePromptProvider;
            string fileName = provider == AiProvider.Google ? "BasePrompt(Gemini).md"
                : provider == AiProvider.NovelAI ? "BasePrompt(NAI).md" : "BasePrompt.md";
            try
            {
                string path = Path.Combine(RimAIPortraitMod.ModPack.RootDir, "Prompts", fileName);
                string prompt = File.ReadAllText(path).Trim();
                if (prompt.NullOrEmpty()) throw new InvalidDataException(fileName + " is empty.");
                basePromptOverride = prompt;
                status = Localization.T("RimAIPortrait.Gallery.BaseReset");
            }
            catch (Exception exception)
            {
                status = Localization.T("RimAIPortrait.Gallery.BaseResetFailed", exception.Message);
            }
        }
        /// <summary>Describes the behavior of this type or member.</summary>
        private void LoadBasePrompt()
        {
            basePromptOverride = PortraitData.BasePrompt(pawn, basePromptProvider);
            if (basePromptOverride.NullOrEmpty())
                basePromptOverride = PortraitPrompt.BaseInstructions(RimAIPortraitMod.Settings);
        }

        private bool SaveRequestFields() => SaveExtraFeatures() && PortraitData.SaveBasePrompt(pawn, basePromptProvider, basePromptOverride);
        /// <summary>Describes the behavior of this type or member.</summary>
        private void RefreshFiles()
        {
            RefreshFiles(false);
            foreach (PortraitState state in Enum.GetValues(typeof(PortraitState)))
                DropTexture(PortraitData.PathFor(pawn, state));
        }
        /// <summary>Describes the behavior of this type or member.</summary>
        private void RefreshFiles(bool clearTextures)
        {
            galleryRoot = PortraitData.Root;
            PortraitData.RefreshExternalFiles(pawn);
            if (clearTextures) { foreach (var texture in textures.Values) UnityEngine.Object.Destroy(texture); textures.Clear(); textureUse.Clear(); }
            files = PortraitData.Gallery(pawn);
            selected.RemoveWhere(path => !File.Exists(path));
            if (selectionAnchor != null && Array.IndexOf(files, selectionAnchor) < 0)
                selectionAnchor = null;
        }
        /// <summary>Describes the behavior of this type or member.</summary>
        public override void PostClose() { SaveRequestFields(); foreach (var t in textures.Values) UnityEngine.Object.Destroy(t); if (debugSprite != null) UnityEngine.Object.Destroy(debugSprite); base.PostClose(); }
    }
}
