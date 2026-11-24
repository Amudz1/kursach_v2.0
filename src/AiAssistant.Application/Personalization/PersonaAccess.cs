using AiAssistant.Application.Ai;
using AiAssistant.Application.Persistence;
using AiAssistant.Domain;

namespace AiAssistant.Application.Personalization;

public sealed class PersonaAccess(IAssistantData data, AiDefaults defaults)
{
    public IQueryable<Persona> Available(Guid userId) => data.Query<Persona>()
        .Where(persona => persona.IsActive && (persona.OwnerId == null || persona.OwnerId == userId));

    public Task<Persona?> FindAsync(Guid userId, Guid personaId, CancellationToken cancellationToken) =>
        data.FirstAsync(Available(userId).Where(persona => persona.Id == personaId), cancellationToken);

    public async Task<Persona?> ResolveAsync(Guid userId, Guid? personaId, CancellationToken cancellationToken)
    {
        if (personaId.HasValue)
        {
            var persona = await FindAsync(userId, personaId.Value, cancellationToken);
            if (persona is not null) return persona;
        }
        return await data.FirstAsync(Available(userId).Where(persona => persona.OwnerId == null).OrderBy(persona => persona.Id),
            cancellationToken);
    }

    public string ModelFor(Persona? persona) => persona?.DefaultModel ?? defaults.Model;
}
