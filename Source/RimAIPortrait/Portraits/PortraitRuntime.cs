// Contains the implementation for PortraitRuntime.

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using RimWorld;
using Verse;

namespace RimAIPortrait
{
    /// <summary>Describes the behavior of this type or member.</summary>
    public sealed class PortraitRuntime : MonoBehaviour
    {
        private sealed class InpaintSelection
        {
            public byte[] Source;
            public byte[] Mask;
            public byte[] RequestMask;
            public float Strength;
            public bool NarrowFeather;
            public string ExtraTag;
            public string Error;
        }

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
        public void GenerateNormal(Pawn pawn, Action<string> report)
        {
            if (!CanGenerate(pawn, report, true, false)) return;
            StartCoroutine(GenerateNormalRoutine(pawn, report));
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
            Dictionary<PortraitState, InpaintSelection> selections = null;
            Dictionary<PortraitState, ExtractedTags> batchTags = null;
            bool batchPrepared = false;
            foreach (var item in prompts.OrderBy(item => item.Key == PortraitState.Normal ? 0 : 1))
            {
                if (!RimAIPortraitMod.Settings.IsStateEnabled(item.Key)) continue;
                if (item.Key != PortraitState.Normal && selections == null && UsesInpaintSelection())
                {
                    yield return CollectInpaintSelections(pawn, prompts.Keys, null, value => selections = value);
                }
                if (item.Key != PortraitState.Normal && !batchPrepared)
                {
                    batchPrepared = true;
                    yield return PrepareBatchTags(pawn, prompts.Keys, selections, null, prompts,
                        value => batchTags = value);
                }
                InpaintSelection selection = null;
                if (selections != null && selections.TryGetValue(item.Key, out selection) && selection.Error != null)
                {
                    report(Localization.T("RimAIPortrait.Status.StateFailed", PortraitStateNames.Label(item.Key), selection.Error));
                    continue;
                }
                byte[] image = null; string error = null; report(Localization.T("RimAIPortrait.Status.GeneratingState", PortraitStateNames.Label(item.Key)));
                yield return Request(pawn, item.Key, null, references, (data, err) => { image = data; error = err; }, item.Value,
                    preparedInpaint: selection, batchTags: batchTags);
                if (image == null) { Fail(Localization.T("RimAIPortrait.Status.StateFailed", PortraitStateNames.Label(item.Key), error), report); continue; }
                PortraitData.Save(pawn, item.Key, image);
                if (item.Key == PortraitState.Normal) InspectPanePatch.ShowStateImmediately(pawn, PortraitState.Normal);
                report(Localization.T("RimAIPortrait.Status.StateSaved", PortraitStateNames.Label(item.Key)));
            }
            report(Localization.T("RimAIPortrait.Status.SelectedEmotionsComplete"));
        }
        /// <summary>Describes the behavior of this type or member.</summary>
        private IEnumerator GenerateEmotionRoutine(Pawn pawn, PortraitState state, string[] references, Action<string> report)
        {
            byte[] image = null; string error = null; report(Localization.T("RimAIPortrait.Status.GeneratingState", PortraitStateNames.Label(state)));
            yield return Request(pawn, state, null, references, (data, err) => { image = data; error = err; });
            if (image == null) { Fail(Localization.T("RimAIPortrait.Status.StateFailed", PortraitStateNames.Label(state), error), report); yield break; }
            PortraitData.Save(pawn, state, image);
            if (state == PortraitState.Normal) InspectPanePatch.ShowStateImmediately(pawn, PortraitState.Normal);
            report(Localization.T("RimAIPortrait.Status.StatePortraitSaved", PortraitStateNames.Label(state)));
        }
        /// <summary>Describes the behavior of this type or member.</summary>
        private IEnumerator GenerateNormalRoutine(Pawn pawn, Action<string> report)
        {
            Log.Message("[Rim AI Expressive Portraits] Starting normal portrait for " + pawn.LabelShort);
            byte[] sprite = RimAIPortraitMod.Settings.IncludeSprite ? PawnCapture.Capture(pawn) : null; byte[] image = null; string error = null;
            if (RimAIPortraitMod.Settings.IncludeSprite && sprite == null)
                Log.Warning("[Rim AI Expressive Portraits] Pawn capture returned no image; continuing without a source image.");
            yield return Request(pawn, PortraitState.Normal, sprite, new string[0],
                (data, err) => { image = data; error = err; }, imageUse: PortraitImageUse.CharacterReference);
            if (image == null) { Fail(Localization.T("RimAIPortrait.Status.GenerationFailed", error), report); yield break; }
            PortraitData.Save(pawn, PortraitState.Normal, image);
            InspectPanePatch.ShowStateImmediately(pawn, PortraitState.Normal);
            report(Localization.T("RimAIPortrait.Status.BaseSaved"));
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
            yield return Request(pawn, PortraitState.Normal, reference, new string[0], (data, err) => { image = data; error = err; }, null, referenceInstruction, PortraitImageUse.Edit);
            if (image == null) { Fail(Localization.T("RimAIPortrait.Status.GenerationFailed", error), report); yield break; }
            PortraitData.Save(pawn, PortraitState.Normal, image);
            InspectPanePatch.ShowStateImmediately(pawn, PortraitState.Normal);
            report(Localization.T("RimAIPortrait.Status.ReferenceBaseSaved"));
            Messages.Message(Localization.T("RimAIPortrait.Status.PortraitSavedFor", pawn.LabelShort), MessageTypeDefOf.PositiveEvent, false);
        }
        /// <summary>Describes the behavior of this type or member.</summary>
        private IEnumerator GenerateEmotionsRoutine(Pawn pawn, string[] references, Action<string> report)
        {
            string error = null;
            Dictionary<PortraitState, InpaintSelection> selections = null;
            if (UsesInpaintSelection())
                yield return CollectInpaintSelections(pawn, Enum.GetValues(typeof(PortraitState)).Cast<PortraitState>(), null,
                    value => selections = value);
            Dictionary<PortraitState, ExtractedTags> batchTags = null;
            yield return PrepareBatchTags(pawn, Enum.GetValues(typeof(PortraitState)).Cast<PortraitState>(),
                selections, null, null, value => batchTags = value);
            foreach (PortraitState state in Enum.GetValues(typeof(PortraitState)))
            {
                InpaintSelection selection = null;
                if (selections != null && selections.TryGetValue(state, out selection) && selection.Error != null)
                {
                    report(Localization.T("RimAIPortrait.Status.StateFailed", PortraitStateNames.Label(state), selection.Error));
                    continue;
                }
                if (state == PortraitState.Normal || !RimAIPortraitMod.Settings.IsStateEnabled(state)) continue; byte[] image = null; report(Localization.T("RimAIPortrait.Status.GeneratingState", PortraitStateNames.Label(state)));
                yield return Request(pawn, state, null, references, (data, err) => { image = data; error = err; },
                    preparedInpaint: selection, batchTags: batchTags);
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
            yield return Request(pawn, PortraitState.Normal, sprite, new string[0], (data, err) => { normal = data; error = err; }, imageUse: PortraitImageUse.CharacterReference);
            if (normal == null) { report(Localization.T("RimAIPortrait.Status.GenerationFailed", error)); yield break; }
            PortraitData.Save(pawn, PortraitState.Normal, normal);
            InspectPanePatch.ShowStateImmediately(pawn, PortraitState.Normal);
            report(Localization.T("RimAIPortrait.Status.BaseSaved"));
            if (s.EnableEmotionChanges)
            {
                Dictionary<PortraitState, InpaintSelection> selections = null;
                if (UsesInpaintSelection())
                    yield return CollectInpaintSelections(pawn, Enum.GetValues(typeof(PortraitState)).Cast<PortraitState>(), normal,
                        value => selections = value);
                Dictionary<PortraitState, ExtractedTags> batchTags = null;
                yield return PrepareBatchTags(pawn, Enum.GetValues(typeof(PortraitState)).Cast<PortraitState>(),
                    selections, normal, null, value => batchTags = value);
                foreach (PortraitState state in Enum.GetValues(typeof(PortraitState)))
                {
                    if (state == PortraitState.Normal || !s.IsStateEnabled(state)) continue;
                    InpaintSelection selection = null;
                    if (selections != null && selections.TryGetValue(state, out selection) && selection.Error != null)
                    {
                        report(Localization.T("RimAIPortrait.Status.StateFailed", PortraitStateNames.Label(state), selection.Error));
                        continue;
                    }
                    byte[] image = null; report(Localization.T("RimAIPortrait.Status.GeneratingState", PortraitStateNames.Label(state)));
                    yield return Request(pawn, state, normal, new string[0], (data, err) => { image = data; error = err; },
                        imageUse: PortraitImageUse.Edit, preparedInpaint: selection, batchTags: batchTags);
                    if (image == null) { Log.Warning("[Rim AI Expressive Portraits] " + state + " failed: " + error); continue; }
                    PortraitData.Save(pawn, state, image);
                }
            }
            report(Localization.T("RimAIPortrait.Status.AllComplete"));
            Messages.Message(Localization.T("RimAIPortrait.Status.AllCompleteFor", pawn.LabelShort), MessageTypeDefOf.PositiveEvent, false);
        }
        /// <summary>Describes the behavior of this type or member.</summary>
        private IEnumerator CollectInpaintSelections(Pawn pawn, IEnumerable<PortraitState> states, byte[] source,
            Action<Dictionary<PortraitState, InpaintSelection>> done)
        {
            var selections = new Dictionary<PortraitState, InpaintSelection>();
            string sourceError = null;
            if (source == null)
            {
                try { source = File.ReadAllBytes(PortraitData.ExistingPathFor(pawn, PortraitState.Normal)); }
                catch (Exception) { sourceError = Localization.T("RimAIPortrait.Gallery.BaseRequired"); }
            }
            foreach (PortraitState state in states)
            {
                if (state == PortraitState.Normal || !RimAIPortraitMod.Settings.IsStateEnabled(state)) continue;
                if (sourceError != null)
                {
                    selections[state] = new InpaintSelection { Error = sourceError };
                    continue;
                }
                InpaintSelection selection = null;
                yield return SelectInpaint(pawn, state, source, value => selection = value);
                selections[state] = selection;
            }
            done(selections);
        }

        private static bool UsesInpaintSelection()
        {
            AiProvider provider = RimAIPortraitMod.Settings.ExpressionProvider;
            return provider == AiProvider.NovelAI || provider == AiProvider.LocalComfyUI;
        }

        private IEnumerator PrepareBatchTags(Pawn pawn, IEnumerable<PortraitState> states,
            Dictionary<PortraitState, InpaintSelection> selections, byte[] source,
            IDictionary<PortraitState, string> overrides, Action<Dictionary<PortraitState, ExtractedTags>> done)
        {
            PortraitSettings settings = RimAIPortraitMod.Settings;
            List<PortraitState> targets = states.Where(state => state != PortraitState.Normal && settings.IsStateEnabled(state)
                && (selections == null || !selections.ContainsKey(state) || selections[state].Error == null))
                .Distinct().ToList();
            if (targets.Count < 2 || !settings.EnableTagExtractor || !TagExtraction.IsConfigured(settings)
                || !UsesInpaintSelection())
            {
                done(null);
                yield break;
            }
            if (settings.ExpressionProvider == AiProvider.LocalComfyUI)
            {
                bool usesTags = false;
                try
                {
                    string workflow = File.ReadAllText(settings.ComfyExpressionWorkflowPath);
                    usesTags = workflow.Contains("__RIM_EXTRACTED_TAG__");
                }
                catch (Exception) { }
                if (!usesTags) { done(null); yield break; }
            }
            if (source == null && selections != null)
                source = selections.Values.Where(value => value != null && value.Error == null)
                    .Select(value => value.Source).FirstOrDefault(value => value != null);
            if (source == null)
            {
                try { source = File.ReadAllBytes(PortraitData.ExistingPathFor(pawn, PortraitState.Normal)); }
                catch (Exception) { }
            }
            var result = new Dictionary<PortraitState, ExtractedTags>();
            if (source != null && source.Length > 0 && source.Length <= 8 * 1024 * 1024)
            {
                var prompts = new Dictionary<PortraitState, string>();
                foreach (PortraitState state in targets)
                {
                    string expressionOverride;
                    if (overrides == null || !overrides.TryGetValue(state, out expressionOverride)) expressionOverride = null;
                    string prompt = PortraitPrompt.Build(pawn, state, settings, expressionOverride);
                    InpaintSelection selection;
                    if (settings.ExpressionProvider == AiProvider.NovelAI && selections != null
                        && selections.TryGetValue(state, out selection) && !string.IsNullOrWhiteSpace(selection.ExtraTag))
                        prompt += "\n\n" + selection.ExtraTag.Trim();
                    prompts[state] = prompt;
                }
                string tagError = null;
                yield return TagExtraction.ExtractBatch(settings, source, prompts,
                    (value, error) => { result = value; tagError = error; });
                if (tagError != null) ShowTagExtractionFailure(tagError);
            }
            else ShowTagExtractionFailure(Localization.T("RimAIPortrait.TagExtractor.ImageRequired"));
            done(result);
        }

        private IEnumerator SelectInpaint(Pawn pawn, PortraitState state, byte[] source, Action<InpaintSelection> done)
        {
            var settings = RimAIPortraitMod.Settings;
            AiProvider providerId = settings.ExpressionProvider;
            float defaultStrength = state == PortraitState.Sleep
                ? settings.NovelAiSleepInpaintStrength : settings.NovelAiInpaintStrength;
            float strength = PortraitData.NovelAiInpaintStrength(pawn, state, defaultStrength);
            byte[] mask = null;
            byte[] requestMask = null;
            string extraTag = null;
            bool narrowFeather = false;
            bool finished = false;
            Find.WindowStack.Add(new Dialog_NovelAiInpaint(source, state, providerId, strength,
                providerId == AiProvider.NovelAI ? PortraitData.NovelAiExtraTag(pawn)
                    : providerId == AiProvider.LocalComfyUI ? PortraitData.ComfyInpaintTag(pawn) : "",
                (value, expandedValue, selectedStrength, selectedAreas, selectedExtraTag) =>
            {
                mask = value;
                requestMask = expandedValue;
                strength = selectedStrength;
                narrowFeather = selectedAreas > 1;
                extraTag = selectedExtraTag;
                finished = true;
            }));
            while (!finished) yield return null;
            var selection = new InpaintSelection {
                Source = source, Mask = mask, RequestMask = requestMask, Strength = strength,
                NarrowFeather = narrowFeather, ExtraTag = extraTag
            };
            if (mask == null && extraTag == null) selection.Error = Localization.T("RimAIPortrait.Inpaint.Cancelled");
            else if (providerId == AiProvider.NovelAI && !PortraitData.SaveNovelAiExtraTag(pawn, extraTag))
                selection.Error = Localization.T("RimAIPortrait.Inpaint.ExtraTagSaveFailed");
            else if (providerId == AiProvider.LocalComfyUI && !PortraitData.SaveComfyInpaintTag(pawn, extraTag))
                selection.Error = Localization.T("RimAIPortrait.Inpaint.ExtraTagSaveFailed");
            else if (providerId == AiProvider.NovelAI && !PortraitData.SaveNovelAiInpaintStrength(pawn, state, strength))
                selection.Error = Localization.T("RimAIPortrait.Inpaint.StrengthSaveFailed");
            done(selection);
        }

        private IEnumerator Request(Pawn pawn, PortraitState state, byte[] reference, IEnumerable<string> extraReferences, Action<byte[], string> done, string expressionOverride = null, string referenceInstruction = null, PortraitImageUse imageUse = PortraitImageUse.Auto, InpaintSelection preparedInpaint = null, Dictionary<PortraitState, ExtractedTags> batchTags = null)
        {
            var s = RimAIPortraitMod.Settings;
            bool expression = state != PortraitState.Normal;
            AiProvider providerId = expression ? s.ExpressionProvider : s.Provider;
            IPortraitGenerationProvider provider = PortraitProviderRegistry.Get(providerId);
            string prompt = PortraitPrompt.Build(pawn, state, s, expressionOverride);
            if (!referenceInstruction.NullOrEmpty()) prompt += "\n\n[Reference Image]\n" + referenceInstruction;
            string styleFolder = expression ? s.ExpressionStyleFolder : s.StyleFolder;
            string[] references = (extraReferences ?? Enumerable.Empty<string>()).ToArray();
            if (preparedInpaint != null) reference = preparedInpaint.Source;
            if (expression)
            {
                string basePath = PortraitData.ExistingPathFor(pawn, PortraitState.Normal);
                if (reference == null)
                {
                    try
                    {
                        if (!File.Exists(basePath)) throw new FileNotFoundException();
                        reference = File.ReadAllBytes(basePath);
                    }
                    catch (Exception)
                    {
                        done(null, Localization.T("RimAIPortrait.Gallery.BaseRequired"));
                        yield break;
                    }
                }
                references = references.Where(path => !string.Equals(path, basePath, StringComparison.OrdinalIgnoreCase)).ToArray();
                imageUse = PortraitImageUse.Edit;
            }
            PortraitProviderConfiguration configuration = provider.CreateConfiguration(s, expression);
            var request = new PortraitGenerationRequest {
                State = state,
                Prompt = prompt,
                PrimaryImage = reference,
                ImageUse = imageUse,
                ReferencePaths = references,
                StyleFolder = styleFolder,
                Configuration = configuration
            };
            InpaintSelection selection = preparedInpaint;
            if (expression && UsesInpaintSelection())
            {
                if (selection == null)
                {
                    yield return SelectInpaint(pawn, state, reference, value => selection = value);
                }
                if (selection.Error != null)
                {
                    done(null, selection.Error);
                    yield break;
                }
                request.InpaintMask = selection.Mask;
                request.InpaintRequestMask = selection.RequestMask;
                request.InpaintTag = selection.ExtraTag;
                if (providerId == AiProvider.NovelAI && !string.IsNullOrWhiteSpace(selection.ExtraTag))
                    request.Prompt += "\n\n" + selection.ExtraTag.Trim();
            }
            request.StylePaths = provider.StyleReferences(request);
            int styleCapacity = provider.StyleReferenceCapacity(request);
            request.StylePaths = request.StylePaths.Take(styleCapacity).ToArray();
            bool supportsTags = providerId == AiProvider.NovelAI || providerId == AiProvider.LocalComfyUI;
            bool extractTags = s.EnableTagExtractor && TagExtraction.IsConfigured(s)
                && supportsTags;
            string tagStatus = !s.EnableTagExtractor ? "disabled"
                : !TagExtraction.IsConfigured(s) ? "missing configuration" : "not requested";
            if (extractTags && providerId == AiProvider.LocalComfyUI)
            {
                extractTags = false;
                try
                {
                    string workflow = File.ReadAllText(((ComfyUiProviderConfiguration)configuration).WorkflowPath);
                    extractTags = workflow.Contains("__RIM_EXTRACTED_TAG__");
                }
                catch (Exception) { }
                if (!extractTags) tagStatus = "workflow has no tag placeholder";
            }
            if (batchTags != null)
            {
                ExtractedTags preparedTags;
                if (batchTags.TryGetValue(state, out preparedTags)) request.ExtractedTags = preparedTags;
                tagStatus = request.ExtractedTags == null ? "batch result unavailable" : "batch result applied";
            }
            else if (extractTags)
            {
                byte[] tagImage = request.PrimaryImage;
                if (state == PortraitState.Normal && imageUse != PortraitImageUse.Edit
                    && (!s.IncludeSprite || tagImage == null))
                {
                    try { tagImage = PawnCapture.Capture(pawn); }
                    catch (Exception) { tagImage = null; }
                }
                if (tagImage == null && references.Length > 0)
                {
                    try { tagImage = File.ReadAllBytes(references[0]); }
                    catch (Exception) { tagImage = null; }
                }
                if (tagImage != null && tagImage.Length > 0 && tagImage.Length <= 8 * 1024 * 1024)
                {
                    ExtractedTags tags = null;
                    string tagError = null;
                    yield return TagExtraction.Extract(s, tagImage, request.Prompt,
                        state != PortraitState.Normal, (value, error) => { tags = value; tagError = error; });
                    request.ExtractedTags = tags;
                    tagStatus = tags == null ? "failed: " + (tagError ?? "invalid response") : "applied";
                    if (tags == null)
                        ShowTagExtractionFailure(tagError ?? Localization.T("RimAIPortrait.TagExtractor.InvalidResponse"));
                }
                else
                {
                    tagStatus = "source image unavailable or too large";
                    ShowTagExtractionFailure(Localization.T("RimAIPortrait.TagExtractor.ImageRequired"));
                }
            }
            if (s.DebugMode && supportsTags)
                Log.Message("[Rim AI Expressive Portraits][Debug] TagExtractor " + tagStatus
                    + ", kind=" + (expression ? "Expression" : "Base")
                    + ", positive=" + (request.ExtractedTags?.positive?.Length ?? 0)
                    + ", negative=" + (request.ExtractedTags?.negative?.Length ?? 0));
            if (expression && providerId == AiProvider.NovelAI)
            {
                if (s.DebugMode) LogDebugRequest(state, expression, provider, request);
                string debugDirectory = s.DebugMode ? Path.Combine(PortraitData.PawnFolder(pawn), "nai_debug") : null;
                yield return ((NovelAiPortraitProvider)provider).GenerateInpaint(request, request.InpaintMask,
                    selection.Strength, selection.NarrowFeather, debugDirectory, result =>
                {
                    done(result.ImageData, result.Error);
                });
                yield break;
            }
            if (s.DebugMode) LogDebugRequest(state, expression, provider, request);
            yield return provider.Generate(request, result =>
            {
                done(result.ImageData, result.Error);
            });
        }
        internal void RemoveBackgroundFromGallery(string[] paths, BackgroundRemovalStep[] steps, bool alignBottom,
            Action<int, int> completed)
        {
            StartCoroutine(RemoveBackgroundFromGalleryRoutine(paths, steps, alignBottom, completed));
        }

        private IEnumerator RemoveBackgroundFromGalleryRoutine(string[] paths, BackgroundRemovalStep[] steps,
            bool alignBottom, Action<int, int> completed)
        {
            int succeeded = 0;
            foreach (string path in paths)
            {
                try
                {
                    byte[] image = BackgroundRemoval.ApplySteps(File.ReadAllBytes(path), steps, path);
                    if (alignBottom) image = BackgroundRemoval.AlignBottom(image);
                    File.WriteAllBytes(path, image);
                    succeeded++;
                }
                catch (Exception exception)
                {
                    Log.Warning("[Rim AI Expressive Portraits] Background removal failed for " + Path.GetFileName(path) + ": " + exception.Message);
                }
                yield return null;
            }
            completed?.Invoke(succeeded, paths.Length - succeeded);
        }
        /// <summary>Describes the behavior of this type or member.</summary>
        private static void ShowTagExtractionFailure(string explanation)
        {
            Messages.Message(Localization.T("RimAIPortrait.TagExtractor.Failed", explanation),
                MessageTypeDefOf.RejectInput, false);
        }

        private static void LogDebugRequest(PortraitState state, bool expression, IPortraitGenerationProvider provider, PortraitGenerationRequest request)
        {
            int styleCount = request.StylePaths.Count;
            Log.Message("[Rim AI Expressive Portraits][Debug] Request summary\n"
                + "kind=" + (expression ? "Expression" : "Base") + ", state=" + PortraitStateNames.Display(state) + ", provider=" + provider.Id + "\n"
                + "primary_image=" + (request.PrimaryImage != null) + ", image_use=" + request.ImageUse
                + ", selected_references=" + request.ReferencePaths.Count + ", style_references=" + styleCount + "\n"
                + "pawn_prompt_characters=" + (request.Prompt?.Length ?? 0)
                + ", extracted_positive=" + (request.ExtractedTags?.positive?.Length ?? 0)
                + ", extracted_negative=" + (request.ExtractedTags?.negative?.Length ?? 0));
        }

    }

}
