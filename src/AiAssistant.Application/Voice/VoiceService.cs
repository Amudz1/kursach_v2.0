using System.Collections.Concurrent;
using AiAssistant.Application.Common;

namespace AiAssistant.Application.Voice;

public sealed class VoiceService(VoiceLimits limits, IVoiceAudio audio, IVoiceTranscriber provider)
{
    private static readonly string[] Languages = ["auto", "ru"];
    private static readonly string[] MimeTypes = ["audio/webm;codecs=opus", "audio/mp4;codecs=mp4a.40.2", "audio/mp4", "audio/ogg;codecs=opus"];
    private readonly ConcurrentDictionary<Guid, byte> active = new();

    public VoiceCapabilities Capabilities() => new(provider.Available, limits.MaxSeconds, limits.MaxBytes,
        limits.StartsPerMinute, limits.TimeoutSeconds, Languages, MimeTypes);

    public async Task<Result<VoiceTranscript>> TranscribeAsync(Guid userId, Stream input, long size, string? requestedLanguage,
        CancellationToken cancellationToken)
    {
        var language = string.IsNullOrWhiteSpace(requestedLanguage) ? "auto" : requestedLanguage;
        if (!Languages.Contains(language, StringComparer.Ordinal))
            return Result<VoiceTranscript>.Fail(ErrorKind.Validation, "voice_request", "Выберите русский язык или автоопределение.");
        if (size <= 0 || size > limits.MaxBytes)
            return Result<VoiceTranscript>.Fail(ErrorKind.Validation, "voice_size", "Размер записи превышает лимит или запись пуста.");
        if (!provider.Available)
            return Result<VoiceTranscript>.Fail(ErrorKind.Unavailable, "voice_unavailable", "Распознавание речи сейчас недоступно.");
        if (!active.TryAdd(userId, 0))
            return Result<VoiceTranscript>.Fail(ErrorKind.Conflict, "voice_active", "Дождитесь завершения текущей расшифровки.");

        try
        {
            using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            deadline.CancelAfter(TimeSpan.FromSeconds(limits.TimeoutSeconds));
            var prepared = await audio.PrepareAsync(input, deadline.Token);
            if (!prepared.IsSuccess) return new(null, prepared.Error);
            var result = await provider.TranscribeAsync(prepared.Value!, language == "auto" ? null : language, deadline.Token);
            if (result.IsSuccess && string.IsNullOrWhiteSpace(result.Value?.Text))
                return Result<VoiceTranscript>.Fail(ErrorKind.Validation, "voice_silence", "Речь в записи не обнаружена.");
            return result;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return Result<VoiceTranscript>.Fail(ErrorKind.Unavailable, "voice_timeout", "Распознавание заняло слишком много времени.");
        }
        finally { active.TryRemove(userId, out _); }
    }
}
