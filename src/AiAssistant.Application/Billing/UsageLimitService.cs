using AiAssistant.Application.Persistence;
using AiAssistant.Domain;

namespace AiAssistant.Application.Billing;

public sealed record UsageView(string PlanCode, string PlanName, long? DailyTokenLimit, long UsedTokensToday,
    long ReservedTokens, long? RemainingTokens, int RequestsToday);

/// <summary>Reads and updates UTC token budgets. Writes participate in the account write transaction.</summary>
public interface IUsageLimitService
{
    /// <summary>Reads today's token balance, outstanding reservations and request statistics.</summary>
    Task<UsageView> ReadAsync(Guid userId, CancellationToken cancellationToken);

    /// <summary>Checks the available token balance and records a queued request for analytics.</summary>
    Task<bool> EnqueueAsync(Guid userId, long? limit, DateTimeOffset startedAt, CancellationToken cancellationToken);

    /// <summary>Returns the remaining budget for the day on which the generation was queued.</summary>
    Task<long?> RemainingAsync(Guid userId, DateTimeOffset startedAt, CancellationToken cancellationToken);

    /// <summary>Records consumed tokens and optionally removes a request that produced no usage.</summary>
    Task FinishAsync(Guid userId, DateTimeOffset startedAt, bool refund, long tokens, CancellationToken cancellationToken);
}

public sealed class UsageLimitService(IAssistantData data, PlanAccess plans, TimeProvider clock) : IUsageLimitService
{
    public const int MinimumGenerationTokens = 900;

    public async Task<UsageView> ReadAsync(Guid userId, CancellationToken cancellationToken)
    {
        var plan = await plans.ResolveAsync(userId, cancellationToken);
        var now = clock.GetUtcNow();
        var counter = await FindAsync(userId, now, false, cancellationToken);
        var reserved = await ReservedAsync(userId, now, cancellationToken);
        var used = counter?.TokensUsed ?? 0;
        return new(plan.Code, plan.Name, plan.DailyTokenLimit, used, reserved,
            CalculateRemaining(plan.DailyTokenLimit, used, reserved), counter?.RequestsCount ?? 0);
    }

    public async Task<bool> EnqueueAsync(Guid userId, long? limit, DateTimeOffset startedAt, CancellationToken cancellationToken)
    {
        var counter = await FindAsync(userId, startedAt, true, cancellationToken);
        if (limit.HasValue)
        {
            var reserved = await ReservedAsync(userId, startedAt, cancellationToken);
            if (CalculateRemaining(limit, counter?.TokensUsed ?? 0, reserved) < MinimumGenerationTokens) return false;
        }
        if (counter is null)
        {
            counter = new UsageCounter { UserId = userId, Date = DateOnly.FromDateTime(startedAt.UtcDateTime) };
            data.Add(counter);
        }
        counter.RequestsCount++;
        return true;
    }

    public async Task<long?> RemainingAsync(Guid userId, DateTimeOffset startedAt, CancellationToken cancellationToken)
    {
        var plan = await plans.ResolveAsync(userId, cancellationToken);
        if (!plan.DailyTokenLimit.HasValue) return null;
        var counter = await FindAsync(userId, startedAt, false, cancellationToken);
        var reserved = await ReservedAsync(userId, startedAt, cancellationToken);
        return CalculateRemaining(plan.DailyTokenLimit, counter?.TokensUsed ?? 0, reserved);
    }

    public async Task FinishAsync(Guid userId, DateTimeOffset startedAt, bool refund, long tokens, CancellationToken cancellationToken)
    {
        var counter = await FindAsync(userId, startedAt, true, cancellationToken);
        if (counter is null) return;
        if (refund && counter.RequestsCount > 0) counter.RequestsCount--;
        counter.TokensUsed += Math.Max(0, tokens);
    }

    private Task<long> ReservedAsync(Guid userId, DateTimeOffset time, CancellationToken cancellationToken)
    {
        var start = new DateTimeOffset(time.UtcDateTime.Date, TimeSpan.Zero);
        var end = start.AddDays(1);
        var active = from message in data.Query<Message>()
                     join chat in data.Query<Chat>() on message.ChatId equals chat.Id
                     where chat.UserId == userId && message.ReservedTokens > 0 &&
                         message.CreatedAt >= start && message.CreatedAt < end
                     group message by 1 into reservations
                     select reservations.Sum(message => (long)message.ReservedTokens);
        return data.FirstAsync(active, cancellationToken);
    }

    private static long? CalculateRemaining(long? limit, long used, long reserved) =>
        limit is { } maximum ? Math.Max(0, maximum - used - reserved) : null;

    private Task<UsageCounter?> FindAsync(Guid userId, DateTimeOffset time, bool tracked, CancellationToken cancellationToken)
    {
        var date = DateOnly.FromDateTime(time.UtcDateTime);
        return data.FirstAsync(data.Query<UsageCounter>(tracked).Where(counter => counter.UserId == userId && counter.Date == date),
            cancellationToken);
    }
}
