namespace AiAssistant.Application.Persistence;

/// <summary>Accesses assistant data and defines the boundary of atomic write operations.</summary>
public interface IAssistantData
{
    /// <summary>Returns a query for an entity type, using tracking only for pending edits.</summary>
    IQueryable<TEntity> Query<TEntity>(bool forUpdate = false) where TEntity : class;

    /// <summary>Materializes a query asynchronously.</summary>
    Task<List<T>> ListAsync<T>(IQueryable<T> query, CancellationToken cancellationToken);

    /// <summary>Returns the first matching value or its default.</summary>
    Task<T?> FirstAsync<T>(IQueryable<T> query, CancellationToken cancellationToken);

    /// <summary>Checks whether the query has any results.</summary>
    Task<bool> AnyAsync<T>(IQueryable<T> query, CancellationToken cancellationToken);

    /// <summary>Counts matching records.</summary>
    Task<int> CountAsync<T>(IQueryable<T> query, CancellationToken cancellationToken);

    /// <summary>Marks an entity for insertion.</summary>
    void Add<TEntity>(TEntity entity) where TEntity : class;

    /// <summary>Marks an entity for deletion.</summary>
    void Remove<TEntity>(TEntity entity) where TEntity : class;

    /// <summary>Persists tracked changes.</summary>
    Task<int> SaveAsync(CancellationToken cancellationToken);

    /// <summary>Serializes a short database operation among requests for the same account.</summary>
    Task<T> ForUserAsync<T>(Guid userId, Func<CancellationToken, Task<T>> operation, CancellationToken cancellationToken);
}
