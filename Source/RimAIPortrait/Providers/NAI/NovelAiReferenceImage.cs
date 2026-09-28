using System;
using UnityEngine;

namespace RimAIPortrait
{
    /// <summary>Prepares images for NovelAI V4.5 Precise Reference.</summary>
    internal static class NovelAiReferenceImage
    {
        public static byte[] PrepareStyle(byte[] image)
        {
            if (image == null || image.Length == 0) throw new ArgumentException("The style reference is empty.");
            var source = new Texture2D(2, 2, TextureFormat.ARGB32, false);
            try
            {
                if (!source.LoadImage(image)) throw new ArgumentException("The style reference is not a valid image.");
                const int size = 1472;
                if (source.width == size && source.height == size) return source.EncodeToPNG();
                var canvas = new Texture2D(size, size, TextureFormat.ARGB32, false);
                try
                {
                    var pixels = new Color32[size * size];
                    for (int y = 0; y < size; y++)
                    {
                        float v = (y + .5f) / size;
                        for (int x = 0; x < size; x++)
                        {
                            Color color = source.GetPixelBilinear((x + .5f) / size, v);
                            pixels[y * size + x] = new Color(color.r * color.a, color.g * color.a, color.b * color.a, 1f);
                        }
                    }
                    canvas.SetPixels32(pixels);
                    canvas.Apply(false, false);
                    return canvas.EncodeToPNG();
                }
                finally { UnityEngine.Object.Destroy(canvas); }
            }
            finally { UnityEngine.Object.Destroy(source); }
        }

        public static byte[] Prepare(byte[] png)
        {
            if (png == null || png.Length == 0) throw new ArgumentException("The Pawn portrait is empty.");
            var source = new Texture2D(2, 2, TextureFormat.ARGB32, false);
            try
            {
                if (!source.LoadImage(png)) throw new ArgumentException("The Pawn portrait is not a valid image.");
                Color32[] pixels = source.GetPixels32();
                int minX = source.width, minY = source.height, maxX = -1, maxY = -1;
                for (int y = 0; y < source.height; y++)
                {
                    for (int x = 0; x < source.width; x++)
                    {
                        if (pixels[y * source.width + x].a < 8) continue;
                        if (x < minX) minX = x;
                        if (x > maxX) maxX = x;
                        if (y < minY) minY = y;
                        if (y > maxY) maxY = y;
                    }
                }
                if (maxX < minX || maxY < minY) throw new ArgumentException("The Pawn portrait is fully transparent.");

                int visibleWidth = maxX - minX + 1;
                int visibleHeight = maxY - minY + 1;
                int padX = Mathf.Max(1, Mathf.CeilToInt(visibleWidth * .075f));
                int padY = Mathf.Max(1, Mathf.CeilToInt(visibleHeight * .075f));
                int cropWidth = visibleWidth + 2 * padX;
                int cropHeight = visibleHeight + 2 * padY;
                float ratio = (float)cropWidth / cropHeight;
                int canvasWidth = 1472, canvasHeight = 1472;
                if (Mathf.Abs(ratio - 2f / 3f) < Mathf.Abs(ratio - 1f) && Mathf.Abs(ratio - 2f / 3f) < Mathf.Abs(ratio - 3f / 2f))
                {
                    canvasWidth = 1024; canvasHeight = 1536;
                }
                else if (Mathf.Abs(ratio - 3f / 2f) < Mathf.Abs(ratio - 1f))
                {
                    canvasWidth = 1536; canvasHeight = 1024;
                }

                float scale = Mathf.Min((float)canvasWidth / cropWidth, (float)canvasHeight / cropHeight);
                int scaledWidth = Mathf.Max(1, Mathf.RoundToInt(cropWidth * scale));
                int scaledHeight = Mathf.Max(1, Mathf.RoundToInt(cropHeight * scale));
                int offsetX = (canvasWidth - scaledWidth) / 2;
                int offsetY = (canvasHeight - scaledHeight) / 2;
                var canvasPixels = new Color32[canvasWidth * canvasHeight];
                for (int i = 0; i < canvasPixels.Length; i++) canvasPixels[i] = new Color32(0, 0, 0, 255);
                for (int y = 0; y < scaledHeight; y++)
                {
                    int sourceY = minY - padY + Mathf.Min(cropHeight - 1, (int)((y + .5f) * cropHeight / scaledHeight));
                    for (int x = 0; x < scaledWidth; x++)
                    {
                        int sourceX = minX - padX + Mathf.Min(cropWidth - 1, (int)((x + .5f) * cropWidth / scaledWidth));
                        if (sourceX < 0 || sourceX >= source.width || sourceY < 0 || sourceY >= source.height) continue;
                        Color32 pixel = pixels[sourceY * source.width + sourceX];
                        byte alpha = pixel.a;
                        canvasPixels[(offsetY + y) * canvasWidth + offsetX + x] = new Color32(
                            (byte)(pixel.r * alpha / 255), (byte)(pixel.g * alpha / 255), (byte)(pixel.b * alpha / 255), 255);
                    }
                }
                var canvas = new Texture2D(canvasWidth, canvasHeight, TextureFormat.ARGB32, false);
                try
                {
                    canvas.SetPixels32(canvasPixels);
                    canvas.Apply(false, false);
                    return canvas.EncodeToPNG();
                }
                finally { UnityEngine.Object.Destroy(canvas); }
            }
            finally { UnityEngine.Object.Destroy(source); }
        }
    }
}
