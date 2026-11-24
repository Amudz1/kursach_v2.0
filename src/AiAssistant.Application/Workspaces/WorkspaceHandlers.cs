using AiAssistant.Application.Common;
using AiAssistant.Application.Appearance;
using AiAssistant.Application.Persistence;
using AiAssistant.Domain;
using MediatR;

namespace AiAssistant.Application.Workspaces;

public sealed record WorkspaceView(Guid Id, string Name, string Instructions, string Materials, string? Color = null, string? Icon = null);
public sealed record ListWorkspaces(Guid UserId) : IRequest<Result<IReadOnlyList<WorkspaceView>>>;
public sealed record SaveWorkspace(Guid UserId, Guid? Id, string Name, string? Instructions, string? Materials)
    : IRequest<Result<Guid>>
{
    public string? Color { get; init; }
    public string? Icon { get; init; }
    public bool UpdateColor { get; init; }
    public bool UpdateIcon { get; init; }
}
public sealed record DeleteWorkspace(Guid UserId, Guid Id) : IRequest<Result<Unit>>;

public sealed class WorkspaceHandlers(IAssistantData data) :
    IRequestHandler<ListWorkspaces, Result<IReadOnlyList<WorkspaceView>>>,
    IRequestHandler<SaveWorkspace, Result<Guid>>, IRequestHandler<DeleteWorkspace, Result<Unit>>
{
    public async Task<Result<IReadOnlyList<WorkspaceView>>> Handle(ListWorkspaces request, CancellationToken ct) =>
        Result<IReadOnlyList<WorkspaceView>>.Ok(await data.ListAsync(data.Query<Workspace>()
            .Where(item => item.UserId == request.UserId).OrderBy(item => item.Name).ThenBy(item => item.Id)
            .Select(item => new WorkspaceView(item.Id, item.Name, item.Instructions, item.Materials, item.Color, item.Icon)), ct));

    public Task<Result<Guid>> Handle(SaveWorkspace request, CancellationToken cancellationToken)
    {
        var name = request.Name.Trim();
        var instructions = request.Instructions?.Trim() ?? "";
        var materials = request.Materials?.Trim() ?? "";
        var errors = new Dictionary<string, string[]>();
        if (!EntityAppearance.ValidColor(request.Color)) errors["color"] = ["Выберите цвет из палитры."];
        if (!EntityAppearance.ValidIcon(request.Icon)) errors["icon"] = ["Выберите иконку из списка."];
        if (errors.Count > 0) return Task.FromResult(Result<Guid>.Invalid(errors));
        if (name.Length is 0 or > 80 || instructions.Length > 2000 || materials.Length > 10000 ||
            name.Contains('\0') || instructions.Contains('\0') || materials.Contains('\0'))
            return Task.FromResult(Result<Guid>.Fail(ErrorKind.Validation, "invalid_workspace",
                "Проверьте длину названия, инструкций и материалов."));

        return data.ForUserAsync(request.UserId, async ct =>
        {
            var workspace = request.Id is { } id ? await FindAsync(request.UserId, id, ct) : null;
            if (request.Id.HasValue && workspace is null) return Missing<Guid>();
            if (workspace is null)
            {
                if (await data.CountAsync(data.Query<Workspace>().Where(item => item.UserId == request.UserId), ct) >= 100)
                    return Result<Guid>.Fail(ErrorKind.Limit, "workspace_limit", "Достигнут лимит пространств.");
                workspace = new Workspace { UserId = request.UserId, Name = name };
                data.Add(workspace);
            }
            workspace.Name = name;
            workspace.Instructions = instructions;
            workspace.Materials = materials;
            if (request.UpdateColor) workspace.Color = request.Color;
            if (request.UpdateIcon) workspace.Icon = request.Icon;
            await data.SaveAsync(ct);
            return Result<Guid>.Ok(workspace.Id);
        }, cancellationToken);
    }

    public Task<Result<Unit>> Handle(DeleteWorkspace request, CancellationToken cancellationToken) =>
        data.ForUserAsync(request.UserId, async ct =>
        {
            var workspace = await FindAsync(request.UserId, request.Id, ct);
            if (workspace is null) return Missing<Unit>();
            var chats = data.Query<Chat>().Where(chat => chat.UserId == request.UserId && chat.WorkspaceId == request.Id);
            if (await data.AnyAsync(data.Query<Message>().Where(message => chats.Any(chat => chat.Id == message.ChatId) &&
                (message.Status == GenerationStatus.Pending || message.Status == GenerationStatus.Streaming)), ct))
                return Result<Unit>.Fail(ErrorKind.Conflict, "workspace_busy", "Дождитесь завершения ответов в пространстве.");
            foreach (var chat in await data.ListAsync(data.Query<Chat>(true)
                .Where(chat => chat.UserId == request.UserId && chat.WorkspaceId == request.Id), ct))
                chat.WorkspaceId = null;
            await data.SaveAsync(ct);
            data.Remove(workspace);
            await data.SaveAsync(ct);
            return Result<Unit>.Ok(Unit.Value);
        }, cancellationToken);

    private Task<Workspace?> FindAsync(Guid user, Guid id, CancellationToken ct) =>
        data.FirstAsync(data.Query<Workspace>(true).Where(item => item.UserId == user && item.Id == id), ct);

    internal static Result<T> Missing<T>() => Result<T>.Fail(ErrorKind.NotFound, "workspace_not_found", "Пространство недоступно.");
}
