namespace AiAssistant.Application.Ai;

public sealed record AiImage(string FileName, string ContentType, string DataUrl);
public sealed record AiMessage(string Role, string Content, IReadOnlyList<AiImage>? Images = null);
public enum AiTask { Chat, ExtractMemory, Summarize, Suggest }
public sealed record AiRequest(string Model, IReadOnlyList<AiMessage> Messages, double Temperature = 0.5,
    int MaxTokens = 1200, AiTask Purpose = AiTask.Chat, ChatRules? Rules = null);
public sealed record AiUsage(int PromptTokens, int CompletionTokens);
public sealed record AiReply(string Content, AiUsage? Usage);
public sealed record AiChunk(string? Content, AiUsage? Usage = null);
public sealed record AiDefaults(string Model, bool Demo, int ContextBudget = 16000,
    IReadOnlyDictionary<string, int>? ModelBudgets = null, IReadOnlySet<string>? VisionModels = null)
{
    public int BudgetFor(string model) =>
        ModelBudgets is not null && ModelBudgets.TryGetValue(model, out var budget) ? budget : ContextBudget;
    public bool SupportsVision(string model) => VisionModels?.Contains(model) ?? model == "openai/gpt-4o-mini";
}

/// <summary>Generates assistant responses using the configured model provider.</summary>
public interface IAiProvider
{
    /// <summary>Returns a complete model response and its token usage when supplied by the provider.</summary>
    Task<AiReply> CompleteAsync(AiRequest request, CancellationToken cancellationToken);

    /// <summary>Streams partial text and final usage in provider order.</summary>
    IAsyncEnumerable<AiChunk> StreamAsync(AiRequest request, CancellationToken cancellationToken);
}

public sealed class AiProviderException(string code, string message, Exception? innerException = null)
    : Exception(message, innerException)
{
    public string Code { get; } = code;
}
