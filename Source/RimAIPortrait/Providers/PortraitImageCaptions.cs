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

        private static string openAiJson;
        private static string localJson;
        private static string googleJson;
        private static PortraitImageCaptions openAi;
        private static PortraitImageCaptions local;
        private static PortraitImageCaptions google;

        public static PortraitImageCaptions For(AiProvider provider)
        {
            string json = EffectiveJson(provider);
            if (provider == AiProvider.LocalOpenAI)
            {
                if (local == null || localJson != json)
                {
                    local = Parse(json, "local-ai-img-caption.json");
                    localJson = json;
                }
                return local;
            }
            if (provider == AiProvider.OpenAI)
            {
                if (openAi == null || openAiJson != json)
                {
                    openAi = Parse(json, "open-ai-img-caption.json");
                    openAiJson = json;
                }
                return openAi;
            }
            if (google == null || googleJson != json)
            {
                google = Parse(json, "google-ai-img-caption.json");
                googleJson = json;
            }
            return google;
        }

        public string Primary(PortraitImageUse imageUse)
        {
            return imageUse == PortraitImageUse.CharacterReference ? characterSprite : basePortrait;
        }

        internal static string EffectiveJson(AiProvider provider, PortraitSettings settings = null)
        {
            if (settings == null) settings = RimAIPortraitMod.Settings;
            string custom = provider == AiProvider.LocalOpenAI ? settings?.LocalOpenAiImageCaptionsJson
                : provider == AiProvider.OpenAI ? settings?.OpenAiImageCaptionsJson
                : settings?.GoogleImageCaptionsJson;
            if (!string.IsNullOrWhiteSpace(custom)) return custom;
            string fileName = provider == AiProvider.LocalOpenAI ? "local-ai-img-caption.json"
                : provider == AiProvider.OpenAI ? "open-ai-img-caption.json" : "google-ai-img-caption.json";
            return File.ReadAllText(Path.Combine(RimAIPortraitMod.ModPack.RootDir, "Json", fileName));
        }

        internal static PortraitImageCaptions Parse(string json, string fileName)
        {
            PortraitImageCaptions captions = JsonUtility.FromJson<PortraitImageCaptions>(json);
            if (captions == null || string.IsNullOrWhiteSpace(captions.characterSprite)
                || string.IsNullOrWhiteSpace(captions.basePortrait)
                || string.IsNullOrWhiteSpace(captions.selectedReference)
                || string.IsNullOrWhiteSpace(captions.styleReference)
                || string.IsNullOrWhiteSpace(captions.priority))
                throw new InvalidDataException(fileName + " is missing an image caption.");
            if (fileName == "open-ai-img-caption.json" || fileName == "local-ai-img-caption.json")
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
