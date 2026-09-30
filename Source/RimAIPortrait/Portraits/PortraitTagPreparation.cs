using System;
using System.Collections;
using System.IO;
using RimWorld;
using Verse;

namespace RimAIPortrait
{
    internal static class PortraitTagPreparation
    {
        public static IEnumerator Prepare(PortraitSettings settings, PortraitRequestOptions options,
            PreparedPortraitRequest prepared)
        {
            PortraitGenerationRequest request = prepared.Request;
            AiProvider providerId = prepared.ProviderId;
            bool supportsTags = providerId == AiProvider.NovelAI || providerId == AiProvider.LocalComfyUI;
            bool extractTags = settings.EnableTagExtractor && TagExtraction.IsConfigured(settings)
                && supportsTags;
            if (settings.EnableTagExtractor && supportsTags && !TagExtraction.IsConfigured(settings))
                ShowFailure(Localization.T("RimAIPortrait.TagExtractor.ConfigurationMissing"));
            string tagStatus = !settings.EnableTagExtractor ? "disabled"
                : !TagExtraction.IsConfigured(settings) ? "missing configuration" : "not requested";
            if (extractTags && providerId == AiProvider.LocalComfyUI)
            {
                extractTags = false;
                try
                {
                    string workflow = File.ReadAllText(((ComfyUiProviderConfiguration)request.Configuration).WorkflowPath);
                    extractTags = workflow.Contains("__RIM_EXTRACTED_TAG__");
                }
                catch (Exception) { }
                if (!extractTags) tagStatus = "workflow has no tag placeholder";
            }
            if (options.BatchTags != null)
            {
                ExtractedTags preparedTags;
                if (options.BatchTags.TryGetValue(options.State, out preparedTags))
                    request.ExtractedTags = preparedTags;
                tagStatus = request.ExtractedTags == null ? "batch result unavailable" : "batch result applied";
            }
            else if (extractTags)
            {
                byte[] tagImage = request.PrimaryImage;
                if (options.State == PortraitState.Normal && request.ImageUse != PortraitImageUse.Edit
                    && (!settings.IncludeSprite || tagImage == null))
                {
                    try { tagImage = PawnCapture.Capture(options.Pawn); }
                    catch (Exception) { tagImage = null; }
                }
                if (tagImage == null && request.ReferencePaths.Count > 0)
                {
                    try { tagImage = File.ReadAllBytes(request.ReferencePaths[0]); }
                    catch (Exception) { tagImage = null; }
                }
                if (tagImage != null && tagImage.Length > 0 && tagImage.Length <= 8 * 1024 * 1024)
                {
                    ExtractedTags tags = null;
                    string tagError = null;
                    yield return TagExtraction.Extract(settings, tagImage, request.Prompt,
                        options.State != PortraitState.Normal,
                        (value, error) => { tags = value; tagError = error; },
                        options.State == PortraitState.Normal
                            ? PortraitPrompt.TagExtractorFeatures(options.Pawn, settings) : null);
                    request.ExtractedTags = tags;
                    tagStatus = tags == null ? "failed: " + (tagError ?? "invalid response") : "applied";
                    if (tags == null)
                        ShowFailure(tagError ?? Localization.T("RimAIPortrait.TagExtractor.InvalidResponse"));
                }
                else
                {
                    tagStatus = "source image unavailable or too large";
                    ShowFailure(Localization.T("RimAIPortrait.TagExtractor.ImageRequired"));
                }
            }
            if (settings.DebugMode && supportsTags)
                Log.Message("[Rim AI Expressive Portraits][Debug] TagExtractor " + tagStatus
                    + ", kind=" + (prepared.Expression ? "Expression" : "Base")
                    + ", positive=" + (request.ExtractedTags?.positive?.Length ?? 0)
                    + ", negative=" + (request.ExtractedTags?.negative?.Length ?? 0));
        }

        public static void ShowFailure(string explanation)
        {
            Messages.Message(Localization.T("RimAIPortrait.TagExtractor.Failed", explanation),
                MessageTypeDefOf.RejectInput, false);
        }
    }
}
