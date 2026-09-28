using System;
using System.Globalization;
using System.IO;
using UnityEngine;

namespace RimAIPortrait
{
    [Serializable]
    internal sealed class PortraitImageCaptions
    {
        public string characterSprite = "";
        public string basePortrait = "";
        public string selectedReference = "";
        public string styleReference = "";
        public string priority = "";

        private static readonly Lazy<PortraitImageCaptions> OpenAi =
            new Lazy<PortraitImageCaptions>(() => Load("open-ai-img-caption.json"));
        private static readonly Lazy<PortraitImageCaptions> Google =
            new Lazy<PortraitImageCaptions>(() => Load("google-ai-img-caption.json"));

        public static PortraitImageCaptions For(AiProvider provider)
        {
            return provider == AiProvider.OpenAI ? OpenAi.Value : Google.Value;
        }

        public string Primary(PortraitImageUse imageUse)
        {
            return imageUse == PortraitImageUse.CharacterReference ? characterSprite : basePortrait;
        }

        private static PortraitImageCaptions Load(string fileName)
        {
            string path = Path.Combine(RimAIPortraitMod.ModPack.RootDir, "Json", fileName);
            PortraitImageCaptions captions = JsonUtility.FromJson<PortraitImageCaptions>(File.ReadAllText(path));
            if (captions == null || string.IsNullOrWhiteSpace(captions.characterSprite)
                || string.IsNullOrWhiteSpace(captions.basePortrait)
                || string.IsNullOrWhiteSpace(captions.selectedReference)
                || string.IsNullOrWhiteSpace(captions.styleReference)
                || string.IsNullOrWhiteSpace(captions.priority))
                throw new InvalidDataException(fileName + " is missing an image caption.");
            if (fileName == "open-ai-img-caption.json")
            {
                string.Format(CultureInfo.InvariantCulture, captions.characterSprite, 1);
                string.Format(CultureInfo.InvariantCulture, captions.basePortrait, 1);
                string.Format(CultureInfo.InvariantCulture, captions.selectedReference, 1);
                string.Format(CultureInfo.InvariantCulture, captions.styleReference, 1);
            }
            return captions;
        }
    }
}
