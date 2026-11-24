using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using AiAssistant.Application.Common;
using AiAssistant.Application.Voice;

namespace AiAssistant.Infrastructure.Voice;

public sealed class OpenAiVoiceTranscriber(IHttpClientFactory clients) : IVoiceTranscriber
{
    public const string ClientName = "voice-openai";
    public bool Available => true;

    public async Task<Result<VoiceTranscript>> TranscribeAsync(PreparedVoice audio, string? language, CancellationToken cancellationToken)
    {
        using var form = new MultipartFormDataContent();
        form.Add(new StringContent("gpt-transcribe"), "model");
        var file = new ByteArrayContent(audio.Wav);
        file.Headers.ContentType = new MediaTypeHeaderValue("audio/wav");
        form.Add(file, "file", "recording.wav");
        if (language is not null) form.Add(new StringContent(language), "languages[]");
        try
        {
            using var client = clients.CreateClient(ClientName);
            using var response = await client.PostAsync("audio/transcriptions", form, cancellationToken);
            if (response.StatusCode is HttpStatusCode.RequestTimeout or HttpStatusCode.GatewayTimeout)
                return Result<VoiceTranscript>.Fail(ErrorKind.Unavailable, "voice_timeout", "Распознавание заняло слишком много времени.");
            if (!response.IsSuccessStatusCode) return Unavailable();
            var output = await response.Content.ReadFromJsonAsync<ProviderTranscript>(cancellationToken);
            if (output?.Text is null) return Unavailable();
            var detected = output.Languages?.FirstOrDefault()?.Code;
            return Result<VoiceTranscript>.Ok(new(output.Text.Trim(), detected));
        }
        catch (HttpRequestException) { return Unavailable(); }
        catch (JsonException) { return Unavailable(); }
    }

    private static Result<VoiceTranscript> Unavailable() =>
        Result<VoiceTranscript>.Fail(ErrorKind.Unavailable, "voice_unavailable", "Сервис распознавания речи сейчас недоступен.");

    private sealed record ProviderTranscript(string? Text, DetectedLanguage[]? Languages);
    private sealed record DetectedLanguage(string? Code);
}

public sealed class DemoVoiceTranscriber(string outcome = "success") : IVoiceTranscriber
{
    public bool Available => outcome != "disabled";

    public async Task<Result<VoiceTranscript>> TranscribeAsync(PreparedVoice audio, string? language, CancellationToken cancellationToken)
    {
        if (outcome == "timeout") await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
        if (outcome == "unavailable") return Result<VoiceTranscript>.Fail(ErrorKind.Unavailable, "voice_unavailable",
            "Сервис распознавания речи сейчас недоступен.");
        return Result<VoiceTranscript>.Ok(new(outcome == "silence" ? "" : "Это тестовая расшифровка Budz.", language ?? "ru"));
    }
}
