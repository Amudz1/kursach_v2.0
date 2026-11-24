namespace AiAssistant.Domain;

public sealed class User
{
    public const string CurrentInterface = "current";
    public const string FocusInterface = "focus";

    public Guid Id { get; init; } = Guid.CreateVersion7();
    public required string Username { get; set; }
    public required string NormalizedUsername { get; set; }
    public required string Email { get; set; }
    public required string NormalizedEmail { get; set; }
    public required string PasswordHash { get; set; }
    public Role Role { get; set; }
    public string InterfaceVariant { get; set; } = CurrentInterface;
    public bool IsBlocked { get; set; }
    public int SessionVersion { get; set; }
    public DateTimeOffset CreatedAt { get; init; }
}
