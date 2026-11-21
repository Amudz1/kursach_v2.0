namespace AiAssistant.Domain;

public sealed class AppearanceSettings
{
    public required Guid UserId { get; init; }
    public short SchemaVersion { get; set; } = 1;
    public string Preset { get; set; } = "classic";
    public string ColorMode { get; set; } = "system";
    public string Settings { get; set; } = "{}";
    public DateTimeOffset UpdatedAt { get; set; }
}
