using AiAssistant.Application.Common;
using AiAssistant.Application.Persistence;
using AiAssistant.Domain;
using MediatR;

namespace AiAssistant.Application.Billing;

public sealed record PlanView(Guid Id, string Code, string Name, string Description, decimal Price, int DurationDays,
    int? DailyRequestLimit, int MaxPersonas, int MaxMemories, int MaxAttachmentBytes, string[] AllowedModels);
public sealed record ListPlans : IRequest<Result<IReadOnlyList<PlanView>>>;
public sealed record PurchasePlan(Guid UserId, Guid PlanId, Guid RequestId, string CardNumber, string CardHolder,
    string ExpireMonth, string ExpireYear, string Cvv) : IRequest<Result<Guid>>;

public sealed class BillingHandlers(IAssistantData data, TimeProvider clock) :
    IRequestHandler<ListPlans, Result<IReadOnlyList<PlanView>>>, IRequestHandler<PurchasePlan, Result<Guid>>
{
    public async Task<Result<IReadOnlyList<PlanView>>> Handle(ListPlans request, CancellationToken cancellationToken)
    {
        var plans = await data.ListAsync(data.Query<SubscriptionPlan>().Where(plan => plan.IsActive)
            .OrderBy(plan => plan.SortOrder).ThenBy(plan => plan.Code).Select(plan => new PlanView(plan.Id, plan.Code, plan.Name,
                plan.Description, plan.Price, plan.DurationDays, plan.DailyRequestLimit, plan.MaxPersonas, plan.MaxMemories,
                plan.MaxAttachmentBytes, plan.AllowedModels)), cancellationToken);
        return Result<IReadOnlyList<PlanView>>.Ok(plans);
    }

    public Task<Result<Guid>> Handle(PurchasePlan request, CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow();
        if (!ValidDemoForm(request, now))
            return Task.FromResult(Result<Guid>.Fail(ErrorKind.Validation, "invalid_demo_payment",
                "Для имитации оплаты используйте тестовую карту 4242 4242 4242 4242 и действительный срок."));

        return data.ForUserAsync(request.UserId, async ct =>
        {
            var prior = await data.FirstAsync(data.Query<Transaction>().Where(item => item.UserId == request.UserId &&
                item.RequestId == request.RequestId), ct);
            if (prior is not null)
                return prior.PlanId == request.PlanId ? Result<Guid>.Ok(prior.Id) :
                    Result<Guid>.Fail(ErrorKind.Conflict, "payment_already_used", "Запрос уже относится к другому тарифу.");

            var plan = await data.FirstAsync(data.Query<SubscriptionPlan>()
                .Where(plan => plan.IsActive && plan.Id == request.PlanId && plan.DurationDays > 0 && plan.Code != "Free"), ct);
            if (plan is null) return Result<Guid>.Fail(ErrorKind.NotFound, "plan_not_found", "Платный тариф недоступен.");
            var previous = await data.FirstAsync(data.Query<Subscription>(true).Where(item => item.UserId == request.UserId &&
                item.Status == SubscriptionStatus.Active), ct);
            if (previous is not null)
            {
                previous.Status = previous.EndsAt <= now ? SubscriptionStatus.Expired : SubscriptionStatus.Replaced;
                await data.SaveAsync(ct);
            }

            data.Add(new Subscription { UserId = request.UserId, PlanId = plan.Id, StartsAt = now, EndsAt = now.AddDays(plan.DurationDays) });
            var transaction = new Transaction
            {
                UserId = request.UserId, PlanId = plan.Id, RequestId = request.RequestId, Amount = plan.Price,
                CardMask = "**** 4242", CreatedAt = now
            };
            data.Add(transaction);
            await data.SaveAsync(ct);
            return Result<Guid>.Ok(transaction.Id);
        }, cancellationToken);
    }

    private static bool ValidDemoForm(PurchasePlan request, DateTimeOffset now)
    {
        var number = request.CardNumber.Replace(" ", "", StringComparison.Ordinal);
        if (request.RequestId == Guid.Empty || number != "4242424242424242" || request.CardHolder.Trim().Length is < 3 or > 100 ||
            request.Cvv.Length is < 3 or > 4 || !request.Cvv.All(char.IsAsciiDigit) ||
            request.ExpireMonth.Length != 2 || request.ExpireYear.Length != 4 ||
            !int.TryParse(request.ExpireMonth, out var month) || month is < 1 or > 12 ||
            !int.TryParse(request.ExpireYear, out var year))
            return false;
        return year > now.Year || (year == now.Year && month >= now.Month);
    }
}
