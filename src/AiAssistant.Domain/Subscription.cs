namespace AiAssistant.Domain;

public sealed class Subscription
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
    public required Guid UserId { get; init; }
    public required Guid PlanId { get; init; }
    public DateTimeOffset StartsAt { get; init; }
    public DateTimeOffset EndsAt { get; init; }
    public SubscriptionStatus Status { get; set; }
}
