using AiAssistant.Application.Memory;

namespace AiAssistant.Api.Realtime;

public sealed class FollowUpWorker(IServiceScopeFactory scopes, ILogger<FollowUpWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                if (await scope.ServiceProvider.GetRequiredService<FollowUpProcessor>().ProcessNextAsync(stoppingToken))
                    continue;
                await Task.Delay(TimeSpan.FromMilliseconds(400), stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception)
            {
                logger.LogWarning("Follow-up worker error: {ErrorType}", exception.GetType().Name);
                await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);
            }
        }
    }
}
