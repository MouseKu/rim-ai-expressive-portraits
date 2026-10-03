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
        internal void GenerateBaseTags(Pawn pawn, string imagePath, Action<ExtractedTags, string> done)
        {
            StartCoroutine(GenerateBaseTagsRoutine(pawn, imagePath, done));
        }

        private IEnumerator GenerateBaseTagsRoutine(Pawn pawn, string imagePath, Action<ExtractedTags, string> done)
        {
            PortraitSettings settings = RimAIPortraitMod.Settings;
            if (!TagExtraction.IsConfigured(settings))
            {
                done(null, Localization.T("RimAIPortrait.TagExtractor.ConfigurationMissing"));
                yield break;
            }
            byte[] source = null;
            if (!string.IsNullOrEmpty(imagePath))
            {
                try { source = File.ReadAllBytes(imagePath); }
                catch (Exception) { }
            }
            else
            {
                try { source = PawnCapture.Capture(pawn); }
                catch (Exception) { }
            }
            if (source == null || source.Length == 0 || source.Length > 8 * 1024 * 1024)
            {
                done(null, Localization.T("RimAIPortrait.TagExtractor.ImageRequired"));
                yield break;
            }
            string prompt = PortraitPrompt.Build(pawn, PortraitState.Base, settings);
            yield return TagExtraction.Extract(settings, source, prompt, done,
                PortraitPrompt.TagExtractorFeatures(pawn, settings));
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
            bool normal = state == PortraitState.Base;
            if (!CanGenerate(pawn, report, normal, !normal)) return;
            StartCoroutine(GenerateEmotionRoutine(pawn, state, references == null ? new string[0] : references.ToArray(), report));
        }
        /// <summary>Describes the behavior of this type or member.</summary>
        public void GenerateSelectedEmotions(Pawn pawn, IEnumerable<string> references, IDictionary<PortraitState, string> prompts, Action<string> report)
        {
            var selected = new Dictionary<PortraitState, string>(prompts);
            bool hasNormal = selected.ContainsKey(PortraitState.Base);
            if (!CanGenerate(pawn, report, hasNormal, selected.Keys.Any(x => x != PortraitState.Base))) return;
            StartCoroutine(GenerateSelectedRoutine(pawn, references.ToArray(), selected, report));
        }
        /// <summary>Describes the behavior of this type or member.</summary>
        private IEnumerator GenerateSelectedRoutine(Pawn pawn, string[] references, Dictionary<PortraitState, string> prompts, Action<string> report)
        {
            Dictionary<PortraitState, InpaintSelection> selections = null;
            foreach (var item in prompts.OrderBy(item => item.Key == PortraitState.Base ? 0 : 1))
            {
                if (!RimAIPortraitMod.Settings.IsStateEnabled(item.Key)) continue;
                if (item.Key != PortraitState.Base && selections == null && UsesInpaintSelection())
                    yield return CollectInpaintSelections(pawn, prompts.Keys, null, prompts,
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
                    ExpressionOverride = item.Value, PreparedInpaint = selection
                }, (data, err) => { image = data; error = err; });
                if (image == null) { Fail(Localization.T("RimAIPortrait.Status.StateFailed", PortraitStateNames.Label(item.Key), error), report); continue; }
                PortraitData.Save(pawn, item.Key, image);
                if (item.Key == PortraitState.Base) InspectPanePatch.ShowStateImmediately(pawn, PortraitState.Base);
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
            if (state == PortraitState.Base) InspectPanePatch.ShowStateImmediately(pawn, PortraitState.Base);
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
                Pawn = pawn, State = PortraitState.Base, PrimaryImage = sprite,
                ImageUse = PortraitImageUse.CharacterReference
            }, (data, err) => { image = data; error = err; });
            if (image == null) { Fail(Localization.T("RimAIPortrait.Status.GenerationFailed", error), report); yield break; }
            PortraitData.Save(pawn, PortraitState.Base, image);
            InspectPanePatch.ShowStateImmediately(pawn, PortraitState.Base);
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
                Pawn = pawn, State = PortraitState.Base, PrimaryImage = reference,
                ImageUse = PortraitImageUse.Edit, Regenerate = true, RegeneratePrompt = prompt
            }, (data, err) => { image = data; error = err; });
            if (image == null) { Fail(Localization.T("RimAIPortrait.Status.GenerationFailed", error), report); yield break; }
            PortraitData.Save(pawn, PortraitState.Base, image);
            InspectPanePatch.ShowStateImmediately(pawn, PortraitState.Base);
            report(Localization.T("RimAIPortrait.Status.ReferenceBaseSaved"));
            Messages.Message(Localization.T("RimAIPortrait.Status.PortraitSavedFor", pawn.LabelShort), MessageTypeDefOf.PositiveEvent, false);
        }
        /// <summary>Describes the behavior of this type or member.</summary>
        private IEnumerator GenerateEmotionsRoutine(Pawn pawn, string[] references, Action<string> report)
        {
            string error = null;
            Dictionary<PortraitState, InpaintSelection> selections = null;
            if (UsesInpaintSelection())
                yield return CollectInpaintSelections(pawn, Enum.GetValues(typeof(PortraitState)).Cast<PortraitState>(),
                    null, null, value => selections = value);
            foreach (PortraitState state in Enum.GetValues(typeof(PortraitState)))
            {
                InpaintSelection selection = null;
                if (selections != null && selections.TryGetValue(state, out selection) && selection.Error != null)
                {
                    report(Localization.T("RimAIPortrait.Status.StateFailed", PortraitStateNames.Label(state), selection.Error));
                    continue;
                }
                if (state == PortraitState.Base || !RimAIPortraitMod.Settings.IsStateEnabled(state)) continue; byte[] image = null; report(Localization.T("RimAIPortrait.Status.GeneratingState", PortraitStateNames.Label(state)));
                yield return Request(new PortraitRequestOptions {
                    Pawn = pawn, State = state, ReferencePaths = references,
                    PreparedInpaint = selection
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
                Pawn = pawn, State = PortraitState.Base, PrimaryImage = sprite,
                ImageUse = PortraitImageUse.CharacterReference
            }, (data, err) => { normal = data; error = err; });
            if (normal == null) { report(Localization.T("RimAIPortrait.Status.GenerationFailed", error)); yield break; }
            PortraitData.Save(pawn, PortraitState.Base, normal);
            InspectPanePatch.ShowStateImmediately(pawn, PortraitState.Base);
            report(Localization.T("RimAIPortrait.Status.BaseSaved"));
            if (s.EnableEmotionChanges)
            {
                Dictionary<PortraitState, InpaintSelection> selections = null;
                if (UsesInpaintSelection())
                    yield return CollectInpaintSelections(pawn, Enum.GetValues(typeof(PortraitState)).Cast<PortraitState>(),
                        normal, null, value => selections = value);
                foreach (PortraitState state in Enum.GetValues(typeof(PortraitState)))
                {
                    if (state == PortraitState.Base || !s.IsStateEnabled(state)) continue;
                    InpaintSelection selection = null;
                    if (selections != null && selections.TryGetValue(state, out selection) && selection.Error != null)
                    {
                        report(Localization.T("RimAIPortrait.Status.StateFailed", PortraitStateNames.Label(state), selection.Error));
                        continue;
                    }
                    byte[] image = null; report(Localization.T("RimAIPortrait.Status.GeneratingState", PortraitStateNames.Label(state)));
                    yield return Request(new PortraitRequestOptions {
                        Pawn = pawn, State = state, PrimaryImage = normal,
                        ImageUse = PortraitImageUse.Edit, PreparedInpaint = selection
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
            IDictionary<PortraitState, string> overrides,
            Action<Dictionary<PortraitState, InpaintSelection>> done)
        {
            var selections = new Dictionary<PortraitState, InpaintSelection>();
            string sourceError = null;
            if (source == null)
            {
                try { source = File.ReadAllBytes(PortraitData.ExistingPathFor(pawn, PortraitState.Base)); }
                catch (Exception) { sourceError = Localization.T("RimAIPortrait.Gallery.BaseRequired"); }
            }
            foreach (PortraitState state in states)
            {
                if (state == PortraitState.Base || !RimAIPortraitMod.Settings.IsStateEnabled(state)) continue;
                if (sourceError != null)
                {
                    selections[state] = new InpaintSelection { Error = sourceError };
                    continue;
                }
                InpaintSelection selection = null;
                string expressionOverride;
                if (overrides == null || !overrides.TryGetValue(state, out expressionOverride)) expressionOverride = null;
                string prompt = PortraitPrompt.Build(pawn, state, RimAIPortraitMod.Settings, expressionOverride);
                yield return SelectInpaint(pawn, state, source, value => selection = value, null, prompt);
                selections[state] = selection;
            }
            done(selections);
        }

        private static bool UsesInpaintSelection()
        {
            AiProvider provider = RimAIPortraitMod.Settings.ExpressionProvider;
            return provider == AiProvider.NovelAI || provider == AiProvider.LocalComfyUI;
        }

        private IEnumerator ExtractInpaintTags(PortraitSettings settings, byte[] source, PortraitState state, string prompt,
            Action<ExtractedTags, string> done)
        {
            if (!TagExtraction.IsConfigured(settings))
            {
                done(null, Localization.T("RimAIPortrait.TagExtractor.ConfigurationMissing"));
                yield break;
            }
            if (source == null || source.Length == 0 || source.Length > 8 * 1024 * 1024)
            {
                done(null, Localization.T("RimAIPortrait.TagExtractor.ImageRequired"));
                yield break;
            }
            ExtractedTags tags = null;
            string error = null;
            yield return TagExtraction.ExtractInpaint(settings, source, state, prompt,
                (value, failure) => { tags = value; error = failure; });
            done(tags, error);
        }

        private IEnumerator ExtractSelectionTags(Pawn pawn, PortraitSettings settings, byte[] source,
            PortraitState state, string prompt, Action<ExtractedTags, string> done)
        {
            ExtractedTags tags = null;
            string error = null;
            if (state == PortraitState.Base)
            {
                if (!TagExtraction.IsConfigured(settings))
                {
                    done(null, Localization.T("RimAIPortrait.TagExtractor.ConfigurationMissing"));
                    yield break;
                }
                if (source == null || source.Length == 0 || source.Length > 8 * 1024 * 1024)
                {
                    done(null, Localization.T("RimAIPortrait.TagExtractor.ImageRequired"));
                    yield break;
                }
                yield return TagExtraction.Extract(settings, source, prompt,
                    (value, failure) => { tags = value; error = failure; },
                    PortraitPrompt.TagExtractorFeatures(pawn, settings));
            }
            else
                yield return ExtractInpaintTags(settings, source, state, prompt,
                    (value, failure) => { tags = value; error = failure; });
            if (tags != null && !(state == PortraitState.Base
                ? PortraitData.SaveBaseTags(pawn, tags) : PortraitData.SaveExpressionTags(pawn, state, tags)))
            {
                done(null, Localization.T("RimAIPortrait.Gallery.TagSaveFailed"));
                yield break;
            }
            done(tags, error);
        }

        private IEnumerator SelectInpaint(Pawn pawn, PortraitState state, byte[] source, Action<InpaintSelection> done,
            AiProvider? selectedProvider = null, string prompt = null)
        {
            var settings = RimAIPortraitMod.Settings;
            AiProvider providerId = selectedProvider ?? settings.ExpressionProvider;
            float defaultStrength = settings.GetInpaintStrength(state);
            float strength = providerId == AiProvider.NovelAI
                ? PortraitData.NovelAiInpaintStrength(pawn, state, defaultStrength) : defaultStrength;
            byte[] mask = null;
            byte[] requestMask = null;
            string extraTag = null;
            ExtractedTags tags = state == PortraitState.Base
                ? PortraitData.BaseTags(pawn) : PortraitData.ExpressionTags(pawn, state);
            bool narrowFeather = false;
            bool finished = false;
            string initialExtraTag = providerId == AiProvider.NovelAI ? PortraitData.NovelAiExtraTag(pawn)
                : providerId == AiProvider.LocalComfyUI ? PortraitData.ComfyInpaintTag(pawn) : "";
            Find.WindowStack.Add(new Dialog_NovelAiInpaint(source, state, providerId, strength,
                initialExtraTag, tags,
                settings.EnableTagExtractor ? (Action<Action<ExtractedTags, string>>)(callback =>
                    StartCoroutine(ExtractSelectionTags(pawn, settings, source, state,
                        prompt ?? PortraitPrompt.Build(pawn, state, settings), callback))) : null,
                (value, expandedValue, selectedStrength, selectedAreas, selectedExtraTag, selectedTags) =>
            {
                mask = value;
                requestMask = expandedValue;
                strength = selectedStrength;
                narrowFeather = selectedAreas > 1;
                extraTag = selectedExtraTag;
                tags = selectedTags;
                finished = true;
            }));
            while (!finished) yield return null;
            var selection = new InpaintSelection {
                Source = source, Mask = mask, RequestMask = requestMask, Strength = strength,
                NarrowFeather = narrowFeather, ExtraTag = extraTag, Tags = tags
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
            bool supportsTags = providerId == AiProvider.NovelAI || providerId == AiProvider.LocalComfyUI;
            IPortraitGenerationProvider provider = prepared.Provider;
            PortraitGenerationRequest request = prepared.Request;
            InpaintSelection selection = options.PreparedInpaint;
            if ((expression || options.Regenerate) && supportsTags)
            {
                if (selection == null)
                {
                    yield return SelectInpaint(options.Pawn, options.State, request.PrimaryImage,
                        value => selection = value, providerId, request.Prompt);
                }
                if (selection.Error != null)
                {
                    done(null, selection.Error);
                    yield break;
                }
                request.InpaintMask = selection.Mask;
                request.InpaintRequestMask = selection.RequestMask;
                request.InpaintTag = selection.ExtraTag;
                request.InpaintStrength = selection.Strength;
                request.ExtractedTags = selection.Tags;
                if (providerId == AiProvider.NovelAI && !string.IsNullOrWhiteSpace(selection.ExtraTag))
                    request.Prompt += "\n\n" + selection.ExtraTag.Trim();
            }
            PortraitRequestPreparation.AddStyleReferences(prepared);
            if (selection == null && supportsTags)
                PortraitTagPreparation.ApplySavedBaseTags(options.Pawn, prepared);
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
            bool flipHorizontal,
            Action<int, int> completed)
        {
            StartCoroutine(RemoveBackgroundFromGalleryRoutine(paths, steps, alignBottom, flipHorizontal, completed));
        }

        private IEnumerator RemoveBackgroundFromGalleryRoutine(string[] paths, BackgroundRemovalStep[] steps,
            bool alignBottom, bool flipHorizontal, Action<int, int> completed)
        {
            int succeeded = 0;
            foreach (string path in paths)
            {
                try
                {
                    byte[] image = BackgroundRemoval.ApplySteps(File.ReadAllBytes(path), steps, path);
                    if (alignBottom) image = BackgroundRemoval.AlignBottom(image);
                    if (flipHorizontal) image = BackgroundRemoval.FlipHorizontal(image);
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
