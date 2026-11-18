namespace AiAssistant.Domain;

public sealed class MessageFeedback
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
    public required Guid MessageId { get; init; }
    public required Guid UserId { get; init; }
    public int Rating { get; set; }
    public string? Reason { get; set; }
    public string? Comment { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
