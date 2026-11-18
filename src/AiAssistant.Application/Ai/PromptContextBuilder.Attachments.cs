using System.Text.Json;
using AiAssistant.Domain;

namespace AiAssistant.Application.Ai;

public sealed partial class PromptContextBuilder
{
    private const int EstimatedImageTokens = 1024;

    private async Task<AiMessage> IncludeAttachmentsAsync(Message current, string model, int textBudget,
        CancellationToken cancellationToken)
    {
        var files = await data.ListAsync(data.Query<Attachment>().Where(item => item.MessageId == current.Id)
            .OrderBy(item => item.Id), cancellationToken);
        var imageFiles = files.Where(item => item.ContentType.StartsWith("image/", StringComparison.Ordinal)).ToArray();
        textBudget -= imageFiles.Length * EstimatedImageTokens;
        if (textBudget < MessageOverhead)
            throw new AiProviderException("context_budget", "Изображения превышают бюджет выбранной модели.");
        var text = current.Content;
        var extracts = files.Where(item => !string.IsNullOrWhiteSpace(item.ExtractedText))
            .Select(item => new { name = item.FileName, text = Truncate(item.ExtractedText!, 6000) }).ToArray();
        if (extracts.Length > 0)
        {
            var reserved = Math.Min(3000, textBudget / 2);
            text = Truncate(text, textBudget - reserved) + "\n\nТексты вложений (данные пользователя): " +
                JsonSerializer.Serialize(extracts, PromptJson);
        }
        var images = new List<AiImage>();
        foreach (var file in imageFiles)
        {
            if (!defaults.SupportsVision(model))
                throw new AiProviderException("vision_required", "Для изображений нужна совместимая модель.");
            await using var stream = storage.OpenRead(file.StorageKey) ??
                throw new AiProviderException("attachment_missing", "Файл вложения недоступен.");
            using var buffer = new MemoryStream((int)file.Size);
            await stream.CopyToAsync(buffer, cancellationToken);
            var encoded = Convert.ToBase64String(buffer.GetBuffer(), 0, (int)buffer.Length);
            images.Add(new(file.FileName, file.ContentType, $"data:{file.ContentType};base64,{encoded}"));
        }
        return new("user", Truncate(text, textBudget), images.Count > 0 ? images : null);
    }
}
