namespace AiAssistant.Domain;

public sealed class MessageBookmark
{
    public required Guid UserId { get; init; }
    public required Guid MessageId { get; init; }
}
