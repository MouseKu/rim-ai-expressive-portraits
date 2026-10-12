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
    public sealed partial class Dialog_PortraitGallery : Window
    {
        private readonly Pawn pawn;
        private readonly HashSet<string> selected = new HashSet<string>();
        private string selectionAnchor;
        private Vector2 scroll;
        private string[] files;
        private string galleryRoot;
        private string extraFeatures;
        private ExtractedTags baseTags;
        private string positiveTagText;
        private string negativeTagText;
        private bool tagEdited;
        private bool generatingTag;
        private string basePrompt;
        private AiProvider basePromptProvider;
        private Texture2D debugSprite;
        private byte[] debugSpriteSource;
        private PortraitState targetState = PortraitState.Base;
        private bool showAll;
        private string status = Localization.T("RimAIPortrait.Gallery.InitialStatus");
        private bool checkedFirstHelp;
        private bool hadBaseImage;
        private bool v5HelpChecked;
        private bool baseHelpChecked;
        private bool expressionHelpChecked;
        private bool helpOpen;
        private bool galleryClosed;
        private const string BasePromptControl = "RimAIPortrait.Gallery.BasePrompt";
        private const string ExtraFeaturesControl = "RimAIPortrait.Gallery.ExtraFeatures";
        private const string PositiveTagControl = "RimAIPortrait.Gallery.PositiveTag";
        private const string NegativeTagControl = "RimAIPortrait.Gallery.NegativeTag";
        public override Vector2 InitialSize => PortraitWindowSizing.Fit(900f, 980f);
        /// <summary>Describes the behavior of this type or member.</summary>
        public Dialog_PortraitGallery(Pawn pawn) { this.pawn = pawn; extraFeatures = PortraitData.ExtraFeatures(pawn); baseTags = PortraitData.BaseTags(pawn); positiveTagText = baseTags?.PositiveText ?? ""; negativeTagText = baseTags?.NegativeText ?? ""; basePromptProvider = RimAIPortraitMod.Settings.Provider; LoadBasePrompt(); doCloseX = true; absorbInputAroundWindow = true; RefreshFiles(); hadBaseImage = HasBaseImage(); }

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
                if (!ShowNoticeOnGalleryOpen()) ShowNextHelp();
            }
            if (basePromptProvider != RimAIPortraitMod.Settings.Provider)
            {
                basePromptProvider = RimAIPortraitMod.Settings.Provider;
                LoadBasePrompt();
            }
            bool emotionChangesAvailable = RimAIPortraitMod.Settings.EnableEmotionChanges && !pawn.RaceProps.Animal;
            if (targetState != PortraitState.Base &&
                (!emotionChangesAvailable || !RimAIPortraitMod.Settings.IsStateEnabled(targetState)))
            {
                targetState = PortraitState.Base;
                UpdateVisibleSelection();
            }
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
            if (Widgets.ButtonText(generateBaseButton, Localization.T("RimAIPortrait.Gallery.GenerateBase"),
                true, true, PortraitRuntime.GenerationButtonReady))
            {
                PortraitRuntime.StartGenerationButtonCooldown();
                SaveRequestFields();
                PortraitRuntime.EnsureCreated();
                status = Localization.T("RimAIPortrait.Status.GeneratingBase");
                PortraitRuntime.Instance.GenerateNormal(pawn, x => { status = x; RefreshFiles(); });
            }
            Tip(generateBaseButton, "GenerateBase");
            Rect regenerateButton = new Rect(firstWidth + 10f, 45f, firstWidth, 34f);
            if (Widgets.ButtonText(regenerateButton,
                Localization.T("RimAIPortrait.Gallery.RegenerateSelected"),
                true, true, PortraitRuntime.GenerationButtonReady))
            {
                if (selected.Count != 1) status = Localization.T("RimAIPortrait.Gallery.SelectOneToRegenerate");
                else
                {
                    SaveRequestFields();
                    PortraitRuntime.EnsureCreated();
                    string referencePath = selected.First();
                    AiProvider provider = RimAIPortraitMod.Settings.RegenerateProvider;
                    if (provider == AiProvider.OpenAI || provider == AiProvider.Google)
                        Find.WindowStack.Add(new Dialog_PortraitRegenerate(referencePath, prompt =>
                        {
                            PortraitRuntime.StartGenerationButtonCooldown();
                            status = Localization.T("RimAIPortrait.Status.RegeneratingSelected");
                            PortraitRuntime.Instance.GenerateNormalFromReference(pawn, referencePath, prompt,
                                x => { status = x; RefreshFiles(); });
                        }));
                    else
                    {
                        PortraitRuntime.StartGenerationButtonCooldown();
                        status = Localization.T("RimAIPortrait.Status.RegeneratingSelected");
                        PortraitRuntime.Instance.GenerateNormalFromReference(pawn, referencePath, null,
                            x => { status = x; RefreshFiles(); });
                    }
                }
            }
            Tip(regenerateButton, "RegenerateSelected");
            Rect emotionRect = new Rect((firstWidth + 10f) * 2f, 45f, firstWidth, 34f);
            if (Widgets.ButtonText(emotionRect, Localization.T("RimAIPortrait.Gallery.GenerateEmotionsFromBase"),
                true, true, emotionChangesAvailable && PortraitRuntime.GenerationButtonReady))
            {
                string basePath = PortraitData.ExistingPathFor(pawn, PortraitState.Base);
                if (!File.Exists(basePath)) status = Localization.T("RimAIPortrait.Gallery.BaseRequired");
                else
                {
                    PortraitRuntime.StartGenerationButtonCooldown();
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
                bool editingAssignedPortrait = selected.Count == 0;
                PortraitState editedState = targetState;
                string[] editPaths = editingAssignedPortrait
                    ? new[] { PortraitData.ExistingPathFor(pawn, editedState) }
                    : selected.OrderBy(path => path, StringComparer.OrdinalIgnoreCase).ToArray();
                if (editingAssignedPortrait && !File.Exists(editPaths[0]))
                    status = Localization.T("RimAIPortrait.Gallery.SelectImagesToRemoveBackground");
                else
                {
                    Find.WindowStack.Add(new Dialog_BackgroundRemoval(editPaths, (succeeded, failed) =>
                    {
                        foreach (string path in editPaths) DropTexture(path);
                        if (editingAssignedPortrait && succeeded > 0)
                            PortraitData.RefreshEditedPortrait(pawn, editedState);
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
                    InspectPanePatch.ShowStateImmediately(pawn, PortraitState.Base);
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
                : ProviderSettingsUi.Label(basePromptProvider,
                    RimAIPortraitMod.Settings.EnableDeprecatedFeatures);
            string providerLabel = Localization.T("RimAIPortrait.Gallery.BaseProviderLabel", providerName);
            float baseLabelWidth = Mathf.Max(85f, Text.CalcSize(providerLabel).x + 8f);
            Widgets.Label(new Rect(0f, 153f, baseLabelWidth, 26f), providerLabel);
            Rect baseArea = new Rect(baseLabelWidth + 5f, 128f, rect.width - baseLabelWidth - 130f, 54f);
            GUI.SetNextControlName(BasePromptControl);
            basePrompt = Widgets.TextArea(baseArea, basePrompt ?? "");
            TooltipHandler.TipRegion(baseArea, Localization.T("RimAIPortrait.Gallery.BasePromptTip"));
            Rect saveBaseButton = new Rect(rect.width - 115f, 128f, 115f, 26f);
            if (Widgets.ButtonText(saveBaseButton,
                Localization.T("RimAIPortrait.Gallery.SaveBase")))
                status = Localization.T(SaveBasePrompt()
                    ? "RimAIPortrait.Gallery.BaseSaved" : "RimAIPortrait.Gallery.BaseSaveFailed");
            Tip(saveBaseButton, "SaveBase");

            Widgets.Label(new Rect(0f, 200f, 85f, 28f), Localization.T("RimAIPortrait.Gallery.ExtraLabel"));
            Rect extraArea = new Rect(90f, 191f, rect.width - 90f, 54f);
            GUI.SetNextControlName(ExtraFeaturesControl);
            extraFeatures = Widgets.TextArea(extraArea, extraFeatures ?? "");
            TooltipHandler.TipRegion(extraArea, Localization.T("RimAIPortrait.Gallery.ExtraFeaturesTip"));

            if (basePromptProvider == AiProvider.NovelAI || basePromptProvider == AiProvider.LocalComfyUI
                || basePromptProvider == AiProvider.LocalOpenAI)
            {
                Widgets.Label(new Rect(0f, 257f, 150f, 28f),
                    Localization.T("RimAIPortrait.Gallery.TagExtractorLabel"));
                Rect editTagButton = new Rect(rect.width - 205f, 252f, 80f, 32f);
                if (Widgets.ButtonText(editTagButton,
                    Localization.T("RimAIPortrait.Gallery.EditTagExtractorPrompt")))
                    RunTagExtractorAction(() => Find.WindowStack.Add(new Dialog_TagExtractorPrompt()));
                Rect generateTagButton = new Rect(rect.width - 115f, 252f, 115f, 32f);
                if (Widgets.ButtonText(generateTagButton, Localization.T("RimAIPortrait.Gallery.ExtractTag"),
                    true, true, !generatingTag))
                    RunTagExtractorAction(ExtractTag);
                TooltipHandler.TipRegion(generateTagButton, Localization.T("RimAIPortrait.Gallery.ExtractTagTip"));
                string placeholder = Localization.T("RimAIPortrait.Gallery.TagPlaceholder");
                string positive = TagDisplay.DrawEditable(new Rect(0f, 288f, 85f, 28f),
                    new Rect(90f, 284f, rect.width - 90f, 30f), Localization.T("RimAIPortrait.Tag.Positive"),
                    positiveTagText, PositiveTagControl, placeholder);
                string negative = TagDisplay.DrawEditable(new Rect(0f, 324f, 85f, 28f),
                    new Rect(90f, 320f, rect.width - 90f, 30f), Localization.T("RimAIPortrait.Tag.Negative"),
                    negativeTagText, NegativeTagControl, placeholder);
                if (positive != positiveTagText || negative != negativeTagText) tagEdited = true;
                positiveTagText = positive;
                negativeTagText = negative;
            }

            Widgets.Label(new Rect(0f, 360f, rect.width, 34f), status ?? "");
            float panelTop = 402f;
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
            if (focusedControl == BasePromptControl || focusedControl == ExtraFeaturesControl
                || focusedControl == PositiveTagControl || focusedControl == NegativeTagControl)
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
                    .Where(state => state == PortraitState.Base || RimAIPortraitMod.Settings.IsStateEnabled(state))
                    .OrderBy(state => state == PortraitState.Base ? 0 : 1).ToArray()
                : new[] { PortraitState.Base };
        }

        private void ChangeState(int direction, bool emotionChangesAvailable)
        {
            PortraitState[] states = AvailableStates(emotionChangesAvailable);
            int nextIndex = Array.IndexOf(states, targetState) + direction;
            if (nextIndex >= 0 && nextIndex < states.Length)
            {
                targetState = states[nextIndex];
                UpdateVisibleSelection();
            }
        }

        private bool IsVisibleFile(string path)
        {
            if (showAll) return true;
            string name = Path.GetFileNameWithoutExtension(path);
            return name.StartsWith(PortraitStateNames.Display(targetState) + "_", StringComparison.OrdinalIgnoreCase);
        }

        private void UpdateVisibleSelection()
        {
            scroll = Vector2.zero;
            selected.RemoveWhere(path => !IsVisibleFile(path));
            if (selectionAnchor != null && !IsVisibleFile(selectionAnchor)) selectionAnchor = null;
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
                    Event current = Event.current;
                    if (current.type == EventType.MouseDown && current.button == 0
                        && imageArea.Contains(current.mousePosition))
                        GUIUtility.keyboardControl = 0;
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
            bool previousShowAll = showAll;
            Widgets.CheckboxLabeled(new Rect(area.x + 8f, area.y, area.width - 16f, 28f),
                Localization.T("RimAIPortrait.Gallery.ShowAll"), ref showAll);
            if (showAll != previousShowAll) UpdateVisibleSelection();
            area = new Rect(area.x, area.y + 32f, area.width, area.height - 32f);
            Widgets.DrawMenuSection(area);
            string[] visibleFiles = files.Where(IsVisibleFile).ToArray();
            float cell = 140f;
            float gap = 10f;
            int columns = Math.Max(1, (int)((area.width - 24f + gap) / (cell + gap)));
            float rowHeight = cell + gap;
            float contentHeight = Math.Max(area.height, ((visibleFiles.Length + columns - 1) / columns) * rowHeight + 8f);
            Rect view = new Rect(0f, 0f, area.width - 20f, contentHeight);
            Widgets.BeginScrollView(area.ContractedBy(4f), ref scroll, view);
            int firstRow = Math.Max(0, (int)(scroll.y / rowHeight));
            int lastRow = Math.Min((visibleFiles.Length + columns - 1) / columns,
                (int)Math.Ceiling((scroll.y + area.height) / rowHeight) + 1);
            for (int i = firstRow * columns; i < Math.Min(visibleFiles.Length, lastRow * columns); i++)
            {
                string file = visibleFiles[i];
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
                    SelectGalleryImage(visibleFiles, i, false, false);
                    AssignSelected();
                    current.Use();
                }
                bool shift = Event.current.shift;
                bool control = Event.current.control;
                if (Widgets.ButtonInvisible(box))
                    SelectGalleryImage(visibleFiles, i, shift, control);
                TooltipHandler.TipRegion(box, Localization.T("RimAIPortrait.Gallery.Tip.GalleryImage") + "\n" + file);
            }
            Widgets.EndScrollView();
        }
        private void SelectGalleryImage(string[] visibleFiles, int index, bool shift, bool control)
        {
            GUIUtility.keyboardControl = 0;
            string file = visibleFiles[index];
            int anchorIndex = selectionAnchor == null ? -1 : Array.IndexOf(visibleFiles, selectionAnchor);
            if (shift && anchorIndex >= 0)
            {
                if (!control) selected.Clear();
                int start = Math.Min(anchorIndex, index);
                int end = Math.Max(anchorIndex, index);
                for (int i = start; i <= end; i++) selected.Add(visibleFiles[i]);
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
            if (PawnCapture.LastPawnThingId != pawn.ThingID || PawnCapture.LastCapture == null
                || PawnCapture.LastCaptureExcludedHeadgear != RimAIPortraitMod.Settings.ExcludeHeadgearFromSprite)
                PawnCapture.Capture(pawn);
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
        private bool SaveExtraFeatures() => PortraitData.SaveExtraFeatures(pawn, extraFeatures);
        private bool SaveBaseTags()
        {
            if (!tagEdited) return true;
            var tags = new ExtractedTags
            {
                positive = SplitTags(positiveTagText),
                negative = SplitTags(negativeTagText)
            };
            if (!PortraitData.SaveBaseTags(pawn, tags)) return false;
            baseTags = tags;
            tagEdited = false;
            return true;
        }

        private static string[] SplitTags(string text)
        {
            return (text ?? "").Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(tag => tag.Trim()).Where(tag => tag.Length > 0).ToArray();
        }

        private void ExtractTag()
        {
            SaveExtraFeatures();
            SaveBasePrompt();
            generatingTag = true;
            status = Localization.T("RimAIPortrait.Gallery.ExtractingTag");
            string imagePath = selected.Count == 1 ? selected.First() : null;
            if (imagePath == null)
            {
                string basePath = PortraitData.ExistingPathFor(pawn, PortraitState.Base);
                if (File.Exists(basePath)) imagePath = basePath;
            }
            PortraitRuntime.EnsureCreated();
            PortraitRuntime.Instance.GenerateBaseTags(pawn, imagePath, (tags, error) =>
            {
                generatingTag = false;
                if (tags == null)
                {
                    status = Localization.T("RimAIPortrait.TagExtractor.Failed", error ??
                        Localization.T("RimAIPortrait.TagExtractor.InvalidResponse"));
                    return;
                }
                positiveTagText = tags.PositiveText;
                negativeTagText = tags.NegativeText;
                tagEdited = true;
                status = Localization.T(SaveBaseTags()
                    ? "RimAIPortrait.Gallery.TagSaved" : "RimAIPortrait.Gallery.TagSaveFailed");
            });
        }
        private bool SaveBasePrompt()
        {
            try
            {
                PortraitSettings settings = RimAIPortraitMod.Settings;
                if (basePromptProvider == AiProvider.Google) settings.GeminiBaseInstructions = basePrompt;
                else if (basePromptProvider == AiProvider.NovelAI) settings.NovelAiBaseInstructions = basePrompt;
                else if (basePromptProvider == AiProvider.LocalComfyUI) settings.ComfyBaseInstructions = basePrompt;
                else if (basePromptProvider == AiProvider.LocalOpenAI) settings.LocalOpenAiBaseInstructions = basePrompt;
                else settings.BaseInstructions = basePrompt;
                RimAIPortraitMod.SaveSettings();
                return true;
            }
            catch (Exception exception)
            {
                Log.Warning("[Rim AI Expressive Portraits] Could not save Base Prompt: " + exception.Message);
                return false;
            }
        }
        /// <summary>Describes the behavior of this type or member.</summary>
        private void LoadBasePrompt()
        {
            PortraitSettings settings = RimAIPortraitMod.Settings;
            basePrompt = basePromptProvider == AiProvider.Google ? settings.GeminiBaseInstructions
                : basePromptProvider == AiProvider.NovelAI ? settings.NovelAiBaseInstructions
                : basePromptProvider == AiProvider.LocalComfyUI ? settings.ComfyBaseInstructions
                : basePromptProvider == AiProvider.LocalOpenAI ? settings.LocalOpenAiBaseInstructions
                : settings.BaseInstructions;
        }

        private bool SaveRequestFields()
        {
            bool extraSaved = SaveExtraFeatures();
            bool promptSaved = SaveBasePrompt();
            bool tagsSaved = SaveBaseTags();
            return extraSaved && promptSaved && tagsSaved;
        }
        private bool HasBaseImage()
        {
            return File.Exists(PortraitData.ExistingPathFor(pawn, PortraitState.Base));
        }

        private void ShowNextHelp()
        {
            if (helpOpen || galleryClosed) return;
            PortraitSettings settings = RimAIPortraitMod.Settings;
            bool showV5 = !v5HelpChecked && settings.Provider == AiProvider.NovelAI
                && NovelAiPortraitProvider.IsV5Model(settings.NovelAiModel)
                && !settings.NovelAiV5HelpDismissed;
            if (showV5)
            {
                v5HelpChecked = true;
                OpenHelp(new Dialog_PortraitHelp(Localization.T("RimAIPortrait.Help.NovelAiV5SpriteWarning"),
                    dismissed => CloseHelp(dismissed, () => settings.NovelAiV5HelpDismissed = true)));
                return;
            }
            if (!baseHelpChecked && !settings.PortraitHelpDismissed)
            {
                baseHelpChecked = true;
                OpenHelp(new Dialog_PortraitHelp(0, 2, null,
                    dismissed => CloseHelp(dismissed, () => settings.PortraitHelpDismissed = true)));
                return;
            }
            if (!expressionHelpChecked && HasBaseImage() && !settings.PortraitHelpAfterImageDismissed)
            {
                expressionHelpChecked = true;
                OpenHelp(new Dialog_PortraitHelp(3, 4, null,
                    dismissed => CloseHelp(dismissed, () => settings.PortraitHelpAfterImageDismissed = true)));
            }
        }

        private void OpenHelp(Dialog_PortraitHelp help)
        {
            helpOpen = true;
            Find.WindowStack.Add(help);
        }

        private void RunTagExtractorAction(Action action)
        {
            PortraitSettings settings = RimAIPortraitMod.Settings;
            if (settings.TagExtractorHelpDismissed)
            {
                action();
                return;
            }
            OpenHelp(new Dialog_PortraitHelp(Dialog_PortraitHelp.TagExtractorFirstPageIndex,
                Dialog_PortraitHelp.TagExtractorLastPageIndex, null, dismissed =>
                {
                    CloseHelp(dismissed, () => settings.TagExtractorHelpDismissed = true);
                    if (!galleryClosed) action();
                }));
        }

        private void CloseHelp(bool dismissed, Action dismiss)
        {
            if (dismissed)
            {
                dismiss();
                RimAIPortraitMod.SaveSettings();
            }
            helpOpen = false;
            ShowNextHelp();
        }
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
            bool hasBaseImage = HasBaseImage();
            if (checkedFirstHelp && !hadBaseImage && hasBaseImage) ShowNextHelp();
            hadBaseImage = hasBaseImage;
            selected.RemoveWhere(path => !File.Exists(path));
            if (selectionAnchor != null && Array.IndexOf(files, selectionAnchor) < 0)
                selectionAnchor = null;
            UpdateVisibleSelection();
        }
        /// <summary>Describes the behavior of this type or member.</summary>
        public override void PostClose() { galleryClosed = true; SaveRequestFields(); foreach (var t in textures.Values) UnityEngine.Object.Destroy(t); if (debugSprite != null) UnityEngine.Object.Destroy(debugSprite); base.PostClose(); }
    }
}
