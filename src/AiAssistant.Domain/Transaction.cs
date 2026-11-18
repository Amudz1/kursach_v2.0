namespace AiAssistant.Domain;

public sealed class Transaction
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
    public required Guid UserId { get; init; }
    public required Guid PlanId { get; init; }
    public required Guid RequestId { get; init; }
    public decimal Amount { get; init; }
    public string? CardMask { get; init; }
    public TransactionStatus Status { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
}
