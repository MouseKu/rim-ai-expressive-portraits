// Contains the implementation for PortraitRuntime.

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;
using RimWorld;
using Verse;

namespace RimAIPortrait
{
    /// <summary>Describes the behavior of this type or member.</summary>
    public sealed class PortraitRuntime : MonoBehaviour
    {
        public static PortraitRuntime Instance { get; private set; }
        /// <summary>Describes the behavior of this type or member.</summary>
        public static void EnsureCreated()
        {
            if (Instance != null) return;
            var go = new GameObject("RimAIPortrait.Runtime"); DontDestroyOnLoad(go); Instance = go.AddComponent<PortraitRuntime>();
        }
        /// <summary>Describes the behavior of this type or member.</summary>
        public void Generate(Pawn pawn, Action<string> report)
        {
            if (!CanGenerate(pawn, report, true, RimAIPortraitMod.Settings != null && RimAIPortraitMod.Settings.EnableEmotionChanges)) return;
            StartCoroutine(GenerateAll(pawn, report));
        }
        /// <summary>Describes the behavior of this type or member.</summary>
        public void GenerateNormal(Pawn pawn, IEnumerable<string> references, Action<string> report)
        {
            if (!CanGenerate(pawn, report, true, false)) return;
            StartCoroutine(GenerateNormalRoutine(pawn, references == null ? new string[0] : references.ToArray(), report));
        }
        /// <summary>Describes the behavior of this type or member.</summary>
        public void GenerateNormalFromReference(Pawn pawn, string referencePath, Action<string> report)
        {
            if (!CanGenerate(pawn, report, true, false)) return;
            StartCoroutine(GenerateNormalFromReferenceRoutine(pawn, referencePath, report));
        }
        /// <summary>Describes the behavior of this type or member.</summary>
        public void GenerateEmotions(Pawn pawn, IEnumerable<string> references, Action<string> report)
        {
            if (!CanGenerate(pawn, report, false, true)) return;
            StartCoroutine(GenerateEmotionsRoutine(pawn, references == null ? new string[0] : references.ToArray(), report));
        }
        /// <summary>Describes the behavior of this type or member.</summary>
        public void GenerateEmotion(Pawn pawn, PortraitState state, IEnumerable<string> references, Action<string> report)
        {
            bool normal = state == PortraitState.Normal;
            if (!CanGenerate(pawn, report, normal, !normal)) return;
            StartCoroutine(GenerateEmotionRoutine(pawn, state, references == null ? new string[0] : references.ToArray(), report));
        }
        /// <summary>Describes the behavior of this type or member.</summary>
        public void GenerateSelectedEmotions(Pawn pawn, IEnumerable<string> references, IDictionary<PortraitState, string> prompts, Action<string> report)
        {
            var selected = new Dictionary<PortraitState, string>(prompts);
            bool hasNormal = selected.ContainsKey(PortraitState.Normal);
            if (!CanGenerate(pawn, report, hasNormal, selected.Keys.Any(x => x != PortraitState.Normal))) return;
            StartCoroutine(GenerateSelectedRoutine(pawn, references.ToArray(), selected, report));
        }
        /// <summary>Describes the behavior of this type or member.</summary>
        private IEnumerator GenerateSelectedRoutine(Pawn pawn, string[] references, Dictionary<PortraitState, string> prompts, Action<string> report)
        {
            foreach (var item in prompts)
            {
                if (!RimAIPortraitMod.Settings.IsStateEnabled(item.Key)) continue;
                byte[] image = null; string error = null; report(Localization.T("RimAIPortrait.Status.GeneratingState", PortraitStateNames.Label(item.Key)));
                yield return Request(pawn, item.Key, null, references, (data, err) => { image = data; error = err; }, item.Value);
                if (image == null) { Fail(Localization.T("RimAIPortrait.Status.StateFailed", PortraitStateNames.Label(item.Key), error), report); continue; }
                PortraitData.Save(pawn, item.Key, image); report(Localization.T("RimAIPortrait.Status.StateSaved", PortraitStateNames.Label(item.Key)));
            }
            report(Localization.T("RimAIPortrait.Status.SelectedEmotionsComplete"));
        }
        /// <summary>Describes the behavior of this type or member.</summary>
        private IEnumerator GenerateEmotionRoutine(Pawn pawn, PortraitState state, string[] references, Action<string> report)
        {
            byte[] image = null; string error = null; report(Localization.T("RimAIPortrait.Status.GeneratingState", PortraitStateNames.Label(state)));
            yield return Request(pawn, state, null, references, (data, err) => { image = data; error = err; });
            if (image == null) { Fail(Localization.T("RimAIPortrait.Status.StateFailed", PortraitStateNames.Label(state), error), report); yield break; }
            PortraitData.Save(pawn, state, image); report(Localization.T("RimAIPortrait.Status.StatePortraitSaved", PortraitStateNames.Label(state)));
        }
        /// <summary>Describes the behavior of this type or member.</summary>
        private IEnumerator GenerateNormalRoutine(Pawn pawn, string[] references, Action<string> report)
        {
            Log.Message("[Rim AI Expressive Portraits] Starting normal portrait for " + pawn.LabelShort);
            byte[] sprite = RimAIPortraitMod.Settings.IncludeSprite ? PawnCapture.Capture(pawn) : null; byte[] image = null; string error = null;
            if (RimAIPortraitMod.Settings.IncludeSprite && sprite == null)
                Log.Warning("[Rim AI Expressive Portraits] Pawn capture returned no image; continuing with selected references.");
            yield return Request(pawn, PortraitState.Normal, sprite, references, (data, err) => { image = data; error = err; });
            if (image == null) { Fail(Localization.T("RimAIPortrait.Status.GenerationFailed", error), report); yield break; }
            PortraitData.Save(pawn, PortraitState.Normal, image); report(Localization.T("RimAIPortrait.Status.BaseSaved"));
            Messages.Message(Localization.T("RimAIPortrait.Status.PortraitSavedFor", pawn.LabelShort), MessageTypeDefOf.PositiveEvent, false);
        }
        /// <summary>Describes the behavior of this type or member.</summary>
        private IEnumerator GenerateNormalFromReferenceRoutine(Pawn pawn, string referencePath, Action<string> report)
        {
            byte[] reference;
            try { reference = File.ReadAllBytes(referencePath); }
            catch (Exception e) { Fail(Localization.T("RimAIPortrait.Status.ReferenceReadFailed", e.Message), report); yield break; }
            byte[] image = null; string error = null;
            Log.Message("[Rim AI Expressive Portraits] Starting referenced normal portrait for " + pawn.LabelShort);
            const string referenceInstruction = "Use the first attached image as the primary portrait to edit. Apply the requested changes from Extra Features. Unless explicitly requested, preserve the character's identity, face, hairstyle, pose, and composition.";
            yield return Request(pawn, PortraitState.Normal, reference, new string[0], (data, err) => { image = data; error = err; }, null, referenceInstruction);
            if (image == null) { Fail(Localization.T("RimAIPortrait.Status.GenerationFailed", error), report); yield break; }
            PortraitData.Save(pawn, PortraitState.Normal, image); report(Localization.T("RimAIPortrait.Status.ReferenceBaseSaved"));
            Messages.Message(Localization.T("RimAIPortrait.Status.PortraitSavedFor", pawn.LabelShort), MessageTypeDefOf.PositiveEvent, false);
        }
        /// <summary>Describes the behavior of this type or member.</summary>
        private IEnumerator GenerateEmotionsRoutine(Pawn pawn, string[] references, Action<string> report)
        {
            string error = null;
            foreach (PortraitState state in Enum.GetValues(typeof(PortraitState)))
            {
                if (state == PortraitState.Normal || !RimAIPortraitMod.Settings.IsStateEnabled(state)) continue; byte[] image = null; report(Localization.T("RimAIPortrait.Status.GeneratingState", PortraitStateNames.Label(state)));
                yield return Request(pawn, state, null, references, (data, err) => { image = data; error = err; });
                if (image == null) { Fail(Localization.T("RimAIPortrait.Status.StateFailed", PortraitStateNames.Label(state), error), report); continue; }
                PortraitData.Save(pawn, state, image);
            }
            report(Localization.T("RimAIPortrait.Status.EmotionsComplete"));
            Messages.Message(Localization.T("RimAIPortrait.Status.EmotionsCompleteFor", pawn.LabelShort), MessageTypeDefOf.PositiveEvent, false);
        }
        /// <summary>Describes the behavior of this type or member.</summary>
        private static void Fail(string message, Action<string> report)
        {
            report(message); Log.Error("[Rim AI Expressive Portraits] " + message); Messages.Message(message, MessageTypeDefOf.RejectInput, false);
        }
        /// <summary>Describes the behavior of this type or member.</summary>
        private static bool CanGenerate(Pawn pawn, Action<string> report, bool needsBase, bool needsExpression)
        {
            var s = RimAIPortraitMod.Settings;
            string error = pawn == null ? Localization.T("RimAIPortrait.Error.NoPawn")
                : s == null ? Localization.T("RimAIPortrait.Error.SettingsUnavailable")
                : needsBase ? ConfigurationError(s, false)
                : null;
            if (error.NullOrEmpty() && needsExpression) error = ConfigurationError(s, true);
            if (error.NullOrEmpty()) return true;
            report(error); return false;
        }
        /// <summary>Describes the behavior of this type or member.</summary>
        private static string ConfigurationError(PortraitSettings s, bool expression)
        {
            AiProvider provider = expression ? s.ExpressionProvider : s.Provider;
            string kind = Localization.T(expression ? "RimAIPortrait.Kind.Expression" : "RimAIPortrait.Kind.Base");
            IPortraitGenerationProvider implementation = PortraitProviderRegistry.Get(provider);
            return implementation.Validate(implementation.CreateConfiguration(s, expression), kind);
        }
        /// <summary>Describes the behavior of this type or member.</summary>
        private IEnumerator GenerateAll(Pawn pawn, Action<string> report)
        {
            var s = RimAIPortraitMod.Settings; byte[] sprite = null;
            if (s.IncludeSprite) { report(Localization.T("RimAIPortrait.Status.CapturingSprite")); yield return null; sprite = PawnCapture.Capture(pawn); }
            byte[] normal = null; string error = null;
            yield return Request(pawn, PortraitState.Normal, sprite, new string[0], (data, err) => { normal = data; error = err; });
            if (normal == null) { report(Localization.T("RimAIPortrait.Status.GenerationFailed", error)); yield break; }
            PortraitData.Save(pawn, PortraitState.Normal, normal); report(Localization.T("RimAIPortrait.Status.BaseSaved"));
            if (s.EnableEmotionChanges)
            {
                foreach (PortraitState state in Enum.GetValues(typeof(PortraitState)))
                {
                    if (state == PortraitState.Normal || !s.IsStateEnabled(state)) continue;
                    byte[] image = null; report(Localization.T("RimAIPortrait.Status.GeneratingState", PortraitStateNames.Label(state)));
                    yield return Request(pawn, state, normal, new string[0], (data, err) => { image = data; error = err; });
                    if (image == null) { Log.Warning("[Rim AI Expressive Portraits] " + state + " failed: " + error); continue; }
                    PortraitData.Save(pawn, state, image);
                }
            }
            report(Localization.T("RimAIPortrait.Status.AllComplete"));
            Messages.Message(Localization.T("RimAIPortrait.Status.AllCompleteFor", pawn.LabelShort), MessageTypeDefOf.PositiveEvent, false);
        }
        /// <summary>Describes the behavior of this type or member.</summary>
        private IEnumerator Request(Pawn pawn, PortraitState state, byte[] reference, IEnumerable<string> extraReferences, Action<byte[], string> done, string expressionOverride = null, string referenceInstruction = null)
        {
            var s = RimAIPortraitMod.Settings;
            bool expression = state != PortraitState.Normal;
            AiProvider providerId = expression ? s.ExpressionProvider : s.Provider;
            IPortraitGenerationProvider provider = PortraitProviderRegistry.Get(providerId);
            string prompt = PortraitPrompt.Build(pawn, state, s, expressionOverride);
            if (!referenceInstruction.NullOrEmpty()) prompt += "\n\n[Reference Image]\n" + referenceInstruction;
            string styleFolder = expression ? s.ExpressionStyleFolder : s.StyleFolder;
            string[] references = (extraReferences ?? Enumerable.Empty<string>()).ToArray();
            PortraitProviderConfiguration configuration = provider.CreateConfiguration(s, expression);
            var request = new PortraitGenerationRequest {
                Prompt = prompt,
                PrimaryImage = reference,
                ReferencePaths = references,
                StyleFolder = styleFolder,
                Configuration = configuration
            };
            if (s.DebugMode) LogDebugRequest(state, expression, provider, request);
            yield return provider.Generate(request, result => done(result.ImageData, result.Error));
        }
        /// <summary>Describes the behavior of this type or member.</summary>
        private static void LogDebugRequest(PortraitState state, bool expression, IPortraitGenerationProvider provider, PortraitGenerationRequest request)
        {
            int styleCount = PortraitProviderUtilities.StyleFiles(request.StyleFolder).Take(8).Count();
            Log.Message("[Rim AI Expressive Portraits][Debug] Request\n"
                + "kind=" + (expression ? "Expression" : "Base") + ", state=" + PortraitStateNames.Display(state) + ", provider=" + provider.Id + "\n"
                + provider.Describe(request.Configuration) + "\n"
                + "primary_image=" + (request.PrimaryImage != null) + ", selected_references=" + request.ReferencePaths.Count + ", style_references=" + styleCount + "\n"
                + "----- PROMPT BEGIN -----\n" + request.Prompt + "\n----- PROMPT END -----");
        }

    }

    /// <summary>Describes the behavior of this type or member.</summary>
    public static class PawnCapture
    {
        public static byte[] LastCapture { get; private set; }
        public static string LastPawnThingId { get; private set; }

        /// <summary>Describes the behavior of this type or member.</summary>
        public static byte[] Capture(Pawn pawn)
        {
            RenderTexture rt = null;
            try
            {
                MethodInfo method = typeof(PortraitsCache).GetMethods(BindingFlags.Public | BindingFlags.Static).FirstOrDefault(m => m.Name == "Get" && m.ReturnType == typeof(RenderTexture));
                if (method == null) { Log.Error("[Rim AI Expressive Portraits] PortraitsCache.Get was not found."); return null; }
                var ps = method.GetParameters(); var args = new object[ps.Length];
                for (int i = 0; i < ps.Length; i++)
                {
                    Type t = ps[i].ParameterType;
                    args[i] = t == typeof(Pawn) ? pawn : t == typeof(Vector2) ? new Vector2(512, 512) : t == typeof(Rot4) ? Rot4.South : t == typeof(Vector3) ? Vector3.zero : t == typeof(float) ? 1f : t == typeof(bool) ? true : ps[i].HasDefaultValue ? ps[i].DefaultValue : t.IsValueType ? Activator.CreateInstance(t) : null;
                }
                rt = method.Invoke(null, args) as RenderTexture; if (rt == null) { Log.Error("[Rim AI Expressive Portraits] PortraitsCache returned no texture."); return null; }
                var old = RenderTexture.active; RenderTexture.active = rt; var tex = new Texture2D(rt.width, rt.height, TextureFormat.ARGB32, false);
                tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0); tex.Apply(); RenderTexture.active = old;
                byte[] png = tex.EncodeToPNG(); UnityEngine.Object.Destroy(tex);
                LastCapture = png; LastPawnThingId = pawn.ThingID;
                return png;
            }
            catch (Exception e) { Log.Error("[Rim AI Expressive Portraits] Pawn capture failed: " + e); return null; }
        }
    }
}
