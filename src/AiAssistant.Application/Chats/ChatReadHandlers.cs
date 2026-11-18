using AiAssistant.Application.Common;
using AiAssistant.Application.Persistence;
using AiAssistant.Domain;
using MediatR;

namespace AiAssistant.Application.Chats;

public sealed class ChatReadHandlers(IAssistantData data, ChatAccess access) :
    IRequestHandler<ListChats, Result<Page<ChatView>>>,
    IRequestHandler<ListMessages, Result<MessagePage>>
{
    public async Task<Result<Page<ChatView>>> Handle(ListChats request, CancellationToken cancellationToken)
    {
        if (request.Page is < 1 or > 500 || request.Limit is < 1 or > 50 || request.Search?.Length > 120)
            return Result<Page<ChatView>>.Fail(ErrorKind.Validation, "invalid_page", "Проверьте параметры поиска.");

        var query = data.Query<Chat>().Where(chat => chat.UserId == request.UserId);
        var search = request.Search?.Trim().ToLowerInvariant();
        if (!string.IsNullOrEmpty(search))
        {
            var messages = data.Query<Message>();
            query = query.Where(chat => chat.Title.ToLower().Contains(search) ||
                messages.Any(message => message.ChatId == chat.Id && message.Content.ToLower().Contains(search)));
        }

        var results = await data.ListAsync(query.OrderByDescending(chat => chat.IsPinned)
            .ThenByDescending(chat => chat.LastMessageAt).ThenByDescending(chat => chat.Id)
            .Skip((request.Page - 1) * request.Limit).Take(request.Limit + 1)
            .Select(chat => new ChatView(chat.Id, chat.Title, chat.PersonaId, chat.IsPinned, chat.LastMessageAt)),
            cancellationToken);
        return Result<Page<ChatView>>.Ok(new(results.Take(request.Limit).ToArray(), results.Count > request.Limit));
    }

    public async Task<Result<MessagePage>> Handle(ListMessages request, CancellationToken cancellationToken)
    {
        if (request.Limit is < 1 or > 50)
            return Result<MessagePage>.Fail(ErrorKind.Validation, "invalid_page", "Размер страницы должен быть от 1 до 50.");
        if (await access.FindAsync(request.UserId, request.ChatId, false, cancellationToken) is null)
            return ChatAccess.NotFound<MessagePage>();

        var query = data.Query<Message>().Where(message => message.ChatId == request.ChatId);
        if (request.Before.HasValue)
        {
            var cursor = await data.FirstAsync(query.Where(message => message.Id == request.Before.Value), cancellationToken);
            if (cursor is null)
                return Result<MessagePage>.Fail(ErrorKind.Validation, "invalid_cursor", "Указанное сообщение недоступно.");
            query = query.Where(message => message.CreatedAt < cursor.CreatedAt ||
                (message.CreatedAt == cursor.CreatedAt && message.Id.CompareTo(cursor.Id) < 0));
        }
        var messages = await data.ListAsync(query.OrderByDescending(message => message.CreatedAt)
            .ThenByDescending(message => message.Id).Take(request.Limit + 1), cancellationToken);
        var items = messages.Take(request.Limit).Reverse().Select(ToView).ToArray();
        return Result<MessagePage>.Ok(new(items, messages.Count > request.Limit ? items[0].Id : null));
    }

    public static MessageView ToView(Message message) => new(message.Id, message.Role.ToString().ToLowerInvariant(),
        message.Content, message.Status.ToString().ToLowerInvariant(), message.Model, message.ErrorCode,
        message.Suggestions, message.CreatedAt, message.TriggerMessageId);
}
