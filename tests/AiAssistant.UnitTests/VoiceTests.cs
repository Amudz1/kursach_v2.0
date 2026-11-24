using System.Net;
using System.Text;
using AiAssistant.Application.Common;
using AiAssistant.Application.Voice;
using AiAssistant.Infrastructure.Voice;
using Xunit;

namespace AiAssistant.UnitTests;

public sealed class VoiceTests
{
    [Fact]
    public async Task InvalidInputsAndSilentAudioNeverReachProvider()
    {
        var audio = new CapturingAudio
        {
            Output = Result<PreparedVoice>.Fail(ErrorKind.Validation, "voice_silence", "")
        };
        var provider = new CapturingProvider();
        var service = new VoiceService(new VoiceLimits(MaxBytes: 8), audio, provider);
        using var source = new MemoryStream([1]);
        var user = Guid.NewGuid();

        Assert.Equal("voice_request", (await service.TranscribeAsync(user, source, 1, "en", default)).Error!.Code);
        Assert.Equal("voice_size", (await service.TranscribeAsync(user, source, 9, "ru", default)).Error!.Code);
        Assert.Equal(0, audio.Total);
        Assert.Equal("voice_silence", (await service.TranscribeAsync(user, source, 1, "ru", default)).Error!.Code);
        Assert.Equal(1, audio.Total);
        Assert.Equal(0, provider.Total);
    }

    [Fact]
    public async Task TimeoutAndUnavailableAreDistinctAndReleaseTheAccountSlot()
    {
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var provider = new CapturingProvider
        {
            Invoke = async ct =>
            {
                entered.TrySetResult();
                await Task.Delay(Timeout.InfiniteTimeSpan, ct);
                return default;
            }
        };
        var service = new VoiceService(new VoiceLimits(TimeoutSeconds: 1), new CapturingAudio(), provider);
        var user = Guid.NewGuid();
        using var source = new MemoryStream([1]);
        using var duplicate = new MemoryStream([1]);
        var first = service.TranscribeAsync(user, source, 1, "auto", default);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(3));
        Assert.Equal("voice_active", (await service.TranscribeAsync(user, duplicate, 1, "auto", default)).Error!.Code);
        Assert.Equal("voice_timeout", (await first).Error!.Code);

        provider.Invoke = _ => Task.FromResult(Result<VoiceTranscript>.Fail(ErrorKind.Unavailable, "voice_unavailable", ""));
        Assert.Equal("voice_unavailable", (await service.TranscribeAsync(user, duplicate, 1, "ru", default)).Error!.Code);
        Assert.Equal(2, provider.Total);
        Assert.Equal("ru", provider.Language);
    }

    [Fact]
    public async Task CancelledRequestReleasesTheAccountSlot()
    {
        var provider = new CapturingProvider
        {
            Invoke = async ct =>
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, ct);
                return default;
            }
        };
        var service = new VoiceService(new VoiceLimits(), new CapturingAudio(), provider);
        var user = Guid.NewGuid();
        using var source = new MemoryStream([1]);
        using var cancellation = new CancellationTokenSource();
        var pending = service.TranscribeAsync(user, source, 1, null, cancellation.Token);
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending);

        provider.Invoke = _ => Task.FromResult(Result<VoiceTranscript>.Ok(new("Привет", "ru")));
        Assert.Equal("Привет", (await service.TranscribeAsync(user, source, 1, null, default)).Value!.Text);
        Assert.Null(provider.Language);
    }

    [Fact]
    public async Task OpenAiAdapterUsesWavAndCurrentLanguageContract()
    {
        using var handler = new CaptureHandler();
        var adapter = new OpenAiVoiceTranscriber(new ClientFactory(handler));
        var result = await adapter.TranscribeAsync(new PreparedVoice("RIFF"u8.ToArray()), "ru", default);
        Assert.True(result.IsSuccess);
        Assert.Equal(new VoiceTranscript("Проверка.", "ru"), result.Value);
        Assert.Equal("/v1/audio/transcriptions", handler.Path);
        Assert.Contains("gpt-transcribe", handler.Body);
        Assert.Contains("languages[]", handler.Body);
        Assert.Contains("audio/wav", handler.Body);
        Assert.Contains("RIFF", handler.Body);
    }

    private sealed class CapturingAudio : IVoiceAudio
    {
        public int Total { get; private set; }
        public Result<PreparedVoice> Output { get; init; } = Result<PreparedVoice>.Ok(new([1]));
        public Task<Result<PreparedVoice>> PrepareAsync(Stream input, CancellationToken cancellationToken)
        {
            Total++;
            return Task.FromResult(Output);
        }
    }

    private sealed class CapturingProvider : IVoiceTranscriber
    {
        public bool Available => true;
        public int Total { get; private set; }
        public string? Language { get; private set; }
        public Func<CancellationToken, Task<Result<VoiceTranscript>>> Invoke { get; set; } =
            _ => Task.FromResult(Result<VoiceTranscript>.Ok(new("Расшифровка.", "ru")));
        public Task<Result<VoiceTranscript>> TranscribeAsync(PreparedVoice audio, string? language, CancellationToken cancellationToken)
        {
            Total++;
            Language = language;
            return Invoke(cancellationToken);
        }
    }

    private sealed class CaptureHandler : HttpMessageHandler
    {
        public string Path { get; private set; } = "";
        public string Body { get; private set; } = "";
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Path = request.RequestUri!.AbsolutePath;
            Body = await request.Content!.ReadAsStringAsync(cancellationToken);
            return new(HttpStatusCode.OK)
            {
                Content = new StringContent("""{"text":" Проверка. ","languages":[{"code":"ru"}]}""", Encoding.UTF8, "application/json")
            };
        }
    }

    private sealed class ClientFactory(HttpMessageHandler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(handler, disposeHandler: false) { BaseAddress = new("https://example.test/v1/") };
    }
}
