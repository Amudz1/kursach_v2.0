using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using static AiAssistant.IntegrationTests.AssistantApiTests;
using Xunit;

namespace AiAssistant.IntegrationTests;

public sealed class AccountInterfaceApiTests(ApiFixture fixture) : IClassFixture<ApiFixture>
{
    [Fact]
    public async Task InterfaceChoiceBelongsToAccountAndIsIndependentOfAppearance()
    {
        const string endpoint = "/api/v1/me/interface";
        const string appearance = "/api/v1/me/appearance";
        using var anonymous = fixture.Client();
        using var owner = fixture.Client();
        using var other = fixture.Client();
        using var nextSession = fixture.Client();

        using var denied = await anonymous.PutAsJsonAsync(endpoint, new { interfaceVariant = "focus" });
        Assert.Equal(HttpStatusCode.Unauthorized, denied.StatusCode);

        await RegisterAsync(owner);
        await RegisterAsync(other);
        var account = await Account(owner);
        var ownerId = account.GetProperty("id").GetGuid();
        Assert.Equal("current", account.GetProperty("interfaceVariant").GetString());
        Assert.Equal("current", (await Account(other)).GetProperty("interfaceVariant").GetString());

        using var otherSelection = await other.PutAsJsonAsync(endpoint, new { interfaceVariant = "focus", userId = ownerId });
        Assert.Equal(HttpStatusCode.NoContent, otherSelection.StatusCode);
        Assert.Equal("current", (await Account(owner)).GetProperty("interfaceVariant").GetString());
        Assert.Equal("focus", (await Account(other)).GetProperty("interfaceVariant").GetString());

        using var theme = await owner.PutAsJsonAsync(appearance, new
        {
            schemaVersion = 1, preset = "midnight", colorMode = "dark", settings = new { messageOutline = "oval" }
        });
        theme.EnsureSuccessStatusCode();
        for (var attempt = 0; attempt < 2; attempt++)
        {
            using var changed = await owner.PutAsJsonAsync(endpoint, new { interfaceVariant = "focus" });
            Assert.Equal(HttpStatusCode.NoContent, changed.StatusCode);
        }

        foreach (var invalidValue in new[] { "unknown", "FOCUS", " ", null })
        {
            using var invalid = await owner.PutAsJsonAsync(endpoint, new { interfaceVariant = invalidValue });
            Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
            Assert.True((await BodyAsync(invalid)).GetProperty("errors").TryGetProperty("interfaceVariant", out _));
        }
        using var missing = await owner.PutAsJsonAsync(endpoint, new { });
        Assert.Equal(HttpStatusCode.BadRequest, missing.StatusCode);

        using var login = await nextSession.PostAsJsonAsync("/api/v1/auth/login", new
        {
            login = account.GetProperty("username").GetString(), password = ApiFixture.Password
        });
        login.EnsureSuccessStatusCode();
        nextSession.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer",
            (await BodyAsync(login)).GetProperty("accessToken").GetString());
        var reloadedAccount = await Account(nextSession);
        Assert.Equal(ownerId, reloadedAccount.GetProperty("id").GetGuid());
        Assert.Equal("focus", reloadedAccount.GetProperty("interfaceVariant").GetString());

        using var originalTheme = await nextSession.GetAsync(appearance);
        var currentTheme = await BodyAsync(originalTheme);
        Assert.Equal("midnight", currentTheme.GetProperty("preset").GetString());
        Assert.Equal("oval", currentTheme.GetProperty("settings").GetProperty("messageOutline").GetString());

        using var replacementTheme = await nextSession.PutAsJsonAsync(appearance, new
        {
            schemaVersion = 1, preset = "studio", colorMode = "light", settings = new { messageOutline = "side" }
        });
        replacementTheme.EnsureSuccessStatusCode();
        Assert.Equal("focus", (await Account(nextSession)).GetProperty("interfaceVariant").GetString());
        using var resetTheme = await nextSession.DeleteAsync(appearance);
        Assert.Equal(HttpStatusCode.NoContent, resetTheme.StatusCode);
        Assert.Equal("focus", (await Account(nextSession)).GetProperty("interfaceVariant").GetString());

        using var restored = await nextSession.PutAsJsonAsync(endpoint, new { interfaceVariant = "current" });
        Assert.Equal(HttpStatusCode.NoContent, restored.StatusCode);
        Assert.Equal("current", (await Account(owner)).GetProperty("interfaceVariant").GetString());
        Assert.Equal("focus", (await Account(other)).GetProperty("interfaceVariant").GetString());
    }

    private static async Task<JsonElement> Account(HttpClient client)
    {
        using var response = await client.GetAsync("/api/v1/me");
        response.EnsureSuccessStatusCode();
        return await BodyAsync(response);
    }
}
