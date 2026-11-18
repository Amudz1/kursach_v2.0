using AiAssistant.Application.Common;
using AiAssistant.Application.Persistence;
using AiAssistant.Application.Personalization;
using AiAssistant.Domain;
using MediatR;

namespace AiAssistant.Application.Administration;

public sealed record AdminPersonaInput(PersonaInput Profile, string? Model, bool IsActive);
public sealed record AdminPersonaItem(Guid Id, AdminPersonaInput Settings);
public sealed record AdminPlanInput(string Code, string Name, string Description, decimal Price, int DurationDays,
    int? DailyRequestLimit, int MaxPersonas, int MaxMemories, int MaxAttachmentBytes, string[] AllowedModels,
    int SortOrder, bool IsActive);
public sealed record AdminPlanItem(Guid Id, AdminPlanInput Settings);
public sealed record ListAdminPersonas(Guid AdminId) : IRequest<Result<IReadOnlyList<AdminPersonaItem>>>;
public sealed record SaveAdminPersona(Guid AdminId, Guid? Id, AdminPersonaInput Value) : IRequest<Result<Guid>>;
public sealed record RemoveAdminPersona(Guid AdminId, Guid Id) : IRequest<Result<Unit>>;
public sealed record ListAdminPlans(Guid AdminId) : IRequest<Result<IReadOnlyList<AdminPlanItem>>>;
public sealed record SaveAdminPlan(Guid AdminId, Guid? Id, AdminPlanInput Value) : IRequest<Result<Guid>>;
public sealed record RemoveAdminPlan(Guid AdminId, Guid Id) : IRequest<Result<Unit>>;

public sealed class AdminCatalogHandlers(IAssistantData data, AdminAccess access, PreferenceReadCache cache) :
    IRequestHandler<ListAdminPersonas, Result<IReadOnlyList<AdminPersonaItem>>>,
    IRequestHandler<SaveAdminPersona, Result<Guid>>, IRequestHandler<RemoveAdminPersona, Result<Unit>>,
    IRequestHandler<ListAdminPlans, Result<IReadOnlyList<AdminPlanItem>>>,
    IRequestHandler<SaveAdminPlan, Result<Guid>>, IRequestHandler<RemoveAdminPlan, Result<Unit>>
{
    public async Task<Result<IReadOnlyList<AdminPersonaItem>>> Handle(ListAdminPersonas request, CancellationToken cancellationToken)
    {
        if (!await access.AllowedAsync(request.AdminId, cancellationToken)) return AdminAccess.Denied<IReadOnlyList<AdminPersonaItem>>();
        var items = await data.ListAsync(data.Query<Persona>().Where(persona => persona.OwnerId == null)
            .OrderBy(persona => persona.Name).Select(persona => new AdminPersonaItem(persona.Id, new(
                new(persona.Name, persona.Description, persona.SystemPrompt, persona.Temperature),
                persona.DefaultModel, persona.IsActive))), cancellationToken);
        return Result<IReadOnlyList<AdminPersonaItem>>.Ok(items);
    }

    public async Task<Result<Guid>> Handle(SaveAdminPersona request, CancellationToken cancellationToken)
    {
        if (!await access.AllowedAsync(request.AdminId, cancellationToken)) return AdminAccess.Denied<Guid>();
        var validation = await PersonaHandlers.Validator.ValidateAsync(request.Value.Profile, cancellationToken);
        if (!validation.IsValid) return Result<Guid>.Invalid(validation.ToDictionary());
        if (request.Value.Model?.Length > 160)
            return Result<Guid>.Fail(ErrorKind.Validation, "invalid_model", "Название модели слишком длинное.");

        Persona persona;
        if (request.Id.HasValue)
        {
            var existing = await FindPersonaAsync(request.Id.Value, cancellationToken);
            if (existing is null) return MissingPersona<Guid>();
            persona = existing;
        }
        else
        {
            persona = new Persona { Name = "", SystemPrompt = "" };
            data.Add(persona);
        }
        PersonaHandlers.Update(persona, request.Value.Profile);
        persona.DefaultModel = string.IsNullOrWhiteSpace(request.Value.Model) ? null : request.Value.Model.Trim();
        persona.IsActive = request.Value.IsActive;
        await data.SaveAsync(cancellationToken);
        cache.InvalidatePersonas();
        return Result<Guid>.Ok(persona.Id);
    }

    public async Task<Result<Unit>> Handle(RemoveAdminPersona request, CancellationToken cancellationToken)
    {
        if (!await access.AllowedAsync(request.AdminId, cancellationToken)) return AdminAccess.Denied<Unit>();
        var persona = await FindPersonaAsync(request.Id, cancellationToken);
        if (persona is null) return MissingPersona<Unit>();
        data.Remove(persona);
        await data.SaveAsync(cancellationToken);
        cache.InvalidatePersonas();
        return Result<Unit>.Ok(Unit.Value);
    }

    public async Task<Result<IReadOnlyList<AdminPlanItem>>> Handle(ListAdminPlans request, CancellationToken cancellationToken)
    {
        if (!await access.AllowedAsync(request.AdminId, cancellationToken)) return AdminAccess.Denied<IReadOnlyList<AdminPlanItem>>();
        var items = await data.ListAsync(data.Query<SubscriptionPlan>().OrderBy(plan => plan.SortOrder)
            .ThenBy(plan => plan.Code).Select(plan => new AdminPlanItem(plan.Id, new(plan.Code, plan.Name, plan.Description,
                plan.Price, plan.DurationDays, plan.DailyRequestLimit, plan.MaxPersonas, plan.MaxMemories,
                plan.MaxAttachmentBytes, plan.AllowedModels, plan.SortOrder, plan.IsActive))), cancellationToken);
        return Result<IReadOnlyList<AdminPlanItem>>.Ok(items);
    }

    public async Task<Result<Guid>> Handle(SaveAdminPlan request, CancellationToken cancellationToken)
    {
        if (!await access.AllowedAsync(request.AdminId, cancellationToken)) return AdminAccess.Denied<Guid>();
        if (!ValidPlan(request.Value))
            return Result<Guid>.Fail(ErrorKind.Validation, "invalid_plan", "Проверьте параметры тарифа и список моделей.");

        var settings = request.Value;
        SubscriptionPlan plan;
        if (request.Id.HasValue)
        {
            var existing = await FindPlanAsync(request.Id.Value, cancellationToken);
            if (existing is null) return MissingPlan<Guid>();
            if (existing.Code != settings.Code)
                return Result<Guid>.Fail(ErrorKind.Validation, "plan_code_immutable", "Код существующего тарифа изменить нельзя.");
            plan = existing;
        }
        else
        {
            plan = new SubscriptionPlan { Code = settings.Code, Name = "" };
            data.Add(plan);
        }

        Apply(plan, settings);
        try { await data.SaveAsync(cancellationToken); }
        catch (DataConflictException)
        {
            return Result<Guid>.Fail(ErrorKind.Conflict, "plan_exists", "Тариф с таким кодом уже существует.");
        }
        return Result<Guid>.Ok(plan.Id);
    }

    public async Task<Result<Unit>> Handle(RemoveAdminPlan request, CancellationToken cancellationToken)
    {
        if (!await access.AllowedAsync(request.AdminId, cancellationToken)) return AdminAccess.Denied<Unit>();
        var plan = await FindPlanAsync(request.Id, cancellationToken);
        if (plan is null) return MissingPlan<Unit>();
        if (plan.Code == "Free")
            return Result<Unit>.Fail(ErrorKind.Conflict, "default_plan_required", "Базовый тариф Free нельзя удалить.");
        if (await data.AnyAsync(data.Query<Subscription>().Where(item => item.PlanId == plan.Id), cancellationToken) ||
            await data.AnyAsync(data.Query<Transaction>().Where(item => item.PlanId == plan.Id), cancellationToken))
        {
            return Result<Unit>.Fail(ErrorKind.Conflict, "plan_in_use", "У тарифа есть история. Отключите его в настройках.");
        }
        data.Remove(plan);
        await data.SaveAsync(cancellationToken);
        return Result<Unit>.Ok(Unit.Value);
    }

    private Task<Persona?> FindPersonaAsync(Guid id, CancellationToken cancellationToken) =>
        data.FirstAsync(data.Query<Persona>(true).Where(persona => persona.Id == id && persona.OwnerId == null), cancellationToken);

    private Task<SubscriptionPlan?> FindPlanAsync(Guid id, CancellationToken cancellationToken) =>
        data.FirstAsync(data.Query<SubscriptionPlan>(true).Where(plan => plan.Id == id), cancellationToken);

    private static bool ValidPlan(AdminPlanInput input)
    {
        if (input.Code.Length is < 2 or > 20 || !input.Code.All(character => char.IsAsciiLetterOrDigit(character) || character is '-' or '_') ||
            input.Name.Trim().Length is < 1 or > 100 || input.Description.Length > 500 ||
            input.Price is < 0 or > 1_000_000 || input.DurationDays is < 0 or > 3650 || input.DailyRequestLimit is < 0 or > 100_000 ||
            input.MaxPersonas is < 0 or > 1000 || input.MaxMemories is < 0 or > 1000 || input.MaxAttachmentBytes is < 0 or > 16 * 1024 * 1024 ||
            input.AllowedModels.Length is < 1 or > 20 ||
            input.AllowedModels.Any(model => string.IsNullOrWhiteSpace(model) || model.Length > 160 || model.Any(char.IsWhiteSpace)))
            return false;
        return input.Code == "Free" ? input.IsActive && input.Price == 0 && input.DurationDays == 0 : input.DurationDays > 0;
    }

    private static void Apply(SubscriptionPlan plan, AdminPlanInput input)
    {
        plan.Name = input.Name.Trim();
        plan.Description = input.Description.Trim();
        plan.Price = input.Price;
        plan.DurationDays = input.DurationDays;
        plan.DailyRequestLimit = input.DailyRequestLimit;
        plan.MaxPersonas = input.MaxPersonas;
        plan.MaxMemories = input.MaxMemories;
        plan.MaxAttachmentBytes = input.MaxAttachmentBytes;
        plan.AllowedModels = input.AllowedModels.Distinct(StringComparer.Ordinal).ToArray();
        plan.SortOrder = input.SortOrder;
        plan.IsActive = input.IsActive;
    }

    private static Result<T> MissingPersona<T>() => Result<T>.Fail(ErrorKind.NotFound, "persona_not_found", "Системная персона не найдена.");
    private static Result<T> MissingPlan<T>() => Result<T>.Fail(ErrorKind.NotFound, "plan_not_found", "Тариф не найден.");
}
