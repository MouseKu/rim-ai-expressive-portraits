// Contains the implementation for PortraitData.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimAIPortrait
{
    /// <summary>Describes the behavior of this type or member.</summary>
    public static partial class PortraitData
    {
        public static string ExtraFeatures(Pawn p)
        {
            string path = ExtraFeaturesPath(p);
            try { return File.Exists(path) ? File.ReadAllText(path) : ""; }
            catch (Exception e) { Log.Warning("[Rim AI Expressive Portraits] Could not read Extra Features: " + e.Message); return ""; }
        }
        /// <summary>Describes the behavior of this type or member.</summary>
        public static bool SaveExtraFeatures(Pawn p, string value)
        {
            try
            {
                string path = ExtraFeaturesPath(p);
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.WriteAllText(path, value ?? "");
                return true;
            }
            catch (Exception e) { Log.Warning("[Rim AI Expressive Portraits] Could not save Extra Features: " + e.Message); return false; }
        }
        internal static ExtractedTags BaseTags(Pawn p)
        {
            return ReadTags(BaseTagsPath(p));
        }
        internal static ExtractedTags ExpressionTags(Pawn p, PortraitState state)
        {
            return ReadTags(ExpressionTagsPath(p, state));
        }
        private static ExtractedTags ReadTags(string path)
        {
            try
            {
                return File.Exists(path) ? JsonUtility.FromJson<ExtractedTags>(File.ReadAllText(path)) : null;
            }
            catch (Exception e)
            {
                Log.Warning("[Rim AI Expressive Portraits] Could not read Base tags: " + e.Message);
                return null;
            }
        }
        internal static bool SaveBaseTags(Pawn p, ExtractedTags tags)
        {
            return WriteTags(BaseTagsPath(p), tags);
        }
        internal static bool SaveExpressionTags(Pawn p, PortraitState state, ExtractedTags tags)
        {
            return WriteTags(ExpressionTagsPath(p, state), tags);
        }
        private static bool WriteTags(string path, ExtractedTags tags)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.WriteAllText(path, JsonUtility.ToJson(tags));
                return true;
            }
            catch (Exception e)
            {
                Log.Warning("[Rim AI Expressive Portraits] Could not save Base tags: " + e.Message);
                return false;
            }
        }
        public static string NovelAiExtraTag(Pawn p)
        {
            string path = NovelAiExtraTagPath(p);
            try { return File.Exists(path) ? File.ReadAllText(path) : ""; }
            catch (Exception e) { Log.Warning("[Rim AI Expressive Portraits] Could not read NovelAI Extra Tag: " + e.Message); return ""; }
        }
        public static bool SaveNovelAiExtraTag(Pawn p, string value)
        {
            try
            {
                string path = NovelAiExtraTagPath(p);
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.WriteAllText(path, value ?? "");
                return true;
            }
            catch (Exception e) { Log.Warning("[Rim AI Expressive Portraits] Could not save NovelAI Extra Tag: " + e.Message); return false; }
        }
        public static string ComfyInpaintTag(Pawn p)
        {
            string path = ComfyInpaintTagPath(p);
            try { return File.Exists(path) ? File.ReadAllText(path) : ""; }
            catch (Exception e) { Log.Warning("[Rim AI Expressive Portraits] Could not read ComfyUI inpaint tag: " + e.Message); return ""; }
        }
        public static bool SaveComfyInpaintTag(Pawn p, string value)
        {
            try
            {
                string path = ComfyInpaintTagPath(p);
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.WriteAllText(path, value ?? "");
                return true;
            }
            catch (Exception e) { Log.Warning("[Rim AI Expressive Portraits] Could not save ComfyUI inpaint tag: " + e.Message); return false; }
        }
        internal static bool LegacyInpaintTagImported(Pawn p, PortraitState state, AiProvider provider)
        {
            return File.Exists(LegacyInpaintTagImportPath(p, state, provider));
        }
        internal static bool MarkLegacyInpaintTagImported(Pawn p, PortraitState state, AiProvider provider)
        {
            try
            {
                string path = LegacyInpaintTagImportPath(p, state, provider);
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.WriteAllText(path, "");
                return true;
            }
            catch (Exception e)
            {
                Log.Warning("[Rim AI Expressive Portraits] Could not mark legacy inpaint tag imported: " + e.Message);
                return false;
            }
        }
        public static float NovelAiInpaintStrength(Pawn p, float fallback)
        {
            float value;
            return TryReadNovelAiInpaintStrength(NovelAiInpaintStrengthPath(p), out value) ? value : fallback;
        }
        public static float NovelAiInpaintStrength(Pawn p, PortraitState state, float fallback)
        {
            float value;
            if (TryReadNovelAiInpaintStrength(NovelAiInpaintStrengthPath(p, state), out value)) return value;
            // Older pawn folders kept one strength for every expression.
            return NovelAiInpaintStrength(p, fallback);
        }
        private static bool TryReadNovelAiInpaintStrength(string path, out float value)
        {
            value = 0f;
            try
            {
                if (!File.Exists(path)) return false;
                if (float.TryParse(File.ReadAllText(path), NumberStyles.Float, CultureInfo.InvariantCulture, out value)
                    && !float.IsNaN(value) && !float.IsInfinity(value) && value >= .1f && value <= 1f)
                    return true;
                Log.Warning("[Rim AI Expressive Portraits] Invalid NovelAI inpaint strength in " + path);
            }
            catch (Exception e) { Log.Warning("[Rim AI Expressive Portraits] Could not read NovelAI inpaint strength: " + e.Message); }
            return false;
        }
        public static bool SaveNovelAiInpaintStrength(Pawn p, float value)
        {
            return SaveNovelAiInpaintStrength(NovelAiInpaintStrengthPath(p), value);
        }
        public static bool SaveNovelAiInpaintStrength(Pawn p, PortraitState state, float value)
        {
            return SaveNovelAiInpaintStrength(NovelAiInpaintStrengthPath(p, state), value);
        }
        private static bool SaveNovelAiInpaintStrength(string path, float value)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.WriteAllText(path, value.ToString("R", CultureInfo.InvariantCulture));
                return true;
            }
            catch (Exception e) { Log.Warning("[Rim AI Expressive Portraits] Could not save NovelAI inpaint strength: " + e.Message); return false; }
        }
        public static float LocalInpaintWeight(Pawn p, PortraitState state, float fallback)
        {
            string path = LocalInpaintWeightPath(p, state);
            try
            {
                if (!File.Exists(path)) return fallback;
                float value;
                if (float.TryParse(File.ReadAllText(path), NumberStyles.Float, CultureInfo.InvariantCulture, out value)
                    && !float.IsNaN(value) && !float.IsInfinity(value) && value >= .1f && value <= 1f)
                    return value;
                Log.Warning("[Rim AI Expressive Portraits] Invalid local inpaint weight in " + path);
            }
            catch (Exception e) { Log.Warning("[Rim AI Expressive Portraits] Could not read local inpaint weight: " + e.Message); }
            return fallback;
        }
        public static bool SaveLocalInpaintWeight(Pawn p, PortraitState state, float value)
        {
            try
            {
                string path = LocalInpaintWeightPath(p, state);
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.WriteAllText(path, value.ToString("R", CultureInfo.InvariantCulture));
                return true;
            }
            catch (Exception e) { Log.Warning("[Rim AI Expressive Portraits] Could not save local inpaint weight: " + e.Message); return false; }
        }
        /// <summary>Describes the behavior of this type or member.</summary>
        /// <summary>Describes the behavior of this type or member.</summary>
        public static void Save(Pawn p, PortraitState state, byte[] png)
        {
            if (state == PortraitState.Base) ExistingPathFor(p, state);
            string path = PathFor(p, state); Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllBytes(path, png);
            File.WriteAllBytes(Path.Combine(PawnFolder(p), PortraitStateNames.Display(state) + "_" + DateTime.Now.ToString("yyyyMMdd_HHmmss_fff") + ".png"), png);
            Invalidate(p, state);
            if (state == PortraitState.Base) SaveDeathPortrait(p, png);
        }
        /// <summary>Describes the behavior of this type or member.</summary>
        public static string[] Gallery(Pawn p)
        {
            string folder = PawnFolder(p); Directory.CreateDirectory(folder);
            return Directory.GetFiles(folder)
                .Where(path => new[] { ".png", ".jpg", ".jpeg", ".webp" }.Contains(Path.GetExtension(path).ToLowerInvariant()))
                .Where(path => !Enum.GetNames(typeof(PortraitState)).Concat(new[] { "Death" }).Contains(Path.GetFileNameWithoutExtension(path)))
                .OrderByDescending(File.GetLastWriteTimeUtc).ToArray();
        }
        /// <summary>Describes the behavior of this type or member.</summary>
        public static void SetActive(Pawn p, PortraitState state, string source)
        {
            if (state == PortraitState.Base) ExistingPathFor(p, state);
            string target = PathFor(p, state); Directory.CreateDirectory(Path.GetDirectoryName(target));
            var texture = new Texture2D(2, 2, TextureFormat.ARGB32, false);
            try
            {
                if (!texture.LoadImage(File.ReadAllBytes(source))) throw new InvalidDataException(Localization.T("RimAIPortrait.Preview.UnsupportedImage"));
                byte[] png = texture.EncodeToPNG();
                File.WriteAllBytes(target, png); Invalidate(p, state);
                if (state == PortraitState.Base) SaveDeathPortrait(p, png);
            }
            finally { UnityEngine.Object.Destroy(texture); }
        }
        /// <summary>Describes the behavior of this type or member.</summary>
        public static void Delete(Pawn p, IEnumerable<string> paths)
        {
            foreach (string path in paths.ToArray()) if (File.Exists(path) && Path.GetDirectoryName(path) == PawnFolder(p)) File.Delete(path);
        }
        /// <summary>Removes all active portrait assignments while preserving gallery images.</summary>
        public static void ResetPortraits(Pawn p)
        {
            foreach (PortraitState state in Enum.GetValues(typeof(PortraitState)))
            {
                string path = PathFor(p, state);
                if (File.Exists(path)) File.Delete(path);
                Invalidate(p, state);
            }
            string legacyBasePath = LegacyNormalPath(p);
            if (File.Exists(legacyBasePath)) File.Delete(legacyBasePath);
            string deathPath = DeathPath(p);
            if (File.Exists(deathPath)) File.Delete(deathPath);
            string deathKey = p.ThingID + "Death";
            MissingPortraits.Remove(deathKey);
            RemoveCachedTexture(deathKey);
        }
        /// <summary>Describes the behavior of this type or member.</summary>
    }
}
