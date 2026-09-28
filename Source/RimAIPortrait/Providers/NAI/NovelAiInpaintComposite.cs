using System;
using System.IO;
using UnityEngine;

namespace RimAIPortrait
{
    /// <summary>Keeps the original portrait outside the selected NovelAI mask.</summary>
    internal static class NovelAiInpaintComposite
    {
        public static void Dimensions(byte[] imageBytes, out int width, out int height)
        {
            Texture2D image = Load(imageBytes);
            try
            {
                width = image.width;
                height = image.height;
            }
            finally { UnityEngine.Object.Destroy(image); }
        }

        public static byte[] PrepareSource(byte[] sourceBytes, byte[] maskBytes, out int width, out int height)
        {
            Texture2D source = null;
            Texture2D mask = null;
            try
            {
                source = Load(sourceBytes);
                mask = Load(maskBytes);
                width = source.width;
                height = source.height;
                if (mask.width != width || mask.height != height)
                    throw new InvalidDataException(Localization.T("RimAIPortrait.Inpaint.DimensionMismatch"));
                bool selected = false;
                foreach (Color32 pixel in mask.GetPixels32())
                {
                    if (pixel.r < 128) continue;
                    selected = true;
                    break;
                }
                if (!selected) throw new InvalidDataException(Localization.T("RimAIPortrait.Inpaint.EmptyMask"));
                return source.EncodeToPNG();
            }
            finally
            {
                if (source != null) UnityEngine.Object.Destroy(source);
                if (mask != null) UnityEngine.Object.Destroy(mask);
            }
        }

        /// <summary>Moves the model's rectangular mask edge outside the area copied into the final portrait.</summary>
        public static byte[] ExpandMask(byte[] maskBytes)
        {
            Texture2D mask = Load(maskBytes);
            try
            {
                int width = mask.width;
                int height = mask.height;
                Color32[] selected = mask.GetPixels32();
                int left = width, right = -1, bottom = height, top = -1;
                for (int y = 0; y < height; y++)
                {
                    for (int x = 0; x < width; x++)
                    {
                        if (selected[y * width + x].r < 128) continue;
                        left = Mathf.Min(left, x);
                        right = Mathf.Max(right, x);
                        bottom = Mathf.Min(bottom, y);
                        top = Mathf.Max(top, y);
                    }
                }
                if (right < left) throw new InvalidDataException(Localization.T("RimAIPortrait.Inpaint.EmptyMask"));
                int padding = Mathf.Clamp(Mathf.RoundToInt(Mathf.Min(width, height) * .05f), 24, 64);
                left = Mathf.Max(0, left - padding);
                right = Mathf.Min(width - 1, right + padding);
                bottom = Mathf.Max(0, bottom - padding);
                top = Mathf.Min(height - 1, top + padding);
                var pixels = new Color32[selected.Length];
                for (int i = 0; i < pixels.Length; i++) pixels[i] = new Color32(0, 0, 0, 255);
                for (int y = bottom; y <= top; y++)
                    for (int x = left; x <= right; x++) pixels[y * width + x] = new Color32(255, 255, 255, 255);
                var expanded = new Texture2D(width, height, TextureFormat.ARGB32, false);
                try
                {
                    expanded.SetPixels32(pixels);
                    expanded.Apply(false, false);
                    return expanded.EncodeToPNG();
                }
                finally { UnityEngine.Object.Destroy(expanded); }
            }
            finally { UnityEngine.Object.Destroy(mask); }
        }

        public static byte[] Composite(byte[] sourceBytes, byte[] maskBytes, byte[] generationMaskBytes,
            byte[] generatedBytes, bool narrowFeather)
        {
            Texture2D source = null;
            Texture2D mask = null;
            Texture2D generationMask = null;
            Texture2D generated = null;
            try
            {
                source = Load(sourceBytes);
                mask = Load(maskBytes);
                generationMask = Load(generationMaskBytes);
                generated = Load(generatedBytes);
                int generatedWidth = generated.width;
                int generatedHeight = generated.height;
                int width = source.width;
                int height = source.height;
                if (mask.width != width || mask.height != height || generationMask.width != width
                    || generationMask.height != height || generatedWidth != width || generatedHeight != height)
                    throw new InvalidDataException(Localization.T("RimAIPortrait.Inpaint.ResponseMismatch",
                        width, height, mask.width, mask.height, generatedWidth, generatedHeight));

                Color32[] original = source.GetPixels32();
                Color32[] finalMask = mask.GetPixels32();
                Color32[] requestMask = generationMask.GetPixels32();
                for (int i = 0; i < finalMask.Length; i++)
                {
                    if (finalMask[i].r >= 128 && requestMask[i].r < 128)
                        throw new InvalidDataException(Localization.T("RimAIPortrait.Inpaint.InvalidImage"));
                }
                // Keep the established single-selection blend until the visual gate in Test8 passes.
                // Multiple selections copy only their selected regions, leaving the request padding untouched.
                Color32[] selected = narrowFeather ? finalMask : requestMask;
                Color32[] result = generated.GetPixels32();
                int count = original.Length;
                var distance = new int[count];
                var queue = new int[count];
                int head = 0;
                int tail = 0;
                for (int i = 0; i < count; i++)
                {
                    if (selected[i].r >= 128) { distance[i] = -1; continue; }
                    queue[tail++] = i;
                }
                while (head < tail)
                {
                    int index = queue[head++];
                    int nextDistance = distance[index] + 1;
                    int x = index % width;
                    int y = index / width;
                    if (x > 0) Visit(index - 1, nextDistance, distance, queue, ref tail);
                    if (x + 1 < width) Visit(index + 1, nextDistance, distance, queue, ref tail);
                    if (y > 0) Visit(index - width, nextDistance, distance, queue, ref tail);
                    if (y + 1 < height) Visit(index + width, nextDistance, distance, queue, ref tail);
                }

                int size = Mathf.Min(width, height);
                float feather = narrowFeather
                    ? Mathf.Clamp(Mathf.RoundToInt(size * .01f), 4, 12)
                    : Mathf.Clamp(Mathf.RoundToInt(size * .019f), 4, 24);
                float restore = narrowFeather ? 0f : Mathf.Clamp(Mathf.RoundToInt(size * .029f), 8, 32);
                for (int i = 0; i < count; i++)
                {
                    if (selected[i].r < 128) continue;
                    float amount = distance[i] < 0 ? 1f : Mathf.Clamp01((distance[i] - 1f - restore) / feather);
                    amount = amount * amount * (3f - 2f * amount);
                    Color32 before = original[i];
                    Color32 after = result[i];
                    original[i] = new Color32(
                        (byte)Mathf.RoundToInt(Mathf.Lerp(before.r, after.r, amount)),
                        (byte)Mathf.RoundToInt(Mathf.Lerp(before.g, after.g, amount)),
                        (byte)Mathf.RoundToInt(Mathf.Lerp(before.b, after.b, amount)),
                        (byte)Mathf.RoundToInt(Mathf.Lerp(before.a, after.a, amount)));
                }
                var output = new Texture2D(width, height, TextureFormat.ARGB32, false);
                try
                {
                    output.SetPixels32(original);
                    output.Apply(false, false);
                    return output.EncodeToPNG();
                }
                finally { UnityEngine.Object.Destroy(output); }
            }
            finally
            {
                if (source != null) UnityEngine.Object.Destroy(source);
                if (mask != null) UnityEngine.Object.Destroy(mask);
                if (generationMask != null) UnityEngine.Object.Destroy(generationMask);
                if (generated != null) UnityEngine.Object.Destroy(generated);
            }
        }

        private static Texture2D Load(byte[] data)
        {
            var texture = new Texture2D(2, 2, TextureFormat.ARGB32, false);
            if (data == null || !texture.LoadImage(data))
            {
                UnityEngine.Object.Destroy(texture);
                throw new InvalidDataException(Localization.T("RimAIPortrait.Inpaint.InvalidImage"));
            }
            return texture;
        }

        private static void Visit(int index, int value, int[] distance, int[] queue, ref int tail)
        {
            if (distance[index] != -1) return;
            distance[index] = value;
            queue[tail++] = index;
        }
    }
}
