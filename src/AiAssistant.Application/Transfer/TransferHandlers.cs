using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AiAssistant.Application.Chats;
using AiAssistant.Application.Appearance;
using AiAssistant.Application.Common;
using AiAssistant.Application.Persistence;
using AiAssistant.Application.Personalization;
using AiAssistant.Application.Workspaces;
using AiAssistant.Domain;
using MediatR;

namespace AiAssistant.Application.Transfer;

public sealed record TransferMessage(string Role, string Content, DateTimeOffset? CreatedAt);
internal sealed record ImportedConversation(string Source, string? SourceId, string? Title, DateTimeOffset? CreatedAt,
    IReadOnlyList<TransferMessage> Messages);
public sealed record TransferConversation(Guid Id, Guid? WorkspaceId, string Title, DateTimeOffset CreatedAt,
    IReadOnlyList<TransferMessage> Messages);
public sealed record TransferMemory(string Content, bool IsEnabled, string Source, DateTimeOffset CreatedAt);
public sealed record TransferArchive(string Format, int Version, DateTimeOffset ExportedAt,
    IReadOnlyList<TransferConversation> Conversations, IReadOnlyList<WorkspaceView>? Workspaces,
    ProfileView? Personalization, IReadOnlyList<TransferMemory>? Memories, AppearanceView? Appearance = null);
public sealed record ImportReport(int Imported, int Skipped, IReadOnlyList<Guid> ChatIds);
public sealed record ImportConversations(Guid UserId, Guid? WorkspaceId, string Json) : IRequest<Result<ImportReport>>;
public sealed record ExportConversations(Guid UserId, Guid? WorkspaceId, Guid? ChatId, bool IncludeAccount)
    : IRequest<Result<TransferArchive>>;

public sealed class TransferHandlers(IAssistantData data, TimeProvider clock) :
    IRequestHandler<ImportConversations, Result<ImportReport>>, IRequestHandler<ExportConversations, Result<TransferArchive>>
{
    public Task<Result<ImportReport>> Handle(ImportConversations request, CancellationToken cancellationToken)
    {
        List<ImportedConversation> incoming;
        try { incoming = ConversationImport.Read(request.Json); }
        catch (InvalidDataException exception)
        {
            return Task.FromResult(Result<ImportReport>.Fail(ErrorKind.Validation, "invalid_archive", exception.Message));
        }
        catch (Exception exception) when (exception is JsonException or InvalidOperationException)
        {
            return Task.FromResult(Result<ImportReport>.Fail(ErrorKind.Validation, "invalid_archive", "Не удалось прочитать JSON выгрузки."));
        }

        var unique = incoming.Select(item => (Item: item, Key: ImportKey(item))).ToArray();
        return data.ForUserAsync(request.UserId, async ct =>
        {
            if (request.WorkspaceId.HasValue && !await data.AnyAsync(data.Query<Workspace>().Where(space =>
                space.UserId == request.UserId && space.Id == request.WorkspaceId), ct)) return WorkspaceHandlers.Missing<ImportReport>();

            var sourceIds = unique.Where(item => item.Item.Source == "Budz" && Guid.TryParse(item.Item.SourceId, out _))
                .Select(item => Guid.Parse(item.Item.SourceId!)).ToArray();
            var keys = unique.Select(item => item.Key).Distinct().ToArray();
            var existing = await data.ListAsync(data.Query<Chat>().Where(chat => chat.UserId == request.UserId &&
                (sourceIds.Contains(chat.Id) || (chat.ImportKey != null && keys.Contains(chat.ImportKey))))
                .Select(chat => new { chat.Id, chat.ImportKey }), ct);
            var alreadyAdded = existing.Select(item => item.ImportKey).ToHashSet(StringComparer.Ordinal);
            var storedIds = existing.Select(item => item.Id).ToHashSet();
            var ids = new List<Guid>();
            foreach (var (item, key) in unique)
            {
                if (item.Source == "Budz" && Guid.TryParse(item.SourceId, out var source) && storedIds.Contains(source)) continue;
                if (!alreadyAdded.Add(key)) continue;
                ids.Add(AddConversation(request, item, key));
            }
            await data.SaveAsync(ct);
            return Result<ImportReport>.Ok(new(ids.Count, incoming.Count - ids.Count, ids));
        }, cancellationToken);
    }

    public async Task<Result<TransferArchive>> Handle(ExportConversations request, CancellationToken ct)
    {
        var chats = data.Query<Chat>().Where(chat => chat.UserId == request.UserId && chat.ExpiresAt == null);
        if (request.WorkspaceId.HasValue)
        {
            if (!await data.AnyAsync(data.Query<Workspace>().Where(space => space.UserId == request.UserId &&
                space.Id == request.WorkspaceId), ct)) return WorkspaceHandlers.Missing<TransferArchive>();
            chats = chats.Where(chat => chat.WorkspaceId == request.WorkspaceId);
        }
        if (request.ChatId.HasValue) chats = chats.Where(chat => chat.Id == request.ChatId);
        var scope = await data.ListAsync(chats.OrderBy(chat => chat.CreatedAt).ThenBy(chat => chat.Id).Take(501)
            .Select(chat => new { chat.Id, chat.WorkspaceId, chat.Title, chat.CreatedAt }), ct);
        if (request.ChatId.HasValue && scope.Count == 0) return ChatAccess.NotFound<TransferArchive>();
        if (scope.Count > 500) return TooLarge();

        var output = new List<TransferConversation>(scope.Count);
        var totalMessages = 0;
        var contentBytes = 0L;
        foreach (var chat in scope)
        {
            var query = data.Query<Message>().Where(message => message.ChatId == chat.Id &&
                message.Role != MessageRole.System && message.Status != GenerationStatus.Superseded &&
                message.Status != GenerationStatus.Pending && message.Status != GenerationStatus.Streaming && message.Content != "")
                .OrderBy(message => message.CreatedAt).ThenBy(message => message.Id)
                .Select(message => new TransferMessage(message.Role == MessageRole.User ? "user" : "assistant",
                    message.Content, message.CreatedAt));
            var messages = new List<TransferMessage>();
            const int batchSize = 128;
            while (true)
            {
                var batch = await data.ListAsync(query.Skip(messages.Count).Take(batchSize), ct);
                totalMessages += batch.Count;
                contentBytes += batch.Sum(message => (long)Encoding.UTF8.GetByteCount(message.Content));
                if (totalMessages > 10000 || contentBytes > 7 * 1024 * 1024) return TooLarge();
                messages.AddRange(batch);
                if (batch.Count < batchSize) break;
            }
            output.Add(new(chat.Id, chat.WorkspaceId, chat.Title, chat.CreatedAt, messages));
        }

        if (!request.IncludeAccount || request.ChatId.HasValue || request.WorkspaceId.HasValue)
            return Result<TransferArchive>.Ok(new("budz-conversations", 1, clock.GetUtcNow(), output, null, null, null));

        var settings = await data.FirstAsync(data.Query<PersonalizationProfile>().Where(item => item.UserId == request.UserId), ct);
        var memory = await data.ListAsync(data.Query<UserMemory>().Where(item => item.UserId == request.UserId)
            .OrderBy(item => item.CreatedAt).ThenBy(item => item.Id).Select(item => new TransferMemory(item.Content,
                item.IsEnabled, item.Source == MemorySource.Auto ? "auto" : "manual", item.CreatedAt)), ct);
        var spaces = await data.ListAsync(data.Query<Workspace>().Where(item => item.UserId == request.UserId)
            .OrderBy(item => item.Name).Select(item => new WorkspaceView(item.Id, item.Name, item.Instructions, item.Materials, item.Color, item.Icon)), ct);
        var appearance = await data.FirstAsync(data.Query<AppearanceSettings>().Where(item => item.UserId == request.UserId), ct);
        return Result<TransferArchive>.Ok(new("budz-conversations", 1, clock.GetUtcNow(), output, spaces,
            settings is null ? null : ProfileView.From(settings), memory, AppearanceView.From(appearance)));
    }

    private Guid AddConversation(ImportConversations request, ImportedConversation item, string key)
    {
        var now = clock.GetUtcNow();
        var latestFirst = now.AddMicroseconds(-item.Messages.Count);
        var first = item.Messages[0].CreatedAt ?? item.CreatedAt ?? latestFirst;
        if (first > latestFirst) first = latestFirst;
        var title = item.Title?.Replace("\0", "").Trim();
        if (string.IsNullOrWhiteSpace(title)) title = $"Импорт из {item.Source}";
        if (title.Length > 120) title = title[..(char.IsHighSurrogate(title[119]) ? 119 : 120)];
        var chat = new Chat { UserId = request.UserId, WorkspaceId = request.WorkspaceId, ImportKey = key,
            Title = title, CreatedAt = item.CreatedAt is { } created && created <= first ? created : first, LastMessageAt = first };
        data.Add(chat);
        Guid? trigger = null;
        var previous = first.AddMicroseconds(-1);
        for (var index = 0; index < item.Messages.Count; index++)
        {
            var turn = item.Messages[index];
            var upper = latestFirst.AddMicroseconds(index);
            var date = turn.CreatedAt is { } time && time <= upper && time - previous >= TimeSpan.FromMicroseconds(1)
                ? time : previous.AddMicroseconds(1);
            var role = turn.Role == "user" ? MessageRole.User : MessageRole.Assistant;
            var message = new Message { ChatId = chat.Id, Role = role, Content = turn.Content, CreatedAt = date,
                Status = GenerationStatus.Completed, TriggerMessageId = role == MessageRole.Assistant ? trigger : null,
                FollowUpCompleted = true };
            data.Add(message);
            if (role == MessageRole.User) trigger = message.Id;
            previous = date;
        }
        chat.LastMessageAt = previous;
        return chat.Id;
    }

    private static string ImportKey(ImportedConversation item) => Convert.ToHexString(SHA256.HashData(
        item.SourceId is { Length: > 0 } id ? Encoding.UTF8.GetBytes(item.Source + "\n" + id) :
        JsonSerializer.SerializeToUtf8Bytes(new { item.Title, item.Messages })));

    private static Result<TransferArchive> TooLarge() => Result<TransferArchive>.Fail(ErrorKind.Validation,
        "export_too_large", "Выгрузка слишком большая. Выберите отдельный диалог или рабочее пространство.");
}
