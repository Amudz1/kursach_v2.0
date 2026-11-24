using System.Text;
using AiAssistant.Application.Ai;
using AiAssistant.Application.Personalization;
using AiAssistant.Domain;
using Xunit;

namespace AiAssistant.UnitTests;

public sealed class PromptTests
{
    [Theory]
    [InlineData("chosen", 8500)]
    [InlineData("unlisted", 4700)]
    public void ModelBudgetsFallBackToDefault(string model, int expected)
    {
        var options = new AiDefaults("chosen", false, 4700, new Dictionary<string, int> { ["chosen"] = 8500 });
        Assert.Equal(expected, options.BudgetFor(model));
    }

    [Fact]
    public async Task ContextOrdersBlocksAndIsolatesMemory()
    {
        var data = new MemoryData();
        var user = Guid.NewGuid();
        var now = new DateTimeOffset(2026, 8, 1, 10, 0, 0, TimeSpan.Zero);
        var recent = new Chat { UserId = user, CreatedAt = now };
        var boundary = new Message { ChatId = recent.Id, Content = "Original stale message", Role = MessageRole.User, CreatedAt = now };
        var history = new Message { ChatId = recent.Id, Content = "Recent complete exchange", Role = MessageRole.Assistant, CreatedAt = now.AddSeconds(1) };
        var current = new Message { ChatId = recent.Id, Content = "Как работают коллекции Rust?", Role = MessageRole.User, CreatedAt = now.AddSeconds(2) };
        var persona = new Persona { OwnerId = user, Name = "Teacher", SystemPrompt = "Teach in stages.", Temperature = 0.3 };
        var answer = new Message { ChatId = recent.Id, PersonaId = persona.Id, Content = "", Role = MessageRole.Assistant,
            TriggerMessageId = current.Id, CreatedAt = now.AddSeconds(3), Model = "test" };
        recent.Summary = "We had worked through ownership.";
        recent.SummaryUntilMessageId = boundary.Id;
        data.Add(recent); data.Add(boundary); data.Add(history); data.Add(current); data.Add(answer); data.Add(persona);
        data.Add(new PersonalizationProfile { UserId = user, Language = "en", ResponseLength = "short", About = "Study Rust." });
        data.Add(new UserMemory { UserId = user, Content = "Uses Rust professionally.", NormalizedContent = "RUST" });
        data.Add(new UserMemory { UserId = user, Content = "Enjoys pottery.", NormalizedContent = "POTTERY" });
        data.Add(new UserMemory { UserId = Guid.NewGuid(), Content = "Foreign Rust memory", NormalizedContent = "FOREIGN" });
        data.Add(new Attachment { MessageId = current.Id, ContentType = "text/plain", FileName = "notes.txt",
            StorageKey = Guid.NewGuid().ToString("N"), ExtractedText = "Input excerpt." });

        var settings = new AiDefaults("test", true);
        var prompt = new PromptContextBuilder(data, new PersonaAccess(data, settings), settings, new NoFiles());
        var result = await prompt.BuildAsync(user, recent.Id, answer.Id, CancellationToken.None);
        Assert.Equal(0.3, result.Temperature);
        Assert.Equal(320, result.MaxTokens);
        Assert.Collection(result.Messages,
            item => { Assert.Equal("system", item.Role); Assert.Contains("Teach in stages.", item.Content); },
            item => Assert.Contains("\"language\":\"en\"", item.Content),
            item => { Assert.Contains("Uses Rust", item.Content); Assert.DoesNotContain("Foreign", item.Content); },
            item => Assert.Contains("worked through ownership", item.Content),
            item => Assert.Equal("Recent complete exchange", item.Content),
            item => { Assert.StartsWith(current.Content, item.Content); Assert.Contains("Input excerpt.", item.Content); });

        var workspace = new Workspace { UserId = user, Name = "Project", Instructions = "Review the tests.", Materials = "Project notes." };
        data.Add(workspace);
        data.Add(new Workspace { UserId = user, Name = "Another project", Materials = "Unrelated workspace secret" });
        recent.WorkspaceId = workspace.Id;
        var isolated = await prompt.BuildAsync(user, recent.Id, answer.Id, CancellationToken.None);
        var system = Assert.Single(isolated.Messages, item => item.Role == "system");
        Assert.Contains(workspace.Instructions, system.Content);
        Assert.Contains(isolated.Messages, item => item.Content.Contains(workspace.Materials, StringComparison.Ordinal));
        Assert.DoesNotContain(isolated.Messages, item => item.Content.Contains("Uses Rust", StringComparison.Ordinal) ||
            item.Content.Contains("Study Rust.", StringComparison.Ordinal) || item.Content.Contains("Unrelated workspace", StringComparison.Ordinal));
        Assert.False(isolated.Rules!.MemoryEnabled);
    }

    [Fact]
    public async Task BudgetBoundsUtf8AndKeepsCurrentMessageLast()
    {
        var data = new MemoryData();
        var owner = Guid.NewGuid();
        var chat = new Chat { UserId = owner };
        var input = new Message { ChatId = chat.Id, Content = new string('Я', 10000), Role = MessageRole.User };
        var answer = new Message { ChatId = chat.Id, Role = MessageRole.Assistant, TriggerMessageId = input.Id,
            Content = "", Model = "limited" };
        data.Add(chat); data.Add(input); data.Add(answer);
        var defaults = new AiDefaults("limited", true, 4000);
        var result = await new PromptContextBuilder(data, new PersonaAccess(data, defaults), defaults, new NoFiles())
            .BuildAsync(owner, chat.Id, answer.Id, CancellationToken.None);
        Assert.StartsWith("Я", result.Messages[^1].Content);
        Assert.True(result.Messages.Sum(message => Encoding.UTF8.GetByteCount(message.Content) + 12) + result.MaxTokens <= 4000);
        Assert.All(result.Messages[^1].Content, character => Assert.Equal('Я', character));
    }

}
