namespace AiAssistant.Domain;

public sealed class UserMemory
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
    public required Guid UserId { get; init; }
    public required string Content { get; set; }
    public required string NormalizedContent { get; set; }
    public MemorySource Source { get; init; }
    public bool IsEnabled { get; set; } = true;
    public DateTimeOffset CreatedAt { get; init; }
}
