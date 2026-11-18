using AiAssistant.Application.Common;
using AiAssistant.Domain;

namespace AiAssistant.Application.Chats;

public sealed partial class ChatSubmission
{
    public Task<Result<Guid>> PrepareRevisionAsync(ReviseChatMessage request, CancellationToken cancellationToken)
    {
        var replacement = request.Content?.Trim();
        if (request.IsEdit && (string.IsNullOrEmpty(replacement) || replacement.Length > 10000))
            return Task.FromResult(Result<Guid>.Fail(ErrorKind.Validation, "invalid_message", "Введите от 1 до 10 000 символов."));

        return data.ForUserAsync(request.UserId, async ct =>
        {
            var target = await data.FirstAsync(data.Query<Message>(true)
                .Where(message => message.Id == request.MessageId), ct);
            if (target is null) return ChatAccess.NotFound<Guid>();
            var chat = await access.FindAsync(request.UserId, target.ChatId, true, ct);
            if (chat is null) return ChatAccess.NotFound<Guid>();

            var query = data.Query<Message>(true).Where(message => message.ChatId == chat.Id);
            var duplicate = await data.FirstAsync(query.Where(message => message.RequestId == request.RequestId), ct);
            if (duplicate is not null) return Result<Guid>.Ok(duplicate.Id);
            var latest = await data.ListAsync(query.Where(message => message.Status != GenerationStatus.Superseded)
                .OrderByDescending(message => message.CreatedAt).ThenByDescending(message => message.Id).Take(2), ct);
            var assistant = latest.FirstOrDefault(message => message.Role == MessageRole.Assistant);
            var user = latest.FirstOrDefault(message => message.Role == MessageRole.User);
            if (user is null || (request.IsEdit ? user.Id != target.Id : assistant?.Id != target.Id))
                return Result<Guid>.Fail(ErrorKind.Conflict, "not_latest_message", "Изменить можно только последнее сообщение.");
            if (assistant?.Status is GenerationStatus.Pending or GenerationStatus.Streaming)
                return Result<Guid>.Fail(ErrorKind.Conflict, "generation_active", "Сначала остановите текущую генерацию.");

            if (assistant is not null) assistant.Status = GenerationStatus.Superseded;
            if (request.IsEdit)
            {
                user.Content = replacement!;
                chat.Summary = null;
                chat.SummaryUntilMessageId = null;
            }

            var persona = await personas.ResolveAsync(request.UserId, chat.PersonaId, ct);
            var answer = new Message
            {
                ChatId = chat.Id, PersonaId = persona?.Id, Role = MessageRole.Assistant, Content = "", Model = personas.ModelFor(persona),
                Status = GenerationStatus.Pending, CreatedAt = clock.GetUtcNow(),
                TriggerMessageId = user.Id, RequestId = request.RequestId
            };
            chat.LastMessageAt = answer.CreatedAt;
            data.Add(answer);
            await data.SaveAsync(ct);
            return Result<Guid>.Ok(answer.Id);
        }, cancellationToken);
    }
}
