namespace AiAssistant.Domain;

public sealed class UsageCounter
{
    public required Guid UserId { get; init; }
    public required DateOnly Date { get; init; }
    public int RequestsCount { get; set; }
    public long TokensUsed { get; set; }
}
