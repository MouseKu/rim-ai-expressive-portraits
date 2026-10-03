using Verse;

namespace RimAIPortrait
{
    internal static class PortraitTagPreparation
    {
        public static void ApplySavedBaseTags(Pawn pawn, PreparedPortraitRequest prepared)
        {
            if (prepared.ProviderId != AiProvider.NovelAI && prepared.ProviderId != AiProvider.LocalComfyUI)
                return;
            prepared.Request.ExtractedTags = PortraitData.BaseTags(pawn);
        }
    }
}
