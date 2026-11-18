using AiAssistant.Domain;

namespace AiAssistant.Application.Auth;

public sealed record SessionGrant(string AccessToken, DateTimeOffset AccessExpiresAt, string RefreshToken,
    DateTimeOffset RefreshExpiresAt, string RefreshHash);

/// <summary>Hashes and verifies account passwords.</summary>
public interface IPasswordService
{
    /// <summary>Creates a versioned password hash for an account.</summary>
    string Hash(User user, string password);

    /// <summary>Checks the password supplied by the caller without exposing its hash.</summary>
    bool Verify(User user, string password);
}

/// <summary>Issues short-lived access tokens and provides refresh token hashing.</summary>
public interface ISessionCredentials
{
    /// <summary>Creates a fresh pair of access and refresh credentials.</summary>
    SessionGrant Issue(User user);

    /// <summary>Returns the hash used to look up a refresh credential.</summary>
    string HashRefresh(string token);
}
