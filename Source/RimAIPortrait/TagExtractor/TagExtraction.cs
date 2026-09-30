using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
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

        public string PositiveText => string.Join(", ", positive ?? new string[0]);
        public string NegativeText => string.Join(", ", negative ?? new string[0]);

        public bool IsValid(bool inpaint)
        {
            return ValidationIssue(inpaint) == null;
        }

        public string ValidationIssue(bool inpaint)
        {
            string issue = ValidateList(positive, inpaint ? 12 : 20, "positive");
            if (issue != null) return issue;
            issue = ValidateList(negative, inpaint ? 10 : 15, "negative");
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

    internal static class TagExtraction
    {
        private const string Schema = "{\"type\":\"object\",\"properties\":{\"positive\":{\"type\":\"array\",\"items\":{\"type\":\"string\"}},\"negative\":{\"type\":\"array\",\"items\":{\"type\":\"string\"}}},\"required\":[\"positive\",\"negative\"],\"additionalProperties\":false}";
        private const string GoogleSchema = "{\"type\":\"object\",\"properties\":{\"positive\":{\"type\":\"array\",\"items\":{\"type\":\"string\"}},\"negative\":{\"type\":\"array\",\"items\":{\"type\":\"string\"}}},\"required\":[\"positive\",\"negative\"]}";
        private const string BatchItem = "{\"type\":\"object\",\"properties\":{\"state\":{\"type\":\"string\"},\"positive\":{\"type\":\"array\",\"items\":{\"type\":\"string\"}},\"negative\":{\"type\":\"array\",\"items\":{\"type\":\"string\"}}},\"required\":[\"state\",\"positive\",\"negative\"]}";
        private static readonly string BatchSchema = "{\"type\":\"object\",\"properties\":{\"states\":{\"type\":\"array\",\"items\":"
            + BatchItem.Substring(0, BatchItem.Length - 1) + ",\"additionalProperties\":false}}},\"required\":[\"states\"],\"additionalProperties\":false}";
        private static readonly string GoogleBatchSchema = "{\"type\":\"object\",\"properties\":{\"states\":{\"type\":\"array\",\"items\":"
            + BatchItem + "}},\"required\":[\"states\"]}";

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

        public static IEnumerator Extract(PortraitSettings settings, byte[] image, string portraitPrompt, bool inpaint,
            Action<ExtractedTags, string> done, string features = null)
        {
            string response = null;
            string error = null;
            yield return Send(settings, image, portraitPrompt, inpaint ? "TagExtractorInpaint.md" : "TagExtractor.md",
                false, (value, failure) => { response = value; error = failure; }, features);
            if (response == null) { done(null, error); yield break; }
            try
            {
                ExtractedTags tags = JsonUtility.FromJson<ExtractedTags>(response);
                string issue = tags == null ? "JSON object missing" : tags.ValidationIssue(inpaint);
                Debug(settings, "validation", "scope=" + (inpaint ? "inpaint" : "base")
                    + ", positive=" + (tags?.positive?.Length ?? -1)
                    + ", negative=" + (tags?.negative?.Length ?? -1)
                    + ", result=" + (issue ?? "valid"));
                if (issue != null) throw new InvalidDataException();
                done(tags, null);
            }
            catch (Exception exception)
            {
                Debug(settings, "parse", "scope=" + (inpaint ? "inpaint" : "base")
                    + ", exception=" + exception.GetType().Name);
                done(null, Localization.T("RimAIPortrait.TagExtractor.InvalidResponse"));
            }
        }

        public static IEnumerator ExtractBatch(PortraitSettings settings, byte[] image,
            IDictionary<PortraitState, string> prompts, Action<Dictionary<PortraitState, ExtractedTags>, string> done)
        {
            var message = new StringBuilder();
            foreach (KeyValuePair<PortraitState, string> entry in prompts)
                message.Append("\n\n[State: ").Append(entry.Key).Append("]\n").Append(entry.Value);
            string response = null;
            string error = null;
            yield return Send(settings, image, message.ToString(), "TagExtractorInpaintBatch.md", true,
                (value, failure) => { response = value; error = failure; });
            var results = new Dictionary<PortraitState, ExtractedTags>();
            if (response == null)
            {
                done(results, error ?? Localization.T("RimAIPortrait.TagExtractor.InvalidResponse"));
                yield break;
            }
            try
            {
                ExtractedTagBatch batch;
                using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(response)))
                    batch = (ExtractedTagBatch)new DataContractJsonSerializer(typeof(ExtractedTagBatch)).ReadObject(stream);
                Debug(settings, "batch validation", "expected_states=" + prompts.Count
                    + ", returned_states=" + (batch?.states?.Length ?? -1)
                    + ", has_states_field=" + HasJsonField(response, "states"));
                if (batch == null || batch.states == null || batch.states.Length != prompts.Count) throw new InvalidDataException();
                foreach (ExtractedStateTags item in batch.states)
                {
                    PortraitState state;
                    ExtractedTags tags = item == null ? null : new ExtractedTags { positive = item.positive, negative = item.negative };
                    string issue = tags == null ? "state item missing" : tags.ValidationIssue(true);
                    Debug(settings, "batch item", "index=" + results.Count
                        + ", positive=" + (tags?.positive?.Length ?? -1)
                        + ", negative=" + (tags?.negative?.Length ?? -1)
                        + ", result=" + (issue ?? "valid"));
                    if (item == null || !Enum.TryParse(item.state, out state) || !prompts.ContainsKey(state)
                        || results.ContainsKey(state) || issue != null) throw new InvalidDataException();
                    results.Add(state, tags);
                }
            }
            catch (Exception exception)
            {
                Debug(settings, "batch parse", "exception=" + exception.GetType().Name);
                results.Clear();
                done(results, Localization.T("RimAIPortrait.TagExtractor.InvalidResponse"));
                yield break;
            }
            done(results, null);
        }

        private static IEnumerator Send(PortraitSettings settings, byte[] image, string portraitPrompt,
            string promptFile, bool batch, Action<string, string> done, string features = null)
        {
            if (image == null || image.Length == 0 || image.Length > 8 * 1024 * 1024)
            {
                done(null, Localization.T("RimAIPortrait.TagExtractor.ImageRequired"));
                yield break;
            }

            string instruction;
            try
            {
                instruction = File.ReadAllText(Path.Combine(RimAIPortraitMod.ModPack.RootDir, "Prompts", "Common", promptFile)).Trim();
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

            string message = instruction + (string.IsNullOrWhiteSpace(features) ? "" : "\n\nFeatures:\n" + features.Trim())
                + "\n\nPortrait request: " + (portraitPrompt ?? "");
            string encodedImage = Convert.ToBase64String(image);
            string mime = image.Length > 3 && image[0] == 0xff && image[1] == 0xd8 ? "image/jpeg"
                : image.Length > 12 && image[0] == 'R' && image[1] == 'I' && image[2] == 'F' && image[3] == 'F'
                    && image[8] == 'W' && image[9] == 'E' && image[10] == 'B' && image[11] == 'P'
                    ? "image/webp" : "image/png";
            string body = settings.TagExtractorBackend == TagExtractorModel.Google
                ? GoogleBody(model, message, mime, encodedImage, batch)
                : settings.TagExtractorBackend == TagExtractorModel.LocalModel
                    ? ChatBody(model, message, mime, encodedImage, batch)
                    : OpenAiBody(model, message, mime, encodedImage, batch);
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
                    done(null, Localization.T("RimAIPortrait.TagExtractor.InvalidResponse"));
                }
            }
        }

        private static bool LocalUrlAllowed(string value)
        {
            Uri uri;
            return Uri.TryCreate(value, UriKind.Absolute, out uri)
                && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
        }

        private static string Q(string value) => "\"" + PortraitProviderUtilities.JsonEscape(value) + "\"";

        private static string OpenAiBody(string model, string message, string mime, string image, bool batch)
        {
            return "{\"model\":" + Q(model) + ",\"input\":[{\"role\":\"user\",\"content\":[{\"type\":\"input_text\",\"text\":"
                + Q(message) + "},{\"type\":\"input_image\",\"image_url\":" + Q("data:" + mime + ";base64," + image)
                + "}]}],\"text\":{\"format\":{\"type\":\"json_schema\",\"name\":\"portrait_tags\",\"strict\":true,\"schema\":"
                + (batch ? BatchSchema : Schema) + "}},\"max_output_tokens\":" + (batch ? "4096" : "600") + "}";
        }

        private static string GoogleBody(string model, string message, string mime, string image, bool batch)
        {
            string thinking = model.StartsWith("gemini-3", StringComparison.OrdinalIgnoreCase)
                ? "{\"thinkingLevel\":\"minimal\"}" : "{\"thinkingBudget\":0}";
            return "{\"contents\":[{\"parts\":[{\"text\":" + Q(message) + "},{\"inlineData\":{\"mimeType\":" + Q(mime)
                + ",\"data\":" + Q(image) + "}}]}],\"generationConfig\":{\"responseMimeType\":\"application/json\",\"responseSchema\":"
                + (batch ? GoogleBatchSchema : GoogleSchema) + ",\"maxOutputTokens\":"
                + (batch ? "4096" : "2048") + ",\"thinkingConfig\":" + thinking + "}}";
        }

        private static string ChatBody(string model, string message, string mime, string image, bool batch)
        {
            return "{\"model\":" + Q(model) + ",\"messages\":[{\"role\":\"user\",\"content\":[{\"type\":\"text\",\"text\":"
                + Q(message) + "},{\"type\":\"image_url\",\"image_url\":{\"url\":" + Q("data:" + mime + ";base64," + image)
                + "}}]}],\"response_format\":{\"type\":\"json_object\"},\"max_tokens\":"
                + (batch ? "4096" : "2048") + "}";
        }

        private static string OpenAiText(PortraitSettings settings, string json)
        {
            Debug(settings, "OpenAI raw response", "json_characters=" + (json?.Length ?? 0)
                + ", has_output=" + HasJsonField(json, "output")
                + ", has_output_text=" + HasJsonField(json, "output_text"));
            OpenAiResponse response;
            using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(json)))
                response = (OpenAiResponse)new DataContractJsonSerializer(typeof(OpenAiResponse)).ReadObject(stream);
            Debug(settings, "OpenAI response", "status=" + SafeStatus(response?.status)
                + ", has_output=" + HasJsonField(json, "output")
                + ", output_items=" + (response?.output?.Length ?? -1));
            if (response == null || response.status != "completed") throw new InvalidDataException();
            string text = response.output == null ? null : string.Concat(response.output
                .Where(item => item != null && item.content != null)
                .SelectMany(item => item.content)
                .Where(part => part != null && part.type == "output_text")
                .Select(part => part.text));
            if (string.IsNullOrWhiteSpace(text)) text = response.output_text;
            if (string.IsNullOrWhiteSpace(text)) throw new InvalidDataException();
            return text;
        }

        private static string GoogleText(PortraitSettings settings, string json)
        {
            Debug(settings, "Google raw response", "json_characters=" + (json?.Length ?? 0)
                + ", root_object=" + (!string.IsNullOrWhiteSpace(json) && json.TrimStart().StartsWith("{", StringComparison.Ordinal))
                + ", has_candidates_field=" + HasJsonField(json, "candidates")
                + ", has_usage_field=" + HasJsonField(json, "usageMetadata")
                + ", has_feedback_field=" + HasJsonField(json, "promptFeedback")
                + ", has_error_field=" + HasJsonField(json, "error"));
            GoogleResponse response;
            using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(json)))
                response = (GoogleResponse)new DataContractJsonSerializer(typeof(GoogleResponse)).ReadObject(stream);
            GoogleCandidate candidate = response?.candidates != null && response.candidates.Length > 0
                ? response.candidates[0] : null;
            Debug(settings, "Google parsed response", "object=" + (response != null)
                + ", candidates=" + (response?.candidates?.Length ?? -1)
                + ", finish_reason=" + SafeStatus(candidate?.finishReason)
                + ", block_reason=" + SafeStatus(response?.promptFeedback?.blockReason)
                + ", parts=" + (candidate?.content?.parts?.Length ?? -1)
                + ", output_tokens=" + (response?.usageMetadata?.candidatesTokenCount ?? -1)
                + ", thought_tokens=" + (response?.usageMetadata?.thoughtsTokenCount ?? -1));
            if (response == null || response.candidates == null || response.candidates.Length == 0
                || candidate.finishReason != "STOP" || candidate.content == null
                || candidate.content.parts == null) throw new InvalidDataException();
            return string.Concat(candidate.content.parts.Select(part => part.text));
        }

        private static string ChatText(PortraitSettings settings, string json)
        {
            ChatResponse response = JsonUtility.FromJson<ChatResponse>(json);
            ChatChoice choice = response?.choices != null && response.choices.Length > 0
                ? response.choices[0] : null;
            Debug(settings, "local response", "choices=" + (response?.choices?.Length ?? -1)
                + ", finish_reason=" + SafeStatus(choice?.finish_reason));
            if (response == null || response.choices == null || response.choices.Length == 0
                || response.choices[0].message == null || response.choices[0].finish_reason != "stop")
                throw new InvalidDataException();
            return response.choices[0].message.content;
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

        [DataContract] private sealed class TextPart
        {
            [DataMember] public string type = null;
            [DataMember] public string text = null;
        }
        [DataContract] private sealed class ExtractedStateTags
        {
            [DataMember] public string state = null;
            [DataMember] public string[] positive = null;
            [DataMember] public string[] negative = null;
        }
        [DataContract] private sealed class ExtractedTagBatch
        {
            [DataMember] public ExtractedStateTags[] states = null;
        }
        [DataContract] private sealed class OpenAiOutput { [DataMember] public TextPart[] content = null; }
        [DataContract] private sealed class OpenAiResponse
        {
            [DataMember] public string status = null;
            [DataMember] public OpenAiOutput[] output = null;
            [DataMember] public string output_text = null;
        }
        [DataContract] private sealed class GooglePart { [DataMember] public string text = null; }
        [DataContract] private sealed class GoogleContent { [DataMember] public GooglePart[] parts = null; }
        [DataContract] private sealed class GoogleCandidate
        {
            [DataMember] public string finishReason = null;
            [DataMember] public GoogleContent content = null;
        }
        [DataContract] private sealed class GooglePromptFeedback { [DataMember] public string blockReason = null; }
        [DataContract] private sealed class GoogleUsageMetadata
        {
            [DataMember] public int candidatesTokenCount = 0;
            [DataMember] public int thoughtsTokenCount = 0;
        }
        [DataContract] private sealed class GoogleResponse
        {
            [DataMember] public GoogleCandidate[] candidates = null;
            [DataMember] public GooglePromptFeedback promptFeedback = null;
            [DataMember] public GoogleUsageMetadata usageMetadata = null;
        }
        [Serializable] private sealed class ChatMessage { public string content = null; }
        [Serializable] private sealed class ChatChoice { public string finish_reason = null; public ChatMessage message = null; }
        [Serializable] private sealed class ChatResponse { public ChatChoice[] choices = null; }
    }
}
