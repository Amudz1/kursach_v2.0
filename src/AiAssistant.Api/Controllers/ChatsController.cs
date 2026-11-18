using System.Text.Json;
using AiAssistant.Application.Chats;
using AiAssistant.Application.Common;
using AiAssistant.Application.Files;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.RateLimiting;

namespace AiAssistant.Api.Controllers;

[Authorize]
[Route("api/v1/chats")]
public sealed class ChatsController(ISender sender) : ApplicationController
{
    [HttpGet]
    public async Task<IActionResult> List(CancellationToken cancellationToken, string? search = null, int page = 1, int limit = 25,
        Guid? workspaceId = null, bool personalOnly = false) =>
        From(await sender.Send(new ListChats(CurrentUserId, search, page, limit, workspaceId, personalOnly), cancellationToken));

    [HttpGet("{chatId:guid}")]
    public async Task<IActionResult> Get(Guid chatId, CancellationToken ct) => From(await sender.Send(new GetChat(CurrentUserId, chatId), ct));

    [HttpGet("rules")]
    public async Task<IActionResult> DefaultRules(Guid? workspaceId, CancellationToken ct) =>
        From(await sender.Send(new GetChatRules(CurrentUserId, null, workspaceId), ct));

    [HttpGet("{chatId:guid}/rules")]
    public async Task<IActionResult> Rules(Guid chatId, CancellationToken ct) =>
        From(await sender.Send(new GetChatRules(CurrentUserId, chatId, null), ct));

    [HttpPut("{chatId:guid}/workspace")]
    public async Task<IActionResult> Move(Guid chatId, MoveBody body, CancellationToken ct) =>
        NoContentFrom(await sender.Send(new MoveChat(CurrentUserId, chatId, body.WorkspaceId), ct));

    [HttpPost]
    public async Task<IActionResult> Create([FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] CreateBody? body,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(new CreateChat(CurrentUserId, body?.PersonaId, body?.WorkspaceId, body?.Temporary ?? false), cancellationToken);
        return result.IsSuccess ? Created($"/api/v1/chats/{result.Value}/messages", new { id = result.Value }) : Failure(result.Error!);
    }

    [HttpPatch("{chatId:guid}")]
    public async Task<IActionResult> Patch(Guid chatId, PatchBody body, CancellationToken cancellationToken) =>
        NoContentFrom(await sender.Send(new PatchChat(CurrentUserId, chatId, body.Title, body.IsPinned, body.PersonaId),
            cancellationToken));

    [HttpDelete("{chatId:guid}")]
    public async Task<IActionResult> Delete(Guid chatId, CancellationToken cancellationToken) =>
        NoContentFrom(await sender.Send(new DeleteChat(CurrentUserId, chatId), cancellationToken));

    [HttpGet("{chatId:guid}/messages")]
    public async Task<IActionResult> Messages(Guid chatId, CancellationToken cancellationToken, Guid? before = null, int limit = 30) =>
        From(await sender.Send(new ListMessages(CurrentUserId, chatId, before, limit), cancellationToken));

    [HttpPost("{chatId:guid}/messages")]
    [EnableRateLimiting("chat")]
    [Consumes("application/json", "multipart/form-data")]
    [RequestSizeLimit(17 * 1024 * 1024)]
    [RequestFormLimits(MultipartBodyLengthLimit = 16 * 1024 * 1024)]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    public async Task<IActionResult> Send(Guid chatId, CancellationToken cancellationToken)
    {
        var files = new List<IncomingAttachment>();
        try
        {
            SendBody? body;
            if (Request.HasFormContentType)
            {
                var form = await Request.ReadFormAsync(cancellationToken);
                var requestId = form["requestId"].ToString();
                if (form.Files.Count > 4 || (requestId.Length > 0 && !Guid.TryParse(requestId, out _)))
                    return Failure(new(ErrorKind.Validation, "invalid_upload", "Проверьте параметры и количество вложений."));
                body = new(form["content"], Guid.TryParse(requestId, out var parsedId) ? parsedId : null, form["model"]);
                foreach (var file in form.Files)
                    files.Add(new(file.FileName, file.Length, file.OpenReadStream()));
            }
            else
            {
                body = await Request.ReadFromJsonAsync<SendBody>(cancellationToken);
            }
            if (body is null)
                return Failure(new(ErrorKind.Validation, "invalid_message", "Введите сообщение или добавьте вложение."));
            var result = await sender.Send(new SendChatMessage(CurrentUserId, chatId, body.RequestId ?? Guid.NewGuid(),
                body.Content ?? "", string.IsNullOrWhiteSpace(body.Model) ? null : body.Model, files), cancellationToken);
            return result.IsSuccess ? Accepted(new { id = result.Value }) : Failure(result.Error!);
        }
        catch (Exception exception) when (exception is JsonException or InvalidDataException)
        {
            return Failure(new(ErrorKind.Validation, "invalid_message", "Не удалось разобрать сообщение или вложение."));
        }
        finally
        {
            foreach (var file in files) await file.Content.DisposeAsync();
        }
    }

    public sealed record CreateBody(Guid? PersonaId, Guid? WorkspaceId = null, bool Temporary = false);
    public sealed record MoveBody(Guid? WorkspaceId);
    public sealed record PatchBody(string? Title, bool? IsPinned, Guid? PersonaId);
    public sealed record SendBody(string? Content, Guid? RequestId, string? Model);
}
