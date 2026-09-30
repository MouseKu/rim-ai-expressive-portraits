using UnityEngine;
using Verse;

namespace RimAIPortrait
{
    internal static class PortraitWindowSizing
    {
        private const float ScreenMargin = 40f;

        public static Vector2 Fit(float width, float height)
        {
            return new Vector2(Mathf.Min(width, UI.screenWidth - ScreenMargin),
                Mathf.Min(height, UI.screenHeight - ScreenMargin));
        }
    }
}
