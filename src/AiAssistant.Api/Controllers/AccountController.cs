using AiAssistant.Application.Administration;
using AiAssistant.Application.Auth;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace AiAssistant.Api.Controllers;

[Authorize]
[Route("api/v1/me")]
public sealed class AccountController(ISender sender) : ApplicationController
{
    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken cancellationToken) =>
        From(await sender.Send(new GetAccount(CurrentUserId), cancellationToken));

    [HttpGet("stats")]
    public async Task<IActionResult> Stats(CancellationToken cancellationToken, int days = 30) =>
        From(await sender.Send(new GetPersonalStatistics(CurrentUserId, days), cancellationToken));

    [HttpPut("email")]
    [EnableRateLimiting("account")]
    public async Task<IActionResult> Email(ChangeEmailBody body, CancellationToken cancellationToken) =>
        NoContentFrom(await sender.Send(new ChangeEmail(CurrentUserId, body.Email, body.Password), cancellationToken));

    [HttpPut("password")]
    [EnableRateLimiting("account")]
    public async Task<IActionResult> Password(ChangePasswordBody body, CancellationToken cancellationToken) =>
        NoContentFrom(await sender.Send(new ChangePassword(CurrentUserId, body.CurrentPassword, body.Password,
            body.PasswordConfirmation), cancellationToken));

    [HttpDelete]
    [EnableRateLimiting("account")]
    public async Task<IActionResult> Delete(DeleteBody body, CancellationToken cancellationToken) =>
        NoContentFrom(await sender.Send(new DeleteAccount(CurrentUserId, body.Password), cancellationToken));

    public sealed record ChangeEmailBody(string Email, string Password);
    public sealed record ChangePasswordBody(string CurrentPassword, string Password, string PasswordConfirmation);
    public sealed record DeleteBody(string Password);
}
