using AiAssistant.Application.Persistence;
using AiAssistant.Domain;

namespace AiAssistant.Application.Billing;

public sealed record UsageView(string PlanCode, string PlanName, int? DailyLimit, int UsedToday, int? RemainingPrompts);

/// <summary>Reads and updates UTC usage counters. Writes participate in the account write transaction.</summary>
public interface IUsageLimitService
{
    /// <summary>Reads current limits and consumption without updating the database.</summary>
    Task<UsageView> ReadAsync(Guid userId, CancellationToken cancellationToken);

    /// <summary>Reserves one request if the applicable UTC counter permits it.</summary>
    Task<bool> ReserveAsync(Guid userId, int? limit, DateTimeOffset startedAt, CancellationToken cancellationToken);

    /// <summary>Records provider-reported usage and optionally releases a reservation.</summary>
    Task FinishAsync(Guid userId, DateTimeOffset startedAt, bool refund, long tokens, CancellationToken cancellationToken);
}

public sealed class UsageLimitService(IAssistantData data, PlanAccess plans, TimeProvider clock) : IUsageLimitService
{
    public async Task<UsageView> ReadAsync(Guid userId, CancellationToken cancellationToken)
    {
        var plan = await plans.ResolveAsync(userId, cancellationToken);
        var counter = await FindAsync(userId, clock.GetUtcNow(), false, cancellationToken);
        var used = counter?.RequestsCount ?? 0;
        return new(plan.Code, plan.Name, plan.DailyRequestLimit, used,
            plan.DailyRequestLimit is { } limit ? Math.Max(0, limit - used) : null);
    }

    public async Task<bool> ReserveAsync(Guid userId, int? limit, DateTimeOffset startedAt, CancellationToken cancellationToken)
    {
        var counter = await FindAsync(userId, startedAt, true, cancellationToken);
        if (limit.HasValue && (counter?.RequestsCount ?? 0) >= limit.Value) return false;
        if (counter is null)
        {
            counter = new UsageCounter { UserId = userId, Date = DateOnly.FromDateTime(startedAt.UtcDateTime) };
            data.Add(counter);
        }
        counter.RequestsCount++;
        return true;
    }

    public async Task FinishAsync(Guid userId, DateTimeOffset startedAt, bool refund, long tokens, CancellationToken cancellationToken)
    {
        var counter = await FindAsync(userId, startedAt, true, cancellationToken);
        if (counter is null) return;
        if (refund && counter.RequestsCount > 0) counter.RequestsCount--;
        counter.TokensUsed += Math.Max(0, tokens);
    }

    private Task<UsageCounter?> FindAsync(Guid userId, DateTimeOffset time, bool tracked, CancellationToken cancellationToken)
    {
        var date = DateOnly.FromDateTime(time.UtcDateTime);
        return data.FirstAsync(data.Query<UsageCounter>(tracked).Where(counter => counter.UserId == userId && counter.Date == date),
            cancellationToken);
    }
}
