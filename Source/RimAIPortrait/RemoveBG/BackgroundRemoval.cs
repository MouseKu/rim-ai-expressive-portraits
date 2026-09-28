using System;
using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace RimAIPortrait
{
    internal struct BackgroundRemovalStep
    {
        public string ColorHex;
        public float Sensitivity;
        public int ErosionPixels;
        public bool RemoveEnclosed;
        public float EnclosedCleanup;
        public string PickedPath;
        public int PickedX;
        public int PickedY;
    }

    /// <summary>Removes edge-connected pixels that resemble a configured background color.</summary>
    internal static class BackgroundRemoval
    {
        public static byte[] ApplyStep(byte[] imageData, BackgroundRemovalStep step, string path)
        {
            bool pickedImage = string.Equals(path, step.PickedPath, StringComparison.OrdinalIgnoreCase);
            return Remove(imageData, step.ColorHex, step.Sensitivity, step.ErosionPixels,
                step.RemoveEnclosed, step.EnclosedCleanup, pickedImage ? step.PickedX : -1,
                pickedImage ? step.PickedY : -1);
        }

        public static byte[] ApplySteps(byte[] imageData, IList<BackgroundRemovalStep> steps, string path)
        {
            foreach (BackgroundRemovalStep step in steps)
                imageData = ApplyStep(imageData, step, path);
            return imageData;
        }

        public static byte[] Remove(byte[] imageData, string colorHex, float sensitivity, int erosionPixels,
            bool removeEnclosed = false, float enclosedCleanup = .5f, int pickedX = -1, int pickedY = -1)
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

                if (pickedX >= 0 && pickedX < width && pickedY >= 0 && pickedY < height)
                    AddPickedBackgroundRegion(pixels, removed, removedPixels, width, height, background,
                        thresholdSquared, pickedY * width + pickedX);
                if (removeEnclosed)
                    AddEnclosedBackgroundRegions(pixels, removed, removedPixels, width, height, background,
                        thresholdSquared, enclosedCleanup);
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
            if (pixels[index].a == 0) return;
            if (ColorDistanceSquared(pixels[index], background) <= thresholdSquared) queue.Enqueue(index);
        }

        private static int ColorDistanceSquared(Color32 pixel, Color32 background)
        {
            int red = pixel.r - background.r;
            int green = pixel.g - background.g;
            int blue = pixel.b - background.b;
            return red * red + green * green + blue * blue;
        }

        private static void AddPickedBackgroundRegion(Color32[] pixels, bool[] removed, List<int> frontier,
            int width, int height, Color32 background, float thresholdSquared, int start)
        {
            // A manual click expands from the chosen pixel even when automatic enclosed cleanup is off.
            if (removed[start] || pixels[start].a == 0
                || ColorDistanceSquared(pixels[start], background) > thresholdSquared) return;

            var visited = new bool[pixels.Length];
            var component = new List<int> { start };
            visited[start] = true;
            for (int head = 0; head < component.Count; head++)
            {
                int index = component[head];
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
                        if (removed[neighbor] || visited[neighbor] || pixels[neighbor].a == 0
                            || ColorDistanceSquared(pixels[neighbor], background) > thresholdSquared) continue;
                        visited[neighbor] = true;
                        component.Add(neighbor);
                    }
                }
            }

            foreach (int index in component)
            {
                removed[index] = true;
                Color32 pixel = pixels[index];
                pixel.a = 0;
                pixels[index] = pixel;
                frontier.Add(index);
            }
        }

        private static void AddEnclosedBackgroundRegions(Color32[] pixels, bool[] removed, List<int> frontier,
            int width, int height, Color32 background, float thresholdSquared, float cleanup)
        {
            float amount = Mathf.Clamp01(cleanup);
            float minAreaRatio = amount <= .5f
                ? Mathf.Lerp(.001f, .0002f, amount * 2f)
                : Mathf.Lerp(.0002f, .00005f, (amount - .5f) * 2f);
            float requiredStrictRatio = amount <= .5f
                ? Mathf.Lerp(.95f, .8f, amount * 2f)
                : Mathf.Lerp(.8f, .65f, (amount - .5f) * 2f);
            int minArea = Mathf.CeilToInt(pixels.Length * minAreaRatio);
            float strictThresholdSquared = thresholdSquared * .16f;
            var visited = new bool[pixels.Length];
            var component = new List<int>();

            for (int start = 0; start < pixels.Length; start++)
            {
                if (visited[start] || pixels[start].a == 0
                    || ColorDistanceSquared(pixels[start], background) > thresholdSquared) continue;
                component.Clear();
                component.Add(start);
                visited[start] = true;
                int strictCount = 0;
                bool touchesOuter = false;
                for (int head = 0; head < component.Count; head++)
                {
                    int index = component[head];
                    if (removed[index]) touchesOuter = true;
                    if (ColorDistanceSquared(pixels[index], background) <= strictThresholdSquared) strictCount++;
                    int x = index % width;
                    int y = index / width;
                    if (x == 0 || x == width - 1 || y == 0 || y == height - 1) touchesOuter = true;
                    for (int dy = -1; dy <= 1; dy++)
                    {
                        int neighborY = y + dy;
                        if (neighborY < 0 || neighborY >= height) continue;
                        for (int dx = -1; dx <= 1; dx++)
                        {
                            int neighborX = x + dx;
                            if ((dx == 0 && dy == 0) || neighborX < 0 || neighborX >= width) continue;
                            int neighbor = neighborY * width + neighborX;
                            if (visited[neighbor] || pixels[neighbor].a == 0
                                || ColorDistanceSquared(pixels[neighbor], background) > thresholdSquared) continue;
                            visited[neighbor] = true;
                            component.Add(neighbor);
                        }
                    }
                }
                if (touchesOuter || component.Count < minArea || strictCount < component.Count * requiredStrictRatio) continue;
                foreach (int index in component)
                {
                    removed[index] = true;
                    Color32 pixel = pixels[index];
                    pixel.a = 0;
                    pixels[index] = pixel;
                    frontier.Add(index);
                }
            }
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
                            if (removed[neighbor] || pixels[neighbor].a == 0) continue;
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
