namespace AiAssistant.Domain;

public sealed class SupportMessage
{
    public long Id { get; init; }
    public Guid UserId { get; init; }
    public Guid RequestId { get; init; }
    public bool FromSupport { get; init; }
    public required string Content { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
}
