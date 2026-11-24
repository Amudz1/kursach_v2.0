using AiAssistant.Application.Administration;
using AiAssistant.Application.Common;
using AiAssistant.Application.Persistence;
using AiAssistant.Domain;
using MediatR;

namespace AiAssistant.Application.Support;

public sealed record SupportInput(string? Content, Guid RequestId);
public sealed record SupportMessageView(long Id, bool FromSupport, string Content, DateTimeOffset CreatedAt)
{
    public static SupportMessageView From(SupportMessage item) => new(item.Id, item.FromSupport, item.Content, item.CreatedAt);
}

public sealed record SupportHistory(IReadOnlyList<SupportMessageView> Messages, long? Previous);
public sealed record SupportThreadView(Guid UserId, string Username, DateTimeOffset LastMessageAt);
public sealed record SupportQueue(IReadOnlyList<SupportThreadView> Threads, long? Previous);

public sealed record GetSupportMessages(Guid ActorId, Guid? AccountId, long? Before, long? After) : IRequest<Result<SupportHistory>>;
public sealed record SendSupportMessage(Guid ActorId, Guid? AccountId, SupportInput Input) : IRequest<Result<SupportMessageView>>;
public sealed record ListSupportThreads(Guid ActorId, long? Before) : IRequest<Result<SupportQueue>>;

public sealed class SupportHandlers(IAssistantData data, AdminAccess adminAccess, TimeProvider clock) :
    IRequestHandler<GetSupportMessages, Result<SupportHistory>>,
    IRequestHandler<SendSupportMessage, Result<SupportMessageView>>,
    IRequestHandler<ListSupportThreads, Result<SupportQueue>>
{
    private const int PageSize = 50;

    public async Task<Result<SupportHistory>> Handle(GetSupportMessages request, CancellationToken ct)
    {
        if (request.AccountId.HasValue && !await adminAccess.AllowedAsync(request.ActorId, ct))
            return AdminAccess.Denied<SupportHistory>();

        var userId = request.AccountId ?? request.ActorId;
        var messages = Messages(userId);
        if (request.After is { } after)
        {
            var updates = await data.ListAsync(messages.Where(item => item.Id > after).OrderBy(item => item.Id).Take(PageSize), ct);
            return Result<SupportHistory>.Ok(new(updates.Select(SupportMessageView.From).ToArray(), null));
        }
        if (request.Before is { } before) messages = messages.Where(item => item.Id < before);
        var page = await data.ListAsync(messages.OrderByDescending(item => item.Id).Take(PageSize + 1), ct);
        if (request.AccountId.HasValue && request.Before is null && page.Count == 0) return Missing<SupportHistory>();

        var previous = page.Count > PageSize ? page[PageSize - 1].Id : (long?)null;
        return Result<SupportHistory>.Ok(new(page.Take(PageSize).Reverse().Select(SupportMessageView.From).ToArray(), previous));
    }

    public async Task<Result<SupportMessageView>> Handle(SendSupportMessage request, CancellationToken ct)
    {
        var fromSupport = request.AccountId.HasValue;
        if (fromSupport && !await adminAccess.AllowedAsync(request.ActorId, ct))
            return AdminAccess.Denied<SupportMessageView>();

        var content = request.Input.Content?.Trim();
        if (string.IsNullOrEmpty(content) || content.Length > 4000)
            return Result<SupportMessageView>.Invalid(new Dictionary<string, string[]>
                { ["content"] = ["Введите сообщение длиной от 1 до 4000 символов."] });
        if (request.Input.RequestId == Guid.Empty)
            return Result<SupportMessageView>.Invalid(new Dictionary<string, string[]>
                { ["requestId"] = ["Требуется идентификатор отправки."] });

        var userId = request.AccountId ?? request.ActorId;
        return await data.ForUserAsync(userId, async token =>
        {
            var messages = Messages(userId);
            var existing = await data.FirstAsync(messages.Where(item => item.RequestId == request.Input.RequestId), token);
            if (existing is not null)
            {
                return existing.Content == content && existing.FromSupport == fromSupport
                    ? Result<SupportMessageView>.Ok(SupportMessageView.From(existing))
                    : Result<SupportMessageView>.Fail(ErrorKind.Conflict, "support_request_conflict", "Эта отправка уже использована.");
            }
            if (fromSupport ? !await data.AnyAsync(messages, token) :
                !await data.AnyAsync(data.Query<User>().Where(user => user.Id == userId && !user.IsBlocked), token))
                return Missing<SupportMessageView>();

            var message = new SupportMessage
            {
                UserId = userId, RequestId = request.Input.RequestId, FromSupport = fromSupport,
                Content = content, CreatedAt = clock.GetUtcNow()
            };
            data.Add(message);
            await data.SaveAsync(token);
            return Result<SupportMessageView>.Ok(SupportMessageView.From(message));
        }, ct);
    }

    public async Task<Result<SupportQueue>> Handle(ListSupportThreads request, CancellationToken ct)
    {
        if (!await adminAccess.AllowedAsync(request.ActorId, ct)) return AdminAccess.Denied<SupportQueue>();

        var messages = data.Query<SupportMessage>();
        var latest = from message in messages
                     group message by message.UserId into thread
                     select new { UserId = thread.Key, LastId = thread.Max(item => item.Id) };
        if (request.Before is { } before) latest = latest.Where(item => item.LastId < before);
        var page = await data.ListAsync((
            from thread in latest
            join user in data.Query<User>() on thread.UserId equals user.Id
            join message in messages on thread.LastId equals message.Id
            orderby thread.LastId descending
            select new { thread.LastId, user.Id, user.Username, message.CreatedAt }).Take(PageSize + 1), ct);

        var previous = page.Count > PageSize ? page[PageSize - 1].LastId : (long?)null;
        var threads = page.Take(PageSize).Select(item => new SupportThreadView(item.Id, item.Username, item.CreatedAt)).ToArray();
        return Result<SupportQueue>.Ok(new(threads, previous));
    }

    private IQueryable<SupportMessage> Messages(Guid userId) => data.Query<SupportMessage>().Where(item => item.UserId == userId);

    private static Result<T> Missing<T>() =>
        Result<T>.Fail(ErrorKind.NotFound, "support_thread_not_found", "Переписка не найдена.");
}
