using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimAIPortrait
{
    internal sealed class Dialog_XenotypeProvider : Window
    {
        private readonly PortraitSettings settings;
        private readonly Action save;
        private readonly List<XenotypeDef> xenotypes;
        private readonly Dictionary<string, XenotypePromptEntry> entriesByDefName;
        private Vector2 scroll;

        public override Vector2 InitialSize => PortraitWindowSizing.Fit(850f, 750f);

        public Dialog_XenotypeProvider(PortraitSettings settings, Action save)
        {
            this.settings = settings;
            this.save = save;
            xenotypes = DefDatabase<XenotypeDef>.AllDefsListForReading
                .OrderBy(def => def.defName, StringComparer.OrdinalIgnoreCase).ToList();
            if (settings.XenotypePromptEntries == null)
                settings.XenotypePromptEntries = new List<XenotypePromptEntry>();
            entriesByDefName = new Dictionary<string, XenotypePromptEntry>(StringComparer.Ordinal);
            foreach (XenotypePromptEntry entry in settings.XenotypePromptEntries)
            {
                if (entry != null && entry.DefName != null && !entriesByDefName.ContainsKey(entry.DefName))
                    entriesByDefName.Add(entry.DefName, entry);
            }
            doCloseX = true;
            absorbInputAroundWindow = true;
            closeOnClickedOutside = false;
        }

        public override void DoWindowContents(Rect rect)
        {
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(0f, 0f, rect.width, 35f),
                Localization.T("RimAIPortrait.Settings.XenotypeProvider"));
            Text.Font = GameFont.Small;
            Widgets.Label(new Rect(0f, 38f, rect.width, 55f),
                Localization.T("RimAIPortrait.Settings.XenotypeProviderHelp"));

            Rect header = new Rect(0f, 100f, rect.width - 18f, 28f);
            Widgets.Label(new Rect(header.x, header.y, 90f, header.height),
                Localization.T("RimAIPortrait.Settings.XenotypeEnabled"));
            Widgets.Label(new Rect(header.x + 95f, header.y, 250f, header.height),
                Localization.T("RimAIPortrait.Settings.XenotypeKey"));
            Widgets.Label(new Rect(header.x + 355f, header.y, header.width - 355f, header.height),
                Localization.T("RimAIPortrait.Settings.XenotypeValue"));

            Rect viewport = new Rect(0f, 130f, rect.width, rect.height - 190f);
            float viewWidth = viewport.width - 18f;
            Rect view = new Rect(0f, 0f, viewWidth, xenotypes.Count * 42f);
            Widgets.BeginScrollView(viewport, ref scroll, view);
            for (int i = 0; i < xenotypes.Count; i++)
            {
                XenotypeDef def = xenotypes[i];
                Rect row = new Rect(0f, i * 42f, viewWidth, 38f);
                if (!row.Overlaps(new Rect(0f, scroll.y, viewWidth, viewport.height))) continue;
                XenotypePromptEntry entry = EntryFor(def);
                bool enabled = entry.Enabled;
                Widgets.Checkbox(new Vector2(28f, row.y + 5f), ref enabled);
                entry.Enabled = enabled;
                TooltipHandler.TipRegion(new Rect(0f, row.y, 90f, row.height),
                    Localization.T("RimAIPortrait.Settings.XenotypeEnabledTip"));
                Widgets.Label(new Rect(95f, row.y + 6f, 250f, 30f), def.defName);
                bool hasJsonValue = XenotypePromptDefaults.HasValueFor(def.defName);
                Rect valueRect = new Rect(355f, row.y + 2f,
                    viewWidth - 355f - (hasJsonValue ? 76f : 0f), 32f);
                entry.Value = Widgets.TextField(valueRect, entry.Value ?? "");
                TooltipHandler.TipRegion(valueRect,
                    Localization.T("RimAIPortrait.Settings.XenotypeValueTip"));
                if (hasJsonValue)
                {
                    Rect resetRect = new Rect(valueRect.xMax + 4f, row.y + 2f, 72f, 32f);
                    if (Widgets.ButtonText(resetRect, "JSON"))
                    {
                        entry.Value = XenotypePromptDefaults.ValueFor(def.defName, def.label);
                        entry.Enabled = true;
                    }
                    TooltipHandler.TipRegion(resetRect,
                        Localization.T("RimAIPortrait.Settings.XenotypeUseJsonTip"));
                }
                TooltipHandler.TipRegion(new Rect(95f, row.y, 250f, 38f), def.LabelCap);
            }
            Widgets.EndScrollView();

            if (Widgets.ButtonText(new Rect(rect.width - 120f, rect.height - 48f, 120f, 38f),
                Localization.T("RimAIPortrait.Common.Save")))
                Close();
        }

        private XenotypePromptEntry EntryFor(XenotypeDef def)
        {
            XenotypePromptEntry entry;
            entriesByDefName.TryGetValue(def.defName, out entry);
            if (entry != null)
            {
                string jsonValue = XenotypePromptDefaults.ValueFor(def.defName, def.label ?? def.defName);
                if (XenotypePromptDefaults.HasValueFor(def.defName) &&
                    (string.Equals(entry.Value, def.label, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(entry.Value, def.defName, StringComparison.OrdinalIgnoreCase)))
                    entry.Value = jsonValue;
                if (entry.DefaultValue == null) entry.DefaultValue = jsonValue;
                return entry;
            }
            entry = new XenotypePromptEntry { DefName = def.defName,
                Value = XenotypePromptDefaults.ValueFor(def.defName, def.label ?? def.defName),
                Enabled = XenotypePromptDefaults.HasValueFor(def.defName) };
            entry.DefaultValue = entry.Value;
            settings.XenotypePromptEntries.Add(entry);
            entriesByDefName.Add(def.defName, entry);
            return entry;
        }

        public override void PostClose()
        {
            save();
            base.PostClose();
        }
    }
}
