using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.Networking;
using Verse;

namespace RimAIPortrait
{
    internal sealed class OpenAiPortraitProvider : IPortraitGenerationProvider
    {
        private const int MaximumStyleReferences = 8;
        private const int MaximumInputImages = 16;
        private readonly AiProvider id;
        public AiProvider Id => id;

        public OpenAiPortraitProvider(AiProvider id = AiProvider.OpenAI)
        {
            this.id = id;
        }

        public IReadOnlyList<string> StyleReferences(PortraitGenerationRequest request)
        {
            if (PortraitProviderUtilities.UsesDefaultStyle(request.StyleFolder))
                return new string[0];
            return PortraitProviderUtilities.StyleFiles(request.StyleFolder)
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase).ToArray();
        }

        public int StyleReferenceCapacity(PortraitGenerationRequest request)
        {
            int remaining = MaximumInputImages - (request.PrimaryImage == null ? 0 : 1)
                - Math.Min(MaximumStyleReferences, request.ReferencePaths.Count);
            return Math.Max(0, Math.Min(MaximumStyleReferences, remaining));
        }

        public PortraitProviderConfiguration CreateConfiguration(PortraitSettings settings, bool expression)
        {
            if (Id == AiProvider.LocalOpenAI)
                return new OpenAiProviderConfiguration {
                    Model = expression ? settings.LocalOpenAiExpressionModel : settings.LocalOpenAiModel,
                    ApiKey = expression ? settings.LocalOpenAiExpressionApiKey : settings.LocalOpenAiApiKey,
                    OptionsJson = expression ? settings.LocalOpenAiExpressionOptions : settings.LocalOpenAiOptions,
                    BaseUrl = expression ? settings.LocalOpenAiExpressionUrl : settings.LocalOpenAiUrl,
                    TimeoutSeconds = settings.LocalOpenAiTimeoutSeconds
                };
            return new OpenAiProviderConfiguration {
                Model = expression ? settings.OpenAiExpressionModel : settings.OpenAiModel,
                ApiKey = expression ? settings.ExpressionApiKey : settings.ApiKey,
                OptionsJson = expression ? settings.OpenAiExpressionOptions : settings.OpenAiOptions
            };
        }

        public string Describe(PortraitProviderConfiguration configuration)
        {
            OpenAiProviderConfiguration options = Require(configuration);
            return "model=" + options.Model + ", server=" + (Id == AiProvider.LocalOpenAI ? options.BaseUrl : "OpenAI")
                + ", options=" + options.OptionsJson;
        }

        public string Validate(PortraitProviderConfiguration configuration, string kind)
        {
            OpenAiProviderConfiguration options = Require(configuration);
            if (Id == AiProvider.LocalOpenAI && !ValidLocalUrl(options.BaseUrl))
                return Localization.T("RimAIPortrait.Error.LocalOpenAiUrlInvalid", kind);
            if (Id == AiProvider.OpenAI && options.ApiKey.NullOrEmpty())
                return Localization.T("RimAIPortrait.Error.ApiKeyMissing", kind);
            if (options.Model.NullOrEmpty()) return Localization.T(Id == AiProvider.LocalOpenAI
                ? "RimAIPortrait.Error.LocalOpenAiModelMissing"
                : "RimAIPortrait.Error.OpenAiModelMissing", kind);
            return null;
        }

        private static bool ValidLocalUrl(string value)
        {
            Uri uri;
            return Uri.TryCreate(value, UriKind.Absolute, out uri)
                && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps)
                && string.IsNullOrEmpty(uri.UserInfo) && string.IsNullOrEmpty(uri.Query)
                && string.IsNullOrEmpty(uri.Fragment)
                && uri.AbsolutePath.TrimEnd('/').EndsWith("/v1", StringComparison.OrdinalIgnoreCase);
        }

        public IEnumerator Generate(PortraitGenerationRequest request, Action<PortraitGenerationResult> done)
        {
            OpenAiProviderConfiguration configuration = Require(request.Configuration);
            if (Id == AiProvider.LocalOpenAI && !ValidLocalUrl(configuration.BaseUrl))
            {
                done(PortraitGenerationResult.Failed(Localization.T("RimAIPortrait.Error.LocalOpenAiUrlInvalid",
                    Localization.T("RimAIPortrait.Settings.LocalOpenAi"))));
                yield break;
            }
            string baseUrl = Id == AiProvider.LocalOpenAI ? configuration.BaseUrl.Trim().TrimEnd('/')
                : "https://api.openai.com/v1";
            Dictionary<string, string> options = PortraitProviderUtilities.ParseOptions(configuration.OptionsJson);
            PortraitImageCaptions captions;
            try { captions = PortraitImageCaptions.For(Id); }
            catch (Exception exception)
            {
                done(PortraitGenerationResult.Failed(Localization.T("RimAIPortrait.Error.ImageCaptionLoadFailed", exception.Message)));
                yield break;
            }
            var imageParts = new List<IMultipartFormSection>();
            var imageCaptions = new StringBuilder();
            int imageNumber = 0;
            bool hasStyleReference = false;
            if (request.PrimaryImage != null)
            {
                imageParts.Add(new MultipartFormFileSection("image[]", request.PrimaryImage, "pawn.png", "image/png"));
                imageCaptions.AppendLine(string.Format(captions.Primary(request.ImageUse), ++imageNumber));
            }
            foreach (string file in request.ReferencePaths.Take(8))
            {
                try
                {
                    imageParts.Add(new MultipartFormFileSection("image[]", File.ReadAllBytes(file), Path.GetFileName(file), PortraitProviderUtilities.Mime(file)));
                    imageCaptions.AppendLine(string.Format(captions.selectedReference, ++imageNumber));
                }
                catch (Exception) { }
            }
            IEnumerable<string> stylePaths = PortraitProviderUtilities.UsesDefaultStyle(request.StyleFolder)
                ? Enumerable.Empty<string>() : request.StylePaths;
            foreach (string file in stylePaths.Take(MaximumStyleReferences))
            {
                try
                {
                    imageParts.Add(new MultipartFormFileSection("image[]", File.ReadAllBytes(file), Path.GetFileName(file), PortraitProviderUtilities.Mime(file)));
                    imageCaptions.AppendLine(string.Format(captions.styleReference, ++imageNumber));
                    hasStyleReference = true;
                }
                catch (Exception) { }
            }
            string prompt = request.Prompt;
            if (Id == AiProvider.LocalOpenAI && request.ExtractedTags != null)
            {
                if (!string.IsNullOrWhiteSpace(request.ExtractedTags.PositiveText))
                    prompt += "\n\n[positive_tag]\n" + request.ExtractedTags.PositiveText;
                if (!string.IsNullOrWhiteSpace(request.ExtractedTags.NegativeText))
                    prompt += "\n\n[negative_tag]\n" + request.ExtractedTags.NegativeText;
            }
            if (Id == AiProvider.LocalOpenAI && request.HasInpaintSelection)
                prompt += "\n\n[weight]\n" + request.InpaintStrength.ToString("R", CultureInfo.InvariantCulture);
            if (imageCaptions.Length > 0)
            {
                prompt += "\n\n[Image references]\n" + imageCaptions;
                if (hasStyleReference) prompt += captions.priority;
            }
            var parts = new List<IMultipartFormSection> {
                new MultipartFormDataSection("model", configuration.Model),
                new MultipartFormDataSection("prompt", prompt),
                new MultipartFormDataSection("output_format", "png")
            };
            foreach (KeyValuePair<string, string> option in options)
                if (!ReservedOption(option.Key))
                    parts.Add(new MultipartFormDataSection(option.Key,
                        option.Key == "size" && Id == AiProvider.OpenAI ? NormalizeSize(option.Value) : option.Value));
            parts.AddRange(imageParts);
            if (Id == AiProvider.LocalOpenAI && request.InpaintMask != null)
            {
                byte[] mask;
                try { mask = PortraitInpaintMask.ForOpenAi(request.InpaintMask); }
                catch (Exception exception)
                {
                    done(PortraitGenerationResult.Failed(exception.Message));
                    yield break;
                }
                parts.Add(new MultipartFormFileSection("mask", mask, "mask.png", "image/png"));
            }
            string url = baseUrl + (Id == AiProvider.LocalOpenAI && imageParts.Count == 0
                ? "/images/generations" : "/images/edits");
            bool textOnly = Id == AiProvider.LocalOpenAI && imageParts.Count == 0;
            string jsonBody = textOnly ? TextOnlyBody(configuration.Model, prompt, options) : null;
            Dialog_DebugRequest.Trace trace = textOnly
                ? Dialog_DebugRequest.Show("Local", url, jsonBody)
                : Dialog_DebugRequest.ShowMultipart(Id == AiProvider.LocalOpenAI ? "Local" : "OpenAI", url, parts);
            using (UnityWebRequest webRequest = textOnly ? JsonRequest(url, jsonBody) : UnityWebRequest.Post(url, parts))
            {
                webRequest.timeout = Id == AiProvider.LocalOpenAI
                    ? Mathf.CeilToInt(Mathf.Clamp(configuration.TimeoutSeconds, 60f, 3600f)) : 300;
                if (!configuration.ApiKey.NullOrEmpty())
                    webRequest.SetRequestHeader("Authorization", "Bearer " + configuration.ApiKey);
                yield return webRequest.SendWebRequest();
                if (trace != null) trace.Respond(webRequest);
                if (PortraitProviderUtilities.Failed(webRequest))
                {
                    done(PortraitGenerationResult.Failed(Localization.T("RimAIPortrait.Error.ProviderHttp",
                        Id == AiProvider.LocalOpenAI ? Localization.T("RimAIPortrait.Settings.LocalOpenAi") : "OpenAI",
                        webRequest.responseCode, webRequest.error, PortraitProviderUtilities.Short(webRequest.downloadHandler.text))));
                    yield break;
                }

                string json = webRequest.downloadHandler.text;
                OpenAiResponse response = JsonUtility.FromJson<OpenAiResponse>(json);
                string encoded = response?.data != null && response.data.Length > 0 ? response.data[0].b64_json : null;
                if (encoded.NullOrEmpty()) encoded = PortraitProviderUtilities.JsonString(json, "b64_json");
                if (encoded.NullOrEmpty()) encoded = PortraitProviderUtilities.JsonString(json, "result");
                if (!encoded.NullOrEmpty())
                {
                    try { done(PortraitGenerationResult.Succeeded(Convert.FromBase64String(encoded))); }
                    catch (Exception exception) { done(PortraitGenerationResult.Failed(Localization.T("RimAIPortrait.Error.ImageDecodeFailed", exception.Message))); }
                    yield break;
                }

                string imageUrl = response?.data != null && response.data.Length > 0 ? response.data[0].url : null;
                if (imageUrl.NullOrEmpty()) imageUrl = PortraitProviderUtilities.JsonString(json, "url");
                if (!imageUrl.NullOrEmpty())
                {
                    using (UnityWebRequest imageRequest = UnityWebRequest.Get(imageUrl))
                    {
                        Dialog_DebugRequest.Trace imageTrace = Dialog_DebugRequest.ShowGet("OpenAI image", imageUrl);
                        imageRequest.timeout = 120;
                        yield return imageRequest.SendWebRequest();
                        if (imageTrace != null) imageTrace.Respond(imageRequest);
                        done(PortraitProviderUtilities.Failed(imageRequest)
                            ? PortraitGenerationResult.Failed(Localization.T("RimAIPortrait.Error.ImageDownloadHttp", imageRequest.responseCode, imageRequest.error))
                            : PortraitGenerationResult.Succeeded(imageRequest.downloadHandler.data));
                    }
                    yield break;
                }
                done(PortraitGenerationResult.Failed(Localization.T("RimAIPortrait.Error.NoImageResponse", PortraitProviderUtilities.Short(json))));
            }
        }

        public static string NormalizeSize(string value)
        {
            string size = (value ?? "").Trim().ToLowerInvariant().Replace(" ", "").Replace('×', 'x');
            if (size == "1024") size = "1024x1024";
            return size == "1024x1024" || size == "1024x1536" || size == "1536x1024" || size == "auto" ? size : "1024x1024";
        }

        private static string TextOnlyBody(string model, string prompt, Dictionary<string, string> options)
        {
            var body = new StringBuilder("{\"model\":\"");
            body.Append(PortraitProviderUtilities.JsonEscape(model));
            body.Append("\",\"prompt\":\"");
            body.Append(PortraitProviderUtilities.JsonEscape(prompt));
            body.Append("\",\"output_format\":\"png\"");
            foreach (KeyValuePair<string, string> option in options)
            {
                if (ReservedOption(option.Key)) continue;
                body.Append(",\"").Append(PortraitProviderUtilities.JsonEscape(option.Key)).Append("\":");
                if (option.Value == "true" || option.Value == "false"
                    || Regex.IsMatch(option.Value ?? "", "^-?(?:0|[1-9][0-9]*)(?:\\.[0-9]+)?(?:[eE][+-]?[0-9]+)?$"))
                    body.Append(option.Value);
                else
                    body.Append('"').Append(PortraitProviderUtilities.JsonEscape(option.Value)).Append('"');
            }
            return body.Append('}').ToString();
        }

        private static bool ReservedOption(string key)
        {
            return string.Equals(key, "model", StringComparison.OrdinalIgnoreCase)
                || string.Equals(key, "prompt", StringComparison.OrdinalIgnoreCase)
                || string.Equals(key, "output_format", StringComparison.OrdinalIgnoreCase)
                || string.Equals(key, "mask", StringComparison.OrdinalIgnoreCase)
                || string.Equals(key, "image", StringComparison.OrdinalIgnoreCase)
                || string.Equals(key, "image[]", StringComparison.OrdinalIgnoreCase)
                || string.Equals(key, "stream", StringComparison.OrdinalIgnoreCase);
        }

        private static UnityWebRequest JsonRequest(string url, string body)
        {
            var request = new UnityWebRequest(url, "POST");
            request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(body));
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");
            return request;
        }

        private static OpenAiProviderConfiguration Require(PortraitProviderConfiguration configuration)
        {
            OpenAiProviderConfiguration typed = configuration as OpenAiProviderConfiguration;
            if (typed == null) throw new ArgumentException("OpenAI provider requires OpenAiProviderConfiguration.");
            return typed;
        }

        [Serializable] private sealed class OpenAiResponse { public OpenAiImage[] data = null; }
        [Serializable] private sealed class OpenAiImage { public string b64_json = null; public string url = null; }
    }
}
