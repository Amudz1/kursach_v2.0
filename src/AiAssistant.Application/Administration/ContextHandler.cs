using System.Text.Json;
using AiAssistant.Application.Common;
using AiAssistant.Application.Persistence;
using AiAssistant.Domain;
using MediatR;
using Microsoft.Extensions.Logging;

namespace AiAssistant.Application.Administration;

public sealed record ContextDiagnostics(bool Enabled);
public sealed record GetMessageContext(Guid AdminId, Guid MessageId) : IRequest<Result<JsonElement>>;

public sealed class ContextHandler(IAssistantData data, AdminAccess access, ContextDiagnostics diagnostics,
    ILogger<ContextHandler> logger) : IRequestHandler<GetMessageContext, Result<JsonElement>>
{
    public async Task<Result<JsonElement>> Handle(GetMessageContext request, CancellationToken cancellationToken)
    {
        if (!await access.AllowedAsync(request.AdminId, cancellationToken)) return AdminAccess.Denied<JsonElement>();
        if (!diagnostics.Enabled)
            return Result<JsonElement>.Fail(ErrorKind.Forbidden, "context_disabled", "Просмотр контекста отключён в этом окружении.");
        var snapshot = await data.FirstAsync(data.Query<Message>().Where(message => message.Id == request.MessageId)
            .Select(message => message.ContextSnapshot), cancellationToken);
        if (snapshot is null)
            return Result<JsonElement>.Fail(ErrorKind.NotFound, "context_not_found", "Для этого сообщения контекст недоступен.");
        logger.LogInformation("Administrator {ActorId} viewed context metadata for message {MessageId}", request.AdminId, request.MessageId);
        return Result<JsonElement>.Ok(JsonSerializer.Deserialize<JsonElement>(snapshot));
    }
}
