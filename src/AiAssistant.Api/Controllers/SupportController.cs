using AiAssistant.Application.Support;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AiAssistant.Api.Controllers;

[Authorize]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
[Route("api/v1/me/support/messages")]
public sealed class SupportController(ISender sender) : ApplicationController
{
    [HttpGet]
    public async Task<IActionResult> Messages(long? before, long? after, CancellationToken ct) =>
        From(await sender.Send(new GetSupportMessages(CurrentUserId, null, before, after), ct));

    [HttpPost]
    public async Task<IActionResult> Send(SupportInput input, CancellationToken ct) =>
        From(await sender.Send(new SendSupportMessage(CurrentUserId, null, input), ct));
}

[Authorize(Roles = "Admin")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
[Route("api/v1/admin/support")]
public sealed class AdminSupportController(ISender sender) : ApplicationController
{
    [HttpGet]
    public async Task<IActionResult> Threads(long? before, CancellationToken ct) =>
        From(await sender.Send(new ListSupportThreads(CurrentUserId, before), ct));

    [HttpGet("{userId:guid}/messages")]
    public async Task<IActionResult> Messages(Guid userId, long? before, long? after, CancellationToken ct) =>
        From(await sender.Send(new GetSupportMessages(CurrentUserId, userId, before, after), ct));

    [HttpPost("{userId:guid}/messages")]
    public async Task<IActionResult> Send(Guid userId, SupportInput input, CancellationToken ct) =>
        From(await sender.Send(new SendSupportMessage(CurrentUserId, userId, input), ct));
}
