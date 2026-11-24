namespace AiAssistant.Domain;

public sealed class Workspace
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
    public required Guid UserId { get; init; }
    public required string Name { get; set; }
    public string Instructions { get; set; } = "";
    public string Materials { get; set; } = "";
}
