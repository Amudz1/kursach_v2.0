using System.Net;
using System.Text;
using System.Text.Json;
using AiAssistant.Application.Ai;
using AiAssistant.Infrastructure.Ai;
using Xunit;

namespace AiAssistant.UnitTests;

public sealed class OpenRouterTransportTests
{
    [Fact]
    public async Task StreamingReadsTextAndUsageAndWritesVisionPayload()
    {
        const string sse = """
            : OPENROUTER PROCESSING
            data: {"choices":[{"delta":{"content":"First "},"finish_reason":null}]}

            data: {"choices":[{"delta":{"content":"second."},"finish_reason":"stop"}]}

            data: {"choices":[],"usage":{"prompt_tokens":42,"completion_tokens":7}}

            data: [DONE]

            """;
        using var handler = new FakeHandler(HttpStatusCode.OK, sse, "text/event-stream");
        using var client = new HttpClient(handler) { BaseAddress = new Uri("https://stub.invalid/api/v1/") };
        var provider = new OpenRouterProvider(client);
        var request = new AiRequest("test/vision", [new("system", "Use the supplied context."),
            new("user", "Explain the picture.", [new("sample.png", "image/png", "data:image/png;base64,AAAA")])]);
        var chunks = new List<AiChunk>();
        await foreach (var item in provider.StreamAsync(request, CancellationToken.None)) chunks.Add(item);

        Assert.Equal("First second.", string.Concat(chunks.Select(chunk => chunk.Content)));
        Assert.Equal(new AiUsage(42, 7), chunks[^1].Usage);
        using var payload = JsonDocument.Parse(handler.Body);
        var sent = payload.RootElement;
        Assert.True(sent.GetProperty("stream").GetBoolean());
        Assert.Equal("data:image/png;base64,AAAA", sent.GetProperty("messages")[1].GetProperty("content")[1]
            .GetProperty("image_url").GetProperty("url").GetString());
        Assert.Equal("/api/v1/chat/completions", handler.Path);
    }

    [Theory]
    [InlineData(401, "ai_credentials")]
    [InlineData(429, "ai_rate_limit")]
    [InlineData(503, "ai_unavailable")]
    public async Task ProviderErrorsUseStableCodesAndHideTheResponseBody(int status, string code)
    {
        using var handler = new FakeHandler((HttpStatusCode)status, "private untrusted provider response", "text/plain");
        using var client = new HttpClient(handler) { BaseAddress = new Uri("https://stub.invalid/api/v1/") };
        var provider = new OpenRouterProvider(client);
        var result = await Assert.ThrowsAsync<AiProviderException>(() =>
            provider.CompleteAsync(new AiRequest("test/text", [new("user", "Hello")]), CancellationToken.None));
        Assert.Equal(code, result.Code);
        Assert.DoesNotContain("private untrusted", result.Message);
    }

    private sealed class FakeHandler(HttpStatusCode status, string content, string mediaType) : HttpMessageHandler
    {
        public string Body { get; private set; } = "";
        public string? Path { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Path = request.RequestUri?.AbsolutePath;
            Body = request.Content is null ? "" : await request.Content.ReadAsStringAsync(cancellationToken);
            return new(status) { Content = new StringContent(content, Encoding.UTF8, mediaType) };
        }
    }
}
