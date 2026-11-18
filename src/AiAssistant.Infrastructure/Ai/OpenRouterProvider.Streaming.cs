using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text.Json;
using AiAssistant.Application.Ai;

namespace AiAssistant.Infrastructure.Ai;

public sealed partial class OpenRouterProvider
{
    public async IAsyncEnumerable<AiChunk> StreamAsync(AiRequest request,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        using var message = new HttpRequestMessage(HttpMethod.Post, "chat/completions")
        {
            Content = JsonContent.Create(new
            {
                model = request.Model,
                messages = request.Messages.Select(item => new { role = item.Role, content = item.Content }),
                temperature = request.Temperature,
                max_tokens = request.MaxTokens,
                stream = true,
                stream_options = new { include_usage = true }
            })
        };

        using var response = await client.SendAsync(message, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        RequireSuccess(response);
        await using var body = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var reader = new StreamReader(body);
        var finished = false;
        while (await reader.ReadLineAsync(cancellationToken) is { } line)
        {
            if (!line.StartsWith("data:", StringComparison.Ordinal)) continue;
            var data = line[5..].Trim();
            if (data == "[DONE]") yield break;
            if (data.Length == 0) continue;
            using var document = JsonDocument.Parse(data);
            var frame = document.RootElement;
            if (frame.TryGetProperty("error", out var error) && error.ValueKind == JsonValueKind.Object)
                throw new AiProviderException("ai_stream_error", "Сервис модели прервал ответ.");

            if (frame.TryGetProperty("choices", out var choices) && choices.ValueKind == JsonValueKind.Array &&
                choices.GetArrayLength() > 0)
            {
                var choice = choices[0];
                if (choice.TryGetProperty("finish_reason", out var reason) && reason.ValueKind == JsonValueKind.String)
                {
                    if (reason.GetString() == "error")
                        throw new AiProviderException("ai_stream_error", "Сервис модели прервал ответ.");
                    finished = true;
                }
                if (choice.TryGetProperty("delta", out var delta) && delta.ValueKind == JsonValueKind.Object &&
                    delta.TryGetProperty("content", out var content) && content.ValueKind == JsonValueKind.String)
                {
                    yield return new(content.GetString());
                }
            }

            if (ReadUsage(frame) is { } usage) yield return new(null, usage);
        }

        if (!finished) throw new AiProviderException("ai_incomplete", "Поток ответа оборвался.");
    }
}
