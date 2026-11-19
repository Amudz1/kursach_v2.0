using AiAssistant.Application.Common;
using AiAssistant.Application.Billing;
using AiAssistant.Application.Persistence;
using AiAssistant.Domain;
using FluentValidation;
using MediatR;

namespace AiAssistant.Application.Personalization;

public sealed record PersonaInput(string Name, string Description, string Instructions, double Temperature);
public sealed record PersonaView(Guid Id, string Name, string Description, string Instructions,
    double Temperature, string? Model, bool IsCustom);

public sealed record ListPersonas(Guid UserId) : IRequest<Result<IReadOnlyList<PersonaView>>>;
public sealed record SavePersona(Guid UserId, Guid? PersonaId, PersonaInput Input) : IRequest<Result<Guid>>;
public sealed record DeletePersona(Guid UserId, Guid PersonaId) : IRequest<Result<Unit>>;

public sealed class PersonaHandlers(IAssistantData data, PersonaAccess access, PlanAccess plans, PreferenceReadCache cache) :
    IRequestHandler<ListPersonas, Result<IReadOnlyList<PersonaView>>>,
    IRequestHandler<SavePersona, Result<Guid>>, IRequestHandler<DeletePersona, Result<Unit>>
{
    internal static readonly IValidator<PersonaInput> Validator = new InlineValidator<PersonaInput>
    {
        value => value.RuleFor(persona => persona.Name).NotEmpty().MaximumLength(80),
        value => value.RuleFor(persona => persona.Description).NotNull().MaximumLength(500),
        value => value.RuleFor(persona => persona.Instructions).NotEmpty().MaximumLength(4000),
        value => value.RuleFor(persona => persona.Temperature).InclusiveBetween(0, 2)
    };

    public async Task<Result<IReadOnlyList<PersonaView>>> Handle(ListPersonas request, CancellationToken cancellationToken)
    {
        var personas = await cache.PersonasAsync(request.UserId, async () =>
            await data.ListAsync(access.Available(request.UserId).OrderBy(persona => persona.OwnerId != null)
            .ThenBy(persona => persona.Id).Select(persona => new PersonaView(persona.Id, persona.Name,
                persona.Description, persona.SystemPrompt, persona.Temperature, persona.DefaultModel, persona.OwnerId != null)),
            cancellationToken));
        return Result<IReadOnlyList<PersonaView>>.Ok(personas);
    }

    public async Task<Result<Guid>> Handle(SavePersona request, CancellationToken cancellationToken)
    {
        var validation = await Validator.ValidateAsync(request.Input, cancellationToken);
        if (!validation.IsValid) return Result<Guid>.Invalid(validation.ToDictionary());

        var result = await data.ForUserAsync(request.UserId, async ct =>
        {
            if (request.PersonaId is not { } id)
            {
                var plan = await plans.ResolveAsync(request.UserId, ct);
                var count = await data.CountAsync(data.Query<Persona>().Where(persona => persona.OwnerId == request.UserId), ct);
                if (count >= plan.MaxPersonas)
                    return Result<Guid>.Fail(ErrorKind.Limit, "persona_limit", "Лимит персон вашего тарифа исчерпан.");
                var newPersona = new Persona
                {
                    OwnerId = request.UserId, Name = request.Input.Name.Trim(), SystemPrompt = request.Input.Instructions.Trim()
                };
                Update(newPersona, request.Input);
                data.Add(newPersona);
                await data.SaveAsync(ct);
                return Result<Guid>.Ok(newPersona.Id);
            }
            var persona = await FindOwnedAsync(request.UserId, id, ct);
            if (persona is null) return NotFound<Guid>();
            Update(persona, request.Input);
            await data.SaveAsync(ct);
            return Result<Guid>.Ok(persona.Id);
        }, cancellationToken);
        if (result.IsSuccess) cache.InvalidatePersonas();
        return result;
    }

    public async Task<Result<Unit>> Handle(DeletePersona request, CancellationToken cancellationToken)
    {
        var result = await data.ForUserAsync(request.UserId, async ct =>
        {
            var persona = await FindOwnedAsync(request.UserId, request.PersonaId, ct);
            if (persona is null) return NotFound<Unit>();
            data.Remove(persona);
            await data.SaveAsync(ct);
            return Result<Unit>.Ok(Unit.Value);
        }, cancellationToken);
        if (result.IsSuccess) cache.InvalidatePersonas();
        return result;
    }

    private Task<Persona?> FindOwnedAsync(Guid userId, Guid personaId, CancellationToken cancellationToken) =>
        data.FirstAsync(data.Query<Persona>(true).Where(persona => persona.Id == personaId && persona.OwnerId == userId), cancellationToken);

    internal static void Update(Persona persona, PersonaInput input)
    {
        persona.Name = input.Name.Trim();
        persona.Description = input.Description.Trim();
        persona.SystemPrompt = input.Instructions.Trim();
        persona.Temperature = input.Temperature;
    }

    private static Result<T> NotFound<T>() =>
        Result<T>.Fail(ErrorKind.NotFound, "persona_not_found", "Персона не найдена.");
}
