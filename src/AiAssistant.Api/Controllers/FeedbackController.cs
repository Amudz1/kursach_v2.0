using AiAssistant.Application.Feedback;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AiAssistant.Api.Controllers;

[Authorize]
[Route("api/v1")]
public sealed class FeedbackController(ISender sender) : ApplicationController
{
    [HttpPost("messages/{messageId:guid}/feedback")]
    public async Task<IActionResult> Rate(Guid messageId, RatingBody body, CancellationToken cancellationToken) =>
        NoContentFrom(await sender.Send(new RateMessage(CurrentUserId, messageId, body.Rating, body.Reason, body.Comment),
            cancellationToken));

    [HttpGet("me/personalization/adaptation")]
    public async Task<IActionResult> Adaptation(CancellationToken cancellationToken)
    {
        var result = await sender.Send(new GetAdaptation(CurrentUserId), cancellationToken);
        return result.Value is null && result.IsSuccess ? NoContent() : From(result);
    }

    [HttpPost("me/personalization/adaptation")]
    public async Task<IActionResult> Decide(DecisionBody body, CancellationToken cancellationToken) =>
        NoContentFrom(await sender.Send(new DecideAdaptation(CurrentUserId, body.FeedbackId, body.Apply), cancellationToken));

    public sealed record RatingBody(int Rating, string? Reason, string? Comment);
    public sealed record DecisionBody(Guid FeedbackId, bool Apply);
}
