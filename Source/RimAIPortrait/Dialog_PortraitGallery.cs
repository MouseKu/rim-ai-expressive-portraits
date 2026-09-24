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
        private readonly Dictionary<string, Texture2D> textures = new Dictionary<string, Texture2D>();
        private readonly Dictionary<string, int> textureUse = new Dictionary<string, int>();
        private int textureUseClock;
        private const int TextureCacheLimit = 64;
        private Vector2 scroll;
        private string[] files;
        private string extraFeatures;
        private string basePromptOverride;
        private Texture2D debugSprite;
        private byte[] debugSpriteSource;
        private PortraitState targetState = PortraitState.Normal;
        private string status = Localization.T("RimAIPortrait.Gallery.InitialStatus");
        public override Vector2 InitialSize => new Vector2(900f, 980f);
        /// <summary>Describes the behavior of this type or member.</summary>
        public Dialog_PortraitGallery(Pawn pawn) { this.pawn = pawn; extraFeatures = PortraitData.ExtraFeatures(pawn); basePromptOverride = PortraitData.BasePrompt(pawn); if (basePromptOverride.NullOrEmpty()) basePromptOverride = PortraitPrompt.BaseInstructions(RimAIPortraitMod.Settings); doCloseX = true; absorbInputAroundWindow = true; RefreshFiles(); }

        /// <summary>Describes the behavior of this type or member.</summary>
        public override void DoWindowContents(Rect rect)
        {
            bool emotionChangesAvailable = RimAIPortraitMod.Settings.EnableEmotionChanges && !pawn.RaceProps.Animal;
            string animalUnavailable = Localization.T("RimAIPortrait.Gallery.UnavailableForAnimals");
            if (!RimAIPortraitMod.Settings.IsStateEnabled(targetState)) targetState = PortraitState.Normal;
            Widgets.Label(new Rect(0, 4, rect.width - 270, 28), Localization.T("RimAIPortrait.Gallery.SaveFolder", PortraitData.PawnFolder(pawn)));
            if (Widgets.ButtonText(new Rect(rect.width - 250, 0, 120, 32), Localization.T("RimAIPortrait.Common.Refresh")))
            {
                RefreshFiles(true); status = Localization.T("RimAIPortrait.Gallery.Refreshed");
            }
            if (Widgets.ButtonText(new Rect(rect.width - 120, 0, 120, 32), Localization.T("RimAIPortrait.Common.OpenFolder")))
            {
                try { string folder = PortraitData.PawnFolder(pawn); Directory.CreateDirectory(folder); Process.Start(folder); }
                catch (Exception e) { status = Localization.T("RimAIPortrait.Error.OpenFolder", e.Message); }
            }

            if (Widgets.ButtonText(new Rect(0, 64, 180, 34), Localization.T("RimAIPortrait.Gallery.GenerateBase")))
            {
                SaveRequestFields();
                PortraitRuntime.EnsureCreated(); status = Localization.T("RimAIPortrait.Status.GeneratingBase");
                PortraitRuntime.Instance.GenerateNormal(pawn, selected, x => { status = x; RefreshFiles(); });
            }
            Rect emotionGenerationRect = new Rect(190, 64, 215, 34);
            if (RimAIPortraitMod.Settings.EnableEmotionChanges && Widgets.ButtonText(emotionGenerationRect,
                Localization.T("RimAIPortrait.Gallery.GenerateEmotionsFromBase"), true, true, emotionChangesAvailable))
            {
                string basePath = PortraitData.ExistingPathFor(pawn, PortraitState.Normal);
                if (!File.Exists(basePath)) status = Localization.T("RimAIPortrait.Gallery.BaseRequired");
                else { SaveRequestFields(); PortraitRuntime.EnsureCreated(); Find.WindowStack.Add(new Dialog_EmotionGeneration(pawn, new[] { basePath }, RefreshFiles)); }
            }
            if (RimAIPortraitMod.Settings.EnableEmotionChanges && pawn.RaceProps.Animal)
                TooltipHandler.TipRegion(emotionGenerationRect, animalUnavailable);
            float regenerateX = RimAIPortraitMod.Settings.EnableEmotionChanges ? 415f : 190f;
            if (Widgets.ButtonText(new Rect(regenerateX, 64, 215, 34), Localization.T("RimAIPortrait.Gallery.RegenerateSelected")))
            {
                if (selected.Count != 1)
                    status = Localization.T("RimAIPortrait.Gallery.SelectOneToRegenerate");
                else
                {
                    SaveRequestFields(); PortraitRuntime.EnsureCreated();
                    status = Localization.T("RimAIPortrait.Status.RegeneratingSelected");
                    PortraitRuntime.Instance.GenerateNormalFromReference(pawn, selected.First(), x => { status = x; RefreshFiles(); });
                }
            }
            float removeBackgroundX = RimAIPortraitMod.Settings.EnableEmotionChanges ? 640f : 415f;
            if (Widgets.ButtonText(new Rect(removeBackgroundX, 64, 220, 34), Localization.T("RimAIPortrait.Gallery.RemoveBackground")))
            {
                if (selected.Count != 1)
                    status = Localization.T("RimAIPortrait.Gallery.SelectOneToRemoveBackground");
                else
                {
                    string selectedPath = selected.First();
                    Find.WindowStack.Add(new Dialog_BackgroundRemoval(selectedPath, () =>
                    {
                        DropTexture(selectedPath);
                        RefreshFiles();
                        status = Localization.T("RimAIPortrait.Gallery.BackgroundRemoved");
                    }));
                }
            }
            if (emotionChangesAvailable && Widgets.ButtonText(new Rect(0, 102, 180, 32), PortraitStateNames.Display(targetState) + " ▼"))
            {
                var options = new List<FloatMenuOption>();
                foreach (PortraitState state in Enum.GetValues(typeof(PortraitState)))
                {
                    if (!RimAIPortraitMod.Settings.IsStateEnabled(state)) continue;
                    PortraitState captured = state; options.Add(new FloatMenuOption(StateLabel(captured), () => targetState = captured));
                }
                Find.WindowStack.Add(new FloatMenu(options));
            }
            if (RimAIPortraitMod.Settings.EnableEmotionChanges && pawn.RaceProps.Animal)
            {
                Rect unavailableStateRect = new Rect(0, 102, 180, 32);
                Widgets.ButtonText(unavailableStateRect, PortraitStateNames.Display(PortraitState.Normal), true, true, false);
                TooltipHandler.TipRegion(unavailableStateRect, animalUnavailable);
            }
            if (!emotionChangesAvailable)
            {
                targetState = PortraitState.Normal;
                if (!RimAIPortraitMod.Settings.EnableEmotionChanges)
                    Widgets.Label(new Rect(0, 106, 180, 28), "Base");
            }
            if (Widgets.ButtonText(new Rect(190, 102, 215, 32), Localization.T(emotionChangesAvailable ? "RimAIPortrait.Gallery.AssignEmotion" : "RimAIPortrait.Gallery.AssignBase")))
            {
                if (selected.Count != 1) status = Localization.T("RimAIPortrait.Gallery.SelectOneToAssign");
                else
                {
                    PortraitData.SetActive(pawn, targetState, selected.First());
                    DropTexture(PortraitData.PathFor(pawn, targetState));
                    InspectPanePatch.ShowStateImmediately(pawn, targetState);
                    status = Localization.T("RimAIPortrait.Gallery.Assigned", StateLabel(targetState));
                }
            }
            if (Widgets.ButtonText(new Rect(415, 102, 150, 32), Localization.T("RimAIPortrait.Common.ClearSelection"))) selected.Clear();
            if (Widgets.ButtonText(new Rect(740, 102, 120, 32), Localization.T("RimAIPortrait.Common.DeleteImage")) && selected.Count > 0)
            {
                string[] deleting = selected.ToArray();
                Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation(Localization.T("RimAIPortrait.Gallery.ConfirmDelete", deleting.Length), () => { PortraitData.Delete(pawn, deleting); selected.Clear(); RefreshFiles(); }));
            }
            Widgets.Label(new Rect(0, 146, 110, 28), "Extra Features");
            TooltipHandler.TipRegion(new Rect(0, 142, rect.width, 72), Localization.T("RimAIPortrait.Gallery.ExtraFeaturesTip"));
            extraFeatures = Widgets.TextArea(new Rect(115, 142, rect.width - 255, 72), extraFeatures ?? "");
            if (Widgets.ButtonText(new Rect(rect.width - 130, 142, 130, 32), Localization.T("RimAIPortrait.Common.Save")))
                status = Localization.T(SaveExtraFeatures() ? "RimAIPortrait.Gallery.ExtraSaved" : "RimAIPortrait.Gallery.ExtraSaveFailed");
            Widgets.Label(new Rect(rect.width - 130, 178, 130, 36), Localization.T("RimAIPortrait.Gallery.PawnInstructions"));
            Widgets.Label(new Rect(0, 224, 115, 28), "Base Prompt");
            TooltipHandler.TipRegion(new Rect(0, 216, rect.width, 116), Localization.T("RimAIPortrait.Gallery.BasePromptTip"));
            basePromptOverride = Widgets.TextArea(new Rect(115, 216, rect.width - 255, 112), basePromptOverride ?? "");
            if (Widgets.ButtonText(new Rect(rect.width - 130, 216, 130, 32), Localization.T("RimAIPortrait.Gallery.SaveBase")))
                status = Localization.T(PortraitData.SaveBasePrompt(pawn, basePromptOverride) ? "RimAIPortrait.Gallery.BaseSaved" : "RimAIPortrait.Gallery.BaseSaveFailed");
            Widgets.Label(new Rect(rect.width - 130, 252, 130, 72), Localization.T("RimAIPortrait.Gallery.BaseDefaultHint"));
            Widgets.Label(new Rect(0, 338, rect.width, 32), status);

            int enabledStateCount = Enum.GetValues(typeof(PortraitState)).Cast<PortraitState>().Count(RimAIPortraitMod.Settings.IsStateEnabled);
            float assignedHeight = emotionChangesAvailable ? Math.Max(116f, ((enabledStateCount + 4) / 5) * 116f) : 116f;
            DrawAssignedStates(new Rect(0, 374, rect.width, assignedHeight));
            float selectedY = 380f + assignedHeight;
            DrawSelected(new Rect(0, selectedY, rect.width, 96));
            float cell = 140f, gap = 10f; int cols = Math.Max(1, (int)((rect.width - 18) / (cell + gap)));
            float contentH = Math.Max(390f, ((files.Length + cols - 1) / cols) * (cell + 38f));
            float galleryY = selectedY + 105f;
            Rect outer = new Rect(0, galleryY, rect.width, rect.height - galleryY); Rect view = new Rect(0, 0, outer.width - 18, contentH);
            Widgets.BeginScrollView(outer, ref scroll, view);
            int rowHeight = (int)(cell + 38f);
            int firstRow = Math.Max(0, (int)(scroll.y / rowHeight));
            int lastRow = Math.Min((files.Length + cols - 1) / cols, (int)Math.Ceiling((scroll.y + outer.height) / rowHeight) + 1);
            int firstIndex = firstRow * cols;
            int lastIndex = Math.Min(files.Length, lastRow * cols);
            for (int i = firstIndex; i < lastIndex; i++)
            {
                string file = files[i]; float x = (i % cols) * (cell + gap), y = (i / cols) * (cell + 38f); Rect box = new Rect(x, y, cell, cell);
                if (selected.Contains(file)) Widgets.DrawBoxSolid(box.ExpandedBy(4), new Color(.25f, .65f, 1f, .9f));
                Texture2D tex = Load(file); if (tex != null) GUI.DrawTexture(box, tex, ScaleMode.ScaleToFit, true);
                if (Widgets.ButtonInvisible(box)) { if (!selected.Add(file)) selected.Remove(file); }
                Widgets.Label(new Rect(x, y + cell + 2, cell, 34), Path.GetFileNameWithoutExtension(file));
                TooltipHandler.TipRegion(box, file);
            }
            Widgets.EndScrollView();
        }

        /// <summary>Describes the behavior of this type or member.</summary>
        private void DrawAssignedStates(Rect area)
        {
            Widgets.DrawMenuSection(area); PortraitState[] states = RimAIPortraitMod.Settings.EnableEmotionChanges && !pawn.RaceProps.Animal
                ? Enum.GetValues(typeof(PortraitState)).Cast<PortraitState>().Where(RimAIPortraitMod.Settings.IsStateEnabled).ToArray()
                : new[] { PortraitState.Normal };
            const int columns = 5; float width = (area.width - 12) / columns, image = 82f;
            for (int i = 0; i < states.Length; i++)
            {
                int row = i / columns, col = i % columns; float x = area.x + 6 + col * width, y = area.y + 4 + row * 116;
                Widgets.Label(new Rect(x, y, width - 4, 24), PortraitStateNames.Display(states[i])); string path = PortraitData.ExistingPathFor(pawn, states[i]);
                if (!File.Exists(path)) continue; Rect imageRect = new Rect(x, y + 25, image, image); Texture2D tex = Load(path);
                if (tex != null) GUI.DrawTexture(imageRect, tex, ScaleMode.ScaleToFit, true);
            }
        }

        /// <summary>Describes the behavior of this type or member.</summary>
        private void DrawSelected(Rect area)
        {
            Widgets.DrawMenuSection(area);
            float selectedStart = area.x + 8;
            if (RimAIPortraitMod.Settings.DebugMode)
            {
                DrawDebugSprite(new Rect(area.x + 8, area.y + 4, 200, area.height - 8));
                selectedStart = area.x + 215;
            }
            Widgets.Label(new Rect(selectedStart, area.y + 5, 160, 24), Localization.T("RimAIPortrait.Gallery.SelectedCount", selected.Count));
            if (Widgets.ButtonText(new Rect(selectedStart, area.y + 34, 145, 34), Localization.T("RimAIPortrait.Gallery.LargePreview")))
            {
                if (selected.Count != 1) status = Localization.T("RimAIPortrait.Gallery.SelectOneToPreview");
                else Find.WindowStack.Add(new Dialog_ImagePreview(selected.First()));
            }
            float x = selectedStart + 155; string remove = null;
            int visibleCount = RimAIPortraitMod.Settings.DebugMode ? 5 : 6;
            foreach (string file in selected.Take(visibleCount))
            {
                Rect thumb = new Rect(x, area.y + 5, 76, 76); Texture2D tex = Load(file);
                if (tex != null) GUI.DrawTexture(thumb, tex, ScaleMode.ScaleToFit, true);
                if (Widgets.ButtonInvisible(thumb)) remove = file;
                TooltipHandler.TipRegion(thumb, Localization.T("RimAIPortrait.Gallery.ClickToDeselect", Path.GetFileName(file))); x += 84;
            }
            if (remove != null) selected.Remove(remove);
            if (selected.Count > visibleCount) Widgets.Label(new Rect(x, area.y + 45, 80, 30), "+" + (selected.Count - visibleCount));
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
        /// <summary>Describes the behavior of this type or member.</summary>
        private bool SaveRequestFields() => SaveExtraFeatures() && PortraitData.SaveBasePrompt(pawn, basePromptOverride);
        /// <summary>Describes the behavior of this type or member.</summary>
        private void RefreshFiles() { RefreshFiles(false); }
        /// <summary>Describes the behavior of this type or member.</summary>
        private void RefreshFiles(bool clearTextures)
        {
            PortraitData.RefreshExternalFiles(pawn);
            if (clearTextures) { foreach (var texture in textures.Values) UnityEngine.Object.Destroy(texture); textures.Clear(); textureUse.Clear(); }
            files = PortraitData.Gallery(pawn);
            selected.RemoveWhere(path => !File.Exists(path));
        }
        /// <summary>Describes the behavior of this type or member.</summary>
        public override void PostClose() { SaveRequestFields(); foreach (var t in textures.Values) UnityEngine.Object.Destroy(t); if (debugSprite != null) UnityEngine.Object.Destroy(debugSprite); base.PostClose(); }
    }
}
