using UnityEngine;
using Verse;

namespace RimAIPortrait
{
    internal static class SettingsTooltips
    {
        private static readonly string[] labelsWithTips = {
            "ApiKey", "NovelAiApiKey", "Model", "OptionsJson", "LocalOpenAiUrl",
            "ComfyUrl", "WorkflowPath", "StyleFolder", "FramePath", "PortraitRootFolder",
            "NegativePrompt", "BasePrompt", "TagExtractorBasePrompt", "TagExtractorInpaintPrompt",
            "CharacterSprite", "ExcludeHeadgearFromSprite", "Gender", "Race", "Xenotype", "Gene",
            "AppearanceGenesOnly", "Age", "BodyType", "SkinColor", "Hair",
            "EyeColor", "Apparel", "Health", "Traits"
        };

        public static void Text(Listing_Standard listing, string label, ref string value)
        {
            Rect labelRect = listing.GetRect(Verse.Text.LineHeight);
            Widgets.Label(labelRect, label);
            Rect fieldRect = listing.GetRect(30f);
            value = Widgets.TextField(fieldRect, value ?? "");
            string tip = TipForLabel(label);
            if (tip == null) return;
            TooltipHandler.TipRegion(labelRect, tip);
            TooltipHandler.TipRegion(fieldRect, tip);
        }

        public static float Slider(Listing_Standard listing, string label, float value,
            float minimum, float maximum, string tipKey)
        {
            Rect labelRect = listing.GetRect(Verse.Text.LineHeight);
            Widgets.Label(labelRect, label);
            Rect sliderRect = listing.GetRect(22f);
            float result = Widgets.HorizontalSlider(sliderRect, value, minimum, maximum);
            string tip = Localization.T(tipKey);
            TooltipHandler.TipRegion(labelRect, tip);
            TooltipHandler.TipRegion(sliderRect, tip);
            return result;
        }

        public static string TipForLabel(string label)
        {
            foreach (string name in labelsWithTips)
            {
                string key = "RimAIPortrait.Settings." + name;
                if (label == Localization.T(key)) return Localization.T(key + "Tip");
            }
            if (label == Localization.T("RimAIPortrait.TagExtractor.LocalUrl"))
                return Localization.T("RimAIPortrait.Settings.LocalOpenAiUrlTip");
            if (label == Localization.T("RimAIPortrait.TagExtractor.BasePrompt"))
                return Localization.T("RimAIPortrait.Settings.TagExtractorBasePromptTip");
            if (label == Localization.T("RimAIPortrait.TagExtractor.InpaintPrompt"))
                return Localization.T("RimAIPortrait.Settings.TagExtractorInpaintPromptTip");
            return null;
        }
    }
}
