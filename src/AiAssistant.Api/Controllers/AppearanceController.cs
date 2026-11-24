using AiAssistant.Application.Appearance;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AiAssistant.Api.Controllers;

[Authorize]
[Route("api/v1/me/appearance")]
public sealed class AppearanceController(ISender sender) : ApplicationController
{
    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken ct) =>
        From(await sender.Send(new GetAppearanceQuery(CurrentUserId), ct));

    [HttpPut]
    public async Task<IActionResult> Save(AppearanceInput body, CancellationToken ct) =>
        From(await sender.Send(new UpdateAppearanceCommand(CurrentUserId, body), ct));

    [HttpDelete]
    public async Task<IActionResult> Reset(CancellationToken ct) =>
        NoContentFrom(await sender.Send(new ResetAppearanceCommand(CurrentUserId), ct));
}
