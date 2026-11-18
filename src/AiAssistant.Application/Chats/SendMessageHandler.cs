using AiAssistant.Application.Ai;
using AiAssistant.Application.Common;
using AiAssistant.Application.Persistence;
using AiAssistant.Domain;
using MediatR;

namespace AiAssistant.Application.Chats;

public sealed class SendMessageHandler(ChatSubmission submission, IAssistantData data, IAiProvider provider) :
    IRequestHandler<SendChatMessage, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(SendChatMessage request, CancellationToken cancellationToken)
    {
        var queued = await submission.PrepareAsync(request, cancellationToken);
        if (!queued.IsSuccess) return new(Guid.Empty, queued.Error);
        var item = queued.Value!;
        if (!item.IsNew) return Result<Guid>.Ok(item.Id);

        var response = await data.FirstAsync(data.Query<Message>().Where(message => message.Id == item.Id), cancellationToken);
        var history = await data.ListAsync(data.Query<Message>().Where(message => message.ChatId == request.ChatId &&
                message.Status == GenerationStatus.Completed).OrderByDescending(message => message.CreatedAt)
            .ThenByDescending(message => message.Id).Take(20), cancellationToken);
        try
        {
            var reply = await provider.CompleteAsync(new(response!.Model!, history.AsEnumerable().Reverse()
                .Select(message => new AiMessage(message.Role.ToString().ToLowerInvariant(), message.Content)).ToArray()),
                cancellationToken);
            await SaveAsync(item.Id, GenerationStatus.Completed, reply, null, cancellationToken);
            return Result<Guid>.Ok(item.Id);
        }
        catch (AiProviderException exception)
        {
            await SaveAsync(item.Id, GenerationStatus.Failed, null, exception.Code, CancellationToken.None);
            return Result<Guid>.Fail(ErrorKind.Unavailable, exception.Code, exception.Message);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            await SaveAsync(item.Id, GenerationStatus.Stopped, null, null, CancellationToken.None);
            throw;
        }
    }

    private async Task SaveAsync(Guid messageId, GenerationStatus status, AiReply? reply, string? error,
        CancellationToken cancellationToken)
    {
        var message = await data.FirstAsync(data.Query<Message>(true).Where(item => item.Id == messageId), cancellationToken);
        if (message is null) return;
        message.Status = status;
        message.Content = reply?.Content ?? "";
        message.ErrorCode = error;
        message.PromptTokens = reply?.Usage?.PromptTokens;
        message.CompletionTokens = reply?.Usage?.CompletionTokens;
        await data.SaveAsync(cancellationToken);
    }
}
