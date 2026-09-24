// Contains the implementation for PortraitData.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
        private const string PawnFolderSeparator = "__";
        public static string Root
        {
            get
            {
                if (rootPath != null) return rootPath;
                string documents = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                string root = Path.Combine(documents, "RimAIPortraits");
                string legacy = Path.Combine(documents, "rim-portraits");
                if (!Directory.Exists(root) && Directory.Exists(legacy))
                {
                    try { Directory.Move(legacy, root); }
                    catch (Exception e) { Log.Warning("[Rim AI Expressive Portraits] Could not migrate portrait root folder: " + e.Message); }
                }
                rootPath = root;
                return rootPath;
            }
        }
        public static void EnsureGameScope()
        {
            if (ReferenceEquals(cachedGame, Current.Game)) return;
            ClearCaches();
            cachedGame = Current.Game;
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
            string cached;
            if (PawnFolders.TryGetValue(p.ThingID, out cached)) return cached;
            string displayName = SafeName(p.Name?.ToStringShort ?? p.LabelShort ?? "Unnamed");
            string pawnId = SafeName(p.ThingID);
            string preferred = Path.Combine(Root, displayName + PawnFolderSeparator + pawnId);
            if (Directory.Exists(preferred)) return PawnFolders[p.ThingID] = preferred;

            // Preserve the intended portrait behavior and compatibility.
            // Preserve the intended portrait behavior and compatibility.
            if (Directory.Exists(Root))
            {
                string suffix = PawnFolderSeparator + pawnId;
                try
                {
                    string existing = Directory.EnumerateDirectories(Root)
                        .FirstOrDefault(path => Path.GetFileName(path).EndsWith(suffix, StringComparison.OrdinalIgnoreCase));
                    if (existing != null) return PawnFolders[p.ThingID] = existing;
                }
                catch (IOException e) { Log.Warning("[Rim AI Expressive Portraits] Could not inspect portrait folders: " + e.Message); }
                catch (UnauthorizedAccessException e) { Log.Warning("[Rim AI Expressive Portraits] Could not inspect portrait folders: " + e.Message); }
            }

            // Preserve the intended portrait behavior and compatibility.
            // Preserve the intended portrait behavior and compatibility.
            // Preserve the intended portrait behavior and compatibility.
            string legacy = Path.Combine(Root, displayName);
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
            return state == PortraitState.Normal && !File.Exists(path) && File.Exists(LegacyNormalPath(p)) ? LegacyNormalPath(p) : path;
        }
        /// <summary>Describes the behavior of this type or member.</summary>
        public static string ExtraFeaturesPath(Pawn p) => Path.Combine(PawnFolder(p), "extra_features.txt");
        /// <summary>Describes the behavior of this type or member.</summary>
        public static string BasePromptPath(Pawn p) => Path.Combine(PawnFolder(p), "base_prompt.txt");
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
        /// <summary>Describes the behavior of this type or member.</summary>
        public static string BasePrompt(Pawn p)
        {
            string path = BasePromptPath(p);
            try { return File.Exists(path) ? File.ReadAllText(path) : ""; }
            catch (Exception e) { Log.Warning("[Rim AI Expressive Portraits] Could not read Base Prompt override: " + e.Message); return ""; }
        }
        /// <summary>Describes the behavior of this type or member.</summary>
        public static bool SaveBasePrompt(Pawn p, string value)
        {
            try
            {
                string path = BasePromptPath(p);
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.WriteAllText(path, value ?? "");
                return true;
            }
            catch (Exception e) { Log.Warning("[Rim AI Expressive Portraits] Could not save Base Prompt override: " + e.Message); return false; }
        }
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
            string path = PathFor(p, state); Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllBytes(path, png);
            File.WriteAllBytes(Path.Combine(PawnFolder(p), PortraitStateNames.Display(state) + "_" + DateTime.Now.ToString("yyyyMMdd_HHmmss_fff") + ".png"), png);
            Invalidate(p, state);
            if (state == PortraitState.Normal) SaveDeathPortrait(p, png);
        }
        /// <summary>Describes the behavior of this type or member.</summary>
        public static string[] Gallery(Pawn p)
        {
            string folder = PawnFolder(p); Directory.CreateDirectory(folder);
            return Directory.GetFiles(folder)
                .Where(path => new[] { ".png", ".jpg", ".jpeg", ".webp" }.Contains(Path.GetExtension(path).ToLowerInvariant()))
                .Where(path => !Enum.GetNames(typeof(PortraitState)).Concat(new[] { "Base", "Death" }).Contains(Path.GetFileNameWithoutExtension(path)))
                .OrderByDescending(File.GetLastWriteTimeUtc).ToArray();
        }
        /// <summary>Describes the behavior of this type or member.</summary>
        public static void SetActive(Pawn p, PortraitState state, string source)
        {
            string target = PathFor(p, state); Directory.CreateDirectory(Path.GetDirectoryName(target));
            var texture = new Texture2D(2, 2, TextureFormat.ARGB32, false);
            try
            {
                if (!texture.LoadImage(File.ReadAllBytes(source))) throw new InvalidDataException(Localization.T("RimAIPortrait.Preview.UnsupportedImage"));
                byte[] png = texture.EncodeToPNG();
                File.WriteAllBytes(target, png); Invalidate(p, state);
                if (state == PortraitState.Normal) SaveDeathPortrait(p, png);
            }
            finally { UnityEngine.Object.Destroy(texture); }
        }
        /// <summary>Describes the behavior of this type or member.</summary>
        public static void Delete(Pawn p, IEnumerable<string> paths)
        {
            foreach (string path in paths.ToArray()) if (File.Exists(path) && Path.GetDirectoryName(path) == PawnFolder(p)) File.Delete(path);
        }
        /// <summary>Describes the behavior of this type or member.</summary>
        private static void Invalidate(Pawn p, PortraitState state)
        {
            MissingPortraits.Remove(p.ThingID + state);
            // Preserve the intended portrait behavior and compatibility.
            // Preserve the intended portrait behavior and compatibility.
            // Preserve the intended portrait behavior and compatibility.
            if (state == PortraitState.Normal)
            {
                foreach (PortraitState cachedState in Enum.GetValues(typeof(PortraitState)))
                    RemoveCachedTexture(p.ThingID + cachedState);

                if (GrayCache.TryGetValue(p.ThingID, out var gray)) UnityEngine.Object.Destroy(gray);
                GrayCache.Remove(p.ThingID);
                return;
            }

            RemoveCachedTexture(p.ThingID + state);
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
            if (!File.Exists(deathPath)) { MissingPortraits.Add(key); return null; }
            try
            {
                var texture = new Texture2D(2, 2, TextureFormat.ARGB32, false);
                if (!texture.LoadImage(File.ReadAllBytes(deathPath))) { UnityEngine.Object.Destroy(texture); return null; }
                texture.wrapMode = TextureWrapMode.Clamp; Cache[key] = texture; return texture;
            }
            catch (Exception e) { Log.Warning("[Rim AI Expressive Portraits] Could not load Death portrait: " + e.Message); return null; }
        }
        /// <summary>Describes the behavior of this type or member.</summary>
        public static Texture2D GetGrayscale(Pawn p)
        {
            if (GrayCache.TryGetValue(p.ThingID, out var cached)) return cached;
            Texture2D source = Get(p, PortraitState.Normal); if (source == null) return null;
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
            string key = p.ThingID + state; if (Cache.TryGetValue(key, out var tex)) return tex;
            if (MissingPortraits.Contains(key)) return state == PortraitState.Normal ? null : Get(p, PortraitState.Normal);
            string path = ExistingPathFor(p, state);
            if (!File.Exists(path))
            {
                MissingPortraits.Add(key);
                return state == PortraitState.Normal ? null : Get(p, PortraitState.Normal);
            }
            try
            {
                tex = new Texture2D(2, 2, TextureFormat.ARGB32, false);
                if (!tex.LoadImage(File.ReadAllBytes(path))) { UnityEngine.Object.Destroy(tex); MissingPortraits.Add(key); return null; }
                tex.wrapMode = TextureWrapMode.Clamp; Cache[key] = tex; return tex;
            }
            catch (Exception e) { Log.Error("[Rim AI Expressive Portraits] Could not load portrait: " + e); return null; }
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
