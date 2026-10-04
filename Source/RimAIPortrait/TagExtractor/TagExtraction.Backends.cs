using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;

namespace RimAIPortrait
{
    internal static partial class TagExtraction
    {
        private static string Q(string value) => "\"" + PortraitProviderUtilities.JsonEscape(value) + "\"";

        private static string OpenAiBody(string model, string message, string mime, string image, bool inpaint,
            Dictionary<string, string> options)
        {
            string tokens = Option(options, "max_output_tokens", Option(options, "max_tokens", inpaint ? "1200" : "600"));
            return "{\"model\":" + Q(model) + ",\"input\":[{\"role\":\"user\",\"content\":[{\"type\":\"input_text\",\"text\":"
                + Q(message) + "},{\"type\":\"input_image\",\"image_url\":" + Q("data:" + mime + ";base64," + image)
                + "}]}],\"text\":{\"format\":{\"type\":\"json_schema\",\"name\":\"portrait_tags\",\"strict\":true,\"schema\":"
                + Schema + "}},\"max_output_tokens\":" + tokens
                + ExtraOptions(options, "max_tokens", "max_output_tokens", "model", "input", "text", "stream") + "}";
        }

        private static string GoogleBody(string model, string message, string mime, string image,
            Dictionary<string, string> options)
        {
            string thinking = model.StartsWith("gemini-3", StringComparison.OrdinalIgnoreCase)
                ? "{\"thinkingLevel\":\"minimal\"}" : "{\"thinkingBudget\":0}";
            return "{\"contents\":[{\"parts\":[{\"text\":" + Q(message) + "},{\"inlineData\":{\"mimeType\":" + Q(mime)
                + ",\"data\":" + Q(image) + "}}]}],\"generationConfig\":{\"responseMimeType\":\"application/json\",\"responseSchema\":"
                + GoogleSchema + ",\"maxOutputTokens\":" + Option(options, "maxOutputTokens", "2048")
                + ",\"thinkingConfig\":" + thinking
                + ExtraOptions(options, "maxOutputTokens", "responseMimeType", "responseSchema", "thinkingConfig") + "}}";
        }

        private static string ChatBody(string model, string message, string mime, string image,
            Dictionary<string, string> options)
        {
            return "{\"model\":" + Q(model) + ",\"messages\":[{\"role\":\"user\",\"content\":[{\"type\":\"text\",\"text\":"
                + Q(message) + "},{\"type\":\"image_url\",\"image_url\":{\"url\":" + Q("data:" + mime + ";base64," + image)
                + "}}]}],\"response_format\":{\"type\":\"json_object\"},\"max_tokens\":"
                + Option(options, "max_tokens", "2048")
                + ExtraOptions(options, "max_tokens", "model", "messages", "response_format", "stream") + "}";
        }

        private static Dictionary<string, string> ParseTagOptions(string json)
        {
            PromptArchive.ValidateOptions(json);
            var result = new Dictionary<string, string>(StringComparer.Ordinal);
            MatchCollection matches = Regex.Matches(json, "\"([A-Za-z][A-Za-z0-9_]*)\"\\s*:\\s*(\"(?:\\\\.|[^\"\\\\])*\"|-?(?:0|[1-9][0-9]*)(?:\\.[0-9]+)?(?:[eE][+-]?[0-9]+)?|true|false|null)");
            string remainder = Regex.Replace(json, "\"([A-Za-z][A-Za-z0-9_]*)\"\\s*:\\s*(\"(?:\\\\.|[^\"\\\\])*\"|-?(?:0|[1-9][0-9]*)(?:\\.[0-9]+)?(?:[eE][+-]?[0-9]+)?|true|false|null)", "");
            if (Regex.Replace(remainder, "[{},\\s]", "").Length != 0)
                throw new InvalidDataException("Tag extractor options must be a flat JSON object.");
            foreach (Match match in matches)
                result[match.Groups[1].Value] = match.Groups[2].Value;
            return result;
        }

        private static string Option(Dictionary<string, string> options, string name, string fallback)
        {
            string value;
            return options.TryGetValue(name, out value) ? value : fallback;
        }

        private static string ExtraOptions(Dictionary<string, string> options, params string[] excluded)
        {
            var result = new StringBuilder();
            foreach (KeyValuePair<string, string> option in options)
            {
                if (excluded.Contains(option.Key)) continue;
                result.Append(',').Append(Q(option.Key)).Append(':').Append(option.Value);
            }
            return result.ToString();
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
            ChatResponse response;
            using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(json)))
                response = (ChatResponse)new DataContractJsonSerializer(typeof(ChatResponse)).ReadObject(stream);
            ChatChoice choice = response?.choices != null && response.choices.Length > 0
                ? response.choices[0] : null;
            Debug(settings, "local response", "choices=" + (response?.choices?.Length ?? -1)
                + ", finish_reason=" + SafeStatus(choice?.finish_reason));
            if (choice?.finish_reason == "length") throw new LocalModelOutputLimitException();
            if (response == null || response.choices == null || response.choices.Length == 0
                || response.choices[0].message == null || response.choices[0].finish_reason != "stop")
                throw new InvalidDataException();
            return response.choices[0].message.content;
        }

        [DataContract] private sealed class TextPart
        {
            [DataMember] public string type = null;
            [DataMember] public string text = null;
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
        [DataContract] private sealed class ChatMessage { [DataMember] public string content = null; }
        [DataContract] private sealed class ChatChoice
        {
            [DataMember] public string finish_reason = null;
            [DataMember] public ChatMessage message = null;
        }
        [DataContract] private sealed class ChatResponse { [DataMember] public ChatChoice[] choices = null; }
        private sealed class LocalModelOutputLimitException : Exception { }
    }
}
