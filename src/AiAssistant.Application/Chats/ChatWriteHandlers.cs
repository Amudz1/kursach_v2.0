using AiAssistant.Application.Common;
using AiAssistant.Application.Persistence;
using AiAssistant.Domain;
using MediatR;

namespace AiAssistant.Application.Chats;

public sealed class ChatWriteHandlers(IAssistantData data, ChatAccess access, TimeProvider clock) :
    IRequestHandler<CreateChat, Result<Guid>>,
    IRequestHandler<PatchChat, Result<Unit>>,
    IRequestHandler<DeleteChat, Result<Unit>>
{
    public Task<Result<Guid>> Handle(CreateChat request, CancellationToken cancellationToken) =>
        data.ForUserAsync(request.UserId, async ct =>
        {
            var now = clock.GetUtcNow();
            var chat = new Chat { UserId = request.UserId, CreatedAt = now, LastMessageAt = now };
            data.Add(chat);
            await data.SaveAsync(ct);
            return Result<Guid>.Ok(chat.Id);
        }, cancellationToken);

    public Task<Result<Unit>> Handle(PatchChat request, CancellationToken cancellationToken) =>
        data.ForUserAsync(request.UserId, async ct =>
        {
            var chat = await access.FindAsync(request.UserId, request.ChatId, true, ct);
            if (chat is null) return ChatAccess.NotFound<Unit>();
            if (request.Title is not null)
            {
                var title = request.Title.Trim();
                if (title.Length is 0 or > 120)
                    return Result<Unit>.Fail(ErrorKind.Validation, "invalid_title", "Название должно содержать от 1 до 120 символов.");
                chat.Title = title;
            }
            if (request.IsPinned.HasValue) chat.IsPinned = request.IsPinned.Value;
            await data.SaveAsync(ct);
            return Result<Unit>.Ok(Unit.Value);
        }, cancellationToken);

    public Task<Result<Unit>> Handle(DeleteChat request, CancellationToken cancellationToken) =>
        data.ForUserAsync(request.UserId, async ct =>
        {
            var chat = await access.FindAsync(request.UserId, request.ChatId, true, ct);
            if (chat is null) return ChatAccess.NotFound<Unit>();
            data.Remove(chat);
            await data.SaveAsync(ct);
            return Result<Unit>.Ok(Unit.Value);
        }, cancellationToken);
}
