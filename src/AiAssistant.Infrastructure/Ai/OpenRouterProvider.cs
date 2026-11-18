using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AiAssistant.Application.Ai;

namespace AiAssistant.Infrastructure.Ai;

public sealed partial class OpenRouterProvider(HttpClient client) : IAiProvider
{
    public async Task<AiReply> CompleteAsync(AiRequest request, CancellationToken cancellationToken)
    {
        try
        {
            using var response = await client.PostAsJsonAsync("chat/completions", Payload(request), cancellationToken);
            RequireSuccess(response);
            using var json = await JsonDocument.ParseAsync(
                await response.Content.ReadAsStreamAsync(cancellationToken), cancellationToken: cancellationToken);
            var root = json.RootElement;
            if (!root.TryGetProperty("choices", out var options) || options.GetArrayLength() == 0 ||
                !options[0].TryGetProperty("message", out var message) ||
                !message.TryGetProperty("content", out var content) || content.ValueKind != JsonValueKind.String ||
                string.IsNullOrWhiteSpace(content.GetString()))
            {
                throw new AiProviderException("ai_empty", "Модель вернула пустой ответ. Попробуйте уточнить вопрос.");
            }
            return new(content.GetString()!, ReadUsage(root));
        }
        catch (OperationCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            throw new AiProviderException("ai_timeout", "Модель не ответила вовремя.", exception);
        }
        catch (Exception exception) when (exception is HttpRequestException or JsonException or InvalidOperationException)
        {
            throw new AiProviderException("ai_unavailable", "Сервис модели временно недоступен.", exception);
        }
    }

    private static object Payload(AiRequest request) => new
    {
        model = request.Model,
        messages = request.Messages.Select(message => new { role = message.Role, content = message.Content }),
        temperature = request.Temperature,
        max_tokens = request.MaxTokens
    };

    private static AiUsage? ReadUsage(JsonElement root)
    {
        if (!root.TryGetProperty("usage", out var usage) || usage.ValueKind != JsonValueKind.Object) return null;
        return new(ReadCount(usage, "prompt_tokens"), ReadCount(usage, "completion_tokens"));
    }

    private static int ReadCount(JsonElement usage, string key) =>
        usage.TryGetProperty(key, out var value) && value.ValueKind == JsonValueKind.Number &&
        value.TryGetInt32(out var count) ? Math.Max(0, count) : 0;

    private static void RequireSuccess(HttpResponseMessage response)
    {
        if (response.IsSuccessStatusCode) return;
        throw response.StatusCode switch
        {
            HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden =>
                new AiProviderException("ai_credentials", "Сервис модели отклонил ключ доступа."),
            HttpStatusCode.TooManyRequests =>
                new AiProviderException("ai_rate_limit", "Сервис модели временно ограничил запросы."),
            HttpStatusCode.PaymentRequired =>
                new AiProviderException("ai_credit", "Сервис модели требует пополнения баланса."),
            _ => new AiProviderException("ai_unavailable", "Сервис модели не смог выполнить запрос.")
        };
    }
}
