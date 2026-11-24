using System.Text.Json;
using AiAssistant.Application.Memory;
using AiAssistant.Domain;

namespace AiAssistant.Application.Ai;

public sealed partial class PromptContextBuilder
{
    private async Task<string?> MemoryBlockAsync(Guid userId, string question, CancellationToken cancellationToken)
    {
        var terms = MemoryText.Terms(question);
        if (terms.Count == 0) return null;
        var memories = await data.ListAsync(data.Query<UserMemory>()
            .Where(memory => memory.UserId == userId && memory.IsEnabled).OrderByDescending(memory => memory.CreatedAt)
            .ThenByDescending(memory => memory.Id).Select(memory => memory.Content).Take(1000), cancellationToken);
        var relevant = memories.Select(content => new { Content = content, Rank = MemoryText.Terms(content).Count(terms.Contains) })
            .Where(item => item.Rank > 0).OrderByDescending(item => item.Rank).Take(5).Select(item => item.Content).ToArray();
        return relevant.Length == 0 ? null : "Релевантные факты профиля: " + JsonSerializer.Serialize(relevant, PromptJson);
    }

    private static string? AddOptional(List<AiMessage> prompt, string? block, int ceiling, ref int remaining,
        string prefix = "")
    {
        var text = FitOptional(block, ceiling, ref remaining, prefix);
        if (text is not null) prompt.Add(new("user", prefix + text));
        return text;
    }

    private static string? FitOptional(string? block, int ceiling, ref int remaining, string prefix)
    {
        if (string.IsNullOrWhiteSpace(block) || remaining < 80) return null;
        var text = Truncate(block, Math.Min(ceiling, remaining - Cost(prefix)));
        if (text.Length == 0) return null;
        remaining -= Cost(prefix + text);
        return text;
    }
}
