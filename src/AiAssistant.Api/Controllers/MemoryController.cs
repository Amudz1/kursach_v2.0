using AiAssistant.Application.Memory;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AiAssistant.Api.Controllers;

[Authorize]
[Route("api/v1/me/memories")]
public sealed class MemoryController(ISender sender) : ApplicationController
{
    [HttpGet]
    public async Task<IActionResult> List(CancellationToken cancellationToken) =>
        From(await sender.Send(new ListMemories(CurrentUserId), cancellationToken));

    [HttpPost]
    public async Task<IActionResult> Create(MemoryBody body, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new SaveMemory(CurrentUserId, null, body.Content, body.IsEnabled), cancellationToken);
        return result.IsSuccess ? StatusCode(StatusCodes.Status201Created, new { id = result.Value }) : Failure(result.Error!);
    }

    [HttpPut("{memoryId:guid}")]
    public async Task<IActionResult> Update(Guid memoryId, MemoryBody body, CancellationToken cancellationToken) =>
        NoContentFrom(await sender.Send(new SaveMemory(CurrentUserId, memoryId, body.Content, body.IsEnabled), cancellationToken));

    [HttpDelete("{memoryId:guid}")]
    public async Task<IActionResult> Delete(Guid memoryId, CancellationToken cancellationToken) =>
        NoContentFrom(await sender.Send(new DeleteMemory(CurrentUserId, memoryId), cancellationToken));

    public sealed record MemoryBody(string Content, bool IsEnabled = true);
}
