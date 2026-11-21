using AiAssistant.Application.Common;
using AiAssistant.Application.Persistence;
using AiAssistant.Domain;
using MediatR;

namespace AiAssistant.Application.Chats;

public sealed record MessageLink(Guid Id, string Role, string Text, int MatchStart, int MatchLength);
public sealed record MessageLinks(IReadOnlyList<MessageLink> Items, Guid? Next);
public sealed record ResumeChatView(ChatView Chat, Guid? AnchorId, string Text);
public sealed record ListMessageLinks(Guid UserId, Guid ChatId, string Kind, string? Search, Guid? After)
    : IRequest<Result<MessageLinks>>;
public sealed record SetMessageBookmark(Guid UserId, Guid ChatId, Guid MessageId, bool Bookmarked) : IRequest<Result<Unit>>;
public sealed record GetResumeChat(Guid UserId, Guid? WorkspaceId, Guid? ChatId, Guid? MessageId)
    : IRequest<Result<ResumeChatView?>>;

public sealed class ChatNavigationHandlers(IAssistantData data, ChatAccess access) :
    IRequestHandler<ListMessageLinks, Result<MessageLinks>>,
    IRequestHandler<SetMessageBookmark, Result<Unit>>,
    IRequestHandler<GetResumeChat, Result<ResumeChatView?>>
{
    private const int PageSize = 25;

    public async Task<Result<MessageLinks>> Handle(ListMessageLinks request, CancellationToken ct)
    {
        if (request.Kind is not ("questions" or "bookmarks" or "search") || request.Search?.Length > 160 ||
            (request.Kind == "search" && string.IsNullOrWhiteSpace(request.Search)))
            return Result<MessageLinks>.Fail(ErrorKind.Validation, "invalid_navigation", "Проверьте параметры поиска.");
        var chat = await access.FindAsync(request.UserId, request.ChatId, false, ct);
        if (chat is null) return ChatAccess.NotFound<MessageLinks>();

        var query = access.VisibleMessages(chat.Id);
        if (request.After is { } after)
        {
            var anchor = await data.FirstAsync(query.Where(message => message.Id == after), ct);
            if (anchor is null) return MessageNotFound<MessageLinks>();
            query = ChatAccess.After(query, anchor);
        }
        string? term = null;
        switch (request.Kind)
        {
            case "questions":
                query = query.Where(message => message.Role == MessageRole.User);
                break;
            case "bookmarks":
                if (chat.ExpiresAt.HasValue) return Result<MessageLinks>.Ok(new([], null));
                var bookmarks = data.Query<MessageBookmark>().Where(bookmark => bookmark.UserId == request.UserId);
                query = query.Where(message => bookmarks.Any(bookmark => bookmark.MessageId == message.Id));
                break;
            case "search":
                term = request.Search!.Trim();
                var lower = term.ToLowerInvariant();
                query = query.Where(message => message.Content.ToLower().Contains(lower));
                break;
        }
        var ordered = query.OrderBy(message => message.CreatedAt).ThenBy(message => message.Id).Take(PageSize + 1);
        var rows = term is null
            ? await data.ListAsync(ordered.Select(message => new
            {
                message.Id, message.Role, Text = message.Content.Length > 180 ? message.Content.Substring(0, 180) + "…" : message.Content,
            }), ct)
            : await data.ListAsync(ordered.Select(message => new { message.Id, message.Role, Text = message.Content }), ct);
        var links = rows.Take(PageSize).Select(row => Link(row.Id, row.Role, row.Text, term)).ToArray();
        return Result<MessageLinks>.Ok(new(links, rows.Count > PageSize ? links[^1].Id : null));
    }

    public Task<Result<Unit>> Handle(SetMessageBookmark request, CancellationToken cancellationToken) =>
        data.ForUserAsync(request.UserId, async ct =>
        {
            var chat = await access.FindAsync(request.UserId, request.ChatId, false, ct);
            if (chat is null) return ChatAccess.NotFound<Unit>();
            if (chat.ExpiresAt.HasValue) return Result<Unit>.Fail(ErrorKind.Validation, "temporary_bookmark", "Временный диалог нельзя добавить в закладки.");
            if (!await data.AnyAsync(access.VisibleMessages(chat.Id).Where(message => message.Id == request.MessageId &&
                message.Role == MessageRole.Assistant && message.Status != GenerationStatus.Pending &&
                message.Status != GenerationStatus.Streaming && message.Content != ""), ct))
                return MessageNotFound<Unit>();
            var bookmark = await data.FirstAsync(data.Query<MessageBookmark>(true).Where(item =>
                item.UserId == request.UserId && item.MessageId == request.MessageId), ct);
            if (request.Bookmarked && bookmark is null)
                data.Add(new MessageBookmark { UserId = request.UserId, MessageId = request.MessageId });
            else if (!request.Bookmarked && bookmark is not null) data.Remove(bookmark);
            else return Result<Unit>.Ok(Unit.Value);
            await data.SaveAsync(ct);
            return Result<Unit>.Ok(Unit.Value);
        }, cancellationToken);

    public async Task<Result<ResumeChatView?>> Handle(GetResumeChat request, CancellationToken ct)
    {
        var messages = data.Query<Message>().Where(message => message.Status != GenerationStatus.Superseded &&
            message.Status != GenerationStatus.Pending && message.Status != GenerationStatus.Streaming);
        var chats = data.Query<Chat>().Where(chat => chat.UserId == request.UserId && chat.ExpiresAt == null &&
            messages.Any(message => message.ChatId == chat.Id));
        if (request.WorkspaceId.HasValue) chats = chats.Where(chat => chat.WorkspaceId == request.WorkspaceId);
        var chat = await data.FirstAsync(chats.OrderByDescending(chat => chat.Id == request.ChatId)
            .ThenByDescending(chat => chat.LastMessageAt).ThenByDescending(chat => chat.Id), ct);
        if (chat is null) return Result<ResumeChatView?>.Ok(null);
        var anchorId = chat.Id == request.ChatId ? request.MessageId : null;
        var row = await data.FirstAsync(messages.Where(message => message.ChatId == chat.Id)
            .OrderByDescending(message => message.Id == anchorId).ThenByDescending(message => message.CreatedAt)
            .ThenByDescending(message => message.Id)
            .Select(message => new
            {
                message.Id, Text = message.Content.Length > 180 ? message.Content.Substring(0, 180) + "…" : message.Content,
            }), ct);
        if (row is null) return Result<ResumeChatView?>.Ok(null);
        return Result<ResumeChatView?>.Ok(new(new(chat.Id, chat.Title, chat.PersonaId, chat.IsPinned, chat.LastMessageAt, chat.WorkspaceId),
            row.Id == anchorId ? anchorId : null, row.Text));
    }

    private static Result<T> MessageNotFound<T>() =>
        Result<T>.Fail(ErrorKind.NotFound, "message_not_found", "Сообщение недоступно.");

    private static MessageLink Link(Guid id, MessageRole role, string content, string? term)
    {
        var author = role.ToString().ToLowerInvariant();
        var index = term is null ? -1 : content.IndexOf(term, StringComparison.OrdinalIgnoreCase);
        if (index < 0)
            return new(id, author, content.Length > 180 ? content[..180] + "…" : content, 0, 0);
        var start = Math.Max(0, index - 45);
        var end = Math.Min(content.Length, index + term!.Length + 75);
        return new(id, author, (start > 0 ? "…" : "") + content[start..end] + (end < content.Length ? "…" : ""),
            index - start + (start > 0 ? 1 : 0), term.Length);
    }
}
