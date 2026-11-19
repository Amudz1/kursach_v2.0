using AiAssistant.Application.Personalization;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AiAssistant.Api.Controllers;

[Authorize]
[Route("api/v1/me/personalization")]
public sealed class PersonalizationController(ISender sender) : ApplicationController
{
    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken cancellationToken) =>
        From(await sender.Send(new GetProfile(CurrentUserId), cancellationToken));

    [HttpPut]
    public async Task<IActionResult> Save(ProfileView body, CancellationToken cancellationToken) =>
        NoContentFrom(await sender.Send(new SaveProfile(CurrentUserId, body), cancellationToken));

    [HttpPost("preview")]
    public async Task<IActionResult> Preview(PreviewProfile body, CancellationToken cancellationToken) =>
        From(await sender.Send(body, cancellationToken));
}
