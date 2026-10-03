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
        public static string Build(Pawn p, PortraitState state, PortraitSettings s, string expressionOverride = null,
            AiProvider? providerOverride = null, bool omitPawnFeatures = false)
        {
            AiProvider provider = providerOverride ?? (state == PortraitState.Base ? s.Provider : s.ExpressionProvider);
            if (state != PortraitState.Base)
            {
                var expression = new StringBuilder(s.GetExpressionInstructions(provider) ?? "");
                string customExpression = expressionOverride ?? StateOverride(s, provider, state);
                Add(expression, true, "Expression", customExpression.NullOrEmpty() ? PromptLibrary.State[state] : customExpression);
                return expression.ToString();
            }
            var b = new StringBuilder(BaseInstructions(s, provider));
            if (!s.Prompt.NullOrEmpty()) b.AppendLine().AppendLine(s.Prompt);
            if (!omitPawnFeatures)
            {
                string extraFeatures = PortraitData.ExtraFeatures(p);
                Add(b, true, "Extra Features", string.IsNullOrWhiteSpace(extraFeatures) ? "None" : extraFeatures.Trim());
                AppendPawnFeatures(b, p, s, false);
            }
            if (s.IncludeTraits && p.story?.traits != null) Add(b, true, "Traits", string.Join(", ", p.story.traits.allTraits.Select(x => x.LabelCap).ToArray()));
            return b.ToString();
        }

        internal static string TagExtractorFeatures(Pawn pawn, PortraitSettings settings)
        {
            var features = new StringBuilder();
            string extraFeatures = PortraitData.ExtraFeatures(pawn);
            if (!string.IsNullOrWhiteSpace(extraFeatures))
                AddFeature(features, true, "Extra Features", extraFeatures.Trim(), true);
            AppendPawnFeatures(features, pawn, settings, true);
            string result = features.ToString().Trim();
            return result.Length == 0 ? "None" : result;
        }

        private static void AppendPawnFeatures(StringBuilder output, Pawn pawn, PortraitSettings settings, bool compact)
        {
            AddFeature(output, settings.IncludeGender, "Gender", pawn.gender.GetLabel(), compact);
            AddFeature(output, settings.IncludeRace, pawn.RaceProps.Animal ? "Animal species" : "Race", pawn.def.LabelCap, compact);
            if (pawn.RaceProps.Humanlike && ModsConfig.BiotechActive && pawn.genes != null)
            {
                AddFeature(output, settings.IncludeXenotype, "Xenotype", pawn.genes.XenotypeLabel, compact);
                if (settings.IncludeGene)
                {
                    string genes = settings.OnlyAppearanceGenes
                        ? AppearanceGeneFeatures(pawn)
                        : string.Join(", ", pawn.genes.GenesListForReading
                            .Where(gene => gene != null && gene.Active && gene.def != null)
                            .Select(gene => gene.def.LabelCap).ToArray());
                    AddFeature(output, true, "Genes", genes, compact);
                }
            }
            AddFeature(output, settings.IncludeAge, "Age", pawn.ageTracker.AgeBiologicalYears.ToString(), compact);
            if (pawn.story != null)
            {
                AddFeature(output, settings.IncludeBody, "Body type", pawn.story.bodyType?.label, compact);
                AddFeature(output, settings.IncludeSkin, "Skin color", ColorUtility.ToHtmlStringRGB(pawn.story.SkinColor), compact);
                AddFeature(output, settings.IncludeHair, "Hair", pawn.story.hairDef?.label + ", "
                    + ColorUtility.ToHtmlStringRGB(pawn.story.HairColor), compact);
            }
            AddFeature(output, settings.IncludeEyes, "Eye color", "preserve from reference image", compact);
            if (settings.IncludeApparel && pawn.apparel != null)
                AddFeature(output, true, "Apparel", string.Join(", ", pawn.apparel.WornApparel
                    .Select(item => item.LabelCap).ToArray()), compact);
            AddFeature(output, settings.IncludeHealth, "Health conditions", HealthConditions(pawn), compact);
        }

        private static string AppearanceGeneFeatures(Pawn pawn)
        {
            if (pawn.story == null) return null;
            return "Body type: " + (pawn.story.bodyType?.label ?? "Unknown")
                + ", Skin color: " + ColorUtility.ToHtmlStringRGB(pawn.story.SkinColor)
                + ", Hair: " + (pawn.story.hairDef?.label ?? "Unknown") + ", "
                + ColorUtility.ToHtmlStringRGB(pawn.story.HairColor);
        }

        private static void AddFeature(StringBuilder output, bool enabled, string name, string value, bool compact)
        {
            if (!enabled || value.NullOrEmpty()) return;
            if (compact) output.Append(name).Append(": ").AppendLine(value);
            else Add(output, true, name, value);
        }
        /// <summary>Returns the default Base prompt for the configured Base provider.</summary>
        public static string BaseInstructions(PortraitSettings settings)
        {
            return settings == null ? "" : BaseInstructions(settings, settings.Provider);
        }

        private static string BaseInstructions(PortraitSettings settings, AiProvider provider)
        {
            if (settings == null) return "";
            return (provider == AiProvider.Google
                ? settings.GeminiBaseInstructions
                : provider == AiProvider.NovelAI
                ? settings.NovelAiBaseInstructions
                : provider == AiProvider.LocalComfyUI
                ? settings.ComfyBaseInstructions
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
        public static string StateOverride(PortraitSettings settings, AiProvider provider, PortraitState state)
        {
            return settings.GetStatePrompt(provider, state);
        }
    }
}
