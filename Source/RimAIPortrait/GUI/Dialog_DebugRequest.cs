using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.Networking;
using Verse;

namespace RimAIPortrait
{
    internal sealed class Dialog_DebugRequest : Window
    {
        internal sealed class Entry
        {
            public string Title;
            public string Preview;
            public Func<string> FullText;
            public string CachedFull;
            public string ResponsePreview;
            public Func<string> ResponseFull;
            public string CachedResponse;
            public string CachedCombined;

            public string Full()
            {
                if (CachedFull == null) CachedFull = FullText();
                if (ResponseFull == null)
                    return CachedFull + "\n\n" + Localization.T("RimAIPortrait.DebugRequest.Waiting");
                if (CachedResponse == null) CachedResponse = ResponseFull();
                if (CachedCombined == null) CachedCombined = CachedFull + "\n\n" + CachedResponse;
                return CachedCombined;
            }

            public string VisiblePreview => Preview + "\n\n" + (ResponsePreview
                ?? Localization.T("RimAIPortrait.DebugRequest.Waiting"));
        }

        internal sealed class Trace
        {
            private readonly Entry entry;

            internal Trace(Entry entry) { this.entry = entry; }

            public void Respond(UnityWebRequest request)
            {
                if (entry == null || request == null) return;
                string contentType = request.GetResponseHeader("Content-Type") ?? "";
                byte[] data = request.downloadHandler?.data ?? new byte[0];
                string header = "HTTP " + request.responseCode + "\nContent-Type: " + contentType
                    + (request.error.NullOrEmpty() ? "" : "\nError: " + request.error) + "\n\n";
                bool textBody = contentType.IndexOf("json", StringComparison.OrdinalIgnoreCase) >= 0
                    || contentType.IndexOf("text", StringComparison.OrdinalIgnoreCase) >= 0
                    || data.Length > 0 && (data[0] == '{' || data[0] == '[');
                if (textBody)
                {
                    string body = request.downloadHandler.text ?? "";
                    entry.ResponsePreview = header + Compact(body);
                    entry.ResponseFull = () => header + body;
                }
                else
                {
                    entry.ResponsePreview = header + Localization.T("RimAIPortrait.DebugRequest.Binary", data.Length);
                    entry.ResponseFull = () => header + "base64: " + Convert.ToBase64String(data);
                }
            }
        }

        private static Dialog_DebugRequest current;
        private readonly List<Entry> entries = new List<Entry>();
        private int selected;
        private bool showFull;
        private Vector2 scroll;

        public override Vector2 InitialSize => PortraitWindowSizing.Fit(1050f, 850f);

        private Dialog_DebugRequest()
        {
            doCloseX = true;
            absorbInputAroundWindow = false;
            closeOnClickedOutside = false;
        }

        public static Trace Show(string provider, string url, string body)
        {
            if (RimAIPortraitMod.Settings?.ShowApiRequest != true) return null;
            string title = "POST " + url + " (" + provider + ")";
            string full = title + "\nContent-Type: application/json\n"
                + Localization.T("RimAIPortrait.DebugRequest.KeysHidden") + "\n\n" + body;
            var entry = new Entry { Title = title, Preview = Compact(full), FullText = () => full };
            Add(entry);
            return new Trace(entry);
        }

        public static Trace ShowGet(string provider, string url)
        {
            if (RimAIPortraitMod.Settings?.ShowApiRequest != true) return null;
            string title = "GET " + url + " (" + provider + ")";
            var entry = new Entry { Title = title, Preview = title, FullText = () => title };
            Add(entry);
            return new Trace(entry);
        }

        public static Trace ShowMultipart(string provider, string url, IList<IMultipartFormSection> parts)
        {
            if (RimAIPortraitMod.Settings?.ShowApiRequest != true) return null;
            var fields = new List<KeyValuePair<string, byte[]>>();
            var names = new List<string>();
            var types = new List<string>();
            foreach (IMultipartFormSection part in parts)
            {
                fields.Add(new KeyValuePair<string, byte[]>(part.sectionName, part.sectionData));
                names.Add(part.fileName);
                types.Add(part.contentType);
            }
            string title = "POST " + url + " (" + provider + ")";
            var preview = new StringBuilder(title).Append("\nContent-Type: multipart/form-data\n")
                .Append(Localization.T("RimAIPortrait.DebugRequest.KeysHidden")).Append("\n\n");
            for (int index = 0; index < fields.Count; index++)
            {
                preview.Append(fields[index].Key).Append(": ");
                if (string.IsNullOrEmpty(names[index]))
                    preview.Append(Encoding.UTF8.GetString(fields[index].Value ?? new byte[0]));
                else
                    preview.Append(names[index]).Append(" (").Append(types[index]).Append(", ")
                        .Append(fields[index].Value?.Length ?? 0).Append(" bytes)");
                preview.AppendLine();
            }
            var entry = new Entry {
                Title = title,
                Preview = preview.ToString(),
                FullText = () => FullMultipart(title, fields, names, types)
            };
            Add(entry);
            return new Trace(entry);
        }

        private static string FullMultipart(string title, List<KeyValuePair<string, byte[]>> fields,
            List<string> names, List<string> types)
        {
            var text = new StringBuilder(title).Append("\nContent-Type: multipart/form-data\n")
                .Append(Localization.T("RimAIPortrait.DebugRequest.KeysHidden")).Append("\n\n");
            for (int index = 0; index < fields.Count; index++)
            {
                text.Append(fields[index].Key).Append(": ");
                if (string.IsNullOrEmpty(names[index]))
                    text.Append(Encoding.UTF8.GetString(fields[index].Value ?? new byte[0]));
                else
                    text.Append(names[index]).Append(" (").Append(types[index]).Append(") base64: ")
                        .Append(Convert.ToBase64String(fields[index].Value ?? new byte[0]));
                text.AppendLine();
            }
            return text.ToString();
        }

        private static string Compact(string full)
        {
            return Regex.Replace(full, "(?<=base64,)[A-Za-z0-9+/=]{512,}|(?<=\")[A-Za-z0-9+/=]{512,}(?=\")",
                match => "[base64: " + match.Length + " characters]");
        }

        private static void Add(Entry entry)
        {
            if (current == null)
            {
                current = new Dialog_DebugRequest();
                Find.WindowStack.Add(current);
                current.entries.Add(entry);
                current.selected = 0;
                return;
            }
            bool followingLatest = current.selected == current.entries.Count - 1;
            current.entries.Add(entry);
            if (current.entries.Count > 100)
            {
                current.entries.RemoveAt(0);
                current.selected = Math.Max(0, current.selected - 1);
            }
            if (followingLatest)
            {
                current.selected = current.entries.Count - 1;
                current.showFull = false;
                current.scroll = Vector2.zero;
            }
        }

        public override void DoWindowContents(Rect rect)
        {
            if (entries.Count == 0) return;
            Entry entry = entries[selected];
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(0f, 0f, rect.width, 36f), Localization.T("RimAIPortrait.DebugRequest.Title"));
            Text.Font = GameFont.Small;
            Widgets.Label(new Rect(0f, 40f, rect.width, 46f), entry.Title);
            if (Widgets.ButtonText(new Rect(0f, 90f, 100f, 32f), "◀")) Select(selected - 1);
            Widgets.Label(new Rect(110f, 90f, 110f, 32f), (selected + 1) + " / " + entries.Count);
            if (Widgets.ButtonText(new Rect(220f, 90f, 100f, 32f), "▶")) Select(selected + 1);
            if (Widgets.ButtonText(new Rect(rect.width - 320f, 90f, 150f, 32f),
                Localization.T(showFull ? "RimAIPortrait.DebugRequest.Preview" : "RimAIPortrait.DebugRequest.Full")))
            {
                showFull = !showFull;
                scroll = Vector2.zero;
            }
            if (Widgets.ButtonText(new Rect(rect.width - 160f, 90f, 160f, 32f),
                Localization.T("RimAIPortrait.DebugRequest.Copy")))
                GUIUtility.systemCopyBuffer = showFull ? entry.Full() : entry.VisiblePreview;
            string content = showFull ? entry.Full() : entry.VisiblePreview;
            if (content.Length > 100000)
                content = content.Substring(0, 100000) + "\n\n"
                    + Localization.T("RimAIPortrait.DebugRequest.DisplayLimit");
            Rect area = new Rect(0f, 130f, rect.width, rect.height - 130f);
            float height = Math.Max(area.height, Text.CalcHeight(content, area.width - 24f) + 20f);
            Rect view = new Rect(0f, 0f, area.width - 18f, height);
            Widgets.BeginScrollView(area, ref scroll, view);
            Widgets.Label(new Rect(4f, 0f, view.width - 8f, height), content);
            Widgets.EndScrollView();
        }

        private void Select(int index)
        {
            if (index < 0 || index >= entries.Count) return;
            selected = index;
            showFull = false;
            scroll = Vector2.zero;
        }

        public override void PostClose()
        {
            if (current == this) current = null;
            base.PostClose();
        }
    }
}
