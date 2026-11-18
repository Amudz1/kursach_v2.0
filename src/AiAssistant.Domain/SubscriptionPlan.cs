namespace AiAssistant.Domain;

public sealed class SubscriptionPlan
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
    public required string Code { get; init; }
    public required string Name { get; set; }
    public string Description { get; set; } = "";
    public decimal Price { get; set; }
    public int DurationDays { get; set; }
    public long? DailyTokenLimit { get; set; }
    public int MaxPersonas { get; set; }
    public int MaxMemories { get; set; }
    public int MaxAttachmentBytes { get; set; }
    public string[] AllowedModels { get; set; } = [];
    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;
}
