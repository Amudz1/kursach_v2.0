using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.RegularExpressions;
using AiAssistant.Application.Ai;

namespace AiAssistant.Infrastructure.Ai;

public sealed partial class DemoAiProvider : IAiProvider
{
    public Task<AiReply> CompleteAsync(AiRequest request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var question = request.Messages.Last(message => message.Role == "user").Content;
        if (request.Purpose == AiTask.ExtractMemory)
        {
            var original = JsonSerializer.Deserialize<string>(question) ?? "";
            var match = DemoFact().Match(original);
            var facts = match.Success ? new[] { match.Value } : [];
            return Task.FromResult(new AiReply(JsonSerializer.Serialize(facts), null));
        }
        if (request.Purpose == AiTask.Summarize)
            return Task.FromResult(new AiReply("Демо: в ранней части диалога пользователь уточнял исходные условия.", null));
        var display = question.Length <= 110 ? question : question[..110] + "…";
        var profile = request.Messages.FirstOrDefault(message => message.Content.StartsWith("Параметры профиля:", StringComparison.Ordinal));
        var inEnglish = profile?.Content.Contains("\"language\":\"en\"", StringComparison.Ordinal) == true;
        var content = inEnglish
            ? $"**Demo response**\n\nYou asked: {display}\n\nTell me your goal and I can give you a step-by-step example."
            : $"**Демонстрационный ответ**\n\nВы спросили: {display}\n\nУточните ожидаемый результат, и разберём задачу по шагам.";
        return Task.FromResult(new AiReply(content, new(question.Length / 4, content.Length / 4)));
    }

    public async IAsyncEnumerable<AiChunk> StreamAsync(AiRequest request,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var reply = await CompleteAsync(request, cancellationToken);
        var words = reply.Content.Split(' ');
        for (var index = 0; index < words.Length; index++)
        {
            await Task.Delay(70, cancellationToken);
            yield return new(words[index] + (index < words.Length - 1 ? " " : ""));
        }
        yield return new(null, reply.Usage);
    }

    [GeneratedRegex(@"(?i)(?:я изучаю|i am learning) [\p{L}\p{N}+#-]+")]
    private static partial Regex DemoFact();
}
