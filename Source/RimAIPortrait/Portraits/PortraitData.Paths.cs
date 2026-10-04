using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Verse;

namespace RimAIPortrait
{
    public static partial class PortraitData
    {
        private static readonly Dictionary<string, string> PawnFolders = new Dictionary<string, string>();

        private static string rootPath;
        private static string configuredRoot;
        private static bool recheckRoot = true;
        private const string PawnFolderSeparator = "__";
        public static string DefaultRoot => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "RimAIPortraits");

        public static bool TryNormalizeCustomRoot(string folder, out string path)
        {
            path = null;
            if (string.IsNullOrWhiteSpace(folder)) return false;
            try
            {
                string value = folder.Trim();
                string root = Path.GetPathRoot(value);
                bool driveAbsolute = root != null && root.Length >= 3 && root[1] == ':'
                    && (root[2] == '\\' || root[2] == '/');
                bool networkAbsolute = root != null && root.StartsWith(@"\\", StringComparison.Ordinal)
                    && root.IndexOf('\\', 2) > 2 && root.IndexOf('\\', 2) < root.Length - 1;
                if (!Path.IsPathRooted(value) || (!driveAbsolute && !networkAbsolute)) return false;
                path = Path.GetFullPath(value);
                return true;
            }
            catch (ArgumentException) { return false; }
            catch (NotSupportedException) { return false; }
            catch (PathTooLongException) { return false; }
        }

        public static bool TryGetCustomRoot(string folder, out string path)
        {
            if (!TryNormalizeCustomRoot(folder, out path)) return false;
            if (Directory.Exists(path)) return true;
            path = null;
            return false;
        }

        public static void RecheckRoot()
        {
            recheckRoot = true;
            _ = Root;
        }

        public static string Root
        {
            get
            {
                string configured = RimAIPortraitMod.Settings?.PortraitRootFolder;
                if (!recheckRoot && rootPath != null
                    && string.Equals(configuredRoot, configured, StringComparison.Ordinal)) return rootPath;
                string custom;
                bool useCustom = TryGetCustomRoot(configured, out custom);
                string root = useCustom ? custom : DefaultRoot;
                if (!useCustom)
                {
                    string legacy = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "rim-portraits");
                    if (!Directory.Exists(root) && Directory.Exists(legacy))
                    {
                        try { Directory.Move(legacy, root); }
                        catch (Exception e) { Log.Warning("[Rim AI Expressive Portraits] Could not migrate portrait root folder: " + e.Message); }
                    }
                }
                if (rootPath != null && !string.Equals(rootPath, root, StringComparison.OrdinalIgnoreCase)) ClearCaches();
                rootPath = root;
                configuredRoot = configured;
                recheckRoot = false;
                return rootPath;
            }
        }

        public static string PawnFolder(Pawn p)
        {
            EnsureGameScope();
            string root = Root;
            string cached;
            if (PawnFolders.TryGetValue(p.ThingID, out cached)) return cached;
            string displayName = SafeName(p.Name?.ToStringShort ?? p.LabelShort ?? "Unnamed");
            string pawnId = SafeName(p.ThingID);
            string preferred = Path.Combine(root, displayName + PawnFolderSeparator + pawnId);
            if (Directory.Exists(preferred)) return PawnFolders[p.ThingID] = preferred;

            // Preserve the intended portrait behavior and compatibility.
            // Preserve the intended portrait behavior and compatibility.
            if (Directory.Exists(root))
            {
                string suffix = PawnFolderSeparator + pawnId;
                try
                {
                    string existing = Directory.EnumerateDirectories(root)
                        .FirstOrDefault(path => Path.GetFileName(path).EndsWith(suffix, StringComparison.OrdinalIgnoreCase));
                    if (existing != null) return PawnFolders[p.ThingID] = existing;
                }
                catch (IOException e) { Log.Warning("[Rim AI Expressive Portraits] Could not inspect portrait folders: " + e.Message); }
                catch (UnauthorizedAccessException e) { Log.Warning("[Rim AI Expressive Portraits] Could not inspect portrait folders: " + e.Message); }
            }

            // Preserve the intended portrait behavior and compatibility.
            // Preserve the intended portrait behavior and compatibility.
            // Preserve the intended portrait behavior and compatibility.
            string legacy = Path.Combine(root, displayName);
            if (Directory.Exists(legacy))
            {
                try { Directory.Move(legacy, preferred); return PawnFolders[p.ThingID] = preferred; }
                catch (IOException e) { Log.Warning("[Rim AI Expressive Portraits] Could not migrate legacy portrait folder: " + e.Message); }
                catch (UnauthorizedAccessException e) { Log.Warning("[Rim AI Expressive Portraits] Could not migrate legacy portrait folder: " + e.Message); }
            }
            PawnFolders[p.ThingID] = preferred;
            return preferred;
        }
        /// <summary>Describes the behavior of this type or member.</summary>
        public static string PathFor(Pawn p, PortraitState state) => Path.Combine(PawnFolder(p), PortraitStateNames.Display(state) + ".png");
        /// <summary>Describes the behavior of this type or member.</summary>
        public static string DeathPath(Pawn p) => Path.Combine(PawnFolder(p), "Death.png");
        /// <summary>Describes the behavior of this type or member.</summary>
        private static string LegacyNormalPath(Pawn p) => Path.Combine(PawnFolder(p), "Normal.png");
        /// <summary>Describes the behavior of this type or member.</summary>
        public static string ExistingPathFor(Pawn p, PortraitState state)
        {
            string path = PathFor(p, state);
            if (state == PortraitState.Base && !File.Exists(path))
            {
                string legacy = LegacyNormalPath(p);
                if (File.Exists(legacy))
                {
                    try { File.Move(legacy, path); }
                    catch (Exception e)
                    {
                        Log.Warning("[Rim AI Expressive Portraits] Could not migrate legacy Base portrait: " + e.Message);
                        return legacy;
                    }
                }
            }
            return path;
        }
        /// <summary>Describes the behavior of this type or member.</summary>
        public static string ExtraFeaturesPath(Pawn p) => Path.Combine(PawnFolder(p), "extra_features.txt");
        public static string BaseTagsPath(Pawn p) => Path.Combine(PawnFolder(p), "base_tags.json");
        public static string ExpressionTagsPath(Pawn p, PortraitState state) =>
            Path.Combine(PawnFolder(p), "expression_tags_" + state.ToString().ToLowerInvariant() + ".json");
        public static string NovelAiExtraTagPath(Pawn p) => Path.Combine(PawnFolder(p), "novelai_extra_tag.txt");
        public static string ComfyInpaintTagPath(Pawn p) => Path.Combine(PawnFolder(p), "comfy_inpaint_tag.txt");
        internal static string LegacyInpaintTagImportPath(Pawn p, PortraitState state, AiProvider provider) =>
            Path.Combine(PawnFolder(p), "legacy_inpaint_tag_" + provider + "_" + state + ".imported");
        public static string NovelAiInpaintStrengthPath(Pawn p) => Path.Combine(PawnFolder(p), "novelai_inpaint_strength.txt");
        public static string NovelAiInpaintStrengthPath(Pawn p, PortraitState state) =>
            Path.Combine(PawnFolder(p), "novelai_inpaint_strength_" + state.ToString().ToLowerInvariant() + ".txt");
        /// <summary>Describes the behavior of this type or member.</summary>
        /// <summary>Describes the behavior of this type or member.</summary>

        public static string SafeId(Pawn p) => SafeName(p.ThingID);
        /// <summary>Describes the behavior of this type or member.</summary>
        private static string SafeName(string value)
        {
            if (value.NullOrEmpty()) return "Unnamed";
            foreach (char c in Path.GetInvalidFileNameChars()) value = value.Replace(c, '_');
            return value.Trim().NullOrEmpty() ? "Unnamed" : value.Trim();
        }
        /// <summary>Describes the behavior of this type or member.</summary>
    }
}
