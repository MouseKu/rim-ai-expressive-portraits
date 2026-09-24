// Contains the implementation for Dialog_ImagePreview.

using System;
using System.IO;
using UnityEngine;
using Verse;

namespace RimAIPortrait
{
    /// <summary>Describes the behavior of this type or member.</summary>
    public sealed class Dialog_ImagePreview : Window
    {
        private readonly string path;
        private Texture2D texture;
        private string error;

        public override Vector2 InitialSize => new Vector2(860f, 860f);

        /// <summary>Describes the behavior of this type or member.</summary>
        /// <param name="path">The path value.</param>
        public Dialog_ImagePreview(string path)
        {
            this.path = path;
            doCloseX = true;
            absorbInputAroundWindow = true;
            closeOnClickedOutside = false;
        }

        /// <summary>Describes the behavior of this type or member.</summary>
        public override void PreOpen()
        {
            base.PreOpen();
            try
            {
                texture = new Texture2D(2, 2, TextureFormat.ARGB32, false);
                if (!texture.LoadImage(File.ReadAllBytes(path))) throw new InvalidDataException(Localization.T("RimAIPortrait.Preview.UnsupportedImage"));
                texture.wrapMode = TextureWrapMode.Clamp;
            }
            catch (Exception e)
            {
                if (texture != null) UnityEngine.Object.Destroy(texture);
                texture = null;
                error = e.Message;
            }
        }

        /// <summary>Describes the behavior of this type or member.</summary>
        public override void DoWindowContents(Rect rect)
        {
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(0, 0, rect.width, 34), Localization.T("RimAIPortrait.Preview.Title"));
            Text.Font = GameFont.Small;
            Widgets.Label(new Rect(0, 38, rect.width, 26), Path.GetFileName(path));
            Rect imageArea = new Rect(0, 70, rect.width, rect.height - 70);
            Widgets.DrawMenuSection(imageArea);
            if (texture != null)
                GUI.DrawTexture(imageArea.ContractedBy(8f), texture, ScaleMode.ScaleToFit, true);
            else
                Widgets.Label(imageArea.ContractedBy(12f), Localization.T("RimAIPortrait.Preview.LoadFailed", error ?? ""));
        }

        /// <summary>Describes the behavior of this type or member.</summary>
        public override void PostClose()
        {
            if (texture != null) UnityEngine.Object.Destroy(texture);
            texture = null;
            base.PostClose();
        }
    }
}
