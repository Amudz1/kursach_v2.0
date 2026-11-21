using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using static AiAssistant.IntegrationTests.AssistantApiTests;
using Xunit;

namespace AiAssistant.IntegrationTests;

public sealed class AppearanceApiTests(ApiFixture fixture) : IClassFixture<ApiFixture>
{

    [Fact]
    public async Task AppearanceRoundTripsValidatesAndRemainsPrivate()
    {
        const string endpoint = "/api/v1/me/appearance";
        using var owner = fixture.Client();
        using var other = fixture.Client();
        await RegisterAsync(owner);
        await RegisterAsync(other);
        using var defaults = await owner.GetAsync(endpoint);
        Assert.Equal("studio", (await BodyAsync(defaults)).GetProperty("preset").GetString());

        using var invalid = await owner.PutAsJsonAsync(endpoint, new
        {
            schemaVersion = 1, preset = "midnight", colorMode = "invalid", settings = new { accent = "red", messageOutline = "invalid" },
        });
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        var fields = (await BodyAsync(invalid)).GetProperty("errors");
        Assert.True(fields.TryGetProperty("colorMode", out _));
        Assert.True(fields.TryGetProperty("settings.accent", out _));
        Assert.True(fields.TryGetProperty("settings.messageOutline", out _));

        using var save = await owner.PutAsJsonAsync(endpoint, new
        {
            schemaVersion = 1, preset = "midnight", colorMode = "dark", settings = new { glow = "bright", accent = "#66aaff", messageOutline = "oval" },
        });
        save.EnsureSuccessStatusCode();
        using var stored = await owner.GetAsync(endpoint);
        var appearance = await BodyAsync(stored);
        Assert.Equal("midnight", appearance.GetProperty("preset").GetString());
        Assert.Equal("#66aaff", appearance.GetProperty("settings").GetProperty("accent").GetString());
        Assert.Equal("oval", appearance.GetProperty("settings").GetProperty("messageOutline").GetString());
        Assert.False(appearance.GetProperty("settings").TryGetProperty("glow", out _));
        using var privateResult = await other.GetAsync(endpoint);
        Assert.Equal("studio", (await BodyAsync(privateResult)).GetProperty("preset").GetString());

        using var export = await owner.GetAsync("/api/v1/data/export?format=json&includeAccount=true");
        var exported = (await BodyAsync(export)).GetProperty("appearance");
        Assert.Equal("midnight", exported.GetProperty("preset").GetString());
        Assert.Equal("oval", exported.GetProperty("settings").GetProperty("messageOutline").GetString());

        using var studio = await owner.PutAsJsonAsync(endpoint, new
        {
            schemaVersion = 1, preset = "studio", colorMode = "system", settings = new { messageOutline = "rectangle" },
        });
        Assert.Equal("rectangle", (await BodyAsync(studio)).GetProperty("settings").GetProperty("messageOutline").GetString());
        using var delete = await owner.DeleteAsync(endpoint);
        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);
        using var reset = await owner.GetAsync(endpoint);
        var legacyDefaults = await BodyAsync(reset);
        Assert.Equal("classic", legacyDefaults.GetProperty("preset").GetString());
        Assert.Equal(JsonValueKind.Null, legacyDefaults.GetProperty("updatedAt").ValueKind);
    }

    [Fact]
    public async Task EntityAppearanceIsOptionalAndUpdatesIndependently()
    {
        using var owner = fixture.Client();
        await RegisterAsync(owner);
        using var createWorkspace = await owner.PostAsJsonAsync("/api/v1/workspaces",
            new { name = "Цветной проект", color = "cat-3", icon = "folder" });
        Assert.Equal(HttpStatusCode.Created, createWorkspace.StatusCode);
        var workspaceId = (await BodyAsync(createWorkspace)).GetProperty("id").GetGuid();
        using var updateWorkspace = await owner.PutAsJsonAsync($"/api/v1/workspaces/{workspaceId}", new { name = "Файлы" });
        Assert.Equal(HttpStatusCode.NoContent, updateWorkspace.StatusCode);
        using var workspaces = await owner.GetAsync("/api/v1/workspaces");
        var workspace = Assert.Single((await BodyAsync(workspaces)).EnumerateArray());
        Assert.Equal("cat-3", workspace.GetProperty("color").GetString());
        Assert.Equal("folder", workspace.GetProperty("icon").GetString());

        var persona = new { name = "Редактор", description = "", instructions = "Уточняй детали.", temperature = .5 };
        using var invalid = await owner.PostAsJsonAsync("/api/v1/personas",
            new { persona.name, persona.description, persona.instructions, persona.temperature, icon = "unknown" });
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        using var createPersona = await owner.PostAsJsonAsync("/api/v1/personas",
            new { persona.name, persona.description, persona.instructions, persona.temperature, color = "cat-5", icon = "pen" });
        Assert.Equal(HttpStatusCode.Created, createPersona.StatusCode);
        var personaId = (await BodyAsync(createPersona)).GetProperty("id").GetGuid();
        using var changePersona = await owner.PutAsJsonAsync($"/api/v1/personas/{personaId}",
            new { persona.name, persona.description, persona.instructions, persona.temperature, color = (string?)null });
        Assert.Equal(HttpStatusCode.NoContent, changePersona.StatusCode);
        using var personas = await owner.GetAsync("/api/v1/personas");
        var saved = Assert.Single((await BodyAsync(personas)).EnumerateArray(), item => item.GetProperty("id").GetGuid() == personaId);
        Assert.Equal(JsonValueKind.Null, saved.GetProperty("color").ValueKind);
        Assert.Equal("pen", saved.GetProperty("icon").GetString());
    }

}
