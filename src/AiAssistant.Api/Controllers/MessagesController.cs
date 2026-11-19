using AiAssistant.Application.Chats;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace AiAssistant.Api.Controllers;

[Authorize]
[EnableRateLimiting("chat")]
[Route("api/v1/messages")]
public sealed class MessagesController(ISender sender) : ApplicationController
{
    [HttpPost("{messageId:guid}/regenerate")]
    public async Task<IActionResult> Regenerate(Guid messageId, RevisionBody body, CancellationToken cancellationToken) =>
        await ReviseAsync(messageId, body.RequestId, false, null, cancellationToken);

    [HttpPatch("{messageId:guid}")]
    public async Task<IActionResult> Edit(Guid messageId, RevisionBody body, CancellationToken cancellationToken) =>
        await ReviseAsync(messageId, body.RequestId, true, body.Content, cancellationToken);

    private async Task<IActionResult> ReviseAsync(Guid messageId, Guid? requestId, bool isEdit, string? content,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(new ReviseChatMessage(CurrentUserId, messageId, requestId ?? Guid.NewGuid(),
            isEdit, content), cancellationToken);
        return result.IsSuccess ? Accepted(new { id = result.Value }) : Failure(result.Error!);
    }

    public sealed record RevisionBody(Guid? RequestId, string? Content);
}
