using AiAssistant.Application.Ai;
using AiAssistant.Application.Chats;
using Microsoft.AspNetCore.SignalR;

namespace AiAssistant.Api.Realtime;

public sealed class HubGenerationEvents(IHubContext<ChatHub> hub) : IGenerationEvents
{
    public Task StartedAsync(Guid userId, Guid chatId, Guid messageId, CancellationToken cancellationToken) =>
        hub.Clients.Group(ChatHub.Group(userId, chatId)).SendAsync("MessageStarted", messageId, chatId, cancellationToken);

    public Task TokenAsync(Guid userId, Guid chatId, Guid messageId, int offset, string chunk, CancellationToken cancellationToken) =>
        hub.Clients.Group(ChatHub.Group(userId, chatId)).SendAsync("TokenReceived", messageId, chunk, offset, chatId,
            cancellationToken);

    public Task FinishedAsync(Guid userId, Guid chatId, MessageView message, AiUsage? usage, CancellationToken cancellationToken)
    {
        var group = hub.Clients.Group(ChatHub.Group(userId, chatId));
        return message.Status switch
        {
            "completed" => group.SendAsync("MessageCompleted", message.Id, usage, message, chatId, cancellationToken),
            "stopped" => group.SendAsync("MessageStopped", message.Id, message, chatId, cancellationToken),
            _ => group.SendAsync("MessageFailed", message.Id, message.ErrorCode, message, chatId, cancellationToken)
        };
    }
}
