using AiAssistant.Application.Files;

namespace AiAssistant.Api.Realtime;

public sealed class AttachmentCleanupWorker(IServiceScopeFactory scopes, ILogger<AttachmentCleanupWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(15));
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                await scope.ServiceProvider.GetRequiredService<AttachmentLifecycle>().PruneAsync(stoppingToken);
            }
            catch (Exception exception) when (!stoppingToken.IsCancellationRequested)
            {
                logger.LogWarning("Private file cleanup failed: {ErrorType}", exception.GetType().Name);
            }
            if (!await timer.WaitForNextTickAsync(stoppingToken)) return;
        }
    }
}
