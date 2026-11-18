using System.Text.RegularExpressions;
using AiAssistant.Application.Billing;
using AiAssistant.Application.Ai;
using AiAssistant.Application.Common;
using AiAssistant.Application.Files;
using AiAssistant.Application.Persistence;
using AiAssistant.Application.Personalization;
using AiAssistant.Domain;

namespace AiAssistant.Application.Chats;

public sealed record QueuedMessage(Guid Id, bool IsNew);

public sealed partial class ChatSubmission(IAssistantData data, ChatAccess access, PersonaAccess personas,
    PlanAccess plans, IUsageLimitService usage, IAttachmentStorage storage, AiDefaults defaults, TimeProvider clock)
{
    public async Task<Result<QueuedMessage>> PrepareAsync(SendChatMessage request, CancellationToken cancellationToken)
    {
        var text = request.Content.Trim();
        var incoming = request.Files ?? [];
        if (text.Length > 10000 || (text.Length == 0 && incoming.Count == 0) || incoming.Count > 4)
            return Result<QueuedMessage>.Fail(ErrorKind.Validation, "invalid_message", "Введите сообщение или добавьте до четырёх вложений.");
        var prepared = await PrepareFilesAsync(request, incoming, cancellationToken);
        if (!prepared.IsSuccess) return new(null, prepared.Error);
        var files = prepared.Value!;
        if (text.Length == 0) text = "Материалы во вложении.";

        return await data.ForUserAsync(request.UserId, async ct =>
        {
            var chat = await access.FindAsync(request.UserId, request.ChatId, true, ct);
            if (chat is null) return ChatAccess.NotFound<QueuedMessage>();
            var messages = data.Query<Message>().Where(message => message.ChatId == chat.Id);
            var prior = await data.FirstAsync(messages.Where(message => message.RequestId == request.RequestId), ct);
            if (prior is not null) return Result<QueuedMessage>.Ok(new(prior.Id, false));
            if (await data.AnyAsync(messages.Where(message =>
                    message.Status == GenerationStatus.Pending || message.Status == GenerationStatus.Streaming), ct))
            {
                return Result<QueuedMessage>.Fail(ErrorKind.Conflict, "generation_active", "Дождитесь текущего ответа или остановите его.");
            }

            if (!await data.AnyAsync(messages, ct) && chat.Title == "Новый чат") chat.Title = Title(text);
            var now = clock.GetUtcNow();
            var persona = await personas.ResolveAsync(request.UserId, chat.PersonaId, ct);
            var plan = await plans.ResolveAsync(request.UserId, ct);
            if (files.Sum(file => (long)file.Bytes.Length) > plan.MaxAttachmentBytes)
                return FileLimit();
            var model = SelectModel(plan, persona, request.Model, files.Any(file => file.IsImage));
            if (model is null)
                return Result<QueuedMessage>.Fail(ErrorKind.Forbidden, "model_not_allowed", "Модель недоступна на вашем тарифе.");
            if (!await usage.ReserveAsync(request.UserId, plan.DailyRequestLimit, now.AddMicroseconds(1), ct))
                return LimitReached<QueuedMessage>();
            var userMessage = new Message { ChatId = chat.Id, Role = MessageRole.User, Content = text, CreatedAt = now };
            var assistant = new Message
            {
                ChatId = chat.Id, PersonaId = persona?.Id, Role = MessageRole.Assistant, Content = "", Model = model,
                Status = GenerationStatus.Pending, CreatedAt = now.AddMicroseconds(1),
                TriggerMessageId = userMessage.Id, RequestId = request.RequestId
            };
            chat.LastMessageAt = assistant.CreatedAt;
            data.Add(userMessage);
            data.Add(assistant);
            foreach (var file in files)
            {
                var key = Guid.NewGuid().ToString("N");
                await storage.WriteAsync(key, file.Bytes, ct);
                data.Add(new Attachment { MessageId = userMessage.Id, StorageKey = key, FileName = file.FileName,
                    ContentType = file.ContentType, Size = file.Bytes.Length, ExtractedText = file.ExtractedText });
            }
            await data.SaveAsync(ct);
            return Result<QueuedMessage>.Ok(new(assistant.Id, true));
        }, cancellationToken);
    }

    private async Task<Result<IReadOnlyList<PreparedAttachment>>> PrepareFilesAsync(SendChatMessage request,
        IReadOnlyList<IncomingAttachment> files, CancellationToken cancellationToken)
    {
        if (files.Count == 0) return Result<IReadOnlyList<PreparedAttachment>>.Ok([]);
        if (await access.FindAsync(request.UserId, request.ChatId, false, cancellationToken) is null)
            return ChatAccess.NotFound<IReadOnlyList<PreparedAttachment>>();
        var plan = await plans.ResolveAsync(request.UserId, cancellationToken);
        if (files.Sum(file => file.Length) > plan.MaxAttachmentBytes)
            return new(null, FileLimit().Error);
        var result = new List<PreparedAttachment>(files.Count);
        try
        {
            using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            deadline.CancelAfter(TimeSpan.FromSeconds(10));
            foreach (var file in files)
                result.Add(await storage.PrepareAsync(file, deadline.Token));
            return Result<IReadOnlyList<PreparedAttachment>>.Ok(result);
        }
        catch (FileRejectedException exception)
        {
            return Result<IReadOnlyList<PreparedAttachment>>.Fail(ErrorKind.Validation, "invalid_attachment", exception.Message);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return Result<IReadOnlyList<PreparedAttachment>>.Fail(ErrorKind.Validation, "attachment_timeout", "Не удалось разобрать файл вовремя.");
        }
    }

    private static Result<QueuedMessage> FileLimit() => Result<QueuedMessage>.Fail(ErrorKind.Validation,
        "attachment_limit", "Общий размер вложений превышает лимит тарифа.");

    private string? SelectModel(SubscriptionPlan plan, Persona? persona, string? desired, bool needsVision = false)
    {
        var allowed = plan.AllowedModels.Where(model => !needsVision || defaults.SupportsVision(model)).ToArray();
        var model = desired ?? personas.ModelFor(persona);
        if (allowed.Contains(model, StringComparer.Ordinal)) return model;
        return desired is null ? allowed.FirstOrDefault() : null;
    }

    private static Result<T> LimitReached<T>() => Result<T>.Fail(ErrorKind.Limit, "daily_limit",
        "Дневной лимит запросов исчерпан. Лимит обновится в полночь UTC.");

    private static string Title(string content)
    {
        var title = string.Join(' ', Spaces().Split(content).Take(6));
        return title.Length <= 65 ? title : title[..64] + "…";
    }

    [GeneratedRegex(@"\s+")]
    private static partial Regex Spaces();
}
