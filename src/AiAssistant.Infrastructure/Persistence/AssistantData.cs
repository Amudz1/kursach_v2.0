using AiAssistant.Application.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AiAssistant.Infrastructure.Persistence;

public sealed class AssistantData(AssistantDbContext context) : IAssistantData
{
    public IQueryable<TEntity> Query<TEntity>(bool forUpdate = false) where TEntity : class =>
        forUpdate ? context.Set<TEntity>() : context.Set<TEntity>().AsNoTracking();

    public Task<List<T>> ListAsync<T>(IQueryable<T> query, CancellationToken cancellationToken) => query.ToListAsync(cancellationToken);

    public Task<T?> FirstAsync<T>(IQueryable<T> query, CancellationToken cancellationToken) =>
        query.FirstOrDefaultAsync(cancellationToken);

    public Task<bool> AnyAsync<T>(IQueryable<T> query, CancellationToken cancellationToken) => query.AnyAsync(cancellationToken);

    public Task<int> CountAsync<T>(IQueryable<T> query, CancellationToken cancellationToken) =>
        query.CountAsync(cancellationToken);

    public void Add<TEntity>(TEntity entity) where TEntity : class => context.Add(entity);

    public void Remove<TEntity>(TEntity entity) where TEntity : class => context.Remove(entity);

    public Task<int> SaveAsync(CancellationToken cancellationToken) => context.SaveChangesAsync(cancellationToken);

    public async Task<T> ForUserAsync<T>(Guid userId, Func<CancellationToken, Task<T>> operation, CancellationToken cancellationToken)
    {
        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT id FROM users WHERE id = {userId} FOR UPDATE", cancellationToken);
        var result = await operation(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return result;
    }
}
