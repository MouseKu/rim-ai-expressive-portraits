using UnityEngine;
using Verse;

namespace RimAIPortrait
{
    internal static class TagDisplay
    {
        public static void Draw(Rect labelRect, Rect valueRect, string label, string value)
        {
            Widgets.Label(labelRect, label);
            bool previousEnabled = GUI.enabled;
            GUI.enabled = false;
            Widgets.TextField(valueRect, value ?? "");
            GUI.enabled = previousEnabled;
            if (!string.IsNullOrEmpty(value)) TooltipHandler.TipRegion(valueRect, value);
        }

        public static string DrawEditable(Rect labelRect, Rect valueRect, string label, string value,
            string controlName, string placeholder)
        {
            Widgets.Label(labelRect, label);
            GUI.SetNextControlName(controlName);
            string edited = Widgets.TextField(valueRect, value ?? "");
            if (string.IsNullOrEmpty(edited) && GUI.GetNameOfFocusedControl() != controlName
                && Event.current.type == EventType.Repaint)
            {
                Color previousColor = GUI.color;
                GUI.color = new Color(.55f, .55f, .55f, 1f);
                Widgets.Label(new Rect(valueRect.x + 6f, valueRect.y + 4f,
                    valueRect.width - 12f, valueRect.height - 8f), placeholder);
                GUI.color = previousColor;
            }
            if (!string.IsNullOrEmpty(edited)) TooltipHandler.TipRegion(valueRect, edited);
            return edited;
        }
    }
}
