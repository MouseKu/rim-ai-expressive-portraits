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
        public static PortraitRuntime Instance { get; private set; }
        private static float nextGenerationButtonTime;
        internal static bool GenerationButtonReady => Time.realtimeSinceStartup >= nextGenerationButtonTime;

        internal static void StartGenerationButtonCooldown()
        {
            nextGenerationButtonTime = Time.realtimeSinceStartup + 1f;
        }

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
        public void GenerateNormalFromReference(Pawn pawn, string referencePath, string prompt, Action<string> report)
        {
            if (pawn == null || RimAIPortraitMod.Settings == null)
            {
                report(Localization.T(pawn == null ? "RimAIPortrait.Error.NoPawn" : "RimAIPortrait.Error.SettingsUnavailable"));
                return;
            }
            PortraitSettings settings = RimAIPortraitMod.Settings;
            IPortraitGenerationProvider provider = PortraitProviderRegistry.Get(settings.RegenerateProvider);
            string error = provider.Validate(settings.RegenerateConfiguration(), Localization.T("RimAIPortrait.Kind.Regenerate"));
            if (!string.IsNullOrEmpty(error)) { report(error); return; }
            StartCoroutine(GenerateNormalFromReferenceRoutine(pawn, referencePath, prompt, report));
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
                if (item.Key != PortraitState.Normal && !batchPrepared)
                {
                    batchPrepared = true;
                    yield return PrepareBatchTags(pawn, prompts.Keys, null, prompts,
                        value => batchTags = value);
                }
                if (item.Key != PortraitState.Normal && selections == null && UsesInpaintSelection())
                    yield return CollectInpaintSelections(pawn, prompts.Keys, null, batchTags,
                        value => selections = value);
                InpaintSelection selection = null;
                if (selections != null && selections.TryGetValue(item.Key, out selection) && selection.Error != null)
                {
                    report(Localization.T("RimAIPortrait.Status.StateFailed", PortraitStateNames.Label(item.Key), selection.Error));
                    continue;
                }
                byte[] image = null; string error = null; report(Localization.T("RimAIPortrait.Status.GeneratingState", PortraitStateNames.Label(item.Key)));
                yield return Request(new PortraitRequestOptions {
                    Pawn = pawn, State = item.Key, ReferencePaths = references,
                    ExpressionOverride = item.Value, PreparedInpaint = selection, BatchTags = batchTags
                }, (data, err) => { image = data; error = err; });
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
            yield return Request(new PortraitRequestOptions {
                Pawn = pawn, State = state, ReferencePaths = references
            }, (data, err) => { image = data; error = err; });
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
            yield return Request(new PortraitRequestOptions {
                Pawn = pawn, State = PortraitState.Normal, PrimaryImage = sprite,
                ImageUse = PortraitImageUse.CharacterReference
            }, (data, err) => { image = data; error = err; });
            if (image == null) { Fail(Localization.T("RimAIPortrait.Status.GenerationFailed", error), report); yield break; }
            PortraitData.Save(pawn, PortraitState.Normal, image);
            InspectPanePatch.ShowStateImmediately(pawn, PortraitState.Normal);
            report(Localization.T("RimAIPortrait.Status.BaseSaved"));
            Messages.Message(Localization.T("RimAIPortrait.Status.PortraitSavedFor", pawn.LabelShort), MessageTypeDefOf.PositiveEvent, false);
        }
        /// <summary>Describes the behavior of this type or member.</summary>
        private IEnumerator GenerateNormalFromReferenceRoutine(Pawn pawn, string referencePath, string prompt, Action<string> report)
        {
            byte[] reference;
            try { reference = File.ReadAllBytes(referencePath); }
            catch (Exception e) { Fail(Localization.T("RimAIPortrait.Status.ReferenceReadFailed", e.Message), report); yield break; }
            byte[] image = null; string error = null;
            Log.Message("[Rim AI Expressive Portraits] Starting referenced normal portrait for " + pawn.LabelShort);
            yield return Request(new PortraitRequestOptions {
                Pawn = pawn, State = PortraitState.Normal, PrimaryImage = reference,
                ImageUse = PortraitImageUse.Edit, Regenerate = true, RegeneratePrompt = prompt
            }, (data, err) => { image = data; error = err; });
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
            Dictionary<PortraitState, ExtractedTags> batchTags = null;
            yield return PrepareBatchTags(pawn, Enum.GetValues(typeof(PortraitState)).Cast<PortraitState>(),
                null, null, value => batchTags = value);
            if (UsesInpaintSelection())
                yield return CollectInpaintSelections(pawn, Enum.GetValues(typeof(PortraitState)).Cast<PortraitState>(),
                    null, batchTags, value => selections = value);
            foreach (PortraitState state in Enum.GetValues(typeof(PortraitState)))
            {
                InpaintSelection selection = null;
                if (selections != null && selections.TryGetValue(state, out selection) && selection.Error != null)
                {
                    report(Localization.T("RimAIPortrait.Status.StateFailed", PortraitStateNames.Label(state), selection.Error));
                    continue;
                }
                if (state == PortraitState.Normal || !RimAIPortraitMod.Settings.IsStateEnabled(state)) continue; byte[] image = null; report(Localization.T("RimAIPortrait.Status.GeneratingState", PortraitStateNames.Label(state)));
                yield return Request(new PortraitRequestOptions {
                    Pawn = pawn, State = state, ReferencePaths = references,
                    PreparedInpaint = selection, BatchTags = batchTags
                }, (data, err) => { image = data; error = err; });
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
            yield return Request(new PortraitRequestOptions {
                Pawn = pawn, State = PortraitState.Normal, PrimaryImage = sprite,
                ImageUse = PortraitImageUse.CharacterReference
            }, (data, err) => { normal = data; error = err; });
            if (normal == null) { report(Localization.T("RimAIPortrait.Status.GenerationFailed", error)); yield break; }
            PortraitData.Save(pawn, PortraitState.Normal, normal);
            InspectPanePatch.ShowStateImmediately(pawn, PortraitState.Normal);
            report(Localization.T("RimAIPortrait.Status.BaseSaved"));
            if (s.EnableEmotionChanges)
            {
                Dictionary<PortraitState, InpaintSelection> selections = null;
                Dictionary<PortraitState, ExtractedTags> batchTags = null;
                yield return PrepareBatchTags(pawn, Enum.GetValues(typeof(PortraitState)).Cast<PortraitState>(),
                    normal, null, value => batchTags = value);
                if (UsesInpaintSelection())
                    yield return CollectInpaintSelections(pawn, Enum.GetValues(typeof(PortraitState)).Cast<PortraitState>(),
                        normal, batchTags, value => selections = value);
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
                    yield return Request(new PortraitRequestOptions {
                        Pawn = pawn, State = state, PrimaryImage = normal,
                        ImageUse = PortraitImageUse.Edit, PreparedInpaint = selection, BatchTags = batchTags
                    }, (data, err) => { image = data; error = err; });
                    if (image == null) { Log.Warning("[Rim AI Expressive Portraits] " + state + " failed: " + error); continue; }
                    PortraitData.Save(pawn, state, image);
                }
            }
            report(Localization.T("RimAIPortrait.Status.AllComplete"));
            Messages.Message(Localization.T("RimAIPortrait.Status.AllCompleteFor", pawn.LabelShort), MessageTypeDefOf.PositiveEvent, false);
        }
        /// <summary>Describes the behavior of this type or member.</summary>
        private IEnumerator CollectInpaintSelections(Pawn pawn, IEnumerable<PortraitState> states, byte[] source,
            Dictionary<PortraitState, ExtractedTags> extractedTags,
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
                ExtractedTags tags;
                if (extractedTags == null || !extractedTags.TryGetValue(state, out tags)) tags = null;
                yield return SelectInpaint(pawn, state, source, value => selection = value, null, tags);
                selections[state] = selection;
            }
            done(selections);
        }

        private static bool UsesInpaintSelection()
        {
            AiProvider provider = RimAIPortraitMod.Settings.ExpressionProvider;
            return provider == AiProvider.NovelAI || provider == AiProvider.LocalComfyUI;
        }

        private IEnumerator PrepareBatchTags(Pawn pawn, IEnumerable<PortraitState> states, byte[] source,
            IDictionary<PortraitState, string> overrides, Action<Dictionary<PortraitState, ExtractedTags>> done)
        {
            PortraitSettings settings = RimAIPortraitMod.Settings;
            List<PortraitState> targets = states.Where(state => state != PortraitState.Normal && settings.IsStateEnabled(state))
                .Distinct().ToList();
            if (targets.Count < 2 || !settings.EnableTagExtractor || !UsesInpaintSelection())
            {
                done(null);
                yield break;
            }
            var result = new Dictionary<PortraitState, ExtractedTags>();
            if (!TagExtraction.IsConfigured(settings))
            {
                PortraitTagPreparation.ShowFailure(Localization.T("RimAIPortrait.TagExtractor.ConfigurationMissing"));
                done(result);
                yield break;
            }
            if (source == null)
            {
                try { source = File.ReadAllBytes(PortraitData.ExistingPathFor(pawn, PortraitState.Normal)); }
                catch (Exception) { }
            }
            if (source != null && source.Length > 0 && source.Length <= 8 * 1024 * 1024)
            {
                var prompts = new Dictionary<PortraitState, string>();
                foreach (PortraitState state in targets)
                {
                    string expressionOverride;
                    if (overrides == null || !overrides.TryGetValue(state, out expressionOverride)) expressionOverride = null;
                    string prompt = PortraitPrompt.Build(pawn, state, settings, expressionOverride);
                    prompts[state] = prompt;
                }
                string tagError = null;
                yield return TagExtraction.ExtractBatch(settings, source, prompts,
                    (value, error) => { result = value; tagError = error; });
                if (tagError != null) PortraitTagPreparation.ShowFailure(tagError);
            }
            else PortraitTagPreparation.ShowFailure(Localization.T("RimAIPortrait.TagExtractor.ImageRequired"));
            done(result);
        }

        private IEnumerator ExtractInpaintTags(PortraitSettings settings, byte[] source, string prompt,
            Action<ExtractedTags> done)
        {
            if (!TagExtraction.IsConfigured(settings))
            {
                PortraitTagPreparation.ShowFailure(Localization.T("RimAIPortrait.TagExtractor.ConfigurationMissing"));
                done(null);
                yield break;
            }
            if (source == null || source.Length == 0 || source.Length > 8 * 1024 * 1024)
            {
                PortraitTagPreparation.ShowFailure(Localization.T("RimAIPortrait.TagExtractor.ImageRequired"));
                done(null);
                yield break;
            }
            ExtractedTags tags = null;
            string error = null;
            yield return TagExtraction.Extract(settings, source, prompt, true,
                (value, failure) => { tags = value; error = failure; });
            if (tags == null)
                PortraitTagPreparation.ShowFailure(error ?? Localization.T("RimAIPortrait.TagExtractor.InvalidResponse"));
            done(tags);
        }

        private IEnumerator SelectInpaint(Pawn pawn, PortraitState state, byte[] source, Action<InpaintSelection> done,
            AiProvider? selectedProvider = null, ExtractedTags extractedTags = null)
        {
            var settings = RimAIPortraitMod.Settings;
            AiProvider providerId = selectedProvider ?? settings.ExpressionProvider;
            float defaultStrength = state == PortraitState.Sleep
                ? settings.NovelAiSleepInpaintStrength : settings.NovelAiInpaintStrength;
            float strength = PortraitData.NovelAiInpaintStrength(pawn, state, defaultStrength);
            byte[] mask = null;
            byte[] requestMask = null;
            string extraTag = null;
            bool narrowFeather = false;
            bool finished = false;
            string initialExtraTag = providerId == AiProvider.NovelAI ? PortraitData.NovelAiExtraTag(pawn)
                : providerId == AiProvider.LocalComfyUI ? PortraitData.ComfyInpaintTag(pawn) : "";
            if (extractedTags != null) initialExtraTag = extractedTags.PositiveText;
            Find.WindowStack.Add(new Dialog_NovelAiInpaint(source, state, providerId, strength,
                initialExtraTag,
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

        private IEnumerator Request(PortraitRequestOptions options, Action<byte[], string> done)
        {
            var s = RimAIPortraitMod.Settings;
            PreparedPortraitRequest prepared = PortraitRequestPreparation.Create(s, options);
            if (prepared.Error != null)
            {
                done(null, prepared.Error);
                yield break;
            }
            bool expression = prepared.Expression;
            AiProvider providerId = prepared.ProviderId;
            IPortraitGenerationProvider provider = prepared.Provider;
            PortraitGenerationRequest request = prepared.Request;
            InpaintSelection selection = options.PreparedInpaint;
            if ((expression && UsesInpaintSelection()) || (options.Regenerate
                && (providerId == AiProvider.NovelAI || providerId == AiProvider.LocalComfyUI)))
            {
                if (selection == null)
                {
                    ExtractedTags prefillTags = null;
                    if (expression && options.BatchTags == null && s.EnableTagExtractor && UsesInpaintSelection())
                        yield return ExtractInpaintTags(s, request.PrimaryImage, request.Prompt,
                            value => prefillTags = value);
                    yield return SelectInpaint(options.Pawn, options.State, request.PrimaryImage,
                        value => selection = value, providerId, prefillTags);
                    if (prefillTags != null)
                        request.ExtractedTags = new ExtractedTags {
                            positive = new string[0], negative = prefillTags.negative
                        };
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
            PortraitRequestPreparation.AddStyleReferences(prepared);
            if (selection != null && options.BatchTags != null)
            {
                ExtractedTags tags;
                if (options.BatchTags.TryGetValue(options.State, out tags) && tags != null)
                    request.ExtractedTags = new ExtractedTags { positive = new string[0], negative = tags.negative };
            }
            else if (selection == null)
                yield return PortraitTagPreparation.Prepare(s, options, prepared);
            if (selection != null && providerId == AiProvider.LocalComfyUI && request.ExtractedTags != null)
                request.ExtractedTags.positive = string.IsNullOrWhiteSpace(selection.ExtraTag)
                    ? new string[0] : new[] { selection.ExtraTag.Trim() };
            if ((expression || options.Regenerate) && providerId == AiProvider.NovelAI)
            {
                if (s.DebugMode) LogDebugRequest(options.State, expression, provider, request);
                string debugDirectory = s.DebugMode ? Path.Combine(PortraitData.PawnFolder(options.Pawn), "nai_debug") : null;
                yield return ((NovelAiPortraitProvider)provider).GenerateInpaint(request, request.InpaintMask,
                    selection.Strength, selection.NarrowFeather, debugDirectory, result =>
                {
                    done(result.ImageData, result.Error);
                });
                yield break;
            }
            if (s.DebugMode) LogDebugRequest(options.State, expression, provider, request);
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
