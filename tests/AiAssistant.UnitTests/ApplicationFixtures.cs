using AiAssistant.Application.Files;
using AiAssistant.Application.Persistence;

namespace AiAssistant.UnitTests;

internal sealed class MemoryData : IAssistantData
{
    private readonly List<object> _entities = [];
    public int Saves { get; private set; }

    public IQueryable<TEntity> Query<TEntity>(bool forUpdate = false) where TEntity : class => _entities.OfType<TEntity>().AsQueryable();
    public Task<List<T>> ListAsync<T>(IQueryable<T> query, CancellationToken cancellationToken) => Task.FromResult(query.ToList());
    public Task<T?> FirstAsync<T>(IQueryable<T> query, CancellationToken cancellationToken) => Task.FromResult(query.FirstOrDefault());
    public Task<bool> AnyAsync<T>(IQueryable<T> query, CancellationToken cancellationToken) => Task.FromResult(query.Any());
    public Task<int> CountAsync<T>(IQueryable<T> query, CancellationToken cancellationToken) => Task.FromResult(query.Count());
    public void Add<TEntity>(TEntity entity) where TEntity : class => _entities.Add(entity);
    public void Remove<TEntity>(TEntity entity) where TEntity : class => _entities.Remove(entity);
    public Task<int> SaveAsync(CancellationToken cancellationToken) => Task.FromResult(++Saves);
    public Task<T> ForUserAsync<T>(Guid userId, Func<CancellationToken, Task<T>> operation, CancellationToken cancellationToken) =>
        operation(cancellationToken);
}

internal sealed class FixedClock(DateTimeOffset now) : TimeProvider
{
    public DateTimeOffset Now { get; set; } = now;
    public override DateTimeOffset GetUtcNow() => Now;
}

internal sealed class NoFiles : IAttachmentStorage
{
    public Task<PreparedAttachment> PrepareAsync(IncomingAttachment incoming, CancellationToken cancellationToken) =>
        throw new NotSupportedException();

    public Task WriteAsync(string key, byte[] bytes, CancellationToken cancellationToken) => throw new NotSupportedException();
    public Stream? OpenRead(string key) => null;
    public void Delete(string key) => throw new NotSupportedException();
    public IEnumerable<string> OldKeys(DateTimeOffset cutoff) => [];
}
