// Contains the implementation for PromptLibrary.

using System.Collections.Generic;

namespace RimAIPortrait
{
    /// <summary>Describes the behavior of this type or member.</summary>
    public enum PortraitState { Normal, High, Low, Sleep, Pain, Down, Hot, Cold, Angry, Distressed }

    /// <summary>Describes the behavior of this type or member.</summary>
    public static class PortraitStateNames
    {
        /// <summary>Describes the behavior of this type or member.</summary>
        /// <param name="state">The state value.</param>
        /// <returns>The resulting value.</returns>
        public static string Display(PortraitState state) => state == PortraitState.Normal ? "Base" : state.ToString();
        /// <summary>Describes the behavior of this type or member.</summary>
        /// <param name="state">The state value.</param>
        /// <returns>The resulting value.</returns>
        public static string Label(PortraitState state) => Localization.T("RimAIPortrait.State." + state);
    }

    /// <summary>
    /// Provides additional behavior and compatibility details.
    /// Provides additional behavior and compatibility details.
    /// Provides additional behavior and compatibility details.
    /// </summary>
    public static class PromptLibrary
    {
        public const string DefaultBase = "Redraw the attached character as a 2D colonist portrait that blends naturally with RimWorld’s in-game visuals.\n\nArt style:\n\nThick, clean, dark brown outlines\nBroad areas of color with simple two- or three-tone cel shading\nA subdued palette centered on muted browns, grays, and creams\nHair drawn in large, distinct shapes without fine strands\nEyes slightly smaller than in the original, with minimal highlights and eyelashes\nSimply drawn nose and mouth while preserving the character’s facial identity\nRetain some cuteness, but give the character an understated, slightly indifferent frontier-colonist look\nKeep the face and silhouette clearly readable at small game UI sizes\nUse a fully transparent background, with alpha set to 0 everywhere outside the character\n\nUse a three-quarter-view bust composition showing the head and shoulders. Center the character with enough padding to avoid cropping the ears or hair.\n\nExclude pixel art, photorealism, 3D rendering, glossy anime effects, oversized eyes, finely detailed hair strands, background scenery, green backgrounds, text, and borders.";
        public static readonly Dictionary<PortraitState, string> State = new Dictionary<PortraitState, string> {
            { PortraitState.Normal, "A calm, understated everyday expression, with the mouth gently closed and a natural forward gaze." },
            { PortraitState.High, "A cheerful, confident expression in high spirits. Preserve the original character’s appearance, clothing, pose, and composition. Change only the facial expression." },
            { PortraitState.Low, "An expression on the verge of a mental breakdown, with unfocused, tearful eyes and trembling lips. Preserve the original character’s appearance, clothing, pose, and composition. Change only the facial expression." },
            { PortraitState.Sleep, "A peaceful sleeping expression with both eyes closed. Preserve the original character’s appearance, clothing, pose, and composition. Change only the facial expression." },
            { PortraitState.Pain, "A grimacing expression, as if enduring pain. Preserve the original character’s appearance, clothing, pose, and composition. Change only the facial expression." },
            { PortraitState.Down, "An unconscious, pained expression with tears streaming down the face, as if having collapsed in agony. Preserve the original character’s appearance, clothing, pose, and composition. Change only the facial expression." },
            { PortraitState.Hot, "An exhausted, sweaty expression with a flushed face from a high fever or heatstroke. Preserve the original character’s appearance, clothing, pose, and composition. Change only the facial expression." },
            { PortraitState.Cold, "A pale, shivering expression from hypothermia and extreme cold. Preserve the original character’s appearance, clothing, pose, and composition. Change only the facial expression." },
            { PortraitState.Angry, "An angry expression with sharply furrowed brows, a fierce gaze, and a tightly clenched jaw. Preserve the original character’s appearance, clothing, pose, and composition. Change only the facial expression." },
            { PortraitState.Distressed, "A deeply distressed expression showing anxiety, anguish, and despair, with tense brows, an unfocused fearful gaze, and trembling lips. Preserve the original character’s appearance, clothing, pose, and composition. Change only the facial expression." }
        };
    }
}
