using System;
using System.Collections.Generic;
using System.IO;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimAIPortrait
{
    public static partial class PortraitData
    {
        private static readonly Dictionary<string, Texture2D> Cache = new Dictionary<string, Texture2D>();
        private static readonly Dictionary<string, LinkedListNode<string>> CacheNodes = new Dictionary<string, LinkedListNode<string>>();
        private static readonly LinkedList<string> CacheOrder = new LinkedList<string>();
        private static readonly HashSet<string> MissingPortraits = new HashSet<string>();
        private static string transientKey;
        private static Texture2D transientTexture;
        private static Game cachedGame;

        public static void EnsureGameScope()
        {
            if (!ReferenceEquals(cachedGame, Current.Game))
            {
                ClearCaches();
                cachedGame = Current.Game;
            }
            _ = Root;
            TrimCache();
        }

        private static void ClearCaches()
        {
            foreach (Texture2D texture in Cache.Values)
                if (texture != null) UnityEngine.Object.Destroy(texture);
            Cache.Clear();
            CacheNodes.Clear();
            CacheOrder.Clear();
            ClearTransient();
            PawnFolders.Clear();
            MissingPortraits.Clear();
        }

        private static int CacheLimit
        {
            get
            {
                PortraitSettings settings = RimAIPortraitMod.Settings;
                return settings == null ? 100 : Mathf.Clamp(settings.PortraitCacheLimit, 0, 1000);
            }
        }

        private static void ClearTransient()
        {
            if (transientTexture != null) UnityEngine.Object.Destroy(transientTexture);
            transientTexture = null;
            transientKey = null;
        }

        private static bool TryGetCachedTexture(string key, out Texture2D texture)
        {
            if (CacheLimit == 0)
            {
                texture = transientKey == key ? transientTexture : null;
                return texture != null;
            }
            if (!Cache.TryGetValue(key, out texture)) return false;
            LinkedListNode<string> node = CacheNodes[key];
            if (node != CacheOrder.Last)
            {
                CacheOrder.Remove(node);
                CacheOrder.AddLast(node);
            }
            return true;
        }

        private static void StoreTexture(string key, Texture2D texture)
        {
            if (CacheLimit == 0)
            {
                ClearTransient();
                transientKey = key;
                transientTexture = texture;
                return;
            }
            Cache[key] = texture;
            CacheNodes[key] = CacheOrder.AddLast(key);
            TrimCache();
        }

        private static void TrimCache()
        {
            int limit = CacheLimit;
            int evicted = 0;
            while (Cache.Count > limit)
            {
                RemoveCachedTexture(CacheOrder.First.Value);
                evicted++;
            }
            if (limit > 0) ClearTransient();
            if (evicted > 0 && Current.Game != null && RimAIPortraitMod.Settings != null && RimAIPortraitMod.Settings.DebugMode)
                Messages.Message(Localization.T("RimAIPortrait.Debug.PortraitCacheEvicted", evicted, Cache.Count, limit),
                    MessageTypeDefOf.NeutralEvent, false);
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

                RemoveCachedTexture(p.ThingID + "Grayscale");
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
            if (CacheNodes.TryGetValue(key, out var node)) CacheOrder.Remove(node);
            CacheNodes.Remove(key);
            if (transientKey == key) ClearTransient();
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
                    RemoveCachedTexture(p.ThingID + "Death");
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
            if (TryGetCachedTexture(key, out var cached)) return cached;
            if (MissingPortraits.Contains(key)) return null;
            string deathPath = DeathPath(p);
            Texture2D texture = null;
            try
            {
                byte[] image = File.ReadAllBytes(deathPath);
                texture = new Texture2D(2, 2, TextureFormat.ARGB32, false);
                if (!texture.LoadImage(image)) throw new InvalidDataException(Localization.T("RimAIPortrait.Preview.UnsupportedImage"));
                texture.wrapMode = TextureWrapMode.Clamp;
                StoreTexture(key, texture);
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
            string key = p.ThingID + "Grayscale";
            if (TryGetCachedTexture(key, out var cached)) return cached;
            Texture2D source = Get(p, PortraitState.Base); if (source == null) return null;
            try
            {
                var pixels = source.GetPixels32();
                for (int i = 0; i < pixels.Length; i++) { byte y = (byte)(pixels[i].r * .299f + pixels[i].g * .587f + pixels[i].b * .114f); pixels[i].r = pixels[i].g = pixels[i].b = y; }
                var gray = new Texture2D(source.width, source.height, TextureFormat.ARGB32, false); gray.SetPixels32(pixels); gray.Apply(); gray.wrapMode = TextureWrapMode.Clamp;
                StoreTexture(key, gray); return gray;
            }
            catch (Exception e) { Log.Error("[Rim AI Expressive Portraits] Grayscale conversion failed: " + e); return source; }
        }
        /// <summary>Describes the behavior of this type or member.</summary>
        public static Texture2D Get(Pawn p, PortraitState state)
        {
            EnsureGameScope();
            if (state == PortraitState.Normal) ExistingPathFor(p, PortraitState.Base);
            string key = p.ThingID + state; if (TryGetCachedTexture(key, out var tex)) return tex;
            if (MissingPortraits.Contains(key)) return FallbackPortrait(p, state);
            string path = ExistingPathFor(p, state);
            tex = null;
            try
            {
                byte[] image = File.ReadAllBytes(path);
                tex = new Texture2D(2, 2, TextureFormat.ARGB32, false);
                if (!tex.LoadImage(image)) throw new InvalidDataException(Localization.T("RimAIPortrait.Preview.UnsupportedImage"));
                tex.wrapMode = TextureWrapMode.Clamp;
                StoreTexture(key, tex);
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
