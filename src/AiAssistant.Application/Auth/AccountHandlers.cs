using AiAssistant.Application.Common;
using AiAssistant.Application.Billing;
using AiAssistant.Application.Persistence;
using AiAssistant.Domain;
using MediatR;

namespace AiAssistant.Application.Auth;

public sealed class AccountHandlers(IAssistantData data, IPasswordService passwords, TimeProvider clock, IUsageLimitService usage) :
    IRequestHandler<GetAccount, Result<AccountView>>,
    IRequestHandler<ChangeEmail, Result<Unit>>,
    IRequestHandler<ChangePassword, Result<Unit>>,
    IRequestHandler<DeleteAccount, Result<Unit>>
{
    public async Task<Result<AccountView>> Handle(GetAccount request, CancellationToken cancellationToken)
    {
        var user = await data.FirstAsync(data.Query<User>().Where(user => user.Id == request.UserId), cancellationToken);
        if (user is null)
            return Result<AccountView>.Fail(ErrorKind.NotFound, "account_not_found", "Аккаунт не найден.");
        var onboardingCompleted = await data.FirstAsync(data.Query<PersonalizationProfile>()
            .Where(profile => profile.UserId == user.Id).Select(profile => profile.OnboardingCompleted), cancellationToken);
        var limits = await usage.ReadAsync(user.Id, cancellationToken);
        return Result<AccountView>.Ok(new(user.Id, user.Username, user.Email, user.Role.ToString(), onboardingCompleted, limits));
    }

    public async Task<Result<Unit>> Handle(ChangeEmail request, CancellationToken cancellationToken)
    {
        request = request with { Email = request.Email.Trim() };
        var validation = await AccountValidators.Email.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid) return Result<Unit>.Invalid(validation.ToDictionary());
        try
        {
            return await ForVerifiedAccountAsync(request.UserId, request.Password, (user, _) =>
            {
                user.Email = request.Email;
                user.NormalizedEmail = request.Email.ToUpperInvariant();
                return Task.CompletedTask;
            }, cancellationToken);
        }
        catch (DataConflictException)
        {
            return Result<Unit>.Fail(ErrorKind.Conflict, "email_exists", "Этот email уже используется.");
        }
    }

    public async Task<Result<Unit>> Handle(ChangePassword request, CancellationToken cancellationToken)
    {
        var validation = await AccountValidators.Password.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid) return Result<Unit>.Invalid(validation.ToDictionary());
        return await ForVerifiedAccountAsync(request.UserId, request.CurrentPassword, async (user, ct) =>
        {
            user.PasswordHash = passwords.Hash(user, request.Password);
            user.SessionVersion++;
            var tokens = await data.ListAsync(data.Query<RefreshToken>(true)
                .Where(token => token.UserId == user.Id && token.RevokedAt == null), ct);
            var now = clock.GetUtcNow();
            foreach (var token in tokens) token.RevokedAt = now;
        }, cancellationToken);
    }

    public Task<Result<Unit>> Handle(DeleteAccount request, CancellationToken cancellationToken) =>
        ForVerifiedAccountAsync(request.UserId, request.Password, (user, _) =>
        {
            data.Remove(user);
            return Task.CompletedTask;
        }, cancellationToken);

    private Task<Result<Unit>> ForVerifiedAccountAsync(Guid userId, string password,
        Func<User, CancellationToken, Task> update, CancellationToken cancellationToken) =>
        data.ForUserAsync(userId, async ct =>
        {
            var user = await data.FirstAsync(data.Query<User>(true).Where(user => user.Id == userId), ct);
            if (user is null || user.IsBlocked || password.Length is 0 or > 128 || !passwords.Verify(user, password))
                return Result<Unit>.Fail(ErrorKind.Forbidden, "password_required", "Подтвердите действие текущим паролем.");

            await update(user, ct);
            await data.SaveAsync(ct);
            return Result<Unit>.Ok(Unit.Value);
        }, cancellationToken);
}
