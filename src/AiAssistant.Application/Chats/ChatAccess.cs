using AiAssistant.Application.Common;
using AiAssistant.Application.Persistence;
using AiAssistant.Domain;

namespace AiAssistant.Application.Chats;

public sealed class ChatAccess(IAssistantData data)
{
    public Task<Chat?> FindAsync(Guid userId, Guid chatId, bool forUpdate, CancellationToken cancellationToken) =>
        data.FirstAsync(data.Query<Chat>(forUpdate).Where(chat => chat.Id == chatId && chat.UserId == userId),
            cancellationToken);

    public static Result<T> NotFound<T>() =>
        Result<T>.Fail(ErrorKind.NotFound, "chat_not_found", "Чат не найден.");
}
