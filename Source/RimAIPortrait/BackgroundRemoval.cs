using System;
using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace RimAIPortrait
{
    /// <summary>Removes edge-connected pixels that resemble a configured background color.</summary>
    internal static class BackgroundRemoval
    {
        public static byte[] Remove(byte[] imageData, string colorHex, float sensitivity, int erosionPixels)
        {
            Color32 background;
            if (imageData == null || !TryParseHex(colorHex, out background)) return imageData;

            var texture = new Texture2D(2, 2, TextureFormat.ARGB32, false);
            try
            {
                if (!texture.LoadImage(imageData)) return imageData;
                int width = texture.width;
                int height = texture.height;
                Color32[] pixels = texture.GetPixels32();
                bool[] visited = new bool[pixels.Length];
                bool[] removed = new bool[pixels.Length];
                var queue = new Queue<int>();
                var removedPixels = new List<int>();
                float normalizedThreshold = Mathf.Clamp01(sensitivity) * .2f;
                float maximumDistance = 255f * Mathf.Sqrt(3f);
                float thresholdSquared = normalizedThreshold * maximumDistance;
                thresholdSquared *= thresholdSquared;

                for (int x = 0; x < width; x++)
                {
                    Enqueue(x, pixels, visited, queue, background, thresholdSquared);
                    Enqueue((height - 1) * width + x, pixels, visited, queue, background, thresholdSquared);
                }
                for (int y = 1; y < height - 1; y++)
                {
                    Enqueue(y * width, pixels, visited, queue, background, thresholdSquared);
                    Enqueue(y * width + width - 1, pixels, visited, queue, background, thresholdSquared);
                }

                while (queue.Count > 0)
                {
                    int index = queue.Dequeue();
                    Color32 pixel = pixels[index];
                    pixel.a = 0;
                    pixels[index] = pixel;
                    removed[index] = true;
                    removedPixels.Add(index);
                    int x = index % width;
                    int y = index / width;
                    if (x > 0) Enqueue(index - 1, pixels, visited, queue, background, thresholdSquared);
                    if (x + 1 < width) Enqueue(index + 1, pixels, visited, queue, background, thresholdSquared);
                    if (y > 0) Enqueue(index - width, pixels, visited, queue, background, thresholdSquared);
                    if (y + 1 < height) Enqueue(index + width, pixels, visited, queue, background, thresholdSquared);
                }

                ErodeForeground(pixels, removed, removedPixels, width, height, Mathf.Clamp(erosionPixels, 0, 10));

                texture.SetPixels32(pixels);
                texture.Apply(false, false);
                return texture.EncodeToPNG();
            }
            catch (Exception exception)
            {
                Log.Warning("[Rim AI Expressive Portraits] Background removal failed: " + exception.Message);
                return imageData;
            }
            finally
            {
                UnityEngine.Object.Destroy(texture);
            }
        }

        /// <summary>Moves all visible pixels down until the lowest one touches the canvas edge.</summary>
        public static byte[] AlignBottom(byte[] imageData)
        {
            if (imageData == null) return imageData;

            var texture = new Texture2D(2, 2, TextureFormat.ARGB32, false);
            try
            {
                if (!texture.LoadImage(imageData)) return imageData;
                int width = texture.width;
                int height = texture.height;
                Color32[] pixels = texture.GetPixels32();
                int lowestVisibleRow = height;

                for (int y = 0; y < height && lowestVisibleRow == height; y++)
                {
                    int rowStart = y * width;
                    for (int x = 0; x < width; x++)
                    {
                        if (pixels[rowStart + x].a == 0) continue;
                        lowestVisibleRow = y;
                        break;
                    }
                }

                if (lowestVisibleRow <= 0 || lowestVisibleRow == height) return imageData;

                var aligned = new Color32[pixels.Length];
                int rowsToCopy = height - lowestVisibleRow;
                Array.Copy(pixels, lowestVisibleRow * width, aligned, 0, rowsToCopy * width);
                texture.SetPixels32(aligned);
                texture.Apply(false, false);
                return texture.EncodeToPNG();
            }
            catch (Exception exception)
            {
                Log.Warning("[Rim AI Expressive Portraits] Portrait alignment failed: " + exception.Message);
                return imageData;
            }
            finally
            {
                UnityEngine.Object.Destroy(texture);
            }
        }

        public static bool TryParseHex(string value, out Color32 color)
        {
            string hex = (value ?? "").Trim().TrimStart('#');
            uint rgb;
            if (hex.Length != 6 || !uint.TryParse(hex, System.Globalization.NumberStyles.HexNumber,
                System.Globalization.CultureInfo.InvariantCulture, out rgb))
            {
                color = new Color32(255, 255, 255, 255);
                return false;
            }
            color = new Color32((byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb, 255);
            return true;
        }

        private static void Enqueue(int index, Color32[] pixels, bool[] visited, Queue<int> queue,
            Color32 background, float thresholdSquared)
        {
            if (visited[index]) return;
            visited[index] = true;
            float red = pixels[index].r - background.r;
            float green = pixels[index].g - background.g;
            float blue = pixels[index].b - background.b;
            if (red * red + green * green + blue * blue <= thresholdSquared) queue.Enqueue(index);
        }

        private static void ErodeForeground(Color32[] pixels, bool[] removed, List<int> frontier,
            int width, int height, int erosionPixels)
        {
            for (int step = 0; step < erosionPixels && frontier.Count > 0; step++)
            {
                var next = new List<int>();
                foreach (int index in frontier)
                {
                    int centerX = index % width;
                    int centerY = index / width;
                    for (int offsetY = -1; offsetY <= 1; offsetY++)
                    {
                        int y = centerY + offsetY;
                        if (y < 0 || y >= height) continue;
                        for (int offsetX = -1; offsetX <= 1; offsetX++)
                        {
                            int x = centerX + offsetX;
                            if ((offsetX == 0 && offsetY == 0) || x < 0 || x >= width) continue;
                            int neighbor = y * width + x;
                            if (removed[neighbor]) continue;
                            removed[neighbor] = true;
                            Color32 pixel = pixels[neighbor];
                            pixel.a = 0;
                            pixels[neighbor] = pixel;
                            next.Add(neighbor);
                        }
                    }
                }
                frontier = next;
            }
        }
    }
}
