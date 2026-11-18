using AiAssistant.Application.Ai;

namespace AiAssistant.Infrastructure.Ai;

public sealed class DemoAiProvider : IAiProvider
{
    public Task<AiReply> CompleteAsync(AiRequest request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var question = request.Messages.Last(message => message.Role == "user").Content;
        var display = question.Length <= 110 ? question : question[..110] + "…";
        var content = $"**Демонстрационный ответ**\n\nВы спросили: {display}\n\nУточните ожидаемый результат, и разберём задачу по шагам.";
        return Task.FromResult(new AiReply(content, new(question.Length / 4, content.Length / 4)));
    }
}
