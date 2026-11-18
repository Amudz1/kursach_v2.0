using AiAssistant.Application.Billing;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AiAssistant.Api.Controllers;

[Route("api/v1")]
public sealed class BillingController(ISender sender) : ApplicationController
{
    [HttpGet("plans")]
    [AllowAnonymous]
    public async Task<IActionResult> Plans(CancellationToken cancellationToken) =>
        From(await sender.Send(new ListPlans(), cancellationToken));

    [HttpPost("subscriptions")]
    [Authorize]
    public async Task<IActionResult> Buy(PurchaseBody body, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new PurchasePlan(CurrentUserId, body.PlanId, body.RequestId, body.CardNumber,
            body.CardHolder, body.ExpireMonth, body.ExpireYear, body.Cvv), cancellationToken);
        return result.IsSuccess ? StatusCode(StatusCodes.Status201Created, new { id = result.Value }) : Failure(result.Error!);
    }

    public sealed record PurchaseBody(Guid PlanId, Guid RequestId, string CardNumber, string CardHolder,
        string ExpireMonth, string ExpireYear, string Cvv);
}
