using AiAssistant.Application.Common;
using MediatR;

namespace AiAssistant.Application.Auth;

public sealed record RegisterAccount(string Username, string Email, string Password, string PasswordConfirmation)
    : IRequest<Result<SessionGrant>>;
public sealed record LoginAccount(string Login, string Password) : IRequest<Result<SessionGrant>>;
public sealed record RotateSession(string? RefreshToken) : IRequest<Result<SessionGrant>>;
public sealed record EndSession(string? RefreshToken) : IRequest<Result<Unit>>;
public sealed record GetAccount(Guid UserId) : IRequest<Result<AccountView>>;
public sealed record ChangeEmail(Guid UserId, string Email, string Password) : IRequest<Result<Unit>>;
public sealed record ChangePassword(Guid UserId, string CurrentPassword, string Password, string PasswordConfirmation)
    : IRequest<Result<Unit>>;
public sealed record DeleteAccount(Guid UserId, string Password) : IRequest<Result<Unit>>;

public sealed record AccountView(Guid Id, string Username, string Email, string Role, bool OnboardingCompleted);
