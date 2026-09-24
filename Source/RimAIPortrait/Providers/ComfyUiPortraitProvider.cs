using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.Networking;
using Verse;

namespace RimAIPortrait
{
    internal sealed class ComfyUiPortraitProvider : IPortraitGenerationProvider
    {
        private const string OutputPrefix = "RimAI";
        public AiProvider Id => AiProvider.LocalComfyUI;

        public PortraitProviderConfiguration CreateConfiguration(PortraitSettings settings, bool expression)
        {
            return new ComfyUiProviderConfiguration {
                ServerUrl = expression ? settings.ComfyExpressionUrl : settings.ComfyUrl,
                WorkflowPath = expression ? settings.ComfyExpressionWorkflowPath : settings.ComfyWorkflowPath,
                NegativePrompt = expression ? settings.ComfyExpressionNegativePrompt : settings.ComfyNegativePrompt,
                TimeoutSeconds = settings.ComfyTimeoutSeconds
            };
        }

        public string Describe(PortraitProviderConfiguration configuration)
        {
            ComfyUiProviderConfiguration options = Require(configuration);
            return "server=" + options.ServerUrl + ", workflow=" + options.WorkflowPath;
        }

        public string Validate(PortraitProviderConfiguration configuration, string kind)
        {
            ComfyUiProviderConfiguration options = Require(configuration);
            if (options.ServerUrl.NullOrEmpty()) return Localization.T("RimAIPortrait.Error.ComfyUrlMissing", kind);
            if (options.WorkflowPath.NullOrEmpty() || !File.Exists(options.WorkflowPath))
                return Localization.T("RimAIPortrait.Error.ComfyWorkflowMissing", kind);
            return null;
        }

        public IEnumerator Generate(PortraitGenerationRequest request, Action<PortraitGenerationResult> done)
        {
            ComfyUiProviderConfiguration configuration = Require(request.Configuration);
            string workflow;
            try { workflow = File.ReadAllText(configuration.WorkflowPath); }
            catch (Exception exception)
            {
                done(PortraitGenerationResult.Failed(Localization.T("RimAIPortrait.Error.ComfyWorkflowReadFailed", exception.Message)));
                yield break;
            }

            string baseUrl = (configuration.ServerUrl ?? "").Trim().TrimEnd('/');
            var images = new List<LocalImage>();
            if (request.PrimaryImage != null)
                images.Add(new LocalImage { Data = request.PrimaryImage, Name = "pawn.png", Mime = "image/png" });
            foreach (string file in request.ReferencePaths.Concat(PortraitProviderUtilities.StyleFiles(request.StyleFolder)))
            {
                if (images.Count >= 9) break;
                try { images.Add(new LocalImage { Data = File.ReadAllBytes(file), Name = Path.GetFileName(file), Mime = PortraitProviderUtilities.Mime(file) }); }
                catch (Exception exception) { Log.Warning("[Rim AI Expressive Portraits] Could not read local reference image: " + exception.Message); }
            }

            var uploaded = new List<string>();
            for (int index = 0; index < images.Count; index++)
            {
                string remoteName = "rim_ai_portrait_" + Guid.NewGuid().ToString("N") + Path.GetExtension(images[index].Name);
                string uploadedName = null;
                string uploadError = null;
                yield return UploadImage(baseUrl, images[index], remoteName, (name, error) => { uploadedName = name; uploadError = error; });
                if (uploadedName.NullOrEmpty())
                {
                    done(PortraitGenerationResult.Failed(uploadError));
                    yield break;
                }
                uploaded.Add(uploadedName);
            }

            workflow = workflow.Replace("__RIM_PROMPT__", PortraitProviderUtilities.JsonEscape(request.Prompt))
                .Replace("__RIM_NEGATIVE_PROMPT__", PortraitProviderUtilities.JsonEscape(configuration.NegativePrompt ?? ""))
                .Replace("__RIM_SEED__", new System.Random().Next(1, int.MaxValue).ToString());
            if (uploaded.Count > 0) workflow = workflow.Replace("__RIM_INPUT_IMAGE__", PortraitProviderUtilities.JsonEscape(uploaded[0]));
            for (int index = 1; index <= 8; index++)
            {
                string value = index < uploaded.Count ? uploaded[index] : uploaded.Count > 0 ? uploaded[0] : "";
                workflow = workflow.Replace("__RIM_REFERENCE_" + index + "__", PortraitProviderUtilities.JsonEscape(value));
            }
            if (workflow.Contains("__RIM_INPUT_IMAGE__"))
            {
                done(PortraitGenerationResult.Failed(Localization.T("RimAIPortrait.Error.ComfyInputRequired")));
                yield break;
            }

            string promptId;
            string body = "{\"prompt\":" + workflow + ",\"client_id\":\"rim-ai-portrait-" + Guid.NewGuid().ToString("N") + "\"}";
            using (UnityWebRequest webRequest = JsonRequest(baseUrl + "/prompt", "POST", body, Mathf.CeilToInt(configuration.TimeoutSeconds)))
            {
                yield return webRequest.SendWebRequest();
                if (PortraitProviderUtilities.Failed(webRequest))
                {
                    done(PortraitGenerationResult.Failed(Localization.T("RimAIPortrait.Error.ComfySubmitHttp", webRequest.responseCode, webRequest.error, PortraitProviderUtilities.Short(webRequest.downloadHandler.text))));
                    yield break;
                }
                promptId = PortraitProviderUtilities.JsonString(webRequest.downloadHandler.text, "prompt_id");
                if (promptId.NullOrEmpty())
                {
                    done(PortraitGenerationResult.Failed(Localization.T("RimAIPortrait.Error.ComfyPromptIdMissing", PortraitProviderUtilities.Short(webRequest.downloadHandler.text))));
                    yield break;
                }
            }

            float deadline = Time.realtimeSinceStartup + Mathf.Max(60f, configuration.TimeoutSeconds);
            while (Time.realtimeSinceStartup < deadline)
            {
                yield return new WaitForSecondsRealtime(1f);
                using (UnityWebRequest history = UnityWebRequest.Get(baseUrl + "/history/" + UnityWebRequest.EscapeURL(promptId)))
                {
                    history.timeout = 30;
                    yield return history.SendWebRequest();
                    if (PortraitProviderUtilities.Failed(history))
                    {
                        done(PortraitGenerationResult.Failed(Localization.T("RimAIPortrait.Error.ComfyHistoryHttp", history.responseCode, history.error)));
                        yield break;
                    }
                    string json = history.downloadHandler.text;
                    if (json == "{}" || json.NullOrEmpty()) continue;
                    if (json.Contains("\"status_str\": \"error\"") || json.Contains("\"status_str\":\"error\""))
                    {
                        done(PortraitGenerationResult.Failed(Localization.T("RimAIPortrait.Error.ComfyWorkflowFailed", PortraitProviderUtilities.Short(json))));
                        yield break;
                    }
                    MatchCollection outputs = Regex.Matches(json, "\\\"filename\\\"\\s*:\\s*\\\"([^\\\"]+)\\\"\\s*,\\s*\\\"subfolder\\\"\\s*:\\s*\\\"([^\\\"]*)\\\"\\s*,\\s*\\\"type\\\"\\s*:\\s*\\\"([^\\\"]+)\\\"");
                    if (outputs.Count == 0) continue;
                    Match output = outputs.Cast<Match>().LastOrDefault(candidate => Path.GetFileName(candidate.Groups[1].Value).StartsWith(OutputPrefix, StringComparison.OrdinalIgnoreCase));
                    if (output == null)
                    {
                        if (!json.Contains("\"completed\": true") && !json.Contains("\"completed\":true")) continue;
                        done(PortraitGenerationResult.Failed(Localization.T("RimAIPortrait.Error.ComfyOutputMissing", OutputPrefix)));
                        yield break;
                    }
                    string url = baseUrl + "/view?filename=" + UnityWebRequest.EscapeURL(output.Groups[1].Value)
                        + "&subfolder=" + UnityWebRequest.EscapeURL(output.Groups[2].Value)
                        + "&type=" + UnityWebRequest.EscapeURL(output.Groups[3].Value);
                    using (UnityWebRequest imageRequest = UnityWebRequest.Get(url))
                    {
                        imageRequest.timeout = 120;
                        yield return imageRequest.SendWebRequest();
                        done(PortraitProviderUtilities.Failed(imageRequest)
                            ? PortraitGenerationResult.Failed(Localization.T("RimAIPortrait.Error.ComfyImageHttp", imageRequest.responseCode, imageRequest.error))
                            : PortraitGenerationResult.Succeeded(imageRequest.downloadHandler.data));
                    }
                    yield break;
                }
            }
            done(PortraitGenerationResult.Failed(Localization.T("RimAIPortrait.Error.ComfyTimeout", configuration.TimeoutSeconds.ToString("0"))));
        }

        private static IEnumerator UploadImage(string baseUrl, LocalImage image, string remoteName, Action<string, string> done)
        {
            var parts = new List<IMultipartFormSection> {
                new MultipartFormFileSection("image", image.Data, remoteName, image.Mime),
                new MultipartFormDataSection("type", "input"),
                new MultipartFormDataSection("overwrite", "true")
            };
            using (UnityWebRequest request = UnityWebRequest.Post(baseUrl + "/upload/image", parts))
            {
                request.timeout = 120;
                yield return request.SendWebRequest();
                if (PortraitProviderUtilities.Failed(request))
                    done(null, Localization.T("RimAIPortrait.Error.ComfyUploadHttp", request.responseCode, request.error, PortraitProviderUtilities.Short(request.downloadHandler.text)));
                else
                    done(PortraitProviderUtilities.JsonString(request.downloadHandler.text, "name") ?? remoteName, null);
            }
        }

        private static UnityWebRequest JsonRequest(string url, string method, string json, int timeout)
        {
            var request = new UnityWebRequest(url, method);
            request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
            request.downloadHandler = new DownloadHandlerBuffer();
            request.timeout = timeout;
            request.SetRequestHeader("Content-Type", "application/json");
            return request;
        }

        private static ComfyUiProviderConfiguration Require(PortraitProviderConfiguration configuration)
        {
            ComfyUiProviderConfiguration typed = configuration as ComfyUiProviderConfiguration;
            if (typed == null) throw new ArgumentException("ComfyUI provider requires ComfyUiProviderConfiguration.");
            return typed;
        }

        private sealed class LocalImage
        {
            public byte[] Data;
            public string Name;
            public string Mime;
        }
    }
}
