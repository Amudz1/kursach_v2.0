using AiAssistant.Application.Common;

namespace AiAssistant.Application.Voice;

public sealed record VoiceLimits(int MaxSeconds = 120, int MaxBytes = 12 * 1024 * 1024,
    int StartsPerMinute = 6, int TimeoutSeconds = 45)
{
    public const int MaxUploadBytes = 16 * 1024 * 1024;
}

public sealed record VoiceCapabilities(bool Available, int MaxSeconds, int MaxBytes, int StartsPerMinute,
    int TimeoutSeconds, string[] Languages, string[] MimeTypes);
public sealed record VoiceTranscript(string Text, string? Language);
public sealed record PreparedVoice(byte[] Wav);

public interface IVoiceAudio
{
    Task<Result<PreparedVoice>> PrepareAsync(Stream input, CancellationToken cancellationToken);
}

public interface IVoiceTranscriber
{
    bool Available { get; }
    Task<Result<VoiceTranscript>> TranscribeAsync(PreparedVoice audio, string? language, CancellationToken cancellationToken);
}
