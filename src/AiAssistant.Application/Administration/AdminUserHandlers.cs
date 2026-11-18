using AiAssistant.Application.Billing;
using AiAssistant.Application.Chats;
using AiAssistant.Application.Common;
using AiAssistant.Application.Persistence;
using AiAssistant.Domain;
using MediatR;

namespace AiAssistant.Application.Administration;

public sealed record AdminUser(Guid Id, string Username, string Email, string Role, bool IsBlocked, string PlanCode);
public sealed record ListAdminUsers(Guid AdminId, string? Search, int Page) : IRequest<Result<Page<AdminUser>>>;
public sealed record UpdateAdminUser(Guid AdminId, Guid UserId, bool? IsBlocked, Guid? PlanId) : IRequest<Result<Unit>>;

public sealed class AdminUserHandlers(IAssistantData data, AdminAccess access, SubscriptionWriter subscriptions,
    GenerationStops stops, IUsageLimitService limits, TimeProvider clock) :
    IRequestHandler<ListAdminUsers, Result<Page<AdminUser>>>, IRequestHandler<UpdateAdminUser, Result<Unit>>
{
    private const int PageSize = 30;

    public async Task<Result<Page<AdminUser>>> Handle(ListAdminUsers request, CancellationToken cancellationToken)
    {
        if (!await access.AllowedAsync(request.AdminId, cancellationToken)) return AdminAccess.Denied<Page<AdminUser>>();
        if (request.Search?.Length > 254 || request.Page is < 1 or > 500)
            return Result<Page<AdminUser>>.Fail(ErrorKind.Validation, "invalid_page", "Проверьте параметры поиска.");

        var filter = request.Search?.Trim().ToUpperInvariant();
        var now = clock.GetUtcNow();
        var users = data.Query<User>();
        if (!string.IsNullOrEmpty(filter))
            users = users.Where(user => user.NormalizedUsername.Contains(filter) || user.NormalizedEmail.Contains(filter));
        var records = await data.ListAsync(users.OrderBy(user => user.NormalizedUsername).ThenBy(user => user.Id)
            .Skip((request.Page - 1) * PageSize).Take(PageSize + 1), cancellationToken);
        var ids = records.Select(user => user.Id).ToArray();
        var plans = await data.ListAsync(
            from sub in data.Query<Subscription>()
            join plan in data.Query<SubscriptionPlan>() on sub.PlanId equals plan.Id
            where ids.Contains(sub.UserId) && sub.Status == SubscriptionStatus.Active &&
                  sub.StartsAt <= now && sub.EndsAt > now
            select new { sub.UserId, plan.Code }, cancellationToken);
        var byUser = plans.ToDictionary(plan => plan.UserId, plan => plan.Code);
        var views = records.Take(PageSize).Select(user => new AdminUser(user.Id, user.Username, user.Email,
            user.Role.ToString(), user.IsBlocked, byUser.GetValueOrDefault(user.Id, "Free"))).ToArray();
        return Result<Page<AdminUser>>.Ok(new(views, records.Count > PageSize));
    }

    public async Task<Result<Unit>> Handle(UpdateAdminUser request, CancellationToken cancellationToken)
    {
        if (!await access.AllowedAsync(request.AdminId, cancellationToken)) return AdminAccess.Denied<Unit>();
        if (request.AdminId == request.UserId && request.IsBlocked == true)
            return Result<Unit>.Fail(ErrorKind.Validation, "admin_self_block", "Нельзя заблокировать собственный аккаунт.");

        List<Guid> cancellations = [];
        var result = await data.ForUserAsync(request.UserId, async ct =>
        {
            var user = await data.FirstAsync(data.Query<User>(true).Where(user => user.Id == request.UserId), ct);
            if (user is null) return Result<Unit>.Fail(ErrorKind.NotFound, "user_not_found", "Аккаунт не найден.");
            var plan = request.PlanId is { } planId
                ? await data.FirstAsync(data.Query<SubscriptionPlan>().Where(item => item.Id == planId && item.IsActive), ct)
                : null;
            if (request.PlanId.HasValue && plan is null)
                return Result<Unit>.Fail(ErrorKind.NotFound, "plan_not_found", "Тариф недоступен.");

            var now = clock.GetUtcNow();
            if (request.IsBlocked is { } blocked && user.IsBlocked != blocked)
            {
                user.IsBlocked = blocked;
                user.SessionVersion++;
                if (blocked) cancellations = await BlockSessionsAsync(user.Id, now, ct);
            }
            if (plan is not null) await subscriptions.ReplaceAsync(user.Id, plan, now, ct);
            await data.SaveAsync(ct);
            return Result<Unit>.Ok(Unit.Value);
        }, cancellationToken);
        if (result.IsSuccess) foreach (var id in cancellations) await stops.StopAsync(id);
        return result;
    }

    private async Task<List<Guid>> BlockSessionsAsync(Guid userId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var tokens = await data.ListAsync(data.Query<RefreshToken>(true)
            .Where(token => token.UserId == userId && token.RevokedAt == null), cancellationToken);
        foreach (var token in tokens) token.RevokedAt = now;

        var active = await data.ListAsync(
            from message in data.Query<Message>(true)
            join chat in data.Query<Chat>() on message.ChatId equals chat.Id
            where chat.UserId == userId && (message.Status == GenerationStatus.Pending || message.Status == GenerationStatus.Streaming)
            select message, cancellationToken);
        foreach (var message in active)
        {
            if (message.Status == GenerationStatus.Pending)
                await limits.FinishAsync(userId, message.CreatedAt, true, 0, cancellationToken);
            message.Status = GenerationStatus.Stopped;
        }
        return active.Select(message => message.Id).ToList();
    }
}
