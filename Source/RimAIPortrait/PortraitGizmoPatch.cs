// Contains the implementation for PortraitGizmoPatch.

using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimAIPortrait
{
    // Preserve the intended portrait behavior and compatibility.
    // Preserve the intended portrait behavior and compatibility.
    /// <summary>Describes the behavior of this type or member.</summary>
    [HarmonyPatch(typeof(Pawn), nameof(Pawn.GetGizmos))]
    public static class PortraitGizmoPatch
    {
        /// <summary>Describes the behavior of this type or member.</summary>
        /// <param name="__result">The __result value.</param>
        /// <param name="__instance">The __instance value.</param>
        /// <returns>The resulting value.</returns>
        [HarmonyPostfix]
        public static IEnumerable<Gizmo> AddPortraitCommand(IEnumerable<Gizmo> __result, Pawn __instance)
        {
            foreach (Gizmo gizmo in __result)
                yield return gizmo;

            if (__instance == null || __instance.Dead ||
                (!__instance.RaceProps.Humanlike && !__instance.RaceProps.Animal) ||
                __instance.Faction != Faction.OfPlayer)
                yield break;

            yield return new Command_Action
            {
                defaultLabel = Localization.T("RimAIPortrait.Gizmo.Label"),
                defaultDesc = Localization.T("RimAIPortrait.Gizmo.Description"),
                icon = PortraitCommandIcon.Texture,
                action = () => Find.WindowStack.Add(new Dialog_PortraitGallery(__instance))
            };
        }
    }

    /// <summary>Describes the behavior of this type or member.</summary>
    internal static class PortraitCommandIcon
    {
        private const string TexturePath = "AIExpressivePortraits/icon_button";
        private static Texture2D texture;

        /// <summary>Describes the behavior of this type or member.</summary>
        public static Texture2D Texture => texture ?? (texture = ContentFinder<Texture2D>.Get(TexturePath));
    }
}
