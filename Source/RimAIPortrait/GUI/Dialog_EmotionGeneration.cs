// Contains the implementation for Dialog_EmotionGeneration.

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using RimWorld;
using Verse;

namespace RimAIPortrait
{
    /// <summary>Describes the behavior of this type or member.</summary>
    public sealed class Dialog_EmotionGeneration : Window
    {
        private readonly Pawn pawn;
        private readonly string[] references;
        private readonly Action onChanged;
        private readonly Dictionary<PortraitState, bool> enabled = new Dictionary<PortraitState, bool>();
        private readonly Dictionary<PortraitState, string> prompts = new Dictionary<PortraitState, string>();
        private Vector2 scroll;
        public override Vector2 InitialSize => new Vector2(780f, 760f);

        /// <summary>Describes the behavior of this type or member.</summary>
        public Dialog_EmotionGeneration(Pawn pawn, IEnumerable<string> references, Action onChanged)
        {
            this.pawn = pawn; this.references = references.ToArray(); this.onChanged = onChanged; doCloseX = true; absorbInputAroundWindow = true;
            foreach (PortraitState state in Enum.GetValues(typeof(PortraitState)))
            {
                if (!RimAIPortraitMod.Settings.IsStateEnabled(state)) continue;
                enabled[state] = false;
                string custom = PortraitPrompt.StateOverride(RimAIPortraitMod.Settings, state);
                prompts[state] = custom.NullOrEmpty() ? PromptLibrary.State[state] : custom;
            }
        }
        /// <summary>Describes the behavior of this type or member.</summary>
        public override void DoWindowContents(Rect rect)
        {
            Text.Font = GameFont.Medium; Widgets.Label(new Rect(0, 0, rect.width, 34), Localization.T("RimAIPortrait.Emotions.Title")); Text.Font = GameFont.Small;
            Widgets.Label(new Rect(0, 36, rect.width, 28), Localization.T("RimAIPortrait.Emotions.Description"));
            int stateCount = Enum.GetValues(typeof(PortraitState)).Cast<PortraitState>().Count(RimAIPortraitMod.Settings.IsStateEnabled);
            Rect outer = new Rect(0, 70, rect.width, rect.height - 125); Rect view = new Rect(0, 0, outer.width - 18, stateCount * 74f);
            Widgets.BeginScrollView(outer, ref scroll, view); float y = 0;
            foreach (PortraitState state in Enum.GetValues(typeof(PortraitState)))
            {
                if (!RimAIPortraitMod.Settings.IsStateEnabled(state)) continue;
                bool value = enabled[state]; Widgets.CheckboxLabeled(new Rect(0, y, 155, 30), PortraitStateNames.Display(state), ref value); enabled[state] = value;
                Widgets.Label(new Rect(165, y, 75, 30), Localization.T("RimAIPortrait.Common.Prompt")); prompts[state] = Widgets.TextArea(new Rect(240, y, view.width - 245, 62), prompts[state] ?? ""); y += 74;
            }
            Widgets.EndScrollView();
            if (Widgets.ButtonText(new Rect(rect.width - 190, rect.height - 45, 190, 40), Localization.T("RimAIPortrait.Emotions.Generate")))
            {
                var chosen = prompts.Where(x => enabled[x.Key]).ToDictionary(x => x.Key, x => x.Value);
                if (chosen.Count == 0) { Messages.Message(Localization.T("RimAIPortrait.Emotions.SelectOne"), MessageTypeDefOf.RejectInput, false); return; }
                PortraitRuntime.Instance.GenerateSelectedEmotions(pawn, references, chosen, message => { onChanged?.Invoke(); Messages.Message(message, MessageTypeDefOf.NeutralEvent, false); }); Close();
            }
        }
    }
}
