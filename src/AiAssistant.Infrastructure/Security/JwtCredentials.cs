using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using AiAssistant.Application.Auth;
using AiAssistant.Domain;
using Microsoft.IdentityModel.Tokens;

namespace AiAssistant.Infrastructure.Security;

public sealed record JwtSettings(string Issuer, string Audience, string SigningKey)
{
    public const string SessionVersionClaim = "sv";
    public const string RoleClaim = "role";
    public const int AccessMinutes = 15;
    public const int RefreshDays = 14;
    public SymmetricSecurityKey SecurityKey => new(Encoding.UTF8.GetBytes(SigningKey));
}

public sealed class JwtCredentials(JwtSettings settings, TimeProvider clock) : ISessionCredentials
{
    public SessionGrant Issue(User user)
    {
        var now = clock.GetUtcNow();
        var accessExpiry = now.AddMinutes(JwtSettings.AccessMinutes);
        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new Claim(JwtSettings.SessionVersionClaim, user.SessionVersion.ToString(System.Globalization.CultureInfo.InvariantCulture)),
            new Claim(JwtSettings.RoleClaim, user.Role.ToString()),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.CreateVersion7().ToString())
        };

        var token = new JwtSecurityToken(settings.Issuer, settings.Audience, claims, now.UtcDateTime,
            accessExpiry.UtcDateTime, new SigningCredentials(settings.SecurityKey, SecurityAlgorithms.HmacSha256));
        var raw = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        return new(new JwtSecurityTokenHandler().WriteToken(token), accessExpiry, raw,
            now.AddDays(JwtSettings.RefreshDays), HashRefresh(raw));
    }

    public string HashRefresh(string token) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}
