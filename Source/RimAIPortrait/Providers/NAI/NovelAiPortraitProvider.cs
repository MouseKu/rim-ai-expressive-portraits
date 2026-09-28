using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEngine.Networking;
using Verse;

namespace RimAIPortrait
{
    internal sealed class NovelAiPortraitProvider : IPortraitGenerationProvider
    {
        private const int MaximumReferenceImages = 16;
        private const string InfillModel = "nai-diffusion-4-5-full-inpainting";

        public AiProvider Id => AiProvider.NovelAI;

        public IReadOnlyList<string> StyleReferences(PortraitGenerationRequest request)
        {
            string[] configured = PortraitProviderUtilities.StyleFiles(request.StyleFolder)
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase).ToArray();
            NovelAiProviderConfiguration configuration = Require(request.Configuration);
            bool preciseBase = request.State == PortraitState.Normal
                && request.ImageUse == PortraitImageUse.CharacterReference && SupportsPreciseReference(configuration.Model);
            return preciseBase && configured.Length == 0
                && (PortraitProviderUtilities.UsesDefaultStyle(request.StyleFolder)
                    || Directory.Exists(request.StyleFolder))
                ? PortraitProviderUtilities.BundledStyleFiles().ToArray() : configured;
        }

        public int StyleReferenceCapacity(PortraitGenerationRequest request)
        {
            NovelAiProviderConfiguration configuration = Require(request.Configuration);
            bool preciseBase = request.State == PortraitState.Normal
                && request.ImageUse == PortraitImageUse.CharacterReference && SupportsPreciseReference(configuration.Model);
            if (preciseBase) return 1;
            return Math.Max(0, MaximumReferenceImages - request.ReferencePaths.Count);
        }

        public PortraitProviderConfiguration CreateConfiguration(PortraitSettings settings, bool expression)
        {
            return new NovelAiProviderConfiguration {
                Model = expression ? settings.NovelAiExpressionModel : settings.NovelAiModel,
                ApiKey = expression ? settings.NovelAiExpressionApiKey : settings.NovelAiApiKey,
                OptionsJson = expression ? settings.NovelAiExpressionOptions : settings.NovelAiOptions
            };
        }

        public string Describe(PortraitProviderConfiguration configuration)
        {
            NovelAiProviderConfiguration options = Require(configuration);
            return "model=" + options.Model + ", options=" + options.OptionsJson;
        }

        public string Validate(PortraitProviderConfiguration configuration, string kind)
        {
            NovelAiProviderConfiguration options = Require(configuration);
            if (options.ApiKey.NullOrEmpty()) return Localization.T("RimAIPortrait.Error.NovelAiApiKeyMissing", kind);
            if (options.Model.NullOrEmpty()) return Localization.T("RimAIPortrait.Error.NovelAiModelMissing", kind);
            return null;
        }

        public IEnumerator Generate(PortraitGenerationRequest request, Action<PortraitGenerationResult> done)
        {
            return GenerateInternal(request, null, null, false, null, done);
        }

        public IEnumerator GenerateInpaint(PortraitGenerationRequest request, byte[] mask, float strength, bool narrowFeather,
            string debugDirectory,
            Action<PortraitGenerationResult> done)
        {
            if (request.ImageUse != PortraitImageUse.Edit || request.PrimaryImage == null || mask == null)
                throw new ArgumentException("NovelAI inpaint requires an edit image and mask.");
            return GenerateInternal(request, mask, strength, narrowFeather, debugDirectory, done);
        }

        private IEnumerator GenerateInternal(PortraitGenerationRequest request, byte[] mask, float? inpaintStrength,
            bool narrowFeather, string debugDirectory,
            Action<PortraitGenerationResult> done)
        {
            NovelAiProviderConfiguration configuration = Require(request.Configuration);
            int sourceWidth = 0;
            int sourceHeight = 0;
            byte[] generationMask = null;
            if (mask != null)
            {
                try
                {
                    request.PrimaryImage = NovelAiInpaintComposite.PrepareSource(request.PrimaryImage, mask, out sourceWidth, out sourceHeight);
                    // The UI supplies a padded request mask; composition handles one and multiple selections separately.
                    generationMask = request.InpaintRequestMask ?? NovelAiInpaintComposite.ExpandMask(mask);
                }
                catch (Exception exception)
                {
                    done(PortraitGenerationResult.Failed(exception.Message));
                    yield break;
                }
                SaveDebug(debugDirectory, "nai_debug_source.png", request.PrimaryImage);
                SaveDebug(debugDirectory, "nai_debug_original_mask.png", mask);
                SaveDebug(debugDirectory, "nai_debug_request_mask.png", generationMask);
            }
            bool preciseBase = request.State == PortraitState.Normal
                && request.ImageUse == PortraitImageUse.CharacterReference && SupportsPreciseReference(configuration.Model);
            bool characterReference = preciseBase && request.PrimaryImage != null;
            List<string> selectedReferences = (request.ReferencePaths ?? new string[0]).ToList();
            List<string> stylePaths = request.StylePaths.ToList();
            if (preciseBase && selectedReferences.Count > 0)
            {
                done(PortraitGenerationResult.Failed(Localization.T("RimAIPortrait.Error.NovelAiPreciseVibeConflict")));
                yield break;
            }
            var styleImages = new List<string>();
            if (preciseBase)
            {
                if (stylePaths.Count == 0
                    && (PortraitProviderUtilities.UsesDefaultStyle(request.StyleFolder)
                        || Directory.Exists(request.StyleFolder)))
                {
                    done(PortraitGenerationResult.Failed(Localization.T("RimAIPortrait.Error.BundledStyleMissing")));
                    yield break;
                }
                foreach (string path in stylePaths.Take(MaximumReferenceImages - (characterReference ? 1 : 0)))
                {
                    try { styleImages.Add(Convert.ToBase64String(NovelAiReferenceImage.PrepareStyle(File.ReadAllBytes(path)))); }
                    catch (Exception exception)
                    {
                        done(PortraitGenerationResult.Failed(Localization.T("RimAIPortrait.Error.NovelAiStyleReferenceImage", path, exception.Message)));
                        yield break;
                    }
                }
            }
            List<string> referenceImages = selectedReferences
                .Concat(preciseBase ? Enumerable.Empty<string>() : stylePaths)
                .Take(MaximumReferenceImages)
                .Select(ReadReference)
                .Where(value => !value.NullOrEmpty())
                .ToList();
            string characterImage = null;
            if (characterReference)
            {
                try { characterImage = Convert.ToBase64String(NovelAiReferenceImage.Prepare(request.PrimaryImage)); }
                catch (Exception)
                {
                    done(PortraitGenerationResult.Failed(Localization.T("RimAIPortrait.Error.NovelAiReferenceImage")));
                    yield break;
                }
            }
            var references = new List<string>();
            foreach (string image in referenceImages)
            {
                string encoded = null;
                string encodeError = null;
                yield return EncodeVibe(image, configuration, (value, error) => { encoded = value; encodeError = error; });
                if (encoded.NullOrEmpty())
                {
                    done(PortraitGenerationResult.Failed(encodeError));
                    yield break;
                }
                references.Add(encoded);
            }
            string addOriginalImage;
            string json = BuildRequestJson(request, configuration, references, characterImage, styleImages, generationMask,
                sourceWidth, sourceHeight, inpaintStrength, RimAIPortraitMod.Settings?.DebugMode == true,
                out addOriginalImage);
            if (mask != null && debugDirectory != null)
                Log.Message("[Rim AI Expressive Portraits][NAI Infill] source=" + sourceWidth + "x" + sourceHeight
                    + ", mask=" + sourceWidth + "x" + sourceHeight + ", action=infill, model=" + InfillModel
                    + ", add_original_image=" + addOriginalImage);
            byte[] body = Encoding.UTF8.GetBytes(json);

            using (var webRequest = new UnityWebRequest("https://image.novelai.net/ai/generate-image", "POST"))
            {
                webRequest.uploadHandler = new UploadHandlerRaw(body);
                webRequest.downloadHandler = new DownloadHandlerBuffer();
                webRequest.timeout = 300;
                webRequest.SetRequestHeader("Content-Type", "application/json");
                webRequest.SetRequestHeader("Accept", "application/zip, application/octet-stream");
                webRequest.SetRequestHeader("Authorization", "Bearer " + configuration.ApiKey);
                yield return webRequest.SendWebRequest();
                if (PortraitProviderUtilities.Failed(webRequest))
                {
                    done(PortraitGenerationResult.Failed(Localization.T("RimAIPortrait.Error.ProviderHttp", "NovelAI", webRequest.responseCode, webRequest.error, PortraitProviderUtilities.Short(webRequest.downloadHandler.text))));
                    yield break;
                }

                try
                {
                    byte[] image = ExtractFirstImage(webRequest.downloadHandler.data);
                    if (image == null)
                    {
                        done(PortraitGenerationResult.Failed(Localization.T("RimAIPortrait.Error.NoImageResponse", Localization.T("RimAIPortrait.Error.NovelAiArchiveEmpty"))));
                        yield break;
                    }
                    if (mask != null)
                    {
                        SaveDebug(debugDirectory, "nai_debug_raw_result.png", image);
                        if (debugDirectory != null)
                        {
                            int rawWidth;
                            int rawHeight;
                            NovelAiInpaintComposite.Dimensions(image, out rawWidth, out rawHeight);
                            Log.Message("[Rim AI Expressive Portraits][NAI Infill] raw_response=" + rawWidth + "x" + rawHeight);
                        }
                        image = NovelAiInpaintComposite.Composite(request.PrimaryImage, mask, generationMask,
                            image, narrowFeather);
                        SaveDebug(debugDirectory, "nai_debug_final.png", image);
                    }
                    done(PortraitGenerationResult.Succeeded(image));
                }
                catch (InvalidDataException exception)
                {
                    done(PortraitGenerationResult.Failed(exception.Message));
                }
                catch (Exception exception)
                {
                    done(PortraitGenerationResult.Failed(Localization.T("RimAIPortrait.Error.ImageDecodeFailed", exception.Message)));
                }
            }
        }

        private static IEnumerator EncodeVibe(string image, NovelAiProviderConfiguration configuration, Action<string, string> done)
        {
            string json = "{\"image\":\"" + image + "\",\"model\":\""
                + PortraitProviderUtilities.JsonEscape(configuration.Model) + "\",\"informationExtracted\":1}";
            byte[] body = Encoding.UTF8.GetBytes(json);
            using (var webRequest = new UnityWebRequest("https://image.novelai.net/ai/encode-vibe", "POST"))
            {
                webRequest.uploadHandler = new UploadHandlerRaw(body);
                webRequest.downloadHandler = new DownloadHandlerBuffer();
                webRequest.timeout = 120;
                webRequest.SetRequestHeader("Content-Type", "application/json");
                webRequest.SetRequestHeader("Authorization", "Bearer " + configuration.ApiKey);
                yield return webRequest.SendWebRequest();
                if (PortraitProviderUtilities.Failed(webRequest))
                {
                    done(null, Localization.T("RimAIPortrait.Error.ProviderHttp", "NovelAI Vibe", webRequest.responseCode, webRequest.error, PortraitProviderUtilities.Short(webRequest.downloadHandler.text)));
                    yield break;
                }
                byte[] data = webRequest.downloadHandler.data;
                done(data == null || data.Length == 0 ? null : Convert.ToBase64String(data),
                    data == null || data.Length == 0 ? Localization.T("RimAIPortrait.Error.NovelAiVibeEmpty") : null);
            }
        }

        private static string BuildRequestJson(PortraitGenerationRequest request, NovelAiProviderConfiguration configuration,
            List<string> references, string characterImage, List<string> styleImages, byte[] mask, int sourceWidth, int sourceHeight,
            float? inpaintStrength, bool debug,
            out string addOriginalImage)
        {
            bool characterReference = !characterImage.NullOrEmpty();
            bool edit = request.PrimaryImage != null && !characterReference;
            bool inpaint = mask != null;
            var options = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) {
                { "width", "832" }, { "height", characterReference ? "832" : "1216" }, { "steps", "23" }, { "scale", "5" },
                { "sampler", "k_euler_ancestral" }, { "noise_schedule", "karras" }, { "n_samples", "1" },
                { "params_version", "3" }, { "legacy", "false" },
                { "legacy_v3_extend", "false" }, { "legacy_uc", "false" }, { "qualityToggle", "false" },
                { "ucPreset", "0" }, { "cfg_rescale", "0" }, { "sm", "false" }, { "sm_dyn", "false" },
                { "dynamic_thresholding", "false" }, { "controlnet_strength", "1" },
                { "add_original_image", "true" }, { "uncond_scale", "1" }, { "prefer_brownian", "true" },
                { "negative_prompt", "lowres, bad anatomy, bad hands, text, watermark" }
            };
            foreach (KeyValuePair<string, string> option in PortraitProviderUtilities.ParseOptions(configuration.OptionsJson))
            {
                if (option.Key.Equals("seed", StringComparison.OrdinalIgnoreCase)) continue;
                options[option.Key] = option.Value;
            }
            if (inpaint)
            {
                options["width"] = sourceWidth.ToString(CultureInfo.InvariantCulture);
                options["height"] = sourceHeight.ToString(CultureInfo.InvariantCulture);
                options["image_format"] = "png";
                options["add_original_image"] = "true";
                options["strength"] = Math.Min(1f, Math.Max(.1f, inpaintStrength ?? .6f)).ToString("R", CultureInfo.InvariantCulture);
            }
            addOriginalImage = options["add_original_image"];
            options["seed"] = GenerateSeed().ToString(CultureInfo.InvariantCulture);
            string positivePrompt;
            options.TryGetValue("positive_prompt", out positivePrompt);
            options.Remove("positive_prompt");
            string prompt = positivePrompt.NullOrEmpty() ? request.Prompt : positivePrompt + "\n\n" + request.Prompt;
            if (request.ExtractedTags != null)
            {
                if (!request.ExtractedTags.PositiveText.NullOrEmpty()) prompt += "\n\n" + request.ExtractedTags.PositiveText;
                if (!request.ExtractedTags.NegativeText.NullOrEmpty())
                    options["negative_prompt"] += ", " + request.ExtractedTags.NegativeText;
            }
            if (inpaint && request.State == PortraitState.Sleep)
            {
                prompt += "\n2::closed eyes::, both eyelids fully shut, sleeping peacefully, relaxed eyebrows, no visible irises, preserve original skin tone and hair color";
                options["negative_prompt"] += ", open eyes, half-closed eyes, visible pupils, looking at viewer";
            }
            if (characterReference) options["qualityToggle"] = "true";
            if (edit)
            {
                if (!options.ContainsKey("strength")) options["strength"] = "0.6";
                if (!options.ContainsKey("noise")) options["noise"] = "0.1";
            }

            var parameters = new List<string>();
            foreach (KeyValuePair<string, string> option in options)
            {
                if (IsReserved(option.Key) || !edit && (option.Key.Equals("strength", StringComparison.OrdinalIgnoreCase)
                    || option.Key.Equals("noise", StringComparison.OrdinalIgnoreCase)
                    || option.Key.Equals("add_original_image", StringComparison.OrdinalIgnoreCase))) continue;
                parameters.Add("\"" + PortraitProviderUtilities.JsonEscape(option.Key) + "\":" + JsonValue(option.Value));
            }
            if (edit)
                parameters.Add("\"image\":\"" + Convert.ToBase64String(request.PrimaryImage) + "\"");
            if (inpaint)
                parameters.Add("\"mask\":\"" + Convert.ToBase64String(mask) + "\"");
            if (edit && (inpaint || configuration.Model.StartsWith("nai-diffusion-5", StringComparison.OrdinalIgnoreCase)))
                parameters.Add("\"img2img\":{\"strength\":" + JsonValue(options["strength"])
                    + ",\"noise\":" + JsonValue(options["noise"]) + "}");
            if (characterReference || styleImages.Count > 0)
            {
                var images = new List<string>();
                var strengths = new List<string>();
                var secondaryStrengths = new List<string>();
                var descriptions = new List<string>();
                if (characterReference)
                {
                    images.Add(characterImage);
                    strengths.Add(CharacterReferenceOption(options, "character_reference_strength", .55f).ToString("R", CultureInfo.InvariantCulture));
                    secondaryStrengths.Add((1f - CharacterReferenceOption(options, "character_reference_fidelity", .7f)).ToString("R", CultureInfo.InvariantCulture));
                    descriptions.Add("{\"caption\":{\"base_caption\":\"character\",\"char_captions\":[]},\"legacy_uc\":false}");
                }
                foreach (string styleImage in styleImages)
                {
                    images.Add(styleImage);
                    strengths.Add(CharacterReferenceOption(options, "style_reference_strength", .75f).ToString("R", CultureInfo.InvariantCulture));
                    secondaryStrengths.Add((1f - CharacterReferenceOption(options, "style_reference_fidelity", .5f)).ToString("R", CultureInfo.InvariantCulture));
                    descriptions.Add("{\"caption\":{\"base_caption\":\"style\",\"char_captions\":[]},\"legacy_uc\":false}");
                }
                parameters.Add("\"director_reference_images\":[\"" + string.Join("\",\"", images.ToArray()) + "\"]");
                parameters.Add("\"director_reference_strength_values\":[" + string.Join(",", strengths.ToArray()) + "]");
                parameters.Add("\"director_reference_secondary_strength_values\":[" + string.Join(",", secondaryStrengths.ToArray()) + "]");
                parameters.Add("\"director_reference_descriptions\":[" + string.Join(",", descriptions.ToArray()) + "]");
                parameters.Add("\"director_reference_information_extracted\":[" + string.Join(",", images.Select(value => "1.0").ToArray()) + "]");
                parameters.Add("\"inpaintImg2ImgStrength\":1.0");
            }
            if (UsesStructuredPrompt(inpaint ? InfillModel : configuration.Model))
            {
                string escapedPrompt = PortraitProviderUtilities.JsonEscape(prompt);
                string negativePrompt = PortraitProviderUtilities.JsonEscape(options["negative_prompt"]);
                parameters.Add("\"v4_prompt\":{\"caption\":{\"base_caption\":\"" + escapedPrompt
                    + "\",\"char_captions\":[]},\"use_coords\":false,\"use_order\":true}");
                parameters.Add("\"v4_negative_prompt\":{\"caption\":{\"base_caption\":\"" + negativePrompt
                    + "\",\"char_captions\":[]},\"use_coords\":false,\"use_order\":true}");
            }
            if (references.Count > 0)
            {
                parameters.Add("\"reference_image_multiple\":[" + string.Join(",", references.Select(value => "\"" + value + "\"").ToArray()) + "]");
                parameters.Add("\"reference_information_extracted_multiple\":[" + string.Join(",", references.Select(value => "1").ToArray()) + "]");
                parameters.Add("\"reference_strength_multiple\":[" + string.Join(",", references.Select(value => "0.6").ToArray()) + "]");
                parameters.Add("\"normalize_reference_strength_multiple\":true");
            }
            if (debug)
            {
                string[] positiveTags = request.ExtractedTags?.positive ?? new string[0];
                string[] negativeTags = request.ExtractedTags?.negative ?? new string[0];
                Log.Message("[Rim AI Expressive Portraits][Debug] NAI effective request (image data and pawn prompt redacted)\n"
                    + "model=" + (inpaint ? InfillModel : configuration.Model)
                    + ", action=" + (inpaint ? "infill" : edit ? "img2img" : "generate")
                    + ", size=" + options["width"] + "x" + options["height"]
                    + ", steps=" + options["steps"] + ", scale=" + options["scale"]
                    + ", sampler=" + options["sampler"] + ", noise_schedule=" + options["noise_schedule"]
                    + ", seed=" + options["seed"]
                    + ", character_reference=" + characterReference + ", style_references=" + styleImages.Count
                    + ", vibe_references=" + references.Count + "\n"
                    + "character_strength=" + (characterReference ? CharacterReferenceOption(options, "character_reference_strength", .55f).ToString("R", CultureInfo.InvariantCulture) : "none")
                    + ", character_fidelity=" + (characterReference ? CharacterReferenceOption(options, "character_reference_fidelity", .7f).ToString("R", CultureInfo.InvariantCulture) : "none")
                    + ", style_strength=" + (styleImages.Count > 0 ? CharacterReferenceOption(options, "style_reference_strength", .75f).ToString("R", CultureInfo.InvariantCulture) : "none")
                    + ", style_fidelity=" + (styleImages.Count > 0 ? CharacterReferenceOption(options, "style_reference_fidelity", .5f).ToString("R", CultureInfo.InvariantCulture) : "none") + "\n"
                    + "prompt_characters=" + prompt.Length + ", negative_characters=" + options["negative_prompt"].Length
                    + ", option_positive_characters=" + (positivePrompt?.Length ?? 0)
                    + ", portrait_prompt_characters=" + (request.Prompt?.Length ?? 0) + "\n"
                    + "directives: nearly_frontal=" + prompt.Contains("nearly frontal view")
                    + ", three_quarter=" + prompt.Contains("three-quarter view")
                    + ", warm_brown_background=" + prompt.Contains("warm-brown background")
                    + ", warm_shading=" + prompt.Contains("warm, restrained shading")
                    + ", preserve_original_colors=" + prompt.Contains("preserve original colors")
                    + ", sepia_excluded=" + options["negative_prompt"].Contains("sepia") + "\n"
                    + "extracted_positive=" + DebugTags(positiveTags) + "\n"
                    + "extracted_negative=" + DebugTags(negativeTags));
            }
            return "{\"input\":\"" + PortraitProviderUtilities.JsonEscape(prompt) + "\",\"model\":\""
                + PortraitProviderUtilities.JsonEscape(inpaint ? InfillModel : configuration.Model) + "\",\"action\":\""
                + (inpaint ? "infill" : edit ? "img2img" : "generate") + "\",\"parameters\":{"
                + string.Join(",", parameters.ToArray()) + "}}";
        }

        private static string DebugTags(string[] tags)
        {
            return "[" + string.Join(",", tags.Select(tag => "\"" + PortraitProviderUtilities.JsonEscape(tag) + "\"").ToArray()) + "]";
        }

        private static uint GenerateSeed()
        {
            byte[] bytes = new byte[sizeof(uint)];
            using (RandomNumberGenerator random = RandomNumberGenerator.Create())
            {
                uint seed;
                do
                {
                    random.GetBytes(bytes);
                    seed = BitConverter.ToUInt32(bytes, 0);
                }
                while (seed == 0);
                return seed;
            }
        }

        private static bool IsReserved(string key)
        {
            return key.Equals("character_reference_strength", StringComparison.OrdinalIgnoreCase)
                || key.Equals("character_reference_fidelity", StringComparison.OrdinalIgnoreCase)
                || key.Equals("style_reference_strength", StringComparison.OrdinalIgnoreCase)
                || key.Equals("style_reference_fidelity", StringComparison.OrdinalIgnoreCase)
                || key.Equals("image", StringComparison.OrdinalIgnoreCase)
                || key.Equals("mask", StringComparison.OrdinalIgnoreCase)
                || key.Equals("img2img", StringComparison.OrdinalIgnoreCase)
                || key.Equals("v4_prompt", StringComparison.OrdinalIgnoreCase)
                || key.Equals("v4_negative_prompt", StringComparison.OrdinalIgnoreCase)
                || key.StartsWith("reference_", StringComparison.OrdinalIgnoreCase)
                || key.Equals("normalize_reference_strength_multiple", StringComparison.OrdinalIgnoreCase)
                || key.StartsWith("director_reference_", StringComparison.OrdinalIgnoreCase)
                || key.Equals("inpaintImg2ImgStrength", StringComparison.OrdinalIgnoreCase);
        }

        private static float CharacterReferenceOption(Dictionary<string, string> options, string key, float fallback)
        {
            string text;
            float value;
            return options.TryGetValue(key, out text)
                && float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value)
                && !float.IsNaN(value) && !float.IsInfinity(value) && value >= 0f && value <= 1f
                ? value : fallback;
        }

        private static bool SupportsPreciseReference(string model)
        {
            return string.Equals(model, "nai-diffusion-4-5-full", StringComparison.OrdinalIgnoreCase)
                || string.Equals(model, "nai-diffusion-4-5-curated", StringComparison.OrdinalIgnoreCase);
        }

        private static bool UsesStructuredPrompt(string model)
        {
            return !model.NullOrEmpty() && (model.StartsWith("nai-diffusion-4", StringComparison.OrdinalIgnoreCase)
                || model.StartsWith("nai-diffusion-5", StringComparison.OrdinalIgnoreCase));
        }

        private static string JsonValue(string value)
        {
            bool boolean;
            double number;
            if (bool.TryParse(value, out boolean)) return boolean ? "true" : "false";
            if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out number)) return value;
            if (string.Equals(value, "null", StringComparison.OrdinalIgnoreCase)) return "null";
            return "\"" + PortraitProviderUtilities.JsonEscape(value) + "\"";
        }

        private static string ReadReference(string path)
        {
            try { return Convert.ToBase64String(File.ReadAllBytes(path)); }
            catch { return null; }
        }

        private static void SaveDebug(string directory, string fileName, byte[] image)
        {
            if (directory == null || image == null) return;
            try
            {
                Directory.CreateDirectory(directory);
                File.WriteAllBytes(Path.Combine(directory, fileName), image);
            }
            catch (Exception exception)
            {
                Log.Warning("[Rim AI Expressive Portraits] Could not save NAI debug image " + fileName + ": " + exception.Message);
            }
        }

        private static byte[] ExtractFirstImage(byte[] data)
        {
            if (data == null || data.Length == 0) return null;
            if (IsImage(data)) return data;
            using (var input = new MemoryStream(data, false))
            using (var archive = new ZipArchive(input, ZipArchiveMode.Read, false))
            {
                ZipArchiveEntry entry = archive.Entries.FirstOrDefault(item =>
                    item.Length > 0 && new[] { ".png", ".jpg", ".jpeg", ".webp" }.Contains(Path.GetExtension(item.Name).ToLowerInvariant()));
                if (entry == null) return null;
                using (Stream stream = entry.Open())
                using (var output = new MemoryStream())
                {
                    stream.CopyTo(output);
                    return output.ToArray();
                }
            }
        }

        private static bool IsImage(byte[] data)
        {
            return data.Length >= 8 && data[0] == 0x89 && data[1] == 0x50 && data[2] == 0x4e && data[3] == 0x47
                || data.Length >= 3 && data[0] == 0xff && data[1] == 0xd8 && data[2] == 0xff
                || data.Length >= 12 && data[0] == 0x52 && data[1] == 0x49 && data[2] == 0x46 && data[3] == 0x46
                    && data[8] == 0x57 && data[9] == 0x45 && data[10] == 0x42 && data[11] == 0x50;
        }

        private static NovelAiProviderConfiguration Require(PortraitProviderConfiguration configuration)
        {
            NovelAiProviderConfiguration typed = configuration as NovelAiProviderConfiguration;
            if (typed == null) throw new ArgumentException("NovelAI provider requires NovelAiProviderConfiguration.");
            return typed;
        }
    }
}
