using AiAssistant.Application.Workspaces;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AiAssistant.Api.Controllers;

[Authorize]
[Route("api/v1/workspaces")]
public sealed class WorkspacesController(ISender sender) : ApplicationController
{
    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct) => From(await sender.Send(new ListWorkspaces(CurrentUserId), ct));

    [HttpPost]
    public async Task<IActionResult> Create(WorkspaceBody body, CancellationToken ct)
    {
        var result = await sender.Send(new SaveWorkspace(CurrentUserId, null, body.Name, body.Instructions, body.Materials), ct);
        return result.IsSuccess ? StatusCode(StatusCodes.Status201Created, new { id = result.Value }) : Failure(result.Error!);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, WorkspaceBody body, CancellationToken ct)
    {
        var result = await sender.Send(new SaveWorkspace(CurrentUserId, id, body.Name, body.Instructions, body.Materials), ct);
        return result.IsSuccess ? NoContent() : Failure(result.Error!);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct) =>
        NoContentFrom(await sender.Send(new DeleteWorkspace(CurrentUserId, id), ct));

    public sealed record WorkspaceBody(string Name, string? Instructions, string? Materials);
}
