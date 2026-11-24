using System.Collections.Concurrent;

namespace AiAssistant.Application.Chats;

public sealed class GenerationStops
{
    private readonly ConcurrentDictionary<Guid, CancellationTokenSource> _sources = new();

    public bool TryTrack(Guid messageId, CancellationTokenSource source) => _sources.TryAdd(messageId, source);

    public void Release(Guid messageId) => _sources.TryRemove(messageId, out _);

    public async Task StopAsync(Guid messageId)
    {
        if (!_sources.TryGetValue(messageId, out var source)) return;
        try
        {
            await source.CancelAsync();
        }
        catch (ObjectDisposedException)
        {
            // The worker can finish while a stop request is in flight.
        }
    }
}
