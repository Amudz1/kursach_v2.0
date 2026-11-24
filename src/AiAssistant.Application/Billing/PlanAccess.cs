using AiAssistant.Application.Persistence;
using AiAssistant.Domain;

namespace AiAssistant.Application.Billing;

public sealed class PlanAccess(IAssistantData data, TimeProvider clock)
{
    public async Task<SubscriptionPlan> ResolveAsync(Guid userId, CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow();
        var paid = await data.FirstAsync(
            from subscription in data.Query<Subscription>()
            join plan in data.Query<SubscriptionPlan>() on subscription.PlanId equals plan.Id
            where subscription.UserId == userId && subscription.Status == SubscriptionStatus.Active &&
                  subscription.StartsAt <= now && subscription.EndsAt > now
            select plan, cancellationToken);
        return paid ?? await data.FirstAsync(data.Query<SubscriptionPlan>().Where(plan => plan.Code == "Free"), cancellationToken)
            ?? throw new InvalidOperationException("Required Free plan is missing.");
    }
}
