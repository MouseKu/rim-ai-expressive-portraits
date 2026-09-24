using System;
using System.Collections;
using System.Collections.Generic;

namespace RimAIPortrait
{
    internal sealed class PortraitGenerationRequest
    {
        public string Prompt { get; set; }
        public byte[] PrimaryImage { get; set; }
        public IReadOnlyList<string> ReferencePaths { get; set; }
        public string StyleFolder { get; set; }
        public PortraitProviderConfiguration Configuration { get; set; }
    }

    internal sealed class PortraitGenerationResult
    {
        public byte[] ImageData { get; private set; }
        public string Error { get; private set; }
        public bool Success => ImageData != null && string.IsNullOrEmpty(Error);

        public static PortraitGenerationResult Succeeded(byte[] imageData)
        {
            return new PortraitGenerationResult { ImageData = imageData };
        }

        public static PortraitGenerationResult Failed(string error)
        {
            return new PortraitGenerationResult { Error = error };
        }
    }

    internal abstract class PortraitProviderConfiguration
    {
    }

    internal sealed class OpenAiProviderConfiguration : PortraitProviderConfiguration
    {
        public string Model { get; set; }
        public string ApiKey { get; set; }
        public string OptionsJson { get; set; }
    }

    internal sealed class GoogleProviderConfiguration : PortraitProviderConfiguration
    {
        public string Model { get; set; }
        public string ApiKey { get; set; }
        public string OptionsJson { get; set; }
    }

    internal sealed class ComfyUiProviderConfiguration : PortraitProviderConfiguration
    {
        public string ServerUrl { get; set; }
        public string WorkflowPath { get; set; }
        public string NegativePrompt { get; set; }
        public float TimeoutSeconds { get; set; }
    }

    internal interface IPortraitGenerationProvider
    {
        AiProvider Id { get; }
        PortraitProviderConfiguration CreateConfiguration(PortraitSettings settings, bool expression);
        string Describe(PortraitProviderConfiguration configuration);
        string Validate(PortraitProviderConfiguration configuration, string kind);
        IEnumerator Generate(PortraitGenerationRequest request, Action<PortraitGenerationResult> done);
    }

    internal static class PortraitProviderRegistry
    {
        private static readonly Dictionary<AiProvider, IPortraitGenerationProvider> Providers =
            new Dictionary<AiProvider, IPortraitGenerationProvider>();

        static PortraitProviderRegistry()
        {
            Register(new OpenAiPortraitProvider());
            Register(new GooglePortraitProvider());
            Register(new ComfyUiPortraitProvider());
        }

        public static void Register(IPortraitGenerationProvider provider)
        {
            if (provider == null) throw new ArgumentNullException(nameof(provider));
            Providers[provider.Id] = provider;
        }

        public static IPortraitGenerationProvider Get(AiProvider id)
        {
            IPortraitGenerationProvider provider;
            if (!Providers.TryGetValue(id, out provider))
                throw new InvalidOperationException("No portrait generation provider is registered for " + id + ".");
            return provider;
        }
    }
}
