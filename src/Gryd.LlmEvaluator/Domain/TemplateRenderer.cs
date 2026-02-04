using System.Text.RegularExpressions;

namespace Gryd.LlmEvaluator.Domain;

public static class TemplateRenderer
{
    private static readonly Regex TokenRegex = new(@"\{\{\s*(?<key>[^\}]+)\s*\}\}", RegexOptions.Compiled);

    public static string Render(string template, IReadOnlyDictionary<string, string> vars)
    {
        return TokenRegex.Replace(template, match =>
        {
            var key = match.Groups["key"].Value.Trim();
            return vars.TryGetValue(key, out var value) ? value : string.Empty;
        });
    }
}
