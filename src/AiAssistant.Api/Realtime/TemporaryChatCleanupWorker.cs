using AiAssistant.Application.Chats;
using AiAssistant.Application.Persistence;
using AiAssistant.Domain;
using MediatR;

namespace AiAssistant.Api.Realtime;

public sealed class TemporaryChatCleanupWorker(IServiceScopeFactory scopes, TimeProvider clock,
    ILogger<TemporaryChatCleanupWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(10));
        do
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                var data = scope.ServiceProvider.GetRequiredService<IAssistantData>();
                var sender = scope.ServiceProvider.GetRequiredService<ISender>();
                var cutoff = clock.GetUtcNow();
                var expired = await data.ListAsync(data.Query<Chat>().Where(chat => chat.ExpiresAt <= cutoff)
                    .OrderBy(chat => chat.ExpiresAt).Take(200).Select(chat => new { chat.Id, chat.UserId }), stoppingToken);
                foreach (var chat in expired)
                    await sender.Send(new DeleteChat(chat.UserId, chat.Id, ExpiredOnly: true), stoppingToken);
            }
            catch (Exception exception) when (!stoppingToken.IsCancellationRequested)
            {
                logger.LogWarning("Temporary chat cleanup failed: {ErrorType}", exception.GetType().Name);
            }
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
