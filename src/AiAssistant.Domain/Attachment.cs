namespace AiAssistant.Domain;

public sealed class Attachment
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
    public required Guid MessageId { get; init; }
    public required string FileName { get; init; }
    public required string StorageKey { get; init; }
    public required string ContentType { get; init; }
    public long Size { get; init; }
    public string? ExtractedText { get; init; }
}
