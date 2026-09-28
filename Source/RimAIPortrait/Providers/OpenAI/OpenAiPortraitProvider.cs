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
    internal sealed class OpenAiPortraitProvider : IPortraitGenerationProvider
    {
        private const int MaximumStyleReferences = 8;
        private const int MaximumInputImages = 16;
        public AiProvider Id => AiProvider.OpenAI;

        public IReadOnlyList<string> StyleReferences(PortraitGenerationRequest request)
        {
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
            return new OpenAiProviderConfiguration {
                Model = expression ? settings.OpenAiExpressionModel : settings.OpenAiModel,
                ApiKey = expression ? settings.ExpressionApiKey : settings.ApiKey,
                OptionsJson = expression ? settings.OpenAiExpressionOptions : settings.OpenAiOptions
            };
        }

        public string Describe(PortraitProviderConfiguration configuration)
        {
            OpenAiProviderConfiguration options = Require(configuration);
            return "model=" + options.Model + ", options=" + options.OptionsJson;
        }

        public string Validate(PortraitProviderConfiguration configuration, string kind)
        {
            OpenAiProviderConfiguration options = Require(configuration);
            if (options.ApiKey.NullOrEmpty()) return Localization.T("RimAIPortrait.Error.ApiKeyMissing", kind);
            if (options.Model.NullOrEmpty()) return Localization.T("RimAIPortrait.Error.OpenAiModelMissing", kind);
            return null;
        }

        public IEnumerator Generate(PortraitGenerationRequest request, Action<PortraitGenerationResult> done)
        {
            OpenAiProviderConfiguration configuration = Require(request.Configuration);
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
            foreach (string file in request.StylePaths.Take(MaximumStyleReferences))
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
                if (option.Key != "model" && option.Key != "prompt" && option.Key != "output_format" && option.Key != "mask")
                    parts.Add(new MultipartFormDataSection(option.Key, option.Key == "size" ? NormalizeSize(option.Value) : option.Value));
            parts.AddRange(imageParts);
            using (UnityWebRequest webRequest = UnityWebRequest.Post("https://api.openai.com/v1/images/edits", parts))
            {
                webRequest.timeout = 300;
                webRequest.SetRequestHeader("Authorization", "Bearer " + configuration.ApiKey);
                yield return webRequest.SendWebRequest();
                if (PortraitProviderUtilities.Failed(webRequest))
                {
                    done(PortraitGenerationResult.Failed(Localization.T("RimAIPortrait.Error.ProviderHttp", "OpenAI", webRequest.responseCode, webRequest.error, PortraitProviderUtilities.Short(webRequest.downloadHandler.text))));
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
                        imageRequest.timeout = 120;
                        yield return imageRequest.SendWebRequest();
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
