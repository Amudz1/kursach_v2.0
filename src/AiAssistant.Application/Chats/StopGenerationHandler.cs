using AiAssistant.Application.Common;
using AiAssistant.Application.Persistence;
using AiAssistant.Domain;
using MediatR;

namespace AiAssistant.Application.Chats;

public sealed record StopGeneration(Guid UserId, Guid MessageId) : IRequest<Result<Unit>>;

public sealed class StopGenerationHandler(IAssistantData data, ChatAccess access, GenerationStops stops, IGenerationEvents events)
    : IRequestHandler<StopGeneration, Result<Unit>>
{
    public async Task<Result<Unit>> Handle(StopGeneration request, CancellationToken cancellationToken)
    {
        var result = await data.ForUserAsync(request.UserId, async ct =>
        {
            var message = await data.FirstAsync(data.Query<Message>(true)
                .Where(message => message.Id == request.MessageId && message.Role == MessageRole.Assistant), ct);
            if (message is null || await access.FindAsync(request.UserId, message.ChatId, false, ct) is null)
                return Result<Message>.Fail(ErrorKind.NotFound, "generation_not_found", "Ответ не найден.");

            if (message.Status is GenerationStatus.Pending or GenerationStatus.Streaming)
            {
                message.Status = GenerationStatus.Stopped;
                await data.SaveAsync(ct);
            }
            return Result<Message>.Ok(message);
        }, cancellationToken);
        if (!result.IsSuccess) return new(default, result.Error);

        var stopped = result.Value!;
        if (stopped.Status == GenerationStatus.Stopped)
        {
            await stops.StopAsync(stopped.Id);
            await events.FinishedAsync(request.UserId, stopped.ChatId, ChatReadHandlers.ToView(stopped), null, cancellationToken);
        }
        return Result<Unit>.Ok(Unit.Value);
    }
}
