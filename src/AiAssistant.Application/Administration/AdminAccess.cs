using AiAssistant.Application.Common;
using AiAssistant.Application.Persistence;
using AiAssistant.Domain;

namespace AiAssistant.Application.Administration;

public sealed class AdminAccess(IAssistantData data)
{
    public Task<bool> AllowedAsync(Guid actorId, CancellationToken cancellationToken) =>
        data.AnyAsync(data.Query<User>().Where(user => user.Id == actorId && !user.IsBlocked && user.Role == Role.Admin),
            cancellationToken);

    public static Result<T> Denied<T>() =>
        Result<T>.Fail(ErrorKind.Forbidden, "admin_required", "Нужны права администратора.");
}
