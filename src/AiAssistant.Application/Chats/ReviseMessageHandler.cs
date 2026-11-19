using AiAssistant.Application.Common;
using MediatR;

namespace AiAssistant.Application.Chats;

public sealed record ReviseChatMessage(Guid UserId, Guid MessageId, Guid RequestId, bool IsEdit, string? Content)
    : IRequest<Result<Guid>>;

public sealed class ReviseMessageHandler(ChatSubmission submission) : IRequestHandler<ReviseChatMessage, Result<Guid>>
{
    public Task<Result<Guid>> Handle(ReviseChatMessage request, CancellationToken cancellationToken) =>
        submission.PrepareRevisionAsync(request, cancellationToken);
}
