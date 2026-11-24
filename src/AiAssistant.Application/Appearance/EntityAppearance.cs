namespace AiAssistant.Application.Appearance;

public static class EntityAppearance
{
    private static readonly HashSet<string> Colors = ["cat-1", "cat-2", "cat-3", "cat-4", "cat-5", "cat-6", "cat-7", "cat-8"];
    private static readonly HashSet<string> Icons =
    [
        "sparkles", "bot", "code", "terminal", "book", "pen", "feather", "file", "folder", "lightbulb",
        "rocket", "star", "heart", "graduation", "briefcase", "flask", "atom", "music", "camera", "palette",
        "globe", "compass", "leaf", "zap", "coffee", "gamepad", "target", "wand",
    ];

    public static bool ValidColor(string? value) => value is null || Colors.Contains(value);
    public static bool ValidIcon(string? value) => value is null || Icons.Contains(value);
}
