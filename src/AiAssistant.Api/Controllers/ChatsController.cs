using AiAssistant.Application.Chats;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AiAssistant.Api.Controllers;

[Authorize]
[Route("api/v1/chats")]
public sealed class ChatsController(ISender sender) : ApplicationController
{
    [HttpGet]
    public async Task<IActionResult> List(CancellationToken cancellationToken, string? search = null, int page = 1, int limit = 25) =>
        From(await sender.Send(new ListChats(CurrentUserId, search, page, limit), cancellationToken));

    [HttpPost]
    public async Task<IActionResult> Create(CancellationToken cancellationToken)
    {
        var result = await sender.Send(new CreateChat(CurrentUserId), cancellationToken);
        return result.IsSuccess ? Created($"/api/v1/chats/{result.Value}/messages", new { id = result.Value }) : Failure(result.Error!);
    }

    [HttpPatch("{chatId:guid}")]
    public async Task<IActionResult> Patch(Guid chatId, PatchBody body, CancellationToken cancellationToken) =>
        NoContentFrom(await sender.Send(new PatchChat(CurrentUserId, chatId, body.Title, body.IsPinned), cancellationToken));

    [HttpDelete("{chatId:guid}")]
    public async Task<IActionResult> Delete(Guid chatId, CancellationToken cancellationToken) =>
        NoContentFrom(await sender.Send(new DeleteChat(CurrentUserId, chatId), cancellationToken));

    [HttpGet("{chatId:guid}/messages")]
    public async Task<IActionResult> Messages(Guid chatId, CancellationToken cancellationToken, Guid? before = null, int limit = 30) =>
        From(await sender.Send(new ListMessages(CurrentUserId, chatId, before, limit), cancellationToken));

    [HttpPost("{chatId:guid}/messages")]
    public async Task<IActionResult> Send(Guid chatId, SendBody body, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new SendChatMessage(CurrentUserId, chatId, body.RequestId ?? Guid.NewGuid(), body.Content),
            cancellationToken);
        return result.IsSuccess ? StatusCode(StatusCodes.Status201Created, new { id = result.Value }) : Failure(result.Error!);
    }

    public sealed record PatchBody(string? Title, bool? IsPinned);
    public sealed record SendBody(string Content, Guid? RequestId);
}
