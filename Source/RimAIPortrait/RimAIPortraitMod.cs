// Contains the implementation for RimAIPortraitMod.

using HarmonyLib;
using Verse;

namespace RimAIPortrait
{
    /// <summary>Describes the behavior of this type or member.</summary>
    public sealed partial class RimAIPortraitMod : Mod
    {
        public static PortraitSettings Settings;
        public static ModContentPack ModPack;
        private static RimAIPortraitMod instance;

        /// <summary>Describes the behavior of this type or member.</summary>
        /// <param name="content">The content value.</param>
        public RimAIPortraitMod(ModContentPack content) : base(content)
        {
            instance = this;
            ModPack = content;
            ProviderOptionDefaults.Load(content);
            SettingsDefaults.Load(content);
            Settings = GetSettings<PortraitSettings>();
            ApplyPromptDefaults(content);
            new Harmony("mint.rimaiportrait").PatchAll();
            LongEventHandler.ExecuteWhenFinished(PortraitRuntime.EnsureCreated);
        }

        /// <summary>Describes the behavior of this type or member.</summary>
        public override string SettingsCategory() => Localization.T("RimAIPortrait.Settings.Category");

        /// <summary>Immediately persists values changed outside the main settings window.</summary>
        public static void SaveSettings()
        {
            instance?.WriteSettings();
        }

    }
}
