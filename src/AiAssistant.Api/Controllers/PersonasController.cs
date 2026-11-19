using AiAssistant.Application.Personalization;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AiAssistant.Api.Controllers;

[Authorize]
[Route("api/v1/personas")]
public sealed class PersonasController(ISender sender) : ApplicationController
{
    [HttpGet]
    public async Task<IActionResult> List(CancellationToken cancellationToken) =>
        From(await sender.Send(new ListPersonas(CurrentUserId), cancellationToken));

    [HttpPost]
    public async Task<IActionResult> Create(PersonaInput body, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new SavePersona(CurrentUserId, null, body), cancellationToken);
        return result.IsSuccess ? StatusCode(StatusCodes.Status201Created, new { id = result.Value }) : Failure(result.Error!);
    }

    [HttpPut("{personaId:guid}")]
    public async Task<IActionResult> Update(Guid personaId, PersonaInput body, CancellationToken cancellationToken) =>
        NoContentFrom(await sender.Send(new SavePersona(CurrentUserId, personaId, body), cancellationToken));

    [HttpDelete("{personaId:guid}")]
    public async Task<IActionResult> Delete(Guid personaId, CancellationToken cancellationToken) =>
        NoContentFrom(await sender.Send(new DeletePersona(CurrentUserId, personaId), cancellationToken));
}
