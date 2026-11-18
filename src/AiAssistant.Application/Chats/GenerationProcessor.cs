using System.Text;
using System.Text.Json;
using AiAssistant.Application.Ai;
using AiAssistant.Application.Billing;
using AiAssistant.Application.Persistence;
using AiAssistant.Domain;
using Microsoft.Extensions.Logging;

namespace AiAssistant.Application.Chats;

public sealed class GenerationProcessor(IAssistantData data, IAiProvider provider, GenerationStops stops,
    IGenerationEvents events, IPromptContextBuilder context, IUsageLimitService limits, ILogger<GenerationProcessor> logger)
{
    public async Task<bool> ProcessNextAsync(CancellationToken cancellationToken)
    {
        var next = await data.FirstAsync(
            from message in data.Query<Message>()
            join chat in data.Query<Chat>() on message.ChatId equals chat.Id
            where message.Role == MessageRole.Assistant && message.Status == GenerationStatus.Pending
            orderby message.CreatedAt, message.Id
            select new WorkItem(message.Id, chat.Id, chat.UserId, message.CreatedAt),
            cancellationToken);
        if (next is null) return false;

        using var stop = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        if (!stops.TryTrack(next.MessageId, stop)) return false;
        try
        {
            if (!await ClaimAsync(next, cancellationToken)) return true;
            await RunAsync(next, stop.Token, cancellationToken);
            return true;
        }
        finally
        {
            stops.Release(next.MessageId);
        }
    }

    public async Task RecoverAsync(CancellationToken cancellationToken)
    {
        var interrupted = await data.ListAsync(data.Query<Message>(true)
            .Where(message => message.Status == GenerationStatus.Streaming || message.ReservedTokens > 0), cancellationToken);
        foreach (var message in interrupted)
        {
            message.ReservedTokens = 0;
            if (message.Status == GenerationStatus.Streaming)
            {
                message.Status = GenerationStatus.Failed;
                message.ErrorCode = "generation_interrupted";
            }
        }
        if (interrupted.Count > 0) await data.SaveAsync(cancellationToken);
    }

    private Task<bool> ClaimAsync(WorkItem item, CancellationToken cancellationToken) =>
        data.ForUserAsync(item.UserId, async ct =>
        {
            var message = await FindAsync(item.MessageId, true, ct);
            if (message?.Status != GenerationStatus.Pending) return false;
            message.Status = GenerationStatus.Streaming;
            await data.SaveAsync(ct);
            return true;
        }, cancellationToken);

    private async Task RunAsync(WorkItem item, CancellationToken stopToken, CancellationToken serviceToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(stopToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(90));
        var text = new StringBuilder();
        AiUsage? usage = null;
        AiRequest? admitted = null;
        string? error = null;
        var status = GenerationStatus.Completed;
        try
        {
            var available = await limits.RemainingAsync(item.UserId, item.StartedAt, timeout.Token);
            var request = await context.BuildAsync(item.UserId, item.ChatId, item.MessageId, timeout.Token, available);
            if (!await ReserveTokensAsync(item, request, timeout.Token))
            {
                timeout.Token.ThrowIfCancellationRequested();
                throw new AiProviderException("generation_interrupted", "Генерация больше не активна.");
            }
            admitted = request;
            await events.StartedAsync(item.UserId, item.ChatId, item.MessageId, timeout.Token);
            await foreach (var chunk in provider.StreamAsync(request, timeout.Token).WithCancellation(timeout.Token))
            {
                if (chunk.Usage is not null) usage = chunk.Usage;
                if (string.IsNullOrEmpty(chunk.Content)) continue;
                var offset = text.Length;
                text.Append(chunk.Content);
                await events.TokenAsync(item.UserId, item.ChatId, item.MessageId, offset, chunk.Content, timeout.Token);
            }

            if (text.Length == 0) throw new AiProviderException("ai_empty", "Модель вернула пустой ответ.");
        }
        catch (OperationCanceledException) when (stopToken.IsCancellationRequested)
        {
            status = GenerationStatus.Stopped;
        }
        catch (OperationCanceledException)
        {
            status = GenerationStatus.Failed;
            error = "ai_timeout";
        }
        catch (AiProviderException exception)
        {
            status = GenerationStatus.Failed;
            error = exception.Code;
        }
        catch (Exception exception)
        {
            status = GenerationStatus.Failed;
            error = "ai_unavailable";
            logger.LogWarning("Generation {MessageId} failed with {ErrorType}", item.MessageId, exception.GetType().Name);
        }

        using var saveTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var content = text.ToString();
        if (usage is null && admitted is not null && content.Length > 0)
            usage = new(PromptContextBuilder.EstimateInput(admitted),
                Math.Min(admitted.MaxTokens, PromptContextBuilder.Cost(content)));
        var saved = await FinalizeAsync(item, content, usage, status, error, saveTimeout.Token);
        if (saved is not null && !serviceToken.IsCancellationRequested)
            await events.FinishedAsync(item.UserId, item.ChatId, saved, usage, serviceToken);
    }

    private Task<bool> ReserveTokensAsync(WorkItem item, AiRequest request, CancellationToken cancellationToken) =>
        data.ForUserAsync(item.UserId, async ct =>
        {
            var current = await FindAsync(item.MessageId, false, ct);
            if (current?.Status != GenerationStatus.Streaming) return false;
            var cost = PromptContextBuilder.EstimateInput(request) + request.MaxTokens;
            var available = await limits.RemainingAsync(item.UserId, item.StartedAt, ct);
            if (available.HasValue && cost > available.Value) throw PromptContextBuilder.TokenLimit();
            var message = await FindAsync(item.MessageId, true, ct);
            if (message is null) return false;
            message.ReservedTokens = cost;
            message.ContextSnapshot = JsonSerializer.Serialize(request.Messages.Select(entry => new
            {
                entry.Role, entry.Content, Images = entry.Images?.Select(image => new { image.FileName, image.ContentType })
            }));
            await data.SaveAsync(ct);
            return true;
        }, cancellationToken);

    private Task<MessageView?> FinalizeAsync(WorkItem item, string content, AiUsage? usage, GenerationStatus status,
        string? error, CancellationToken cancellationToken) =>
        data.ForUserAsync<MessageView?>(item.UserId, async ct =>
        {
            // The current state might have changed in a different request while the provider was streaming.
            var current = await FindAsync(item.MessageId, false, ct);
            var consumed = (long)(usage?.PromptTokens ?? 0) + (usage?.CompletionTokens ?? 0);
            if (current is null)
            {
                await limits.FinishAsync(item.UserId, item.StartedAt, consumed == 0, consumed, ct);
                await data.SaveAsync(ct);
                return null;
            }
            var message = await FindAsync(item.MessageId, true, ct);
            if (message is null) return null;
            message.Status = current.Status is GenerationStatus.Stopped or GenerationStatus.Superseded ? current.Status : status;
            message.Content = content;
            message.ErrorCode = message.Status == GenerationStatus.Failed ? error : null;
            message.PromptTokens = usage?.PromptTokens;
            message.CompletionTokens = usage?.CompletionTokens;
            message.ReservedTokens = 0;
            await limits.FinishAsync(item.UserId, message.CreatedAt, consumed == 0 && status != GenerationStatus.Completed, consumed, ct);
            await data.SaveAsync(ct);
            return message.Status == GenerationStatus.Superseded ? null : ChatReadHandlers.ToView(message);
        }, cancellationToken);

    private Task<Message?> FindAsync(Guid id, bool tracked, CancellationToken cancellationToken) =>
        data.FirstAsync(data.Query<Message>(tracked).Where(message => message.Id == id), cancellationToken);

    private sealed record WorkItem(Guid MessageId, Guid ChatId, Guid UserId, DateTimeOffset StartedAt);
}
