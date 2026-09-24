using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine.Networking;
using Verse;

namespace RimAIPortrait
{
    internal static class PortraitProviderUtilities
    {
        public static bool Failed(UnityWebRequest request)
        {
            return request.result == UnityWebRequest.Result.ConnectionError
                || request.result == UnityWebRequest.Result.ProtocolError
                || request.result == UnityWebRequest.Result.DataProcessingError;
        }

        public static string Short(string value)
        {
            if (value.NullOrEmpty()) return Localization.T("RimAIPortrait.Error.EmptyResponse");
            return value.Length <= 800 ? value : value.Substring(0, 800);
        }

        public static string JsonString(string json, string field)
        {
            if (json.NullOrEmpty()) return null;
            Match match = Regex.Match(json, "\\\"" + Regex.Escape(field) + "\\\"\\s*:\\s*\\\"([^\\\"]+)\\\"");
            return match.Success ? match.Groups[1].Value.Replace("\\/", "/") : null;
        }

        public static Dictionary<string, string> ParseOptions(string json)
        {
            var result = new Dictionary<string, string>(System.StringComparer.OrdinalIgnoreCase);
            foreach (Match match in Regex.Matches(json ?? "", "\\\"([^\\\"]+)\\\"\\s*:\\s*(?:\\\"([^\\\"]*)\\\"|([^,}\\s]+))"))
                result[match.Groups[1].Value] = match.Groups[2].Success ? match.Groups[2].Value : match.Groups[3].Value;
            return result;
        }

        public static string JsonEscape(string value)
        {
            return (value ?? "").Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\r", "\\r").Replace("\n", "\\n").Replace("\t", "\\t");
        }

        public static IEnumerable<string> StyleFiles(string folder)
        {
            return Directory.Exists(folder)
                ? Directory.EnumerateFiles(folder).Where(file => new[] { ".png", ".jpg", ".jpeg", ".webp" }.Contains(Path.GetExtension(file).ToLowerInvariant()))
                : Enumerable.Empty<string>();
        }

        public static string Mime(string file)
        {
            string extension = Path.GetExtension(file).ToLowerInvariant();
            return extension == ".png" ? "image/png" : extension == ".webp" ? "image/webp" : "image/jpeg";
        }
    }
}
