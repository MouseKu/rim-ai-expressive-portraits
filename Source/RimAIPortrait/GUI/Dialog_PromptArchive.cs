using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using UnityEngine;
using Verse;

namespace RimAIPortrait
{
    /// <summary>Exports prompt settings and imports a selected prompt ZIP.</summary>
    internal sealed class Dialog_PromptArchive : Window
    {
        private readonly PortraitSettings settings;
        private readonly Action saveSettings;
        private string selectedPath = "";
        private string status = "";
        private string[] archives = new string[0];
        private Vector2 scroll;

        public override Vector2 InitialSize => new Vector2(720f, 540f);

        public Dialog_PromptArchive(PortraitSettings settings, Action saveSettings)
        {
            this.settings = settings;
            this.saveSettings = saveSettings;
            doCloseX = true;
            absorbInputAroundWindow = true;
            closeOnClickedOutside = false;
        }

        public override void PreOpen()
        {
            base.PreOpen();
            Refresh();
        }

        public override void DoWindowContents(Rect rect)
        {
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(0f, 0f, rect.width, 38f), Localization.T("RimAIPortrait.PromptArchive.Title"));
            Text.Font = GameFont.Small;
            Widgets.Label(new Rect(0f, 43f, rect.width, 42f), Localization.T("RimAIPortrait.PromptArchive.Scope"));

            if (Widgets.ButtonText(new Rect(0f, 90f, 150f, 34f), Localization.T("RimAIPortrait.PromptArchive.Export")))
            {
                try
                {
                    selectedPath = PromptArchive.Export(settings);
                    status = Localization.T("RimAIPortrait.PromptArchive.Exported");
                    Refresh();
                }
                catch (Exception exception)
                {
                    status = Localization.T("RimAIPortrait.PromptArchive.Failed", exception.Message);
                }
            }
            if (Widgets.ButtonText(new Rect(160f, 90f, 150f, 34f), Localization.T("RimAIPortrait.PromptArchive.OpenFolder")))
            {
                try
                {
                    Directory.CreateDirectory(PromptArchive.DirectoryPath);
                    Process.Start(PromptArchive.DirectoryPath);
                }
                catch (Exception exception)
                {
                    status = Localization.T("RimAIPortrait.PromptArchive.Failed", exception.Message);
                }
            }
            if (Widgets.ButtonText(new Rect(320f, 90f, 120f, 34f), Localization.T("RimAIPortrait.PromptArchive.Refresh")))
                Refresh();

            Widgets.Label(new Rect(0f, 135f, rect.width, 28f), Localization.T("RimAIPortrait.PromptArchive.Path"));
            selectedPath = Widgets.TextField(new Rect(0f, 165f, rect.width - 160f, 30f), selectedPath ?? "");
            if (Widgets.ButtonText(new Rect(rect.width - 150f, 165f, 150f, 30f),
                Localization.T("RimAIPortrait.PromptArchive.Import")))
            {
                string path = selectedPath.Trim();
                if (path.Length == 0)
                    status = Localization.T("RimAIPortrait.PromptArchive.SelectFile");
                else
                    Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation(
                        Localization.T("RimAIPortrait.PromptArchive.ConfirmImport"), () => Import(path)));
            }

            Widgets.Label(new Rect(0f, 205f, rect.width, 26f), Localization.T("RimAIPortrait.PromptArchive.Available"));
            Rect listRect = new Rect(0f, 235f, rect.width, rect.height - 300f);
            Widgets.DrawMenuSection(listRect);
            Rect view = new Rect(0f, 0f, listRect.width - 18f, Math.Max(listRect.height, archives.Length * 32f));
            Widgets.BeginScrollView(listRect, ref scroll, view);
            for (int index = 0; index < archives.Length; index++)
            {
                string path = archives[index];
                if (Widgets.ButtonText(new Rect(4f, index * 32f, view.width - 8f, 30f), Path.GetFileName(path)))
                    selectedPath = path;
            }
            Widgets.EndScrollView();
            Widgets.Label(new Rect(0f, rect.height - 56f, rect.width, 52f), status);
        }

        private void Refresh()
        {
            try
            {
                archives = Directory.Exists(PromptArchive.DirectoryPath)
                    ? Directory.GetFiles(PromptArchive.DirectoryPath, "*.zip")
                        .OrderByDescending(path => File.GetLastWriteTimeUtc(path)).ToArray()
                    : new string[0];
            }
            catch (Exception exception)
            {
                archives = new string[0];
                status = Localization.T("RimAIPortrait.PromptArchive.Failed", exception.Message);
            }
        }

        private void Import(string path)
        {
            try
            {
                PromptArchive.Import(settings, path);
                saveSettings();
                status = Localization.T("RimAIPortrait.PromptArchive.Imported");
            }
            catch (Exception exception)
            {
                status = Localization.T("RimAIPortrait.PromptArchive.Failed", exception.Message);
            }
        }
    }
}
