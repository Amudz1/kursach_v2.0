using AiAssistant.Application.Persistence;
using AiAssistant.Domain;

namespace AiAssistant.Application.Billing;

public sealed class SubscriptionWriter(IAssistantData data)
{
    public async Task ReplaceAsync(Guid userId, SubscriptionPlan plan, DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var previous = await data.FirstAsync(data.Query<Subscription>(true).Where(item => item.UserId == userId &&
            item.Status == SubscriptionStatus.Active), cancellationToken);
        if (previous is not null)
        {
            previous.Status = previous.EndsAt <= now ? SubscriptionStatus.Expired : SubscriptionStatus.Replaced;
            await data.SaveAsync(cancellationToken);
        }
        if (plan.Code != "Free")
        {
            data.Add(new Subscription { UserId = userId, PlanId = plan.Id, StartsAt = now,
                EndsAt = now.AddDays(plan.DurationDays) });
        }
    }
}
