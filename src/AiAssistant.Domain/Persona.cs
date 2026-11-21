namespace AiAssistant.Domain;

public sealed class Persona
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
    public Guid? OwnerId { get; init; }
    public required string Name { get; set; }
    public string Description { get; set; } = "";
    public required string SystemPrompt { get; set; }
    public double Temperature { get; set; } = 0.5;
    public string? DefaultModel { get; set; }
    public string? Color { get; set; }
    public string? Icon { get; set; }
    public bool IsActive { get; set; } = true;
}
