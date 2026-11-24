using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using static AiAssistant.IntegrationTests.AssistantApiTests;
using Xunit;

namespace AiAssistant.IntegrationTests;

public sealed class SupportApiTests(ApiFixture fixture) : IClassFixture<ApiFixture>
{
    [Fact]
    public async Task AccountMessagesAndAdminRepliesArePrivateAndPersistAcrossSessions()
    {
        const string messages = "/api/v1/me/support/messages";
        const string queue = "/api/v1/admin/support";
        using var visitor = fixture.Client();
        using var owner = fixture.Client();
        using var other = fixture.Client();
        using var admin = fixture.Client();

        using var anonymous = await visitor.GetAsync(messages);
        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
        await RegisterAsync(owner);
        await RegisterAsync(other);
        using var account = await owner.GetAsync("/api/v1/me");
        var accountId = (await BodyAsync(account)).GetProperty("id").GetGuid();
        var thread = $"{queue}/{accountId}/messages";

        using var invalid = await owner.PostAsJsonAsync(messages, new { content = "  ", requestId = Guid.NewGuid() });
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        var question = new { content = "Как открыть закладки?", requestId = Guid.NewGuid() };
        using var sent = await owner.PostAsJsonAsync(messages, question);
        sent.EnsureSuccessStatusCode();
        var firstId = (await BodyAsync(sent)).GetProperty("id").GetInt64();
        using var retry = await owner.PostAsJsonAsync(messages, question);
        Assert.Equal(firstId, (await BodyAsync(retry)).GetProperty("id").GetInt64());
        using var conflict = await owner.PostAsJsonAsync(messages, new { content = "Другой текст", question.requestId });
        Assert.Equal(HttpStatusCode.Conflict, conflict.StatusCode);

        using var privateHistory = await other.GetAsync(messages);
        Assert.Empty((await BodyAsync(privateHistory)).GetProperty("messages").EnumerateArray());
        using var deniedQueue = await other.GetAsync(queue);
        Assert.Equal(HttpStatusCode.Forbidden, deniedQueue.StatusCode);
        using var deniedRead = await other.GetAsync(thread);
        Assert.Equal(HttpStatusCode.Forbidden, deniedRead.StatusCode);
        using var deniedWrite = await other.PostAsJsonAsync(thread, question);
        Assert.Equal(HttpStatusCode.Forbidden, deniedWrite.StatusCode);

        using var login = await admin.PostAsJsonAsync("/api/v1/auth/login", new { login = "demo-admin", password = ApiFixture.Password });
        login.EnsureSuccessStatusCode();
        admin.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", (await BodyAsync(login)).GetProperty("accessToken").GetString());
        using var listed = await admin.GetAsync(queue);
        var listedThread = Assert.Single((await BodyAsync(listed)).GetProperty("threads").EnumerateArray());
        Assert.Equal(accountId, listedThread.GetProperty("userId").GetGuid());
        using var adminHistory = await admin.GetAsync(thread);
        var received = Assert.Single((await BodyAsync(adminHistory)).GetProperty("messages").EnumerateArray());
        Assert.Equal(question.content, received.GetProperty("content").GetString());
        Assert.False(received.GetProperty("fromSupport").GetBoolean());

        const string answer = "Откройте оглавление беседы.";
        using var replied = await admin.PostAsJsonAsync(thread, new { content = answer, requestId = Guid.NewGuid() });
        replied.EnsureSuccessStatusCode();
        using var updates = await owner.GetAsync($"{messages}?after={firstId}");
        var reply = Assert.Single((await BodyAsync(updates)).GetProperty("messages").EnumerateArray());
        Assert.Equal(answer, reply.GetProperty("content").GetString());
        Assert.True(reply.GetProperty("fromSupport").GetBoolean());

        using var refresh = await owner.PostAsync("/api/v1/auth/refresh", null);
        refresh.EnsureSuccessStatusCode();
        owner.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", (await BodyAsync(refresh)).GetProperty("accessToken").GetString());
        using var persisted = await owner.GetAsync(messages);
        Assert.Equal(2, (await BodyAsync(persisted)).GetProperty("messages").GetArrayLength());
        using var delete = await owner.SendAsync(new HttpRequestMessage(HttpMethod.Delete, "/api/v1/me")
        {
            Content = JsonContent.Create(new { password = ApiFixture.Password })
        });
        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);
        using var removed = await admin.GetAsync(thread);
        Assert.Equal(HttpStatusCode.NotFound, removed.StatusCode);
    }
}
