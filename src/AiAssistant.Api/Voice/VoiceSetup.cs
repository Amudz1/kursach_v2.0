using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Threading.RateLimiting;
using AiAssistant.Application.Voice;
using AiAssistant.Infrastructure.Voice;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace AiAssistant.Api.Voice;

internal static class VoiceSetup
{
    public const string RatePolicy = "voice-transcriptions";

    public static IServiceCollection AddAssistantVoice(this IServiceCollection services, IConfiguration configuration, IHostEnvironment environment)
    {
        var limits = new VoiceLimits(configuration.GetValue("Voice:MaxSeconds", 120),
            configuration.GetValue("Voice:MaxBytes", 12 * 1024 * 1024), configuration.GetValue("Voice:StartsPerMinute", 6),
            configuration.GetValue("Voice:TimeoutSeconds", 45));
        if (limits.MaxSeconds is < 1 or > 300 || limits.MaxBytes is < 1 or > VoiceLimits.MaxUploadBytes ||
            limits.StartsPerMinute is < 1 or > 60 || limits.TimeoutSeconds is < 1 or > 120)
            throw new InvalidOperationException("Проверьте настройки лимитов Voice.");
        services.AddSingleton(limits);
        services.AddSingleton<IVoiceAudio, FfmpegVoiceAudio>();
        services.AddSingleton<VoiceService>();
        services.AddRateLimiter(options => options.AddPolicy<string, VoiceRatePolicy>(RatePolicy));

        var mode = configuration["Voice:Mode"] ?? "disabled";
        switch (mode.ToLowerInvariant())
        {
            case "openai" when !string.IsNullOrWhiteSpace(configuration["Voice:OpenAiApiKey"]):
                services.AddHttpClient(OpenAiVoiceTranscriber.ClientName, client =>
                {
                    client.BaseAddress = new Uri("https://api.openai.com/v1/");
                    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", configuration["Voice:OpenAiApiKey"]);
                    client.Timeout = Timeout.InfiniteTimeSpan;
                    client.MaxResponseContentBufferSize = 256 * 1024;
                });
                services.AddSingleton<IVoiceTranscriber, OpenAiVoiceTranscriber>();
                break;
            case "demo" when environment.IsDevelopment():
                var outcome = configuration["Voice:DemoOutcome"] ?? "success";
                if (outcome is not ("success" or "timeout" or "unavailable" or "silence"))
                    throw new InvalidOperationException("Проверьте Voice:DemoOutcome.");
                services.AddSingleton<IVoiceTranscriber>(new DemoVoiceTranscriber(outcome));
                break;
            case "disabled":
            case "openai":
                services.AddSingleton<IVoiceTranscriber>(new DemoVoiceTranscriber("disabled"));
                break;
            default:
                throw new InvalidOperationException("Проверьте Voice:Mode. Режим Demo доступен в Development.");
        }
        return services;
    }
}

internal sealed class VoiceRatePolicy(VoiceLimits limits) : IRateLimiterPolicy<string>
{
    public RateLimitPartition<string> GetPartition(HttpContext context) =>
        RateLimitPartition.GetSlidingWindowLimiter(context.User.FindFirstValue(JwtRegisteredClaimNames.Sub) ?? "guest", _ => new()
        {
            Window = TimeSpan.FromMinutes(1), SegmentsPerWindow = 6, PermitLimit = limits.StartsPerMinute, QueueLimit = 0,
        });

    public Func<OnRejectedContext, CancellationToken, ValueTask> OnRejected => async (rejection, ct) =>
    {
        var response = rejection.HttpContext.Response;
        response.StatusCode = StatusCodes.Status429TooManyRequests;
        var seconds = rejection.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter)
            ? Math.Max(1, Math.Ceiling(retryAfter.TotalSeconds)) : 60;
        response.Headers.RetryAfter = seconds.ToString(System.Globalization.CultureInfo.InvariantCulture);
        var problem = new ProblemDetails { Status = response.StatusCode, Title = "Слишком много попыток распознавания. Повторите позже." };
        problem.Extensions["code"] = "voice_rate_limit";
        await response.WriteAsJsonAsync(problem, options: null, contentType: "application/problem+json", cancellationToken: ct);
    };
}
