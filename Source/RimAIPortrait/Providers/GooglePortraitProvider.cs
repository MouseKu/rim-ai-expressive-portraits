using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;
using Verse;

namespace RimAIPortrait
{
    internal sealed class GooglePortraitProvider : IPortraitGenerationProvider
    {
        public AiProvider Id => AiProvider.Google;

        public PortraitProviderConfiguration CreateConfiguration(PortraitSettings settings, bool expression)
        {
            return new GoogleProviderConfiguration {
                Model = expression ? settings.GoogleExpressionModel : settings.GoogleModel,
                ApiKey = expression ? settings.ExpressionApiKey : settings.ApiKey,
                OptionsJson = expression ? settings.GoogleExpressionOptions : settings.GoogleOptions
            };
        }

        public string Describe(PortraitProviderConfiguration configuration)
        {
            GoogleProviderConfiguration options = Require(configuration);
            return "model=" + options.Model + ", options=" + options.OptionsJson;
        }

        public string Validate(PortraitProviderConfiguration configuration, string kind)
        {
            GoogleProviderConfiguration options = Require(configuration);
            if (options.ApiKey.NullOrEmpty()) return Localization.T("RimAIPortrait.Error.ApiKeyMissing", kind);
            if (options.Model.NullOrEmpty()) return Localization.T("RimAIPortrait.Error.GoogleModelMissing", kind);
            return null;
        }

        public IEnumerator Generate(PortraitGenerationRequest request, Action<PortraitGenerationResult> done)
        {
            GoogleProviderConfiguration configuration = Require(request.Configuration);
            Dictionary<string, string> options = PortraitProviderUtilities.ParseOptions(configuration.OptionsJson);
            string aspectRatio = options.ContainsKey("aspect_ratio") ? options["aspect_ratio"] : "1:1";
            string configuredImageSize = options.ContainsKey("image_size") ? options["image_size"] : "1K";
            string imageSize = SupportsImageSize(configuration.Model) ? configuredImageSize : null;
            var pieces = new List<GooglePart> { new GooglePart { text = request.Prompt } };
            if (request.PrimaryImage != null)
                pieces.Add(ImagePart(request.PrimaryImage, "image/png"));
            foreach (string file in request.ReferencePaths.Take(8))
                try { pieces.Add(ImagePart(File.ReadAllBytes(file), PortraitProviderUtilities.Mime(file))); } catch { }
            foreach (string file in PortraitProviderUtilities.StyleFiles(request.StyleFolder).Take(8))
                try { pieces.Add(ImagePart(File.ReadAllBytes(file), PortraitProviderUtilities.Mime(file))); } catch { }

            string body = BuildRequestJson(pieces, aspectRatio, imageSize);
            string url = "https://generativelanguage.googleapis.com/v1beta/models/" + UnityWebRequest.EscapeURL(configuration.Model) + ":generateContent";
            using (var webRequest = new UnityWebRequest(url, "POST"))
            {
                webRequest.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(body));
                webRequest.downloadHandler = new DownloadHandlerBuffer();
                webRequest.timeout = 300;
                webRequest.SetRequestHeader("Content-Type", "application/json");
                webRequest.SetRequestHeader("x-goog-api-key", configuration.ApiKey);
                yield return webRequest.SendWebRequest();
                if (PortraitProviderUtilities.Failed(webRequest))
                {
                    done(PortraitGenerationResult.Failed(Localization.T("RimAIPortrait.Error.ProviderHttp", "Google", webRequest.responseCode, webRequest.error, PortraitProviderUtilities.Short(webRequest.downloadHandler.text))));
                    yield break;
                }

                string json = webRequest.downloadHandler.text;
                GoogleResponse response = JsonUtility.FromJson<GoogleResponse>(json);
                string data = response?.candidates?.SelectMany(candidate => candidate.content?.parts ?? new GooglePart[0])
                    .FirstOrDefault(part => part.inlineData != null)?.inlineData?.data;
                if (data.NullOrEmpty()) data = PortraitProviderUtilities.JsonString(json, "data");
                if (data.NullOrEmpty())
                {
                    string responseText = PortraitProviderUtilities.JsonString(json, "text");
                    string reason = PortraitProviderUtilities.JsonString(json, "finishReason");
                    string detail = (reason.NullOrEmpty() ? "" : "finishReason=" + reason + " ")
                        + (responseText.NullOrEmpty() ? PortraitProviderUtilities.Short(json) : responseText);
                    done(PortraitGenerationResult.Failed(Localization.T("RimAIPortrait.Error.NoImageResponse", detail)));
                    yield break;
                }
                try { done(PortraitGenerationResult.Succeeded(Convert.FromBase64String(data))); }
                catch (Exception exception) { done(PortraitGenerationResult.Failed(Localization.T("RimAIPortrait.Error.ImageDecodeFailed", exception.Message))); }
            }
        }

        private static GooglePart ImagePart(byte[] data, string mime)
        {
            return new GooglePart { inline_data = new InlineData { mime_type = mime, data = Convert.ToBase64String(data) } };
        }

        private static bool SupportsImageSize(string model)
        {
            return !model.NullOrEmpty() && model.IndexOf("gemini-3", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static string BuildRequestJson(IEnumerable<GooglePart> pieces, string aspectRatio, string imageSize)
        {
            var jsonParts = new List<string>();
            foreach (GooglePart piece in pieces)
            {
                if (!piece.text.NullOrEmpty()) jsonParts.Add("{\"text\":\"" + PortraitProviderUtilities.JsonEscape(piece.text) + "\"}");
                InlineData image = piece.inline_data ?? piece.inlineData;
                if (image != null && !image.data.NullOrEmpty())
                {
                    string mime = !image.mime_type.NullOrEmpty() ? image.mime_type : image.mimeType;
                    jsonParts.Add("{\"inlineData\":{\"mimeType\":\"" + PortraitProviderUtilities.JsonEscape(mime) + "\",\"data\":\"" + PortraitProviderUtilities.JsonEscape(image.data) + "\"}}");
                }
            }
            string imageConfig = "{\"aspectRatio\":\"" + PortraitProviderUtilities.JsonEscape(aspectRatio) + "\"";
            if (!imageSize.NullOrEmpty()) imageConfig += ",\"imageSize\":\"" + PortraitProviderUtilities.JsonEscape(imageSize) + "\"";
            imageConfig += "}";
            return "{\"contents\":[{\"parts\":[" + string.Join(",", jsonParts.ToArray())
                + "]}],\"generationConfig\":{\"responseModalities\":[\"TEXT\",\"IMAGE\"],\"imageConfig\":" + imageConfig + "}}";
        }

        private static GoogleProviderConfiguration Require(PortraitProviderConfiguration configuration)
        {
            GoogleProviderConfiguration typed = configuration as GoogleProviderConfiguration;
            if (typed == null) throw new ArgumentException("Google provider requires GoogleProviderConfiguration.");
            return typed;
        }

        [Serializable] private sealed class GoogleContent { public GooglePart[] parts = null; }
        [Serializable] private sealed class GooglePart { public string text = null; public InlineData inline_data = null; public InlineData inlineData = null; }
        [Serializable] private sealed class InlineData { public string mime_type = null; public string mimeType = null; public string data = null; }
        [Serializable] private sealed class GoogleResponse { public GoogleCandidate[] candidates = null; }
        [Serializable] private sealed class GoogleCandidate { public GoogleContent content = null; }
    }
}
