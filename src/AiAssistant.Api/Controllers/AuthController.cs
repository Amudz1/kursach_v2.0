using AiAssistant.Application.Auth;
using AiAssistant.Application.Common;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace AiAssistant.Api.Controllers;

[AllowAnonymous]
[Route("api/v1/auth")]
[EnableRateLimiting("account")]
public sealed class AuthController(ISender sender, IHostEnvironment environment, IConfiguration configuration)
    : ApplicationController
{
    private const string RefreshCookie = "budz_refresh";
    private const string CookiePath = "/api/v1/auth";

    [HttpPost("register")]
    public async Task<IActionResult> Register(RegisterBody body, CancellationToken cancellationToken)
    {
        if (!IsAllowedOrigin()) return OriginFailure();
        var result = await sender.Send(new RegisterAccount(body.Username, body.Email, body.Password,
            body.PasswordConfirmation), cancellationToken);
        return SendGrant(result, StatusCodes.Status201Created);
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login(LoginBody body, CancellationToken cancellationToken)
    {
        if (!IsAllowedOrigin()) return OriginFailure();
        var result = await sender.Send(new LoginAccount(body.Login, body.Password), cancellationToken);
        return SendGrant(result);
    }

    [HttpPost("refresh")]
    public async Task<IActionResult> Refresh(CancellationToken cancellationToken)
    {
        if (!IsAllowedOrigin()) return OriginFailure();
        var result = await sender.Send(new RotateSession(Request.Cookies[RefreshCookie]), cancellationToken);
        if (!result.IsSuccess) Response.Cookies.Delete(RefreshCookie, Cookie());
        return SendGrant(result);
    }

    [HttpPost("logout")]
    public async Task<IActionResult> Logout(CancellationToken cancellationToken)
    {
        if (!IsAllowedOrigin()) return OriginFailure();
        var result = await sender.Send(new EndSession(Request.Cookies[RefreshCookie]), cancellationToken);
        Response.Cookies.Delete(RefreshCookie, Cookie());
        return NoContentFrom(result);
    }

    private IActionResult SendGrant(Result<SessionGrant> result, int status = StatusCodes.Status200OK)
    {
        Response.Headers.CacheControl = "no-store";
        if (!result.IsSuccess) return Failure(result.Error!);
        var grant = result.Value!;
        var cookie = Cookie();
        cookie.Expires = grant.RefreshExpiresAt;
        Response.Cookies.Append(RefreshCookie, grant.RefreshToken, cookie);
        return StatusCode(status, new AccessGrant(grant.AccessToken, grant.AccessExpiresAt));
    }

    private CookieOptions Cookie() => new()
    {
        HttpOnly = true,
        Secure = !environment.IsDevelopment() || Request.IsHttps,
        SameSite = SameSiteMode.Strict,
        Path = CookiePath,
        IsEssential = true
    };

    private bool IsAllowedOrigin()
    {
        if (string.Equals(Request.Headers["Sec-Fetch-Site"], "cross-site", StringComparison.OrdinalIgnoreCase))
            return false;

        var origin = Request.Headers.Origin;
        if (origin.Count == 0) return true;
        if (origin.Count != 1) return false;
        var applicationOrigin = configuration["Frontend:Origin"] ?? $"{Request.Scheme}://{Request.Host}";
        return string.Equals(origin[0], applicationOrigin.TrimEnd('/'), StringComparison.OrdinalIgnoreCase);
    }

    private IActionResult OriginFailure() =>
        Failure(new(ErrorKind.Forbidden, "invalid_origin", "Запрос должен исходить из интерфейса приложения."));

    public sealed record RegisterBody(string Username, string Email, string Password, string PasswordConfirmation);
    public sealed record LoginBody(string Login, string Password);
    public sealed record AccessGrant(string AccessToken, DateTimeOffset ExpiresAt);
}
