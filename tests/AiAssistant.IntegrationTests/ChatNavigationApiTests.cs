using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using AiAssistant.Application.Chats;
using static AiAssistant.IntegrationTests.AssistantApiTests;
using Xunit;

namespace AiAssistant.IntegrationTests;

public sealed class ChatNavigationApiTests(ApiFixture fixture) : IClassFixture<ApiFixture>
{
    [Fact]
    public async Task SearchesAndNavigatesOldMessagesAndKeepsBookmarksPrivate()
    {
        using var owner = fixture.Client();
        using var other = fixture.Client();
        await RegisterAsync(owner);
        await RegisterAsync(other);
        using var workspace = await owner.PostAsJsonAsync("/api/v1/workspaces", new { name = "Материалы" });
        var workspaceId = (await BodyAsync(workspace)).GetProperty("id").GetGuid();

        var messages = Enumerable.Range(0, 80).Select(index => new
        {
            role = index % 2 == 0 ? "user" : "assistant",
            content = index == 1 ? "Пример\n\n```cs\nvar МОРСКАЯ = 7;\n```" : $"Реплика {index}",
            createdAt = new DateTimeOffset(2025, 1, 1, 0, index % 60, index / 60, TimeSpan.Zero).AddHours(index / 60),
        });
        var json = JsonSerializer.Serialize(new { conversations = new[] { new { title = "Длинная беседа", messages } } });
        using var form = new MultipartFormDataContent();
        form.Add(new StringContent(json, Encoding.UTF8, "application/json"), "file", "long-chat.json");
        form.Add(new StringContent(workspaceId.ToString()), "workspaceId");
        using var imported = await owner.PostAsync("/api/v1/data/import", form);
        imported.EnsureSuccessStatusCode();
        var chatId = (await BodyAsync(imported)).GetProperty("chatIds")[0].GetGuid();
        var url = $"/api/v1/chats/{chatId}";
        var tail = (await owner.GetFromJsonAsync<MessagePage>(url + "/messages"))!;
        Assert.DoesNotContain(tail.Items, item => item.Content.Contains("МОРСКАЯ", StringComparison.Ordinal));

        var found = (await owner.GetFromJsonAsync<MessageLinks>(url + "/navigation?kind=search&search=" + Uri.EscapeDataString("морская")))!;
        var result = Assert.Single(found.Items);
        Assert.Equal("assistant", result.Role);
        Assert.Equal("МОРСКАЯ", result.Text.Substring(result.MatchStart, result.MatchLength));
        Assert.Empty((await owner.GetFromJsonAsync<MessageLinks>(url + "/navigation?kind=search&search=%25"))!.Items);
        var questions = (await owner.GetFromJsonAsync<MessageLinks>(url + "/navigation?kind=questions"))!;
        Assert.Equal(25, questions.Items.Count);
        Assert.Equal("Реплика 0", questions.Items[0].Text);
        var next = (await owner.GetFromJsonAsync<MessageLinks>(url + $"/navigation?kind=questions&after={questions.Next}"))!;
        Assert.Equal("Реплика 50", next.Items[0].Text);

        var bookmark = url + $"/bookmarks/{result.Id}";
        using var rejected = await other.PutAsync(bookmark, null);
        Assert.Equal(HttpStatusCode.NotFound, rejected.StatusCode);
        using var foreignSearch = await other.GetAsync(url + "/navigation?kind=search&search=7");
        Assert.Equal(HttpStatusCode.NotFound, foreignSearch.StatusCode);
        using var firstSave = await owner.PutAsync(bookmark, null);
        using var secondSave = await owner.PutAsync(bookmark, null);
        Assert.Equal(HttpStatusCode.NoContent, firstSave.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, secondSave.StatusCode);
        var saved = (await owner.GetFromJsonAsync<MessageLinks>(url + "/navigation?kind=bookmarks"))!;
        Assert.Equal(result.Id, Assert.Single(saved.Items).Id);

        var around = (await owner.GetFromJsonAsync<MessagePage>(url + $"/messages?around={result.Id}&limit=6"))!;
        Assert.True(Assert.Single(around.Items, item => item.Id == result.Id).IsBookmarked);
        Assert.NotNull(around.NextAfter);
        var following = (await owner.GetFromJsonAsync<MessagePage>(url + $"/messages?after={around.NextAfter}&limit=3"))!;
        Assert.Equal("Реплика 4", following.Items[0].Content);
        var resume = (await owner.GetFromJsonAsync<ResumeChatView>($"/api/v1/chats/resume?workspaceId={workspaceId}&chatId={chatId}&messageId={result.Id}"))!;
        Assert.Equal(result.Id, resume.AnchorId);
        Assert.Equal(chatId, resume.Chat.Id);
        var missing = (await owner.GetFromJsonAsync<ResumeChatView>($"/api/v1/chats/resume?chatId={chatId}&messageId={Guid.NewGuid()}"))!;
        Assert.Null(missing.AnchorId);
        Assert.Equal("Реплика 79", missing.Text);

        using var temporary = await owner.PostAsJsonAsync("/api/v1/chats", new { temporary = true });
        var temporaryId = (await BodyAsync(temporary)).GetProperty("id").GetGuid();
        var temporaryUrl = $"/api/v1/chats/{temporaryId}";
        Assert.Empty((await owner.GetFromJsonAsync<MessageLinks>(temporaryUrl + "/navigation?kind=search&search=7"))!.Items);
        using var temporaryBookmark = await owner.PutAsync(temporaryUrl + "/bookmarks/" + result.Id, null);
        Assert.Equal(HttpStatusCode.BadRequest, temporaryBookmark.StatusCode);
        await fixture.ExpireAsync(temporaryId);
        using var expired = await owner.GetAsync(temporaryUrl + "/navigation?kind=search&search=7");
        Assert.Equal(HttpStatusCode.NotFound, expired.StatusCode);

        using var removed = await owner.DeleteAsync(bookmark);
        Assert.Equal(HttpStatusCode.NoContent, removed.StatusCode);
        Assert.Empty((await owner.GetFromJsonAsync<MessageLinks>(url + "/navigation?kind=bookmarks"))!.Items);
        using var savedAgain = await owner.PutAsync(bookmark, null);
        Assert.Equal(HttpStatusCode.NoContent, savedAgain.StatusCode);
        using var deleted = await owner.DeleteAsync(url);
        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
        using var missingHistory = await owner.GetAsync(url + "/navigation?kind=bookmarks");
        Assert.Equal(HttpStatusCode.NotFound, missingHistory.StatusCode);
    }
}
