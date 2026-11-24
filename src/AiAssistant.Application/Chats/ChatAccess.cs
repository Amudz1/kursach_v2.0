using AiAssistant.Application.Common;
using AiAssistant.Application.Persistence;
using AiAssistant.Domain;

namespace AiAssistant.Application.Chats;

public sealed class ChatAccess(IAssistantData data, TimeProvider? clock = null)
{
    public Task<Chat?> FindAsync(Guid userId, Guid chatId, bool forUpdate, CancellationToken cancellationToken)
    {
        var now = (clock ?? TimeProvider.System).GetUtcNow();
        return data.FirstAsync(data.Query<Chat>(forUpdate).Where(chat => chat.Id == chatId && chat.UserId == userId &&
            (chat.ExpiresAt == null || chat.ExpiresAt > now)),
            cancellationToken);
    }

    public static Result<T> NotFound<T>() =>
        Result<T>.Fail(ErrorKind.NotFound, "chat_not_found", "Чат не найден.");

    public IQueryable<Message> VisibleMessages(Guid chatId) =>
        data.Query<Message>().Where(message => message.ChatId == chatId && message.Status != GenerationStatus.Superseded);

    public static IQueryable<Message> Before(IQueryable<Message> query, Message anchor) =>
        query.Where(message => message.CreatedAt < anchor.CreatedAt ||
            (message.CreatedAt == anchor.CreatedAt && message.Id.CompareTo(anchor.Id) < 0));

    public static IQueryable<Message> After(IQueryable<Message> query, Message anchor) =>
        query.Where(message => message.CreatedAt > anchor.CreatedAt ||
            (message.CreatedAt == anchor.CreatedAt && message.Id.CompareTo(anchor.Id) > 0));
}
