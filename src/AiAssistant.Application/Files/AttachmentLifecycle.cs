using AiAssistant.Application.Persistence;
using AiAssistant.Domain;
using Microsoft.Extensions.Logging;

namespace AiAssistant.Application.Files;

public sealed class AttachmentLifecycle(IAssistantData data, IAttachmentStorage storage,
    TimeProvider clock, ILogger<AttachmentLifecycle> logger)
{
    public IQueryable<string> KeysFor(Guid userId, Guid? chatId = null) =>
        from attachment in data.Query<Attachment>()
        join message in data.Query<Message>() on attachment.MessageId equals message.Id
        join chat in data.Query<Chat>() on message.ChatId equals chat.Id
        where chat.UserId == userId && (chatId == null || chat.Id == chatId)
        select attachment.StorageKey;

    public void Delete(IEnumerable<string> keys)
    {
        foreach (var key in keys)
        {
            try { storage.Delete(key); }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                logger.LogWarning("Private attachment deletion will be retried: {ErrorType}", exception.GetType().Name);
            }
        }
    }

    public async Task PruneAsync(CancellationToken cancellationToken)
    {
        var cutoff = clock.GetUtcNow().AddHours(-1);
        foreach (var keys in storage.OldKeys(cutoff).Chunk(100))
        {
            var retained = await data.ListAsync(data.Query<Attachment>().Where(item => keys.Contains(item.StorageKey))
                .Select(item => item.StorageKey), cancellationToken);
            Delete(keys.Except(retained, StringComparer.Ordinal));
        }
    }
}
