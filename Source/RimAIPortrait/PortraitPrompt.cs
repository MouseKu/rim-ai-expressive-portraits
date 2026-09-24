// Contains the implementation for PortraitPrompt.

using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using RimWorld;
using Verse;

namespace RimAIPortrait
{
    /// <summary>Describes the behavior of this type or member.</summary>
    public static class PortraitPrompt
    {
        /// <summary>Describes the behavior of this type or member.</summary>
        /// <param name="p">The p value.</param>
        /// <param name="state">The state value.</param>
        /// <param name="s">The s value.</param>
        /// <param name="expressionOverride">The expressionOverride value.</param>
        /// <returns>The resulting value.</returns>
        public static string Build(Pawn p, PortraitState state, PortraitSettings s, string expressionOverride = null)
        {
            string pawnBasePrompt = PortraitData.BasePrompt(p);
            var b = new StringBuilder(string.IsNullOrWhiteSpace(pawnBasePrompt) ? BaseInstructions(s) : pawnBasePrompt.Trim());
            if (!s.Prompt.NullOrEmpty()) b.AppendLine().AppendLine(s.Prompt);
            string extraFeatures = PortraitData.ExtraFeatures(p);
            Add(b, true, "Extra Features", string.IsNullOrWhiteSpace(extraFeatures) ? "None" : extraFeatures.Trim());
            Add(b, s.IncludeGender, "Gender", p.gender.GetLabel());
            Add(b, s.IncludeRace, p.RaceProps.Animal ? "Animal species" : "Race", p.def.LabelCap);
            if (p.RaceProps.Humanlike && ModsConfig.BiotechActive && p.genes != null)
                Add(b, s.IncludeXenotype, "Xenotype", p.genes.XenotypeLabel);
            Add(b, s.IncludeAge, "Age", p.ageTracker.AgeBiologicalYears.ToString());
            if (p.story != null)
            {
                Add(b, s.IncludeBody, "Body type", p.story.bodyType?.label);
                Add(b, s.IncludeSkin, "Skin color", ColorUtility.ToHtmlStringRGB(p.story.SkinColor));
                Add(b, s.IncludeHair, "Hair", p.story.hairDef?.label + ", " + ColorUtility.ToHtmlStringRGB(p.story.HairColor));
            }
            Add(b, s.IncludeEyes, "Eye color", "preserve from reference image");
            if (s.IncludeApparel && p.apparel != null) Add(b, true, "Apparel", string.Join(", ", p.apparel.WornApparel.Select(x => x.LabelCap).ToArray()));
            Add(b, s.IncludeHealth, "Health conditions", HealthConditions(p));
            if (s.IncludeTraits && p.story?.traits != null) Add(b, true, "Traits", string.Join(", ", p.story.traits.allTraits.Select(x => x.LabelCap).ToArray()));
            if (state != PortraitState.Normal || s.EnableEmotionChanges)
            {
                string custom = expressionOverride ?? StateOverride(s, state);
                Add(b, true, "Expression", custom.NullOrEmpty() ? PromptLibrary.State[state] : custom);
            }
            return b.ToString();
        }
        /// <summary>Returns the default Base prompt for the configured Base provider.</summary>
        public static string BaseInstructions(PortraitSettings settings)
        {
            if (settings == null) return "";
            return (settings.Provider == AiProvider.Google
                ? settings.GeminiBaseInstructions
                : settings.BaseInstructions) ?? "";
        }
        private static string HealthConditions(Pawn pawn)
        {
            if (pawn.health?.hediffSet?.hediffs == null) return null;
            return string.Join(", ", pawn.health.hediffSet.hediffs
                .Where(hediff => hediff != null && hediff.Visible)
                .Select(hediff => hediff.Part == null
                    ? hediff.LabelCap
                    : hediff.LabelCap + " (" + hediff.Part.LabelCap + ")")
                .Where(label => !label.NullOrEmpty())
                .Distinct()
                .ToArray());
        }
        /// <summary>Describes the behavior of this type or member.</summary>
        private static void Add(StringBuilder b, bool enabled, string name, string value)
        {
            if (enabled && !value.NullOrEmpty()) b.AppendLine().AppendLine("[" + name + "]").AppendLine(value);
        }
        /// <summary>Describes the behavior of this type or member.</summary>
        /// <param name="s">The s value.</param>
        /// <param name="st">The st value.</param>
        /// <returns>The resulting value.</returns>
        public static string StateOverride(PortraitSettings s, PortraitState st) => st == PortraitState.Normal ? s.NormalPrompt : st == PortraitState.High ? s.HighPrompt : st == PortraitState.Low ? s.LowPrompt : st == PortraitState.Sleep ? s.SleepPrompt : st == PortraitState.Pain ? s.PainPrompt : st == PortraitState.Down ? s.DownPrompt : st == PortraitState.Hot ? s.HotPrompt : st == PortraitState.Cold ? s.ColdPrompt : st == PortraitState.Angry ? s.AngryPrompt : s.DistressedPrompt;
    }
}
