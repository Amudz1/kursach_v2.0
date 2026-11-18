using System.Net;
using System.Net.Http.Headers;
using AiAssistant.Application.Ai;
using AiAssistant.Infrastructure.Ai;
using Microsoft.Extensions.Http.Resilience;
using Polly;

namespace AiAssistant.Api.Ai;

internal static class AiSetup
{
    public static IServiceCollection AddAssistantAi(this IServiceCollection services, IConfiguration settings,
        IHostEnvironment environment)
    {
        var mode = settings["Ai:Mode"] ?? "OpenRouter";
        var model = settings["Ai:Model"] ?? "deepseek/deepseek-chat";
        if (mode.Equals("Demo", StringComparison.OrdinalIgnoreCase))
        {
            if (!environment.IsDevelopment())
                throw new InvalidOperationException("Режим Ai:Mode=Demo доступен в Development.");
            services.AddSingleton(new AiDefaults(model, true));
            services.AddSingleton<IAiProvider, DemoAiProvider>();
            return services;
        }
        if (!mode.Equals("OpenRouter", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Задайте Ai:Mode: OpenRouter или Demo.");

        var key = settings["OpenRouter:ApiKey"];
        if (string.IsNullOrWhiteSpace(key)) key = settings["OPENROUTER_API_KEY"];
        if (string.IsNullOrWhiteSpace(key)) throw new InvalidOperationException("Задайте OpenRouter:ApiKey.");

        services.AddSingleton(new AiDefaults(model, false));
        services.AddHttpClient<IAiProvider, OpenRouterProvider>(client =>
        {
            client.BaseAddress = new Uri("https://openrouter.ai/api/v1/");
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", key);
            client.DefaultRequestHeaders.Add("X-Title", "AI Helper Budz");
            client.Timeout = TimeSpan.FromSeconds(60);
        }).AddResilienceHandler("openrouter", pipeline =>
        {
            pipeline.AddRetry(new HttpRetryStrategyOptions
            {
                MaxRetryAttempts = 2,
                Delay = TimeSpan.FromMilliseconds(300),
                MaxDelay = TimeSpan.FromSeconds(5),
                BackoffType = DelayBackoffType.Exponential,
                UseJitter = true,
                ShouldRetryAfterHeader = true,
                ShouldHandle = new PredicateBuilder<HttpResponseMessage>().HandleResult(IsTemporary)
            });
            pipeline.AddCircuitBreaker(new HttpCircuitBreakerStrategyOptions
            {
                ShouldHandle = new PredicateBuilder<HttpResponseMessage>().HandleResult(IsTemporary)
                    .Handle<HttpRequestException>(),
                SamplingDuration = TimeSpan.FromSeconds(30),
                FailureRatio = 0.75,
                MinimumThroughput = 8,
                BreakDuration = TimeSpan.FromSeconds(20)
            });
        });
        return services;
    }

    private static bool IsTemporary(HttpResponseMessage response) =>
        response.StatusCode is HttpStatusCode.TooManyRequests or HttpStatusCode.ServiceUnavailable;
}
