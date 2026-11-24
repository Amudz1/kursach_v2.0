using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using AiAssistant.Application.Common;
using Microsoft.AspNetCore.Mvc;

namespace AiAssistant.Api.Controllers;

[ApiController]
public abstract class ApplicationController : ControllerBase
{
    protected Guid CurrentUserId => Guid.Parse(User.FindFirstValue(JwtRegisteredClaimNames.Sub)!);

    protected IActionResult From<T>(Result<T> result) => result.IsSuccess ? Ok(result.Value) : Failure(result.Error!);

    protected IActionResult NoContentFrom<T>(Result<T> result) => result.IsSuccess ? NoContent() : Failure(result.Error!);

    protected ObjectResult Failure(AppError error)
    {
        var statusCode = error.Kind switch
        {
            ErrorKind.Validation => StatusCodes.Status400BadRequest,
            ErrorKind.Unauthorized => StatusCodes.Status401Unauthorized,
            ErrorKind.Forbidden => StatusCodes.Status403Forbidden,
            ErrorKind.NotFound => StatusCodes.Status404NotFound,
            ErrorKind.Conflict => StatusCodes.Status409Conflict,
            ErrorKind.Limit => StatusCodes.Status429TooManyRequests,
            _ => StatusCodes.Status503ServiceUnavailable
        };
        var extensions = new Dictionary<string, object?> { ["code"] = error.Code };
        if (error.Fields is not null) extensions["errors"] = error.Fields;
        return Problem(title: error.Message, statusCode: statusCode, extensions: extensions);
    }
}
