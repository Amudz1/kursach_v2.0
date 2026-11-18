namespace AiAssistant.Domain;

public sealed class Chat
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
    public required Guid UserId { get; init; }
    public Guid? PersonaId { get; set; }
    public string Title { get; set; } = "Новый чат";
    public bool IsPinned { get; set; }
    public string? Summary { get; set; }
    public Guid? SummaryUntilMessageId { get; set; }
    public DateTimeOffset LastMessageAt { get; set; }
    public DateTimeOffset CreatedAt { get; init; }
}
