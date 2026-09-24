// Contains the implementation for Localization.

using Verse;

namespace RimAIPortrait
{
    /// <summary>Describes the behavior of this type or member.</summary>
    internal static class Localization
    {
        /// <summary>Describes the behavior of this type or member.</summary>
        /// <param name="key">The key value.</param>
        /// <param name="args">The args value.</param>
        /// <returns>The resulting value.</returns>
        public static string T(string key, params object[] args)
        {
            var namedArguments = new NamedArgument[args.Length];
            for (int index = 0; index < args.Length; index++)
                namedArguments[index] = new NamedArgument(args[index], null);
            return key.Translate(namedArguments).ToString();
        }
    }
}
