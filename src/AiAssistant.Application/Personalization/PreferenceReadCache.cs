using Microsoft.Extensions.Caching.Memory;

namespace AiAssistant.Application.Personalization;

public sealed class PreferenceReadCache(IMemoryCache memory)
{
    private static readonly TimeSpan Lifetime = TimeSpan.FromSeconds(30);
    private long _profileRevision;
    private long _personaRevision;

    public async Task<ProfileView> ProfileAsync(Guid userId, Func<Task<ProfileView>> read)
    {
        var key = ("profile", userId, Interlocked.Read(ref _profileRevision));
        return (await memory.GetOrCreateAsync(key, entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = Lifetime;
            return read();
        }))!;
    }

    public async Task<IReadOnlyList<PersonaView>> PersonasAsync(Guid userId, Func<Task<IReadOnlyList<PersonaView>>> read)
    {
        var key = ("personas", userId, Interlocked.Read(ref _personaRevision));
        return (await memory.GetOrCreateAsync(key, entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = Lifetime;
            return read();
        }))!;
    }

    public void InvalidateProfiles() => Interlocked.Increment(ref _profileRevision);
    public void InvalidatePersonas() => Interlocked.Increment(ref _personaRevision);
}
