using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using AiAssistant.Application.Personalization;
using Xunit;

namespace AiAssistant.IntegrationTests;

public sealed class AssistantApiTests(ApiFixture fixture) : IClassFixture<ApiFixture>
{
    [Fact]
    public async Task AccountSessionRotatesAndRejectsReplay()
    {
        using var client = fixture.Client(false);
        var user = NewLogin();
        using var signup = await client.PostAsJsonAsync("/api/v1/auth/register", Registration(user));
        Assert.Equal(HttpStatusCode.Created, signup.StatusCode);
        var json = await BodyAsync(signup);
        Assert.False(json.TryGetProperty("refreshToken", out _));
        client.DefaultRequestHeaders.Authorization = new("Bearer", json.GetProperty("accessToken").GetString());
        using var me = await client.GetAsync("/api/v1/me");
        Assert.Equal(user, (await BodyAsync(me)).GetProperty("username").GetString());

        var cookie = ReadCookie(signup);
        using var duplicate = await client.PostAsJsonAsync("/api/v1/auth/register", Registration(user));
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
        using var foreignOrigin = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/refresh");
        foreignOrigin.Headers.Add("Origin", "https://foreign.invalid");
        using var invalid = await client.SendAsync(foreignOrigin);
        Assert.Equal(HttpStatusCode.Forbidden, invalid.StatusCode);

        using var rotated = await RefreshAsync(client, cookie);
        Assert.Equal(HttpStatusCode.OK, rotated.StatusCode);
        var refreshedCookie = ReadCookie(rotated);
        Assert.True(cookie != refreshedCookie, "Refresh rotation must replace the cookie.");
        using var replay = await RefreshAsync(client, cookie);
        Assert.Equal(HttpStatusCode.Unauthorized, replay.StatusCode);
        using var staleSession = await RefreshAsync(client, refreshedCookie);
        Assert.Equal(HttpStatusCode.Unauthorized, staleSession.StatusCode);
    }

    [Fact]
    public async Task ChatHistoryIsIdempotentAndNeverLeaksToAnotherAccount()
    {
        using var owner = fixture.Client();
        using var other = fixture.Client();
        await RegisterAsync(owner);
        await RegisterAsync(other);
        var chat = await CreateChatAsync(owner);
        var path = $"/api/v1/chats/{chat}/messages";
        using var forbidden = await other.GetAsync(path);
        Assert.Equal(HttpStatusCode.NotFound, forbidden.StatusCode);

        var input = new { Content = "Как устроены коллекции C#?", RequestId = Guid.NewGuid() };
        using var first = await owner.PostAsJsonAsync(path, input);
        using var duplicate = await owner.PostAsJsonAsync(path, input);
        Assert.Equal(HttpStatusCode.Accepted, first.StatusCode);
        Assert.Equal(HttpStatusCode.Accepted, duplicate.StatusCode);
        var id = (await BodyAsync(first)).GetProperty("id").GetGuid();
        Assert.Equal(id, (await BodyAsync(duplicate)).GetProperty("id").GetGuid());
        var history = await WaitForAnswerAsync(owner, path, id);
        Assert.Equal(2, history.GetArrayLength());
        Assert.Equal("user", history[0].GetProperty("role").GetString());
        Assert.Equal("assistant", history[1].GetProperty("role").GetString());
        var answer = history[1].GetProperty("content").GetString()!;
        Assert.Contains("Демонстрационный", answer);
        using var account = await owner.GetAsync("/api/v1/me");
        var usage = (await BodyAsync(account)).GetProperty("usage");
        var expectedTokens = input.Content.Length / 4 + answer.Length / 4;
        Assert.Equal(expectedTokens, usage.GetProperty("usedTokensToday").GetInt64());
        Assert.Equal(0, usage.GetProperty("reservedTokens").GetInt64());
        Assert.Equal(usage.GetProperty("dailyTokenLimit").GetInt64() - expectedTokens,
            usage.GetProperty("remainingTokens").GetInt64());
        using var page = await owner.GetAsync(path + "?limit=1");
        var cursor = (await BodyAsync(page)).GetProperty("nextBefore").GetGuid();
        using var previous = await owner.GetAsync(path + $"?limit=1&before={cursor}");
        Assert.Equal("user", (await BodyAsync(previous)).GetProperty("items")[0].GetProperty("role").GetString());

        using var rating = await other.PostAsJsonAsync($"/api/v1/messages/{id}/feedback", new { rating = 1 });
        Assert.Equal(HttpStatusCode.NotFound, rating.StatusCode);
        using var savedRating = await owner.PostAsJsonAsync($"/api/v1/messages/{id}/feedback", new { rating = 1 });
        Assert.Equal(HttpStatusCode.NoContent, savedRating.StatusCode);
        using var stats = await owner.GetAsync("/api/v1/me/stats");
        Assert.Equal(1, (await BodyAsync(stats)).GetProperty("requests").GetInt32());

        var profile = (await owner.GetFromJsonAsync<ProfileView>("/api/v1/me/personalization"))! with
        {
            AdaptToTask = true, UseMemoryInReplies = false, Goal = "Подготовить диплом",
            TaskRules = [new("code", "short", "code", "Проверяй границы.")]
        };
        using var preferences = await owner.PutAsJsonAsync("/api/v1/me/personalization", profile);
        Assert.Equal(HttpStatusCode.NoContent, preferences.StatusCode);
        var saved = (await owner.GetFromJsonAsync<ProfileView>("/api/v1/me/personalization"))!;
        Assert.False(saved.UseMemoryInReplies);
        Assert.Equal(profile.TaskRules, saved.TaskRules);
        using var preview = await owner.PostAsJsonAsync("/api/v1/me/personalization/preview",
            new PreviewProfile(saved, "Проверь функцию на C#"));
        Assert.Equal(HttpStatusCode.OK, preview.StatusCode);
        Assert.Equal("short", (await BodyAsync(preview)).GetProperty("length").GetString());
    }

    [Fact]
    public async Task AttachmentUploadAndDownloadRespectOwnershipAndDeletion()
    {
        using var owner = fixture.Client();
        using var other = fixture.Client();
        await RegisterAsync(owner);
        await RegisterAsync(other);
        var chat = await CreateChatAsync(owner);
        var path = $"/api/v1/chats/{chat}/messages";
        using var form = new MultipartFormDataContent();
        form.Add(new StringContent(Guid.NewGuid().ToString()), "requestId");
        byte[] contents = Encoding.UTF8.GetBytes("Test extract for context");
        form.Add(new ByteArrayContent(contents), "attachment", "notes.txt");
        using var sent = await owner.PostAsync(path, form);
        Assert.Equal(HttpStatusCode.Accepted, sent.StatusCode);
        var id = (await BodyAsync(sent)).GetProperty("id").GetGuid();
        var history = await WaitForAnswerAsync(owner, path, id);
        var fileId = history[0].GetProperty("attachments")[0].GetProperty("id").GetGuid();
        var target = $"/api/v1/attachments/{fileId}";
        using var denied = await other.GetAsync(target);
        Assert.Equal(HttpStatusCode.NotFound, denied.StatusCode);
        using var download = await owner.GetAsync(target);
        Assert.Equal(contents, await download.Content.ReadAsByteArrayAsync());
        using var removed = await owner.DeleteAsync($"/api/v1/chats/{chat}");
        Assert.Equal(HttpStatusCode.NoContent, removed.StatusCode);
        using var missing = await owner.GetAsync(target);
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
    }

    [Fact]
    public async Task AdminContextAndApiDocumentationAreProtectedAsDocumented()
    {
        using var user = fixture.Client();
        using var admin = fixture.Client();
        await RegisterAsync(user);
        var chat = await CreateChatAsync(user);
        var path = $"/api/v1/chats/{chat}/messages";
        using var submitted = await user.PostAsJsonAsync(path, new { content = "Repeat the documented context" });
        var answer = (await BodyAsync(submitted)).GetProperty("id").GetGuid();
        await WaitForAnswerAsync(user, path, answer);
        using var login = await admin.PostAsJsonAsync("/api/v1/auth/login",
            new { login = "demo-admin", password = ApiFixture.Password });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        await SetTokenAsync(admin, login);

        var route = $"/api/v1/admin/messages/{answer}/context";
        using var denied = await user.GetAsync(route);
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        using var report = await admin.GetAsync(route);
        Assert.Equal(HttpStatusCode.OK, report.StatusCode);
        Assert.Equal("system", (await BodyAsync(report))[0].GetProperty("Role").GetString());
        using var health = await user.GetAsync("/health");
        Assert.Equal(HttpStatusCode.OK, health.StatusCode);
        using var definition = await user.GetAsync("/swagger/v1/swagger.json");
        Assert.Equal(HttpStatusCode.OK, definition.StatusCode);
        var operation = (await BodyAsync(definition)).GetProperty("paths")
            .GetProperty("/api/v1/chats/{chatId}/messages").GetProperty("post");
        Assert.True(operation.GetProperty("requestBody").GetProperty("content").TryGetProperty("multipart/form-data", out _));
        Assert.True(operation.GetProperty("responses").TryGetProperty("202", out _));
    }

    private static async Task RegisterAsync(HttpClient client)
    {
        using var result = await client.PostAsJsonAsync("/api/v1/auth/register", Registration(NewLogin()));
        Assert.Equal(HttpStatusCode.Created, result.StatusCode);
        await SetTokenAsync(client, result);
    }

    private static object Registration(string name) => new
    {
        username = name, email = name + "@demo.invalid", password = ApiFixture.Password, passwordConfirmation = ApiFixture.Password
    };

    private static string NewLogin() => "test-" + Guid.NewGuid().ToString("N")[..12];

    private static async Task SetTokenAsync(HttpClient client, HttpResponseMessage response) =>
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer",
            (await BodyAsync(response)).GetProperty("accessToken").GetString());

    private static async Task<Guid> CreateChatAsync(HttpClient client)
    {
        using var response = await client.PostAsync("/api/v1/chats", null);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await BodyAsync(response)).GetProperty("id").GetGuid();
    }

    private static string ReadCookie(HttpResponseMessage response)
    {
        var cookie = Assert.Single(response.Headers.GetValues("Set-Cookie"));
        Assert.Contains("httponly", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=strict", cookie, StringComparison.OrdinalIgnoreCase);
        return cookie.Split(';')[0];
    }

    private static async Task<HttpResponseMessage> RefreshAsync(HttpClient client, string cookie)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/refresh");
        request.Headers.Add("Cookie", cookie);
        return await client.SendAsync(request);
    }

    private static async Task<JsonElement> WaitForAnswerAsync(HttpClient client, string path, Guid answer)
    {
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        while (true)
        {
            using var response = await client.GetAsync(path, deadline.Token);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var messages = (await BodyAsync(response)).GetProperty("items");
            var item = messages.EnumerateArray().FirstOrDefault(message => message.GetProperty("id").GetGuid() == answer);
            if (item.ValueKind != JsonValueKind.Undefined)
            {
                var status = item.GetProperty("status").GetString();
                if (status == "completed") return messages;
                Assert.NotEqual("failed", status);
            }
            await Task.Delay(150, deadline.Token);
        }
    }

    private static Task<JsonElement> BodyAsync(HttpResponseMessage response) => response.Content.ReadFromJsonAsync<JsonElement>();
}
