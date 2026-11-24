using System.Text.RegularExpressions;

namespace AiAssistant.Application.Personalization;

public sealed record TaskPreference(string Task, string? Length, string? Format, string? Instructions);
public sealed record ResponseDecision(string Task, string Length, string Format, bool FromHistory,
    string? Rule, string? Instructions);

public static partial class ResponsePolicy
{
    private static readonly (string Task, Regex Pattern, string Rule)[] Signals =
    [
        ("code", Code(), "Проверь предположения и граничные случаи. При разборе кода указывай конкретные изменения и способы проверки."),
        ("learn", Learn(), "Начни с сути, затем объясни ход мысли на уровне пользователя и приведи уместный пример."),
        ("write", Write(), "Сохрани смысл исходника, учитывай аудиторию и выдели только значимые редакторские правки."),
        ("plan", Plan(), "Упорядочь шаги по зависимостям. Отдели явно заданные ограничения от предположений.")
    ];

    public static bool IsTask(string task) => task is "code" or "learn" or "write" or "plan" or "general";
    public static bool IsLength(string? value) => value is "short" or "medium" or "long";
    public static bool IsFormat(string? value) => value is "text" or "lists" or "code" or "steps";

    public static ResponseDecision Resolve(ProfileView profile, string text, string? previous = null)
    {
        if (profile.AdaptToTask == false)
            return new("general", profile.ResponseLength, profile.ResponseFormat, false, null, null);

        var task = Classify(text);
        var fromHistory = false;
        if (task == "general" && previous is not null && text.Length <= 120 && Continuation().IsMatch(text))
        {
            task = Classify(previous);
            fromHistory = task != "general";
        }

        var preference = profile.TaskRules?.FirstOrDefault(item => item.Task == task);
        return new(task, preference?.Length ?? profile.ResponseLength, preference?.Format ?? profile.ResponseFormat,
            fromHistory, Signals.FirstOrDefault(signal => signal.Task == task).Rule, preference?.Instructions);
    }

    private static string Classify(string text)
    {
        if (text.Contains("```", StringComparison.Ordinal)) return "code";
        var best = "general";
        var score = 0;
        foreach (var signal in Signals)
        {
            var matches = signal.Pattern.Matches(text).Select(match => match.Value.ToLowerInvariant()).Distinct().Count();
            if (matches <= score) continue;
            best = signal.Task;
            score = matches;
        }
        return best;
    }

    [GeneratedRegex(@"\bc#(?!\w)|\b(?:код\w*|функци\w*|рефактор\w*|отлад\w*|баг\w*|компил\w*|sql|python|javascript|debug\w*|bug|code|api)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex Code();
    [GeneratedRegex(@"\b(?:объясн\w*|научи\w*|изуч\w*|разбер\w*|пример\w*|урок\w*|explain|learn\w*|teach\w*|tutorial)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex Learn();
    [GeneratedRegex(@"\b(?:перепиш\w*|отредакт\w*|текст\w*|письм\w*|стать\w*|тон|rewrite|draft|proofread|editing|article|copywriting)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex Write();
    [GeneratedRegex(@"\b(?:план\w*|этап\w*|дорожн\w*|расписан\w*|приоритет\w*|roadmap|plan\w*|schedule|milestone\w*)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex Plan();
    [GeneratedRegex(@"^\s*(?:а\s+|ещ[её]\s|почему\b|продолж\w*\b|подробнее\b|дальше\b|why\b|continue\b|what about\b)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex Continuation();
}
