using System.Text.Json;
using AiAssistant.Application.Ai;
using AiAssistant.Application.Common;
using AiAssistant.Application.Files;
using AiAssistant.Application.Persistence;
using AiAssistant.Domain;
using MediatR;

namespace AiAssistant.Application.Chats;

public sealed class ChatReadHandlers(IAssistantData data, ChatAccess access, IPromptContextBuilder prompts) :
    IRequestHandler<ListChats, Result<Page<ChatView>>>,
    IRequestHandler<ListMessages, Result<MessagePage>>, IRequestHandler<GetChat, Result<ChatView>>,
    IRequestHandler<GetChatRules, Result<ChatRules>>
{
    public async Task<Result<Page<ChatView>>> Handle(ListChats request, CancellationToken cancellationToken)
    {
        if (request.Page is < 1 or > 500 || request.Limit is < 1 or > 50 || request.Search?.Length > 120)
            return Result<Page<ChatView>>.Fail(ErrorKind.Validation, "invalid_page", "Проверьте параметры поиска.");

        var query = data.Query<Chat>().Where(chat => chat.UserId == request.UserId && chat.ExpiresAt == null);
        if (request.WorkspaceId.HasValue) query = query.Where(chat => chat.WorkspaceId == request.WorkspaceId);
        else if (request.PersonalOnly) query = query.Where(chat => chat.WorkspaceId == null);
        var search = request.Search?.Trim().ToLowerInvariant();
        if (!string.IsNullOrEmpty(search))
        {
            var messages = data.Query<Message>();
            query = query.Where(chat => chat.Title.ToLower().Contains(search) ||
                messages.Any(message => message.ChatId == chat.Id && message.Status != GenerationStatus.Superseded &&
                    message.Content.ToLower().Contains(search)));
        }

        var results = await data.ListAsync(query.OrderByDescending(chat => chat.IsPinned)
            .ThenByDescending(chat => chat.LastMessageAt).ThenByDescending(chat => chat.Id)
            .Skip((request.Page - 1) * request.Limit).Take(request.Limit + 1)
            .Select(chat => new ChatView(chat.Id, chat.Title, chat.PersonaId, chat.IsPinned, chat.LastMessageAt, chat.WorkspaceId)),
            cancellationToken);
        return Result<Page<ChatView>>.Ok(new(results.Take(request.Limit).ToArray(), results.Count > request.Limit));
    }

    public async Task<Result<ChatView>> Handle(GetChat request, CancellationToken ct)
    {
        var chat = await access.FindAsync(request.UserId, request.ChatId, false, ct);
        return chat is null ? ChatAccess.NotFound<ChatView>() : Result<ChatView>.Ok(
            new(chat.Id, chat.Title, chat.PersonaId, chat.IsPinned, chat.LastMessageAt, chat.WorkspaceId, chat.ExpiresAt));
    }

    public async Task<Result<ChatRules>> Handle(GetChatRules request, CancellationToken ct)
    {
        if (request.ChatId is not { } id)
            return await prompts.DescribeAsync(request.UserId, request.WorkspaceId, null, ct);
        var chat = await access.FindAsync(request.UserId, id, false, ct);
        return chat is null ? ChatAccess.NotFound<ChatRules>() :
            await prompts.DescribeAsync(request.UserId, chat.WorkspaceId, chat.PersonaId, ct, chat.ExpiresAt.HasValue);
    }

    public async Task<Result<MessagePage>> Handle(ListMessages request, CancellationToken cancellationToken)
    {
        if (request.Limit is < 1 or > 50)
            return Result<MessagePage>.Fail(ErrorKind.Validation, "invalid_page", "Размер страницы должен быть от 1 до 50.");
        if (await access.FindAsync(request.UserId, request.ChatId, false, cancellationToken) is null)
            return ChatAccess.NotFound<MessagePage>();

        var query = data.Query<Message>().Where(message => message.ChatId == request.ChatId &&
            message.Status != GenerationStatus.Superseded);
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
        var ids = messages.Select(message => message.Id).ToArray();
        var feedback = (await data.ListAsync(data.Query<MessageFeedback>().Where(item => item.UserId == request.UserId &&
            ids.Contains(item.MessageId)), cancellationToken)).ToDictionary(item => item.MessageId);
        var attachments = (await data.ListAsync(data.Query<Attachment>().Where(item => ids.Contains(item.MessageId)),
            cancellationToken)).ToLookup(item => item.MessageId,
                item => new AttachmentView(item.Id, item.FileName, item.ContentType, item.Size));
        var items = messages.Take(request.Limit).Reverse().Select(message =>
            ToView(message, feedback.GetValueOrDefault(message.Id), attachments[message.Id].ToArray())).ToArray();
        return Result<MessagePage>.Ok(new(items, messages.Count > request.Limit ? items[0].Id : null));
    }

    public static MessageView ToView(Message message, MessageFeedback? feedback = null,
        IReadOnlyList<AttachmentView>? attachments = null) => new(message.Id, message.Role.ToString().ToLowerInvariant(),
        message.Content, message.Status.ToString().ToLowerInvariant(), message.Model, message.ErrorCode,
        message.Suggestions, message.CreatedAt, message.TriggerMessageId, feedback?.Rating, feedback?.Reason, attachments,
        message.AppliedRules is null ? null : JsonSerializer.Deserialize<ChatRules>(message.AppliedRules));
}
