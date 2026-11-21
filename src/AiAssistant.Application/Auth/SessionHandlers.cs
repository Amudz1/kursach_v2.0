using AiAssistant.Application.Common;
using AiAssistant.Application.Persistence;
using AiAssistant.Domain;
using MediatR;

namespace AiAssistant.Application.Auth;

public sealed class SessionHandlers(
    IAssistantData data, IPasswordService passwords, ISessionCredentials credentials, TimeProvider clock) :
    IRequestHandler<RegisterAccount, Result<SessionGrant>>,
    IRequestHandler<LoginAccount, Result<SessionGrant>>,
    IRequestHandler<RotateSession, Result<SessionGrant>>,
    IRequestHandler<EndSession, Result<Unit>>
{
    public async Task<Result<SessionGrant>> Handle(RegisterAccount request, CancellationToken cancellationToken)
    {
        request = request with { Username = request.Username.Trim(), Email = request.Email.Trim() };
        var validation = await AccountValidators.Registration.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid) return Result<SessionGrant>.Invalid(validation.ToDictionary());

        var now = clock.GetUtcNow();
        var user = new User
        {
            Username = request.Username, NormalizedUsername = request.Username.ToUpperInvariant(),
            Email = request.Email, NormalizedEmail = request.Email.ToUpperInvariant(),
            PasswordHash = "", CreatedAt = now
        };
        user.PasswordHash = passwords.Hash(user, request.Password);
        data.Add(user);
        data.Add(new PersonalizationProfile { UserId = user.Id, UpdatedAt = now });
        data.Add(new AppearanceSettings { UserId = user.Id, Preset = AppearanceSettings.RegistrationPreset, UpdatedAt = now });

        try
        {
            return await IssueAsync(user, Guid.CreateVersion7(), cancellationToken);
        }
        catch (DataConflictException)
        {
            return Result<SessionGrant>.Fail(ErrorKind.Conflict, "account_exists", "Такой логин или email уже используется.");
        }
    }

    public async Task<Result<SessionGrant>> Handle(LoginAccount request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Login) || request.Login.Length > 254 || request.Password.Length is 0 or > 128)
            return InvalidLogin();

        var login = request.Login.Trim().ToUpperInvariant();
        var userId = await data.FirstAsync(data.Query<User>()
            .Where(user => user.NormalizedUsername == login || user.NormalizedEmail == login)
            .Select(user => user.Id), cancellationToken);
        if (userId == Guid.Empty) return InvalidLogin();

        return await data.ForUserAsync(userId, async ct =>
        {
            var user = await data.FirstAsync(data.Query<User>().Where(user => user.Id == userId), ct);
            if (user is null || !passwords.Verify(user, request.Password)) return InvalidLogin();
            if (user.IsBlocked)
                return Result<SessionGrant>.Fail(ErrorKind.Forbidden, "account_blocked", "Доступ к аккаунту ограничен.");
            return await IssueAsync(user, Guid.CreateVersion7(), ct);
        }, cancellationToken);
    }

    public async Task<Result<SessionGrant>> Handle(RotateSession request, CancellationToken cancellationToken)
    {
        var current = await FindRefreshAsync(request.RefreshToken, cancellationToken);
        if (current is null) return InvalidRefresh();

        return await data.ForUserAsync(current.UserId, async ct =>
        {
            var user = await data.FirstAsync(data.Query<User>(true).Where(user => user.Id == current.UserId), ct);
            var token = await data.FirstAsync(data.Query<RefreshToken>(true).Where(token => token.Id == current.Id), ct);
            var now = clock.GetUtcNow();
            if (user is null || user.IsBlocked || token is null || token.ExpiresAt <= now) return InvalidRefresh();
            if (token.RevokedAt.HasValue)
            {
                await RevokeFamilyAsync(token, now, ct);
                user.SessionVersion++;
                await data.SaveAsync(ct);
                return InvalidRefresh();
            }

            token.RevokedAt = now;
            return await IssueAsync(user, token.FamilyId, ct);
        }, cancellationToken);
    }

    public async Task<Result<Unit>> Handle(EndSession request, CancellationToken cancellationToken)
    {
        var token = await FindRefreshAsync(request.RefreshToken, cancellationToken);
        if (token is null) return Result<Unit>.Ok(Unit.Value);
        return await data.ForUserAsync(token.UserId, async ct =>
        {
            await RevokeFamilyAsync(token, clock.GetUtcNow(), ct);
            await data.SaveAsync(ct);
            return Result<Unit>.Ok(Unit.Value);
        }, cancellationToken);
    }

    private async Task<Result<SessionGrant>> IssueAsync(User user, Guid familyId, CancellationToken cancellationToken)
    {
        var grant = credentials.Issue(user);
        data.Add(new RefreshToken
        {
            UserId = user.Id, FamilyId = familyId, TokenHash = grant.RefreshHash, ExpiresAt = grant.RefreshExpiresAt
        });
        await data.SaveAsync(cancellationToken);
        return Result<SessionGrant>.Ok(grant);
    }

    private async Task<RefreshToken?> FindRefreshAsync(string? raw, CancellationToken cancellationToken)
    {
        if (raw is null || raw.Length != 64) return null;
        var hash = credentials.HashRefresh(raw);
        return await data.FirstAsync(data.Query<RefreshToken>().Where(token => token.TokenHash == hash), cancellationToken);
    }

    private async Task RevokeFamilyAsync(RefreshToken token, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var active = await data.ListAsync(data.Query<RefreshToken>(true)
            .Where(item => item.UserId == token.UserId && item.FamilyId == token.FamilyId && item.RevokedAt == null),
            cancellationToken);
        foreach (var member in active) member.RevokedAt = now;
    }

    private static Result<SessionGrant> InvalidLogin() =>
        Result<SessionGrant>.Fail(ErrorKind.Unauthorized, "invalid_credentials", "Неверный логин или пароль.");
    private static Result<SessionGrant> InvalidRefresh() =>
        Result<SessionGrant>.Fail(ErrorKind.Unauthorized, "session_expired", "Сессия истекла. Войдите снова.");
}
