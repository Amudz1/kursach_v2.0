using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Xunit;
using static AiAssistant.IntegrationTests.AssistantApiTests;

namespace AiAssistant.IntegrationTests;

public sealed class PortabilityApiTests(ApiFixture fixture) : IClassFixture<ApiFixture>
{

    [Fact]
    public async Task WorkspacesAndTransfersKeepScopeAndAppliedRules()
    {
        using var owner = fixture.Client();
        using var other = fixture.Client();
        await RegisterAsync(owner);
        await RegisterAsync(other);
        var definition = new { name = "Диплом", instructions = "Покажи проверку решения.", materials = "Материалы диплома." };
        using var workspace = await owner.PostAsJsonAsync("/api/v1/workspaces", definition);
        Assert.Equal(HttpStatusCode.Created, workspace.StatusCode);
        var space = (await BodyAsync(workspace)).GetProperty("id").GetGuid();
        using var unauthorized = await other.PostAsJsonAsync("/api/v1/chats", new { workspaceId = space });
        Assert.Equal(HttpStatusCode.NotFound, unauthorized.StatusCode);

        const string chatGpt = """
            [{"title":"Перенос 🧪","id":"external-one","current_node":"a","mapping":{
              "root":{"parent":null,"message":{"author":{"role":"system"},"content":{"parts":["Внутреннее правило"]}}},
              "u":{"parent":"root","message":{"author":{"role":"user"},"content":{"parts":["Привет from ChatGPT"]}}},
              "a":{"parent":"u","message":{"author":{"role":"assistant"},"content":{"parts":["Ответ α"]}}},
              "other":{"parent":"u","message":{"author":{"role":"assistant"},"content":{"parts":["Старая ветка"]}}}
            }}]
            """;
        using var foreignImport = await ImportAsync(other, chatGpt, space);
        Assert.Equal(HttpStatusCode.NotFound, foreignImport.StatusCode);
        using var imported = await ImportAsync(owner, chatGpt, space);
        Assert.Equal(HttpStatusCode.OK, imported.StatusCode);
        var chat = (await BodyAsync(imported)).GetProperty("chatIds")[0].GetGuid();
        using var repeated = await ImportAsync(owner, chatGpt, space);
        Assert.Equal(1, (await BodyAsync(repeated)).GetProperty("skipped").GetInt32());
        using var personal = await owner.GetAsync("/api/v1/chats?personalOnly=true");
        Assert.Empty((await BodyAsync(personal)).GetProperty("items").EnumerateArray());
        using var ownRules = await owner.GetAsync($"/api/v1/chats/{chat}/rules");
        Assert.Equal(definition.instructions, (await BodyAsync(ownRules)).GetProperty("workspaceInstructions").GetString());
        using var foreignRules = await other.GetAsync($"/api/v1/chats/{chat}/rules");
        Assert.Equal(HttpStatusCode.NotFound, foreignRules.StatusCode);

        using var exported = await owner.GetAsync($"/api/v1/data/export?chatId={chat}");
        Assert.Equal(HttpStatusCode.OK, exported.StatusCode);
        var portable = await exported.Content.ReadAsStringAsync();
        Assert.Contains("Ответ α", portable);
        Assert.DoesNotContain("Старая ветка", portable);
        Assert.DoesNotContain("Внутреннее правило", portable);
        using var foreignExport = await other.GetAsync($"/api/v1/data/export?chatId={chat}");
        Assert.Equal(HttpStatusCode.NotFound, foreignExport.StatusCode);
        using var restored = await ImportAsync(other, portable);
        var restoredReport = await BodyAsync(restored);
        Assert.Equal(1, restoredReport.GetProperty("imported").GetInt32());
        var restoredId = restoredReport.GetProperty("chatIds")[0].GetGuid();
        using var restoredHistory = await other.GetAsync($"/api/v1/chats/{restoredId}/messages");
        Assert.Equal(new[] { "Привет from ChatGPT", "Ответ α" }, (await BodyAsync(restoredHistory)).GetProperty("items")
            .EnumerateArray().Select(item => item.GetProperty("content").GetString()));
        using var markdown = await owner.GetAsync($"/api/v1/data/export?chatId={chat}&format=markdown");
        Assert.Contains("### Пользователь\n\nПривет from ChatGPT", await markdown.Content.ReadAsStringAsync());
        using var claude = await ImportAsync(owner, """
            [{"name":"Claude","uuid":"c1","chat_messages":[{"sender":"human","text":"Мой вопрос"},
            {"sender":"assistant","content":[{"type":"text","text":"Решение Claude"}]}]}]
            """);
        Assert.Equal(1, (await BodyAsync(claude)).GetProperty("imported").GetInt32());

        var messages = $"/api/v1/chats/{chat}/messages";
        using var sent = await owner.PostAsJsonAsync(messages, new { content = "Проверь мою функцию на C#" });
        Assert.Equal(HttpStatusCode.Accepted, sent.StatusCode);
        var answer = (await BodyAsync(sent)).GetProperty("id").GetGuid();
        var history = await WaitForAnswerAsync(owner, messages, answer);
        Assert.Equal(new[] { "user", "assistant", "user", "assistant" },
            history.EnumerateArray().Select(item => item.GetProperty("role").GetString()));
        var applied = history[3].GetProperty("rules");
        Assert.Equal("code", applied.GetProperty("task").GetString());
        Assert.Equal(definition.instructions, applied.GetProperty("workspaceInstructions").GetString());
        Assert.True(applied.GetProperty("workspaceMaterials").GetBoolean());
        Assert.False(applied.GetProperty("memoryEnabled").GetBoolean());

        using var renamed = await owner.PutAsJsonAsync($"/api/v1/workspaces/{space}", definition with { instructions = "Другое правило" });
        Assert.Equal(HttpStatusCode.NoContent, renamed.StatusCode);
        using var after = await owner.GetAsync(messages);
        Assert.Equal(definition.instructions, (await BodyAsync(after)).GetProperty("items")[3].GetProperty("rules")
            .GetProperty("workspaceInstructions").GetString());

        using var detached = await owner.DeleteAsync($"/api/v1/workspaces/{space}");
        Assert.Equal(HttpStatusCode.NoContent, detached.StatusCode);
        using var preserved = await owner.GetAsync($"/api/v1/chats/{chat}");
        Assert.Equal(JsonValueKind.Null, (await BodyAsync(preserved)).GetProperty("workspaceId").ValueKind);

        using var temporary = await owner.PostAsJsonAsync("/api/v1/chats", new { temporary = true });
        Assert.Equal(HttpStatusCode.Created, temporary.StatusCode);
        var temporaryId = (await BodyAsync(temporary)).GetProperty("id").GetGuid();
        using var detail = await owner.GetAsync($"/api/v1/chats/{temporaryId}");
        Assert.Equal(JsonValueKind.String, (await BodyAsync(detail)).GetProperty("expiresAt").ValueKind);
        using var rules = await owner.GetAsync($"/api/v1/chats/{temporaryId}/rules");
        Assert.False((await BodyAsync(rules)).GetProperty("memoryEnabled").GetBoolean());
        using var excluded = await owner.GetAsync($"/api/v1/data/export?chatId={temporaryId}");
        Assert.Equal(HttpStatusCode.NotFound, excluded.StatusCode);
        using var listing = await owner.GetAsync("/api/v1/chats");
        Assert.DoesNotContain((await BodyAsync(listing)).GetProperty("items").EnumerateArray(),
            item => item.GetProperty("id").GetGuid() == temporaryId);

        await fixture.ExpireAsync(temporaryId);
        using var expired = await owner.GetAsync($"/api/v1/chats/{temporaryId}");
        Assert.Equal(HttpStatusCode.NotFound, expired.StatusCode);
    }

    private static async Task<HttpResponseMessage> ImportAsync(HttpClient client, string json, Guid? workspace = null)
    {
        using var form = new MultipartFormDataContent();
        form.Add(new StringContent(json, Encoding.UTF8, "application/json"), "file", "conversations.json");
        if (workspace.HasValue) form.Add(new StringContent(workspace.ToString()!), "workspaceId");
        return await client.PostAsync("/api/v1/data/import", form);
    }
}
