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
    public static class PortraitData
    {
        private static readonly Dictionary<string, Texture2D> Cache = new Dictionary<string, Texture2D>();
        private static readonly Dictionary<string, Texture2D> GrayCache = new Dictionary<string, Texture2D>();
        private static readonly Dictionary<string, string> PawnFolders = new Dictionary<string, string>();
        private static readonly HashSet<string> MissingPortraits = new HashSet<string>();
        private static Game cachedGame;
        private static string rootPath;
        private static string configuredRoot;
        private static bool recheckRoot = true;
        private const string PawnFolderSeparator = "__";
        public static string DefaultRoot => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "RimAIPortraits");

        public static bool TryNormalizeCustomRoot(string folder, out string path)
        {
            path = null;
            if (string.IsNullOrWhiteSpace(folder)) return false;
            try
            {
                string value = folder.Trim();
                string root = Path.GetPathRoot(value);
                bool driveAbsolute = root != null && root.Length >= 3 && root[1] == ':'
                    && (root[2] == '\\' || root[2] == '/');
                bool networkAbsolute = root != null && root.StartsWith(@"\\", StringComparison.Ordinal)
                    && root.IndexOf('\\', 2) > 2 && root.IndexOf('\\', 2) < root.Length - 1;
                if (!Path.IsPathRooted(value) || (!driveAbsolute && !networkAbsolute)) return false;
                path = Path.GetFullPath(value);
                return true;
            }
            catch (ArgumentException) { return false; }
            catch (NotSupportedException) { return false; }
            catch (PathTooLongException) { return false; }
        }

        public static bool TryGetCustomRoot(string folder, out string path)
        {
            if (!TryNormalizeCustomRoot(folder, out path)) return false;
            if (Directory.Exists(path)) return true;
            path = null;
            return false;
        }

        public static void RecheckRoot()
        {
            recheckRoot = true;
            _ = Root;
        }

        public static string Root
        {
            get
            {
                string configured = RimAIPortraitMod.Settings?.PortraitRootFolder;
                if (!recheckRoot && rootPath != null
                    && string.Equals(configuredRoot, configured, StringComparison.Ordinal)) return rootPath;
                string custom;
                bool useCustom = TryGetCustomRoot(configured, out custom);
                string root = useCustom ? custom : DefaultRoot;
                if (!useCustom)
                {
                    string legacy = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "rim-portraits");
                    if (!Directory.Exists(root) && Directory.Exists(legacy))
                    {
                        try { Directory.Move(legacy, root); }
                        catch (Exception e) { Log.Warning("[Rim AI Expressive Portraits] Could not migrate portrait root folder: " + e.Message); }
                    }
                }
                if (rootPath != null && !string.Equals(rootPath, root, StringComparison.OrdinalIgnoreCase)) ClearCaches();
                rootPath = root;
                configuredRoot = configured;
                recheckRoot = false;
                return rootPath;
            }
        }
        public static void EnsureGameScope()
        {
            if (!ReferenceEquals(cachedGame, Current.Game))
            {
                ClearCaches();
                cachedGame = Current.Game;
            }
            _ = Root;
        }

        private static void ClearCaches()
        {
            var destroyed = new HashSet<Texture2D>();
            foreach (Texture2D texture in Cache.Values)
                if (texture != null && destroyed.Add(texture)) UnityEngine.Object.Destroy(texture);
            foreach (Texture2D texture in GrayCache.Values)
                if (texture != null && destroyed.Add(texture)) UnityEngine.Object.Destroy(texture);
            Cache.Clear();
            GrayCache.Clear();
            PawnFolders.Clear();
            MissingPortraits.Clear();
        }
        /// <summary>Describes the behavior of this type or member.</summary>
        public static string PawnFolder(Pawn p)
        {
            EnsureGameScope();
            string root = Root;
            string cached;
            if (PawnFolders.TryGetValue(p.ThingID, out cached)) return cached;
            string displayName = SafeName(p.Name?.ToStringShort ?? p.LabelShort ?? "Unnamed");
            string pawnId = SafeName(p.ThingID);
            string preferred = Path.Combine(root, displayName + PawnFolderSeparator + pawnId);
            if (Directory.Exists(preferred)) return PawnFolders[p.ThingID] = preferred;

            // Preserve the intended portrait behavior and compatibility.
            // Preserve the intended portrait behavior and compatibility.
            if (Directory.Exists(root))
            {
                string suffix = PawnFolderSeparator + pawnId;
                try
                {
                    string existing = Directory.EnumerateDirectories(root)
                        .FirstOrDefault(path => Path.GetFileName(path).EndsWith(suffix, StringComparison.OrdinalIgnoreCase));
                    if (existing != null) return PawnFolders[p.ThingID] = existing;
                }
                catch (IOException e) { Log.Warning("[Rim AI Expressive Portraits] Could not inspect portrait folders: " + e.Message); }
                catch (UnauthorizedAccessException e) { Log.Warning("[Rim AI Expressive Portraits] Could not inspect portrait folders: " + e.Message); }
            }

            // Preserve the intended portrait behavior and compatibility.
            // Preserve the intended portrait behavior and compatibility.
            // Preserve the intended portrait behavior and compatibility.
            string legacy = Path.Combine(root, displayName);
            if (Directory.Exists(legacy))
            {
                try { Directory.Move(legacy, preferred); return PawnFolders[p.ThingID] = preferred; }
                catch (IOException e) { Log.Warning("[Rim AI Expressive Portraits] Could not migrate legacy portrait folder: " + e.Message); }
                catch (UnauthorizedAccessException e) { Log.Warning("[Rim AI Expressive Portraits] Could not migrate legacy portrait folder: " + e.Message); }
            }
            PawnFolders[p.ThingID] = preferred;
            return preferred;
        }
        /// <summary>Describes the behavior of this type or member.</summary>
        public static string PathFor(Pawn p, PortraitState state) => Path.Combine(PawnFolder(p), PortraitStateNames.Display(state) + ".png");
        /// <summary>Describes the behavior of this type or member.</summary>
        public static string DeathPath(Pawn p) => Path.Combine(PawnFolder(p), "Death.png");
        /// <summary>Describes the behavior of this type or member.</summary>
        private static string LegacyNormalPath(Pawn p) => Path.Combine(PawnFolder(p), "Normal.png");
        /// <summary>Describes the behavior of this type or member.</summary>
        public static string ExistingPathFor(Pawn p, PortraitState state)
        {
            string path = PathFor(p, state);
            if (state == PortraitState.Base && !File.Exists(path))
            {
                string legacy = LegacyNormalPath(p);
                if (File.Exists(legacy))
                {
                    try { File.Move(legacy, path); }
                    catch (Exception e)
                    {
                        Log.Warning("[Rim AI Expressive Portraits] Could not migrate legacy Base portrait: " + e.Message);
                        return legacy;
                    }
                }
            }
            return path;
        }
        /// <summary>Describes the behavior of this type or member.</summary>
        public static string ExtraFeaturesPath(Pawn p) => Path.Combine(PawnFolder(p), "extra_features.txt");
        public static string BaseTagsPath(Pawn p) => Path.Combine(PawnFolder(p), "base_tags.json");
        public static string ExpressionTagsPath(Pawn p, PortraitState state) =>
            Path.Combine(PawnFolder(p), "expression_tags_" + state.ToString().ToLowerInvariant() + ".json");
        public static string NovelAiExtraTagPath(Pawn p) => Path.Combine(PawnFolder(p), "novelai_extra_tag.txt");
        public static string ComfyInpaintTagPath(Pawn p) => Path.Combine(PawnFolder(p), "comfy_inpaint_tag.txt");
        public static string NovelAiInpaintStrengthPath(Pawn p) => Path.Combine(PawnFolder(p), "novelai_inpaint_strength.txt");
        public static string NovelAiInpaintStrengthPath(Pawn p, PortraitState state) =>
            Path.Combine(PawnFolder(p), "novelai_inpaint_strength_" + state.ToString().ToLowerInvariant() + ".txt");
        /// <summary>Describes the behavior of this type or member.</summary>
        /// <summary>Describes the behavior of this type or member.</summary>
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
        /// <summary>Describes the behavior of this type or member.</summary>
        /// <summary>Describes the behavior of this type or member.</summary>
        public static string SafeId(Pawn p) => SafeName(p.ThingID);
        /// <summary>Describes the behavior of this type or member.</summary>
        private static string SafeName(string value)
        {
            if (value.NullOrEmpty()) return "Unnamed";
            foreach (char c in Path.GetInvalidFileNameChars()) value = value.Replace(c, '_');
            return value.Trim().NullOrEmpty() ? "Unnamed" : value.Trim();
        }
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
        private static void Invalidate(Pawn p, PortraitState state)
        {
            MissingPortraits.Remove(p.ThingID + state);
            // Preserve the intended portrait behavior and compatibility.
            // Preserve the intended portrait behavior and compatibility.
            // Preserve the intended portrait behavior and compatibility.
            if (state == PortraitState.Base)
            {
                foreach (PortraitState cachedState in Enum.GetValues(typeof(PortraitState)))
                    RemoveCachedTexture(p.ThingID + cachedState);

                if (GrayCache.TryGetValue(p.ThingID, out var gray)) UnityEngine.Object.Destroy(gray);
                GrayCache.Remove(p.ThingID);
                return;
            }

            RemoveCachedTexture(p.ThingID + state);
        }
        public static void RefreshEditedPortrait(Pawn p, PortraitState state)
        {
            EnsureGameScope();
            Invalidate(p, state);
            if (state == PortraitState.Base)
                SaveDeathPortrait(p, File.ReadAllBytes(ExistingPathFor(p, state)));
        }
        /// <summary>Describes the behavior of this type or member.</summary>
        private static void RemoveCachedTexture(string key)
        {
            if (Cache.TryGetValue(key, out var texture)) UnityEngine.Object.Destroy(texture);
            Cache.Remove(key);
        }
        /// <summary>Describes the behavior of this type or member.</summary>
        private static void SaveDeathPortrait(Pawn p, byte[] sourcePng)
        {
            var source = new Texture2D(2, 2, TextureFormat.ARGB32, false);
            try
            {
                if (!source.LoadImage(sourcePng)) throw new InvalidDataException("Unsupported Base portrait image.");
                Color32[] pixels = source.GetPixels32();
                for (int i = 0; i < pixels.Length; i++)
                {
                    byte alpha = pixels[i].a;
                    float luminance = pixels[i].r * .299f + pixels[i].g * .587f + pixels[i].b * .114f;
                    byte gray = (byte)Mathf.Clamp(luminance * .75f, 0f, 255f);
                    pixels[i] = new Color32(gray, gray, gray, alpha);
                }
                var death = new Texture2D(source.width, source.height, TextureFormat.ARGB32, false);
                try
                {
                    death.SetPixels32(pixels); death.Apply();
                    string path = DeathPath(p); Directory.CreateDirectory(Path.GetDirectoryName(path));
                    File.WriteAllBytes(path, death.EncodeToPNG());
                    MissingPortraits.Remove(p.ThingID + "Death");
                    if (Cache.TryGetValue(p.ThingID + "Death", out var old)) UnityEngine.Object.Destroy(old);
                    Cache.Remove(p.ThingID + "Death");
                }
                finally { UnityEngine.Object.Destroy(death); }
            }
            catch (Exception e) { Log.Warning("[Rim AI Expressive Portraits] Could not create Death portrait: " + e.Message); }
            finally { UnityEngine.Object.Destroy(source); }
        }
        /// <summary>Describes the behavior of this type or member.</summary>
        public static Texture2D GetDeath(Pawn p)
        {
            EnsureGameScope();
            string key = p.ThingID + "Death";
            if (Cache.TryGetValue(key, out var cached)) return cached;
            if (MissingPortraits.Contains(key)) return null;
            string deathPath = DeathPath(p);
            Texture2D texture = null;
            try
            {
                byte[] image = File.ReadAllBytes(deathPath);
                texture = new Texture2D(2, 2, TextureFormat.ARGB32, false);
                if (!texture.LoadImage(image)) throw new InvalidDataException(Localization.T("RimAIPortrait.Preview.UnsupportedImage"));
                texture.wrapMode = TextureWrapMode.Clamp;
                Cache[key] = texture;
                Texture2D loaded = texture;
                texture = null;
                return loaded;
            }
            catch (FileNotFoundException) { MissingPortraits.Add(key); return null; }
            catch (DirectoryNotFoundException) { MissingPortraits.Add(key); return null; }
            catch (Exception e) { ReportLoadFailure(p, key, deathPath, e); return null; }
            finally { if (texture != null) UnityEngine.Object.Destroy(texture); }
        }
        /// <summary>Describes the behavior of this type or member.</summary>
        public static Texture2D GetGrayscale(Pawn p)
        {
            EnsureGameScope();
            if (GrayCache.TryGetValue(p.ThingID, out var cached)) return cached;
            Texture2D source = Get(p, PortraitState.Base); if (source == null) return null;
            try
            {
                var pixels = source.GetPixels32();
                for (int i = 0; i < pixels.Length; i++) { byte y = (byte)(pixels[i].r * .299f + pixels[i].g * .587f + pixels[i].b * .114f); pixels[i].r = pixels[i].g = pixels[i].b = y; }
                var gray = new Texture2D(source.width, source.height, TextureFormat.ARGB32, false); gray.SetPixels32(pixels); gray.Apply(); gray.wrapMode = TextureWrapMode.Clamp;
                GrayCache[p.ThingID] = gray; return gray;
            }
            catch (Exception e) { Log.Error("[Rim AI Expressive Portraits] Grayscale conversion failed: " + e); return source; }
        }
        /// <summary>Describes the behavior of this type or member.</summary>
        public static Texture2D Get(Pawn p, PortraitState state)
        {
            EnsureGameScope();
            if (state == PortraitState.Normal) ExistingPathFor(p, PortraitState.Base);
            string key = p.ThingID + state; if (Cache.TryGetValue(key, out var tex)) return tex;
            if (MissingPortraits.Contains(key)) return FallbackPortrait(p, state);
            string path = ExistingPathFor(p, state);
            tex = null;
            try
            {
                byte[] image = File.ReadAllBytes(path);
                tex = new Texture2D(2, 2, TextureFormat.ARGB32, false);
                if (!tex.LoadImage(image)) throw new InvalidDataException(Localization.T("RimAIPortrait.Preview.UnsupportedImage"));
                tex.wrapMode = TextureWrapMode.Clamp;
                Cache[key] = tex;
                Texture2D loaded = tex;
                tex = null;
                return loaded;
            }
            catch (FileNotFoundException) { MissingPortraits.Add(key); }
            catch (DirectoryNotFoundException) { MissingPortraits.Add(key); }
            catch (Exception e) { ReportLoadFailure(p, key, path, e); }
            finally { if (tex != null) UnityEngine.Object.Destroy(tex); }
            return FallbackPortrait(p, state);
        }

        private static Texture2D FallbackPortrait(Pawn p, PortraitState state)
        {
            if (state == PortraitState.Base) return null;
            if (state == PortraitState.Angry || state == PortraitState.Distressed)
            {
                Texture2D low = Get(p, PortraitState.Low);
                if (low != null) return low;
            }
            return Get(p, PortraitState.Base);
        }

        private static void ReportLoadFailure(Pawn p, string key, string path, Exception error)
        {
            if (!MissingPortraits.Add(key)) return;
            Log.Warning("[Rim AI Expressive Portraits] Could not load portrait " + path + ": " + error.Message);
            Messages.Message(Localization.T("RimAIPortrait.Error.PortraitLoadFailed", p.LabelShort, Path.GetFileName(path)),
                MessageTypeDefOf.RejectInput, false);
        }

        public static void RefreshExternalFiles(Pawn p)
        {
            PawnFolders.Remove(p.ThingID);
            foreach (PortraitState state in Enum.GetValues(typeof(PortraitState)))
                MissingPortraits.Remove(p.ThingID + state);
            MissingPortraits.Remove(p.ThingID + "Death");
        }
    }
}
