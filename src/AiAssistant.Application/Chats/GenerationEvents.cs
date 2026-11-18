using AiAssistant.Application.Ai;

namespace AiAssistant.Application.Chats;

/// <summary>Publishes generation updates to the authenticated owner of a chat.</summary>
public interface IGenerationEvents
{
    /// <summary>Informs the client that a worker has started generating the response.</summary>
    Task StartedAsync(Guid userId, Guid chatId, Guid messageId, CancellationToken cancellationToken);

    /// <summary>Publishes a text chunk at its UTF-16 offset in the stored response.</summary>
    Task TokenAsync(Guid userId, Guid chatId, Guid messageId, int offset, string chunk, CancellationToken cancellationToken);

    /// <summary>Publishes a terminal status and the final text persisted in history.</summary>
    Task FinishedAsync(Guid userId, Guid chatId, MessageView message, AiUsage? usage, CancellationToken cancellationToken);
}
