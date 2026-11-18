using AiAssistant.Application.Auth;
using AiAssistant.Domain;
using Microsoft.AspNetCore.Identity;

namespace AiAssistant.Infrastructure.Security;

public sealed class IdentityPasswordService(IPasswordHasher<User> hasher) : IPasswordService
{
    public string Hash(User user, string password) => hasher.HashPassword(user, password);

    public bool Verify(User user, string password) =>
        hasher.VerifyHashedPassword(user, user.PasswordHash, password) != PasswordVerificationResult.Failed;
}
