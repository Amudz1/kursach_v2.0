using AiAssistant.Application.Personalization;
using Xunit;

namespace AiAssistant.UnitTests;

public sealed class ResponsePolicyTests
{
    private static readonly ProfileView Profile = new("ru", "friendly", "intermediate", "text", "medium", [],
        "", false, true, true, true, "Подготовить диплом", [new("code", "short", "code", "Проверь границы.")]);

    [Theory]
    [InlineData("Проверь мою функцию на C#", "code")]
    [InlineData("Объясни идею на простом примере", "learn")]
    [InlineData("Отредактируй текст письма", "write")]
    [InlineData("Составь план с этапами", "plan")]
    [InlineData("Привет", "general")]
    public void ChoosesApproachFromTheRequest(string prompt, string expected)
    {
        var decision = ResponsePolicy.Resolve(Profile, prompt);
        Assert.Equal(expected, decision.Task);
        Assert.False(decision.FromHistory);
    }

    [Fact]
    public void ShortContinuationUsesPreviousTaskAndSavedOverrides()
    {
        var decision = ResponsePolicy.Resolve(Profile, "А почему?", "Проверь функцию на C#");
        Assert.True(decision.FromHistory);
        Assert.Equal("code", decision.Task);
        Assert.Equal("short", decision.Length);
        Assert.Equal("code", decision.Format);
        Assert.Equal("Проверь границы.", decision.Instructions);
        Assert.Equal("write", ResponsePolicy.Resolve(Profile, "Перепиши текст письма", "Проверь функцию").Task);
    }

    [Fact]
    public void OwnerCanDisableTaskDetection()
    {
        var result = ResponsePolicy.Resolve(Profile with { AdaptToTask = false }, "Проверь функцию");
        Assert.Equal("general", result.Task);
        Assert.Equal("medium", result.Length);
        Assert.Null(result.Rule);
        Assert.Null(result.Instructions);
    }
}
