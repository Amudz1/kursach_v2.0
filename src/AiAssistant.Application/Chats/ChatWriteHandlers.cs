using AiAssistant.Application.Common;
using AiAssistant.Application.Billing;
using AiAssistant.Application.Files;
using AiAssistant.Application.Persistence;
using AiAssistant.Application.Personalization;
using AiAssistant.Application.Workspaces;
using AiAssistant.Domain;
using MediatR;

namespace AiAssistant.Application.Chats;

public sealed class ChatWriteHandlers(IAssistantData data, ChatAccess access, PersonaAccess personas,
    AttachmentLifecycle attachments, GenerationStops stops, IUsageLimitService usage, TimeProvider clock) :
    IRequestHandler<CreateChat, Result<Guid>>,
    IRequestHandler<PatchChat, Result<Unit>>,
    IRequestHandler<DeleteChat, Result<Unit>>, IRequestHandler<MoveChat, Result<Unit>>
{
    public Task<Result<Guid>> Handle(CreateChat request, CancellationToken cancellationToken) =>
        data.ForUserAsync(request.UserId, async ct =>
        {
            var now = clock.GetUtcNow();
            if (request.Temporary && (request.WorkspaceId.HasValue || request.PersonaId.HasValue))
                return Result<Guid>.Fail(ErrorKind.Validation, "invalid_temporary_scope", "Временный диалог создаётся отдельно от пространств и ролей.");
            if (request.Temporary && await data.CountAsync(data.Query<Chat>().Where(chat => chat.UserId == request.UserId &&
                chat.ExpiresAt > now), ct) >= 20)
                return Result<Guid>.Fail(ErrorKind.Limit, "temporary_chat_limit", "Удалите один из временных диалогов перед созданием нового.");
            if (request.PersonaId is { } personaId && await personas.FindAsync(request.UserId, personaId, ct) is null)
                return Result<Guid>.Fail(ErrorKind.NotFound, "persona_not_found", "Персона недоступна.");
            if (!await CanUseWorkspaceAsync(request.UserId, request.WorkspaceId, ct)) return WorkspaceHandlers.Missing<Guid>();
            var chat = new Chat { UserId = request.UserId, PersonaId = request.PersonaId, WorkspaceId = request.WorkspaceId,
                ExpiresAt = request.Temporary ? now.AddHours(24) : null,
                CreatedAt = now, LastMessageAt = now };
            data.Add(chat);
            await data.SaveAsync(ct);
            return Result<Guid>.Ok(chat.Id);
        }, cancellationToken);

    public Task<Result<Unit>> Handle(MoveChat request, CancellationToken cancellationToken) =>
        data.ForUserAsync(request.UserId, async ct =>
        {
            var chat = await access.FindAsync(request.UserId, request.ChatId, true, ct);
            if (chat is null) return ChatAccess.NotFound<Unit>();
            if (chat.ExpiresAt.HasValue)
                return Result<Unit>.Fail(ErrorKind.Conflict, "temporary_chat", "Временный диалог нельзя переместить.");
            if (chat.WorkspaceId == request.WorkspaceId) return Result<Unit>.Ok(Unit.Value);
            if (!await CanUseWorkspaceAsync(request.UserId, request.WorkspaceId, ct)) return WorkspaceHandlers.Missing<Unit>();
            if (await data.AnyAsync(data.Query<Message>().Where(item => item.ChatId == chat.Id &&
                (item.Status == GenerationStatus.Pending || item.Status == GenerationStatus.Streaming)), ct))
                return Result<Unit>.Fail(ErrorKind.Conflict, "chat_busy", "Дождитесь завершения ответа перед переносом.");
            chat.WorkspaceId = request.WorkspaceId;
            chat.Summary = null;
            chat.SummaryUntilMessageId = null;
            await data.SaveAsync(ct);
            return Result<Unit>.Ok(Unit.Value);
        }, cancellationToken);

    private Task<bool> CanUseWorkspaceAsync(Guid user, Guid? workspace, CancellationToken ct) => workspace is null
        ? Task.FromResult(true)
        : data.AnyAsync(data.Query<Workspace>().Where(item => item.UserId == user && item.Id == workspace), ct);

    public Task<Result<Unit>> Handle(PatchChat request, CancellationToken cancellationToken) =>
        data.ForUserAsync(request.UserId, async ct =>
        {
            var chat = await access.FindAsync(request.UserId, request.ChatId, true, ct);
            if (chat is null) return ChatAccess.NotFound<Unit>();
            if (chat.ExpiresAt.HasValue && (request.PersonaId.HasValue || request.IsPinned == true))
                return Result<Unit>.Fail(ErrorKind.Conflict, "temporary_chat", "Временный диалог нельзя закрепить или привязать к роли.");
            if (request.Title is not null)
            {
                var title = request.Title.Trim();
                if (title.Length is 0 or > 120)
                    return Result<Unit>.Fail(ErrorKind.Validation, "invalid_title", "Название должно содержать от 1 до 120 символов.");
                chat.Title = title;
            }
            if (request.IsPinned.HasValue) chat.IsPinned = request.IsPinned.Value;
            if (request.PersonaId is { } personaId)
            {
                if (await personas.FindAsync(request.UserId, personaId, ct) is null)
                    return Result<Unit>.Fail(ErrorKind.NotFound, "persona_not_found", "Персона недоступна.");
                chat.PersonaId = personaId;
            }
            await data.SaveAsync(ct);
            return Result<Unit>.Ok(Unit.Value);
        }, cancellationToken);

    public async Task<Result<Unit>> Handle(DeleteChat request, CancellationToken cancellationToken)
    {
        List<string> keys = [];
        Guid? generation = null;
        var result = await data.ForUserAsync(request.UserId, async ct =>
        {
            var now = clock.GetUtcNow();
            var chat = request.ExpiredOnly
                ? await data.FirstAsync(data.Query<Chat>(true).Where(item => item.UserId == request.UserId &&
                    item.Id == request.ChatId && item.ExpiresAt <= now), ct)
                : await access.FindAsync(request.UserId, request.ChatId, true, ct);
            if (chat is null) return ChatAccess.NotFound<Unit>();
            keys = await data.ListAsync(attachments.KeysFor(request.UserId, chat.Id), ct);
            var active = await data.FirstAsync(data.Query<Message>().Where(message => message.ChatId == chat.Id &&
                (message.Status == GenerationStatus.Pending || message.Status == GenerationStatus.Streaming)), ct);
            generation = active?.Id;
            if (active?.Status == GenerationStatus.Pending)
                await usage.FinishAsync(request.UserId, active.CreatedAt, true, 0, ct);
            data.Remove(chat);
            await data.SaveAsync(ct);
            return Result<Unit>.Ok(Unit.Value);
        }, cancellationToken);
        if (!result.IsSuccess) return result;
        if (generation is { } id) await stops.StopAsync(id);
        attachments.Delete(keys);
        return result;
    }
}
