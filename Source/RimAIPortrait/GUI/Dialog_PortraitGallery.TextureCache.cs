using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace RimAIPortrait
{
    public sealed partial class Dialog_PortraitGallery
    {
        private readonly Dictionary<string, Texture2D> textures = new Dictionary<string, Texture2D>();
        private readonly Dictionary<string, int> textureUse = new Dictionary<string, int>();
        private int textureUseClock;
        private const int TextureCacheLimit = 64;
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
    }
}
