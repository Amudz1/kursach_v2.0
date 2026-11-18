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

    private static void AddOptional(List<AiMessage> prompt, string? block, int ceiling, ref int remaining)
    {
        if (string.IsNullOrWhiteSpace(block) || remaining < 80) return;
        var text = Truncate(block, Math.Min(ceiling, remaining - MessageOverhead));
        if (text.Length < 10) return;
        prompt.Add(new("user", text));
        remaining -= Cost(text);
    }
}
