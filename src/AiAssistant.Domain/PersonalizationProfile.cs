namespace AiAssistant.Domain;

public sealed class PersonalizationProfile
{
    public required Guid UserId { get; init; }
    public string Language { get; set; } = "auto";
    public string Tone { get; set; } = "friendly";
    public string ExpertiseLevel { get; set; } = "beginner";
    public string ResponseFormat { get; set; } = "text";
    public string ResponseLength { get; set; } = "medium";
    public string About { get; set; } = "";
    public string[] Interests { get; set; } = [];
    public bool AutoMemoryEnabled { get; set; }
    public bool OnboardingCompleted { get; set; }
    public Guid? AdaptationAcknowledgedFor { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
