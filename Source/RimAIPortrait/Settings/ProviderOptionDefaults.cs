using System;
using System.IO;
using System.Text;
using Verse;

namespace RimAIPortrait
{
    public static class ProviderOptionDefaults
    {
        public static string OpenAiBase = "{}";
        public static string OpenAiExpression = "{}";
        public static string GoogleBase = "{}";
        public static string GoogleExpression = "{}";
        public static string NovelAiBase = "{}";
        public static string NovelAiExpression = "{}";

        public static void Load(ModContentPack content)
        {
            OpenAiBase = Read(content, "openai-base-options.json");
            OpenAiExpression = Read(content, "openai-expression-options.json");
            GoogleBase = Read(content, "google-base-options.json");
            GoogleExpression = Read(content, "google-expression-options.json");
            NovelAiBase = Read(content, "nai-base-options.json");
            NovelAiExpression = Read(content, "nai-expression-options.json");
        }

        private static string Read(ModContentPack content, string fileName)
        {
            try
            {
                string value = File.ReadAllText(Path.Combine(content.RootDir, "Json", fileName));
                var result = new StringBuilder(value.Length);
                bool quoted = false;
                bool escaped = false;
                foreach (char character in value)
                {
                    if (quoted)
                    {
                        result.Append(character);
                        if (escaped) escaped = false;
                        else if (character == '\\') escaped = true;
                        else if (character == '"') quoted = false;
                    }
                    else if (character == '"')
                    {
                        quoted = true;
                        result.Append(character);
                    }
                    else if (!char.IsWhiteSpace(character)) result.Append(character);
                }
                if (result.Length == 0 || quoted) throw new FormatException("Empty or incomplete JSON");
                return result.ToString();
            }
            catch (Exception exception)
            {
                Log.Warning("[Rim AI Expressive Portraits] Could not load Json/" + fileName + ": " + exception.Message);
                return "{}";
            }
        }
    }
}
