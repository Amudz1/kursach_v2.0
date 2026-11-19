using AiAssistant.Application.Files;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AiAssistant.Api.Controllers;

[Authorize]
[Route("api/v1/attachments")]
public sealed class AttachmentsController(ISender sender) : ApplicationController
{
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Download(Guid id, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new GetAttachment(CurrentUserId, id), cancellationToken);
        if (!result.IsSuccess) return Failure(result.Error!);
        var attachment = result.Value!;
        Response.Headers.CacheControl = "private, no-store";
        Response.Headers.XContentTypeOptions = "nosniff";
        return File(attachment.Content, attachment.ContentType, attachment.FileName, enableRangeProcessing: true);
    }
}
