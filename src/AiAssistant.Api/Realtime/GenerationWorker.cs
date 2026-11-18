using AiAssistant.Application.Chats;

namespace AiAssistant.Api.Realtime;

public sealed class GenerationWorker(IServiceScopeFactory scopes, ILogger<GenerationWorker> logger) : BackgroundService
{
    private const int ParallelWorkers = 4;
    private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(250);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await using (var scope = scopes.CreateAsyncScope())
        {
            await scope.ServiceProvider.GetRequiredService<GenerationProcessor>().RecoverAsync(stoppingToken);
        }
        await Task.WhenAll(Enumerable.Range(0, ParallelWorkers).Select(_ => WorkAsync(stoppingToken)));
    }

    private async Task WorkAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                if (await scope.ServiceProvider.GetRequiredService<GenerationProcessor>().ProcessNextAsync(stoppingToken))
                    continue;
                await Task.Delay(PollInterval, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception)
            {
                logger.LogWarning("Generation worker error: {ErrorType}", exception.GetType().Name);
                await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);
            }
        }
    }
}
