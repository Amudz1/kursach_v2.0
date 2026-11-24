namespace AiAssistant.Domain;

public sealed class Message
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
    public required Guid ChatId { get; init; }
    public Guid? PersonaId { get; init; }
    public Guid? RequestId { get; init; }
    public Guid? TriggerMessageId { get; init; }
    public required MessageRole Role { get; init; }
    public GenerationStatus Status { get; set; }
    public required string Content { get; set; }
    public string? Model { get; init; }
    public int? PromptTokens { get; set; }
    public int? CompletionTokens { get; set; }
    public int ReservedTokens { get; set; }
    public string? ContextSnapshot { get; set; }
    public string? AppliedRules { get; set; }
    public string[] Suggestions { get; set; } = [];
    public string? ErrorCode { get; set; }
    public bool FollowUpCompleted { get; set; }
    public DateTimeOffset CreatedAt { get; init; }
}
