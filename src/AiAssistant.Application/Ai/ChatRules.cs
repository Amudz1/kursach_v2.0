using AiAssistant.Application.Common;
using AiAssistant.Application.Personalization;
using AiAssistant.Application.Workspaces;
using AiAssistant.Domain;

namespace AiAssistant.Application.Ai;

public sealed record ChatRules(string Language, string Tone, string Level, string Length, string Format, bool AdaptToTask,
    string? Task, string? TaskRule, string? TaskInstructions, string? Goal, string? Persona,
    string? WorkspaceName, string? WorkspaceInstructions, bool WorkspaceMaterials, bool MemoryEnabled, bool? MemoryUsed);

public sealed partial class PromptContextBuilder
{
    public async Task<Result<ChatRules>> DescribeAsync(Guid user, Guid? workspaceId, Guid? personaId, CancellationToken ct,
        bool temporary = false)
    {
        var workspace = await ReadWorkspaceAsync(user, workspaceId, ct);
        if (workspaceId.HasValue && workspace is null) return WorkspaceHandlers.Missing<ChatRules>();
        var profile = await ReadProfileAsync(user, ct);
        var persona = personaId is { } id ? await personas.FindAsync(user, id, ct) : null;
        return Result<ChatRules>.Ok(Describe(profile, workspace, persona?.Name, temporary));
    }

    private static ChatRules Describe(PersonalizationProfile profile, Workspace? workspace, string? persona, bool temporary,
        ResponseDecision? decision = null) => new(profile.Language, profile.Tone, profile.ExpertiseLevel,
            decision?.Length ?? profile.ResponseLength, decision?.Format ?? profile.ResponseFormat, profile.AdaptToTask,
            decision?.Task, decision?.Rule, decision?.Instructions, workspace is null && !temporary ? Present(profile.Goal) : null, persona,
            workspace?.Name, Present(workspace?.Instructions), Present(workspace?.Materials) is not null,
            workspace is null && !temporary && profile.UseMemoryInReplies, null);

    private async Task<PersonalizationProfile> ReadProfileAsync(Guid user, CancellationToken ct) =>
        await data.FirstAsync(data.Query<PersonalizationProfile>().Where(profile => profile.UserId == user), ct)
        ?? new PersonalizationProfile { UserId = user };

    private Task<Workspace?> ReadWorkspaceAsync(Guid user, Guid? workspace, CancellationToken ct) => workspace is null
        ? Task.FromResult<Workspace?>(null)
        : data.FirstAsync(data.Query<Workspace>().Where(item => item.Id == workspace && item.UserId == user), ct);

    private static string? Present(string? value) => string.IsNullOrWhiteSpace(value) ? null : value;
}
