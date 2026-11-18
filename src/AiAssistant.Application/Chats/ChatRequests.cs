using AiAssistant.Application.Common;
using MediatR;

namespace AiAssistant.Application.Chats;

public sealed record ChatView(Guid Id, string Title, Guid? PersonaId, bool IsPinned, DateTimeOffset LastMessageAt);
public sealed record MessageView(Guid Id, string Role, string Content, string Status, string? Model,
    string? ErrorCode, string[] Suggestions, DateTimeOffset CreatedAt, Guid? TriggerMessageId, int? Rating = null,
    string? FeedbackReason = null);
public sealed record Page<T>(IReadOnlyList<T> Items, bool HasMore);
public sealed record MessagePage(IReadOnlyList<MessageView> Items, Guid? NextBefore);

public sealed record ListChats(Guid UserId, string? Search, int Page, int Limit) : IRequest<Result<Page<ChatView>>>;
public sealed record CreateChat(Guid UserId, Guid? PersonaId = null) : IRequest<Result<Guid>>;
public sealed record PatchChat(Guid UserId, Guid ChatId, string? Title, bool? IsPinned, Guid? PersonaId = null) : IRequest<Result<Unit>>;
public sealed record DeleteChat(Guid UserId, Guid ChatId) : IRequest<Result<Unit>>;
public sealed record ListMessages(Guid UserId, Guid ChatId, Guid? Before, int Limit) : IRequest<Result<MessagePage>>;
public sealed record SendChatMessage(Guid UserId, Guid ChatId, Guid RequestId, string Content, string? Model = null)
    : IRequest<Result<Guid>>;
