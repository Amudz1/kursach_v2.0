using AiAssistant.Application.Common;
using AiAssistant.Application.Ai;
using AiAssistant.Application.Files;
using MediatR;

namespace AiAssistant.Application.Chats;

public sealed record ChatView(Guid Id, string Title, Guid? PersonaId, bool IsPinned, DateTimeOffset LastMessageAt, Guid? WorkspaceId,
    DateTimeOffset? ExpiresAt = null);
public sealed record MessageView(Guid Id, string Role, string Content, string Status, string? Model,
    string? ErrorCode, string[] Suggestions, DateTimeOffset CreatedAt, Guid? TriggerMessageId, int? Rating = null,
    string? FeedbackReason = null, IReadOnlyList<AttachmentView>? Attachments = null, ChatRules? Rules = null, bool IsBookmarked = false);
public sealed record Page<T>(IReadOnlyList<T> Items, bool HasMore);
public sealed record MessagePage(IReadOnlyList<MessageView> Items, Guid? NextBefore, Guid? NextAfter = null);

public sealed record ListChats(Guid UserId, string? Search, int Page, int Limit, Guid? WorkspaceId = null, bool PersonalOnly = false) : IRequest<Result<Page<ChatView>>>;
public sealed record GetChat(Guid UserId, Guid ChatId) : IRequest<Result<ChatView>>;
public sealed record GetChatRules(Guid UserId, Guid? ChatId, Guid? WorkspaceId) : IRequest<Result<ChatRules>>;
public sealed record CreateChat(Guid UserId, Guid? PersonaId = null, Guid? WorkspaceId = null, bool Temporary = false) : IRequest<Result<Guid>>;
public sealed record MoveChat(Guid UserId, Guid ChatId, Guid? WorkspaceId) : IRequest<Result<Unit>>;
public sealed record PatchChat(Guid UserId, Guid ChatId, string? Title, bool? IsPinned, Guid? PersonaId = null) : IRequest<Result<Unit>>;
public sealed record DeleteChat(Guid UserId, Guid ChatId, bool ExpiredOnly = false) : IRequest<Result<Unit>>;
public sealed record ListMessages(Guid UserId, Guid ChatId, Guid? Before, int Limit, Guid? Around = null, Guid? After = null)
    : IRequest<Result<MessagePage>>;
public sealed record SendChatMessage(Guid UserId, Guid ChatId, Guid RequestId, string Content, string? Model = null,
    IReadOnlyList<IncomingAttachment>? Files = null)
    : IRequest<Result<Guid>>;
