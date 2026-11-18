using System.Text.RegularExpressions;

namespace AiAssistant.Application.Memory;

internal static partial class MemoryText
{
    private static readonly HashSet<string> StopWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "который", "приведи", "почему", "можно", "свой", "чтобы", "помоги", "объясни", "такое",
        "про", "это", "есть", "мне", "для", "как", "what", "with", "about", "please", "explain", "the"
    };

    public static string Normalize(string text) => Spaces().Replace(text.Trim(), " ").ToUpperInvariant();

    public static HashSet<string> Terms(string text) => Words().Matches(text).Select(match => match.Value)
        .Where(value => (value.Length >= 3 || value.Contains('#')) && !StopWords.Contains(value))
        .Take(60).ToHashSet(StringComparer.OrdinalIgnoreCase);

    public static bool IsSensitive(string text) => CredentialTerms().IsMatch(text);

    [GeneratedRegex(@"\s+")]
    private static partial Regex Spaces();

    [GeneratedRegex(@"[\p{L}\p{N}+#]+")]
    private static partial Regex Words();

    [GeneratedRegex(@"(?i)парол|секрет|\bpassword\b|\bpasscode\b|\bsecret\b|токен|\btoken\b|api[ _-]?key|private key|sk-[a-z0-9_-]{10,}|\b\d{13,19}\b")]
    private static partial Regex CredentialTerms();
}
