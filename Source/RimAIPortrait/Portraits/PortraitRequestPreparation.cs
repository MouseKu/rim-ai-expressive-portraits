using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Verse;

namespace RimAIPortrait
{
    internal sealed class InpaintSelection
    {
        public byte[] Source;
        public byte[] Mask;
        public byte[] RequestMask;
        public float Strength;
        public bool NarrowFeather;
        public ExtractedTags Tags;
        public string Error;
    }

    internal sealed class PortraitRequestOptions
    {
        public Pawn Pawn;
        public PortraitState State;
        public byte[] PrimaryImage;
        public IEnumerable<string> ReferencePaths;
        public string ExpressionOverride;
        public PortraitImageUse ImageUse = PortraitImageUse.Auto;
        public InpaintSelection PreparedInpaint;
        public bool Regenerate;
        public string RegeneratePrompt;
    }

    internal sealed class PreparedPortraitRequest
    {
        public AiProvider ProviderId;
        public IPortraitGenerationProvider Provider;
        public PortraitGenerationRequest Request;
        public bool Expression;
        public string Error;
    }

    internal static class PortraitRequestPreparation
    {
        public static PreparedPortraitRequest Create(PortraitSettings settings, PortraitRequestOptions options)
        {
            bool expression = options.State != PortraitState.Base;
            AiProvider providerId = options.Regenerate ? settings.RegenerateProvider
                : expression ? settings.ExpressionProvider : settings.Provider;
            IPortraitGenerationProvider provider = PortraitProviderRegistry.Get(providerId);
            ExtractedTags baseTags = !options.Regenerate && !expression ? PortraitData.BaseTags(options.Pawn) : null;
            bool useExtractedFeatures = baseTags != null && baseTags.positive != null && baseTags.positive.Length > 0
                && (providerId == AiProvider.NovelAI || providerId == AiProvider.LocalComfyUI
                    && WorkflowUsesExtractedTags(settings.ComfyWorkflowPath));
            string prompt = options.Regenerate && (providerId == AiProvider.OpenAI
                || providerId == AiProvider.LocalOpenAI || providerId == AiProvider.Google)
                ? options.RegeneratePrompt : PortraitPrompt.Build(options.Pawn, options.State, settings,
                    options.ExpressionOverride, options.Regenerate ? (AiProvider?)providerId : null, useExtractedFeatures);
            byte[] primaryImage = options.PreparedInpaint == null
                ? options.PrimaryImage : options.PreparedInpaint.Source;
            string[] references = (options.ReferencePaths ?? Enumerable.Empty<string>()).ToArray();
            PortraitImageUse imageUse = options.ImageUse;
            if (expression)
            {
                string basePath = PortraitData.ExistingPathFor(options.Pawn, PortraitState.Base);
                if (primaryImage == null)
                {
                    try
                    {
                        if (!File.Exists(basePath)) throw new FileNotFoundException();
                        primaryImage = File.ReadAllBytes(basePath);
                    }
                    catch (Exception)
                    {
                        return new PreparedPortraitRequest {
                            Error = Localization.T("RimAIPortrait.Gallery.BaseRequired")
                        };
                    }
                }
                references = references.Where(path =>
                    !string.Equals(path, basePath, StringComparison.OrdinalIgnoreCase)).ToArray();
                imageUse = PortraitImageUse.Edit;
            }

            PortraitProviderConfiguration configuration = options.Regenerate
                ? settings.RegenerateConfiguration() : provider.CreateConfiguration(settings, expression);
            string styleFolder = options.Regenerate ? settings.RegenerateStyleFolder
                : expression ? "" : settings.StyleFolder;
            return new PreparedPortraitRequest {
                ProviderId = providerId,
                Provider = provider,
                Expression = expression,
                Request = new PortraitGenerationRequest {
                    State = options.State,
                    Prompt = prompt,
                    PrimaryImage = primaryImage,
                    ImageUse = imageUse,
                    ReferencePaths = references,
                    InpaintStrength = settings.GetInpaintStrength(options.State),
                    StyleFolder = styleFolder,
                    Configuration = configuration
                }
            };
        }

        private static bool WorkflowUsesExtractedTags(string path)
        {
            try { return File.ReadAllText(path).Contains("__RIM_EXTRACTED_TAG__"); }
            catch (Exception) { return false; }
        }

        public static void AddStyleReferences(PreparedPortraitRequest prepared)
        {
            PortraitGenerationRequest request = prepared.Request;
            request.StylePaths = prepared.Provider.StyleReferences(request)
                .Take(prepared.Provider.StyleReferenceCapacity(request)).ToArray();
        }
    }
}
