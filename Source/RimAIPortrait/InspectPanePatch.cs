// Contains the implementation for InspectPanePatch.

using System;
using System.Collections.Generic;
using System.IO;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimAIPortrait
{
    /// <summary>Describes the behavior of this type or member.</summary>
    [HarmonyPatch(typeof(UIRoot), nameof(UIRoot.UIRootOnGUI))]
    public static class InspectPanePatch
    {
        private static readonly Dictionary<string, PortraitState> LastState = new Dictionary<string, PortraitState>();
        private static readonly Dictionary<string, float> NextCheck = new Dictionary<string, float>();
        private static readonly Dictionary<string, PortraitState> PreviewState = new Dictionary<string, PortraitState>();
        private static readonly Dictionary<string, float> PreviewUntil = new Dictionary<string, float>();
        private static Texture2D frameTexture;
        private static string loadedFramePath;
        private static long loadedFrameWriteTicks;
        private static string warnedFramePath;
        private static string configuredFramePath;
        private static float nextFrameFileCheck;
        private static bool frameFileChecked;
        private static Game cachedGame;

        /// <summary>Describes the behavior of this type or member.</summary>
        [HarmonyPostfix]
        public static void DrawPortrait()
        {
            if (Current.ProgramState != ProgramState.Playing || Find.MainTabsRoot == null) return;
            if (!ReferenceEquals(cachedGame, Current.Game))
            {
                LastState.Clear();
                NextCheck.Clear();
                PreviewState.Clear();
                PreviewUntil.Clear();
                cachedGame = Current.Game;
            }
            PortraitData.EnsureGameScope();
            var inspect = Find.MainTabsRoot.OpenTab?.TabWindow as MainTabWindow_Inspect;
            Thing selected = Find.Selector.SingleSelectedThing;
            var pawn = selected as Pawn;
            if (pawn == null && selected is Corpse corpse) pawn = corpse.InnerPawn;
            if (inspect == null || pawn == null ||
                (!pawn.RaceProps.Humanlike && !pawn.RaceProps.Animal)) return;
            var s = RimAIPortraitMod.Settings; if (s == null) return;
            if (pawn.Dead)
            {
                DrawPortraitTexture(PortraitData.GetDeath(pawn), s);
                return;
            }
            PortraitState state;
            if (!TryPreviewState(pawn, out state))
                state = s.EnableEmotionChanges && !pawn.RaceProps.Animal
                    ? TimedState(pawn, s.SwapSeconds)
                    : PortraitState.Normal;
            Texture2D portrait = PortraitData.Get(pawn, state);
            DrawPortraitTexture(portrait, s);
        }

        /// <summary>Describes the behavior of this type or member.</summary>
        private static void DrawPortraitTexture(Texture2D portrait, PortraitSettings s)
        {
            if (portrait == null) return;
            float w = s.Width * s.Scale, h = w * portrait.height / portrait.width;
            float bottom = UI.screenHeight - s.OffsetY;
            Rect portraitRect = new Rect(s.OffsetX, bottom - h, w, h);
            Rect imageRect = portraitRect;
            if (s.EnableFrame)
            {
                // Preserve the intended portrait behavior and compatibility.
                // Preserve the intended portrait behavior and compatibility.
                float insetX = portraitRect.width * .05f;
                float insetY = portraitRect.height * .05f;
                imageRect = new Rect(portraitRect.x + insetX, portraitRect.y + insetY,
                    portraitRect.width - insetX * 2f, portraitRect.height - insetY * 2f);
            }
            GUI.DrawTexture(imageRect, portrait, ScaleMode.ScaleToFit, true);
            if (s.EnableFrame)
            {
                Texture2D frame = FrameTexture(s.FrameImagePath);
                if (frame != null)
                {
                    float frameScale = Mathf.Clamp(s.FrameScale, .5f, 1.5f);
                    float frameWidth = portraitRect.width * frameScale;
                    float frameHeight = portraitRect.height * frameScale;
                    Rect frameRect = new Rect(
                        portraitRect.center.x - frameWidth * .5f,
                        portraitRect.center.y - frameHeight * .5f,
                        frameWidth,
                        frameHeight);
                    GUI.DrawTexture(frameRect, frame, ScaleMode.StretchToFill, true);
                }
            }
        }

        /// <summary>Describes the behavior of this type or member.</summary>
        private static Texture2D FrameTexture(string path)
        {
            string configured = path.NullOrEmpty() ? "Textures/AIExpressivePortraits/default_frame.png" : path.Trim();
            if (configuredFramePath != configured)
            {
                configuredFramePath = configured;
                nextFrameFileCheck = 0f;
                frameFileChecked = false;
                try
                {
                    loadedFramePath = Path.GetFullPath(Path.IsPathRooted(configured) ? configured : Path.Combine(RimAIPortraitMod.ModPack.RootDir, configured));
                }
                catch (Exception e)
                {
                    loadedFramePath = null;
                    WarnFrameOnce(path ?? "", Localization.T("RimAIPortrait.Error.FramePathInvalid", e.Message));
                    return null;
                }
            }

            if (Time.realtimeSinceStartup < nextFrameFileCheck) return frameTexture;
            nextFrameFileCheck = Time.realtimeSinceStartup + 2f;
            string fullPath = loadedFramePath;
            bool exists = !fullPath.NullOrEmpty() && File.Exists(fullPath);
            long writeTicks = exists ? File.GetLastWriteTimeUtc(fullPath).Ticks : 0L;
            if (frameFileChecked && loadedFrameWriteTicks == writeTicks) return frameTexture;
            frameFileChecked = true;
            if (frameTexture != null) UnityEngine.Object.Destroy(frameTexture);
            frameTexture = null; loadedFrameWriteTicks = writeTicks;
            if (!exists)
            {
                WarnFrameOnce(fullPath, Localization.T("RimAIPortrait.Error.FrameFileMissing"));
                return null;
            }
            try
            {
                var texture = new Texture2D(2, 2, TextureFormat.ARGB32, false);
                if (!texture.LoadImage(File.ReadAllBytes(fullPath))) { UnityEngine.Object.Destroy(texture); throw new InvalidDataException(Localization.T("RimAIPortrait.Preview.UnsupportedImage")); }
                texture.wrapMode = TextureWrapMode.Clamp; frameTexture = texture; warnedFramePath = null; return frameTexture;
            }
            catch (Exception e) { WarnFrameOnce(fullPath, Localization.T("RimAIPortrait.Error.FrameLoadFailed", e.Message)); return null; }
        }

        /// <summary>Describes the behavior of this type or member.</summary>
        private static void WarnFrameOnce(string path, string message)
        {
            if (warnedFramePath == path) return;
            warnedFramePath = path;
            Log.Warning("[Rim AI Expressive Portraits] " + message + (path.NullOrEmpty() ? "" : " " + path));
        }

        /// <summary>Describes the behavior of this type or member.</summary>
        private static PortraitState TimedState(Pawn p, float seconds)
        {
            string id = p.ThingID;
            if (!LastState.ContainsKey(id) || !NextCheck.ContainsKey(id) ||
                !RimAIPortraitMod.Settings.IsStateEnabled(LastState[id]) ||
                Time.realtimeSinceStartup >= NextCheck[id])
            {
                LastState[id] = CurrentState(p); NextCheck[id] = Time.realtimeSinceStartup + Mathf.Max(.5f, seconds);
            }
            return LastState[id];
        }

        /// <summary>Describes the behavior of this type or member.</summary>
        public static void ShowStateImmediately(Pawn pawn, PortraitState state)
        {
            if (pawn == null) return;
            LastState[pawn.ThingID] = state;
            NextCheck[pawn.ThingID] = Time.realtimeSinceStartup + Mathf.Max(.5f, RimAIPortraitMod.Settings?.SwapSeconds ?? 3f);
            PreviewState[pawn.ThingID] = state;
            PreviewUntil[pawn.ThingID] = Time.realtimeSinceStartup + 30f;
        }

        /// <summary>Describes the behavior of this type or member.</summary>
        private static bool TryPreviewState(Pawn pawn, out PortraitState state)
        {
            string id = pawn.ThingID;
            if (PreviewState.TryGetValue(id, out state) && PreviewUntil.TryGetValue(id, out var until) && Time.realtimeSinceStartup < until)
                return true;
            PreviewState.Remove(id);
            PreviewUntil.Remove(id);
            state = PortraitState.Normal;
            return false;
        }

        /// <summary>Describes the behavior of this type or member.</summary>
        private static PortraitState CurrentState(Pawn p)
        {
            var settings = RimAIPortraitMod.Settings;
            if (settings.EnableDown && p.Downed) return PortraitState.Down;
            if (settings.EnableHot && p.health?.hediffSet != null && p.health.hediffSet.HasHediff(HediffDefOf.Heatstroke)) return PortraitState.Hot;
            if (settings.EnableCold && p.health?.hediffSet != null && p.health.hediffSet.HasHediff(HediffDefOf.Hypothermia)) return PortraitState.Cold;
            if (settings.EnableSleep && !p.Awake()) return PortraitState.Sleep;
            if (settings.EnablePain && p.health?.hediffSet != null && p.health.hediffSet.PainTotal > .15f) return PortraitState.Pain;
            MentalStateDef mentalState = p.MentalStateDef;
            if (settings.EnableAngry && (mentalState == MentalStateDefOf.Berserk || mentalState == MentalStateDefOf.BerserkPermanent || mentalState == MentalStateDefOf.SocialFighting))
                return PortraitState.Angry;
            if (settings.EnableDistressed && mentalState != null) return PortraitState.Distressed;
            if (p.needs?.mood != null)
            {
                float mood = p.needs.mood.CurLevelPercentage;
                if (settings.EnableHigh && mood >= settings.HighMoodThreshold) return PortraitState.High;
                if (settings.EnableLow && mood <= settings.LowMoodThreshold) return PortraitState.Low;
            }
            return PortraitState.Normal;
        }
    }
}
