using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.Networking;

namespace RimAIPortrait
{
    [Serializable]
    internal sealed class ExtractedTags
    {
        public string[] positive = null;
        public string[] negative = null;
        public bool deduplicatePrompt;

        public string PositiveText => string.Join(", ", positive ?? new string[0]);
        public string NegativeText => string.Join(", ", negative ?? new string[0]);

        public bool IsValid(int positiveLimit, int negativeLimit)
        {
            return ValidationIssue(positiveLimit, negativeLimit) == null;
        }

        public bool TrimExcess(int positiveLimit, int negativeLimit)
        {
            bool changed = false;
            if (positive != null && positive.Length > positiveLimit)
            {
                positive = positive.Take(positiveLimit).ToArray();
                changed = true;
            }
            if (negative != null && negative.Length > negativeLimit)
            {
                negative = negative.Take(negativeLimit).ToArray();
                changed = true;
            }
            return changed;
        }

        public string ValidationIssue(int positiveLimit, int negativeLimit)
        {
            string issue = ValidateList(positive, positiveLimit, "positive");
            if (issue != null) return issue;
            issue = ValidateList(negative, negativeLimit, "negative");
            if (issue != null) return issue;
            return positive.Length + negative.Length == 0 ? "both tag lists empty" : null;
        }

        private static string ValidateList(string[] values, int limit, string name)
        {
            if (values == null) return name + " array missing";
            if (values.Length > limit) return name + " count exceeds " + limit;
            if (values.Any(string.IsNullOrWhiteSpace)) return name + " contains empty tag";
            if (values.Any(value => value.Length > 100)) return name + " contains tag over 100 characters";
            if (values.Any(value => value.IndexOfAny(new[] { '\r', '\n', '\t' }) >= 0))
                return name + " contains control characters";
            if (values.Distinct(StringComparer.OrdinalIgnoreCase).Count() != values.Length)
                return name + " contains duplicates";
            return null;
        }
    }

    internal static partial class TagExtraction
    {
        private const string Schema = "{\"type\":\"object\",\"properties\":{\"positive\":{\"type\":\"array\",\"items\":{\"type\":\"string\"}},\"negative\":{\"type\":\"array\",\"items\":{\"type\":\"string\"}}},\"required\":[\"positive\",\"negative\"],\"additionalProperties\":false}";
        private const string GoogleSchema = "{\"type\":\"object\",\"properties\":{\"positive\":{\"type\":\"array\",\"items\":{\"type\":\"string\"}},\"negative\":{\"type\":\"array\",\"items\":{\"type\":\"string\"}}},\"required\":[\"positive\",\"negative\"]}";

        public static bool IsConfigured(PortraitSettings settings)
        {
            if (settings.TagExtractorBackend == TagExtractorModel.Google)
                return !string.IsNullOrWhiteSpace(settings.TagExtractorGoogleKey)
                    && !string.IsNullOrWhiteSpace(settings.TagExtractorGoogleModel);
            if (settings.TagExtractorBackend == TagExtractorModel.LocalModel)
                return !string.IsNullOrWhiteSpace(settings.TagExtractorLocalModel)
                    && LocalUrlAllowed(settings.TagExtractorLocalUrl);
            return !string.IsNullOrWhiteSpace(settings.TagExtractorOpenAiKey)
                && !string.IsNullOrWhiteSpace(settings.TagExtractorOpenAiModel);
        }

        public static IEnumerator Extract(PortraitSettings settings, byte[] image, string portraitPrompt,
            Action<ExtractedTags, string> done, string features = null)
        {
            string response = null;
            string error = null;
            yield return Send(settings, image, portraitPrompt, "TagExtractor.md",
                false, (value, failure) => { response = value; error = failure; }, features);
            if (response == null) { done(null, error); yield break; }
            try
            {
                ExtractedTags tags = JsonUtility.FromJson<ExtractedTags>(response);
                int positiveLimit = Limit(settings.TagExtractorBasePositiveLimit, 1);
                int negativeLimit = Limit(settings.TagExtractorBaseNegativeLimit, 0);
                if (tags != null && tags.TrimExcess(positiveLimit, negativeLimit))
                    Debug(settings, "tag limits", "scope=base, positive=" + (tags.positive?.Length ?? -1)
                        + ", negative=" + (tags.negative?.Length ?? -1));
                string issue = tags == null ? "JSON object missing" : tags.ValidationIssue(positiveLimit, negativeLimit);
                Debug(settings, "validation", "scope=base"
                    + ", positive=" + (tags?.positive?.Length ?? -1)
                    + ", negative=" + (tags?.negative?.Length ?? -1)
                    + ", result=" + (issue ?? "valid"));
                if (issue != null) throw new InvalidDataException();
                done(tags, null);
            }
            catch (Exception exception)
            {
                Debug(settings, "parse", "scope=base"
                    + ", exception=" + exception.GetType().Name);
                done(null, Localization.T("RimAIPortrait.TagExtractor.InvalidResponse"));
            }
        }

        public static IEnumerator ExtractInpaint(PortraitSettings settings, byte[] image, PortraitState state,
            string prompt, Action<ExtractedTags, string> done)
        {
            string response = null;
            string error = null;
            string message = "[State: " + state + "]\n" + prompt;
            yield return Send(settings, image, message, "TagExtractorInpaint.md", true,
                (value, failure) => { response = value; error = failure; });
            if (response == null)
            {
                done(null, error ?? Localization.T("RimAIPortrait.TagExtractor.InvalidResponse"));
                yield break;
            }
            try
            {
                ExtractedTags tags = JsonUtility.FromJson<ExtractedTags>(response);
                int positiveLimit = Limit(settings.TagExtractorInpaintPositiveLimit, 1);
                int negativeLimit = Limit(settings.TagExtractorInpaintNegativeLimit, 0);
                if (tags != null && tags.TrimExcess(positiveLimit, negativeLimit))
                    Debug(settings, "tag limits", "scope=inpaint, state=" + state
                        + ", positive=" + (tags.positive?.Length ?? -1)
                        + ", negative=" + (tags.negative?.Length ?? -1));
                string issue = tags == null ? "JSON object missing" : tags.ValidationIssue(positiveLimit, negativeLimit);
                Debug(settings, "validation", "scope=inpaint, state=" + state
                    + ", positive=" + (tags?.positive?.Length ?? -1)
                    + ", negative=" + (tags?.negative?.Length ?? -1)
                    + ", result=" + (issue ?? "valid"));
                if (issue != null || HasJsonField(response, "states")) throw new InvalidDataException();
                done(tags, null);
            }
            catch (Exception exception)
            {
                Debug(settings, "parse", "scope=inpaint, exception=" + exception.GetType().Name);
                done(null, Localization.T("RimAIPortrait.TagExtractor.InvalidResponse"));
            }
        }

        private static IEnumerator Send(PortraitSettings settings, byte[] image, string portraitPrompt,
            string promptFile, bool inpaint, Action<string, string> done, string features = null)
        {
            if (image == null || image.Length == 0 || image.Length > 8 * 1024 * 1024)
            {
                done(null, Localization.T("RimAIPortrait.TagExtractor.ImageRequired"));
                yield break;
            }

            string instruction = inpaint ? settings.TagExtractorInpaintPrompt : settings.TagExtractorBasePrompt;
            try
            {
                if (string.IsNullOrWhiteSpace(instruction))
                    instruction = File.ReadAllText(Path.Combine(RimAIPortraitMod.ModPack.RootDir, "Prompts", "Common", promptFile));
                instruction = instruction.Trim();
                if (instruction.Length == 0) throw new InvalidDataException();
            }
            catch (Exception)
            {
                done(null, Localization.T("RimAIPortrait.TagExtractor.PromptMissing"));
                yield break;
            }

            string model;
            string key;
            string url;
            if (settings.TagExtractorBackend == TagExtractorModel.Google)
            {
                model = settings.TagExtractorGoogleModel;
                key = settings.TagExtractorGoogleKey;
                url = "https://generativelanguage.googleapis.com/v1beta/models/" + Uri.EscapeDataString(model ?? "") + ":generateContent";
            }
            else if (settings.TagExtractorBackend == TagExtractorModel.LocalModel)
            {
                model = settings.TagExtractorLocalModel;
                key = settings.TagExtractorLocalKey;
                url = (settings.TagExtractorLocalUrl ?? "").TrimEnd('/') + "/v1/chat/completions";
            }
            else
            {
                model = settings.TagExtractorOpenAiModel;
                key = settings.TagExtractorOpenAiKey;
                url = "https://api.openai.com/v1/responses";
            }
            if (string.IsNullOrWhiteSpace(model) || (settings.TagExtractorBackend != TagExtractorModel.LocalModel && string.IsNullOrWhiteSpace(key))
                || settings.TagExtractorBackend == TagExtractorModel.LocalModel && !LocalUrlAllowed(settings.TagExtractorLocalUrl))
            {
                done(null, Localization.T("RimAIPortrait.TagExtractor.ConfigurationMissing"));
                yield break;
            }

            int positiveTagLimit = Limit(inpaint ? settings.TagExtractorInpaintPositiveLimit
                : settings.TagExtractorBasePositiveLimit, 1);
            int negativeTagLimit = Limit(inpaint ? settings.TagExtractorInpaintNegativeLimit
                : settings.TagExtractorBaseNegativeLimit, 0);
            string message = instruction + (string.IsNullOrWhiteSpace(features) ? "" : "\n\nFeatures:\n" + features.Trim())
                + "\n\nPortrait request: " + (portraitPrompt ?? "")
                + "\n\nTag limits for this request: at most " + positiveTagLimit + " positive and "
                + negativeTagLimit + " negative tags. These limits override earlier numeric tag limits."
                + (negativeTagLimit == 0 ? " Return an empty negative array." : "");
            System.Collections.Generic.Dictionary<string, string> options;
            try
            {
                string optionJson = settings.TagExtractorBackend == TagExtractorModel.Google
                    ? settings.TagExtractorGoogleOptions : settings.TagExtractorBackend == TagExtractorModel.LocalModel
                        ? settings.TagExtractorLocalOptions : settings.TagExtractorOpenAiOptions;
                options = ParseTagOptions(optionJson ?? "{}");
            }
            catch (Exception exception)
            {
                done(null, Localization.T("RimAIPortrait.TagExtractor.InvalidOptions", exception.Message));
                yield break;
            }
            string encodedImage = Convert.ToBase64String(image);
            string mime = image.Length > 3 && image[0] == 0xff && image[1] == 0xd8 ? "image/jpeg"
                : image.Length > 12 && image[0] == 'R' && image[1] == 'I' && image[2] == 'F' && image[3] == 'F'
                    && image[8] == 'W' && image[9] == 'E' && image[10] == 'B' && image[11] == 'P'
                    ? "image/webp" : "image/png";
            string body = settings.TagExtractorBackend == TagExtractorModel.Google
                ? GoogleBody(model, message, mime, encodedImage, options)
                : settings.TagExtractorBackend == TagExtractorModel.LocalModel
                    ? ChatBody(model, message, mime, encodedImage, options)
                    : OpenAiBody(model, message, mime, encodedImage, inpaint, options);
            Dialog_DebugRequest.Trace trace = Dialog_DebugRequest.Show("Tag Extractor / " + settings.TagExtractorBackend, url, body);
            using (var request = new UnityWebRequest(url, "POST"))
            {
                request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(body));
                request.downloadHandler = new DownloadHandlerBuffer();
                request.timeout = 120;
                request.SetRequestHeader("Content-Type", "application/json");
                if (settings.TagExtractorBackend == TagExtractorModel.Google)
                    request.SetRequestHeader("x-goog-api-key", key);
                else if (!string.IsNullOrWhiteSpace(key))
                    request.SetRequestHeader("Authorization", "Bearer " + key);
                yield return request.SendWebRequest();
                if (trace != null) trace.Respond(request);
                Debug(settings, "HTTP", "model=" + SafeModel(model) + ", status=" + request.responseCode
                    + ", bytes=" + (request.downloadHandler?.data?.Length ?? 0));
                if (PortraitProviderUtilities.Failed(request))
                {
                    done(null, Localization.T("RimAIPortrait.TagExtractor.HttpError", request.responseCode, request.error));
                    yield break;
                }
                try
                {
                    string json = request.downloadHandler.text;
                    string tagsJson = settings.TagExtractorBackend == TagExtractorModel.Google ? GoogleText(settings, json)
                        : settings.TagExtractorBackend == TagExtractorModel.LocalModel ? ChatText(settings, json) : OpenAiText(settings, json);
                    Debug(settings, "response", "tag_json_characters=" + (tagsJson?.Length ?? 0));
                    done(tagsJson, null);
                }
                catch (Exception exception)
                {
                    Debug(settings, "response parse", "exception=" + exception.GetType().Name);
                    done(null, Localization.T(exception is LocalModelOutputLimitException
                        ? "RimAIPortrait.TagExtractor.LocalOutputLimit"
                        : "RimAIPortrait.TagExtractor.InvalidResponse"));
                }
            }
        }

        private static bool LocalUrlAllowed(string value)
        {
            Uri uri;
            return Uri.TryCreate(value, UriKind.Absolute, out uri)
                && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
        }

        private static int Limit(int value, int minimum)
        {
            return Math.Max(minimum, Math.Min(50, value));
        }


        private static void Debug(PortraitSettings settings, string stage, string detail)
        {
            if (settings.DebugMode)
                Verse.Log.Message("[Rim AI Expressive Portraits][Debug] TagExtractor "
                    + settings.TagExtractorBackend + " " + stage + ": " + detail);
        }

        private static string SafeStatus(string value)
        {
            if (string.IsNullOrEmpty(value)) return "missing";
            return value.Length <= 32 && value.All(character => char.IsLetterOrDigit(character) || character == '_')
                ? value : "unrecognized";
        }

        private static string SafeModel(string value)
        {
            if (string.IsNullOrEmpty(value)) return "missing";
            return value.Length <= 80 && value.All(character => char.IsLetterOrDigit(character)
                || character == '-' || character == '_' || character == '.') ? value : "unrecognized";
        }

        private static bool HasJsonField(string json, string name)
        {
            return !string.IsNullOrEmpty(json)
                && Regex.IsMatch(json, "\"" + Regex.Escape(name) + "\"\\s*:");
        }

    }
}
