using System.Text.RegularExpressions;
using AiAssistant.Application.Ai;
using AiAssistant.Application.Common;
using AiAssistant.Application.Persistence;
using AiAssistant.Application.Personalization;
using AiAssistant.Domain;

namespace AiAssistant.Application.Chats;

public sealed record QueuedMessage(Guid Id, bool IsNew);

public sealed partial class ChatSubmission(IAssistantData data, ChatAccess access, PersonaAccess personas, TimeProvider clock)
{
    public async Task<Result<QueuedMessage>> PrepareAsync(SendChatMessage request, CancellationToken cancellationToken)
    {
        var text = request.Content.Trim();
        if (text.Length is 0 or > 10000)
            return Result<QueuedMessage>.Fail(ErrorKind.Validation, "invalid_message", "Сообщение должно содержать от 1 до 10 000 символов.");

        return await data.ForUserAsync(request.UserId, async ct =>
        {
            var chat = await access.FindAsync(request.UserId, request.ChatId, true, ct);
            if (chat is null) return ChatAccess.NotFound<QueuedMessage>();
            var messages = data.Query<Message>().Where(message => message.ChatId == chat.Id);
            var prior = await data.FirstAsync(messages.Where(message => message.RequestId == request.RequestId), ct);
            if (prior is not null) return Result<QueuedMessage>.Ok(new(prior.Id, false));
            if (await data.AnyAsync(messages.Where(message =>
                    message.Status == GenerationStatus.Pending || message.Status == GenerationStatus.Streaming), ct))
            {
                return Result<QueuedMessage>.Fail(ErrorKind.Conflict, "generation_active", "Дождитесь текущего ответа или остановите его.");
            }

            if (!await data.AnyAsync(messages, ct) && chat.Title == "Новый чат") chat.Title = Title(text);
            var now = clock.GetUtcNow();
            var persona = await personas.ResolveAsync(request.UserId, chat.PersonaId, ct);
            var userMessage = new Message { ChatId = chat.Id, Role = MessageRole.User, Content = text, CreatedAt = now };
            var assistant = new Message
            {
                ChatId = chat.Id, PersonaId = persona?.Id, Role = MessageRole.Assistant, Content = "", Model = personas.ModelFor(persona),
                Status = GenerationStatus.Pending, CreatedAt = now.AddMicroseconds(1),
                TriggerMessageId = userMessage.Id, RequestId = request.RequestId
            };
            chat.LastMessageAt = assistant.CreatedAt;
            data.Add(userMessage);
            data.Add(assistant);
            await data.SaveAsync(ct);
            return Result<QueuedMessage>.Ok(new(assistant.Id, true));
        }, cancellationToken);
    }

    private static string Title(string content)
    {
        var title = string.Join(' ', Spaces().Split(content).Take(6));
        return title.Length <= 65 ? title : title[..64] + "…";
    }

    [GeneratedRegex(@"\s+")]
    private static partial Regex Spaces();
}
