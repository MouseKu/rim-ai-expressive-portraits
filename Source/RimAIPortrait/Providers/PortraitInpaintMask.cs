using System;
using UnityEngine;

namespace RimAIPortrait
{
    internal static class PortraitInpaintMask
    {
        // The selection dialog produces opaque black (keep) and white (edit) pixels.
        // OpenAI instead expects the editable region to have zero alpha.
        public static byte[] ForOpenAi(byte[] maskBytes)
        {
            Texture2D mask = new Texture2D(2, 2, TextureFormat.ARGB32, false);
            Texture2D converted = null;
            try
            {
                if (!mask.LoadImage(maskBytes)) throw new ArgumentException(Localization.T("RimAIPortrait.Inpaint.InvalidImage"));
                Color32[] pixels = mask.GetPixels32();
                for (int index = 0; index < pixels.Length; index++)
                    pixels[index] = new Color32(255, 255, 255, pixels[index].r >= 128 ? (byte)0 : (byte)255);
                converted = new Texture2D(mask.width, mask.height, TextureFormat.ARGB32, false);
                converted.SetPixels32(pixels);
                converted.Apply(false, false);
                return converted.EncodeToPNG();
            }
            finally
            {
                if (converted != null) UnityEngine.Object.Destroy(converted);
                UnityEngine.Object.Destroy(mask);
            }
        }
    }
}
