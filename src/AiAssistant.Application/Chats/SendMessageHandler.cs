using AiAssistant.Application.Common;
using MediatR;

namespace AiAssistant.Application.Chats;

public sealed class SendMessageHandler(ChatSubmission submission) : IRequestHandler<SendChatMessage, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(SendChatMessage request, CancellationToken cancellationToken)
    {
        var queued = await submission.PrepareAsync(request, cancellationToken);
        return queued.IsSuccess ? Result<Guid>.Ok(queued.Value!.Id) : new(Guid.Empty, queued.Error);
    }
}
