using AiAssistant.Application.Administration;
using AiAssistant.Application.Common;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AiAssistant.Api.Controllers;

[Authorize(Roles = "Admin")]
[Route("api/v1/admin")]
public sealed class AdminController(ISender sender) : ApplicationController
{
    [HttpGet("stats")]
    public async Task<IActionResult> Stats(CancellationToken cancellationToken, int days = 30) =>
        From(await sender.Send(new GetAdminStatistics(CurrentUserId, days), cancellationToken));

    [HttpGet("users")]
    public async Task<IActionResult> Users(CancellationToken cancellationToken, string? search = null, int page = 1) =>
        From(await sender.Send(new ListAdminUsers(CurrentUserId, search, page), cancellationToken));

    [HttpPatch("users/{id:guid}")]
    public async Task<IActionResult> PatchUser(Guid id, UpdateUserBody value, CancellationToken cancellationToken) =>
        NoContentFrom(await sender.Send(new UpdateAdminUser(CurrentUserId, id, value.IsBlocked, value.PlanId), cancellationToken));

    [HttpGet("personas")]
    public async Task<IActionResult> Personas(CancellationToken cancellationToken) =>
        From(await sender.Send(new ListAdminPersonas(CurrentUserId), cancellationToken));

    [HttpPost("personas")]
    public async Task<IActionResult> CreatePersona(AdminPersonaInput input, CancellationToken cancellationToken) =>
        CreatedFrom(await sender.Send(new SaveAdminPersona(CurrentUserId, null, input), cancellationToken));

    [HttpPut("personas/{id:guid}")]
    public async Task<IActionResult> EditPersona(Guid id, AdminPersonaInput input, CancellationToken cancellationToken) =>
        NoContentFrom(await sender.Send(new SaveAdminPersona(CurrentUserId, id, input), cancellationToken));

    [HttpDelete("personas/{id:guid}")]
    public async Task<IActionResult> DeletePersona(Guid id, CancellationToken cancellationToken) =>
        NoContentFrom(await sender.Send(new RemoveAdminPersona(CurrentUserId, id), cancellationToken));

    [HttpGet("plans")]
    public async Task<IActionResult> Plans(CancellationToken cancellationToken) =>
        From(await sender.Send(new ListAdminPlans(CurrentUserId), cancellationToken));

    [HttpPost("plans")]
    public async Task<IActionResult> CreatePlan(AdminPlanInput input, CancellationToken cancellationToken) =>
        CreatedFrom(await sender.Send(new SaveAdminPlan(CurrentUserId, null, input), cancellationToken));

    [HttpPut("plans/{id:guid}")]
    public async Task<IActionResult> EditPlan(Guid id, AdminPlanInput input, CancellationToken cancellationToken) =>
        NoContentFrom(await sender.Send(new SaveAdminPlan(CurrentUserId, id, input), cancellationToken));

    [HttpDelete("plans/{id:guid}")]
    public async Task<IActionResult> DeletePlan(Guid id, CancellationToken cancellationToken) =>
        NoContentFrom(await sender.Send(new RemoveAdminPlan(CurrentUserId, id), cancellationToken));

    [HttpGet("messages/{id:guid}/context")]
    public async Task<IActionResult> Context(Guid id, CancellationToken cancellationToken)
    {
        Response.Headers.CacheControl = "private, no-store";
        return From(await sender.Send(new GetMessageContext(CurrentUserId, id), cancellationToken));
    }

    private IActionResult CreatedFrom(Result<Guid> result) =>
        result.IsSuccess ? StatusCode(StatusCodes.Status201Created, new { id = result.Value }) : Failure(result.Error!);

    public sealed record UpdateUserBody(bool? IsBlocked, Guid? PlanId);
}
