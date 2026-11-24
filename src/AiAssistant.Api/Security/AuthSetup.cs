using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Threading.RateLimiting;
using AiAssistant.Application.Auth;
using AiAssistant.Application.Persistence;
using AiAssistant.Domain;
using AiAssistant.Infrastructure.Security;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.IdentityModel.Tokens;

namespace AiAssistant.Api.Security;

internal static class AuthSetup
{
    public static IServiceCollection AddAssistantAuth(this IServiceCollection services, IConfiguration configuration,
        IHostEnvironment environment)
    {
        var key = configuration["Jwt:SigningKey"];
        if (string.IsNullOrEmpty(key) && environment.IsDevelopment())
            key = Convert.ToHexString(RandomNumberGenerator.GetBytes(48));
        if (key is null || Encoding.UTF8.GetByteCount(key) < 32)
            throw new InvalidOperationException("Задайте Jwt:SigningKey длиной от 32 байт.");
        var settings = new JwtSettings(configuration["Jwt:Issuer"] ?? "AiHelperBudz",
            configuration["Jwt:Audience"] ?? "AiHelperBudz.Web", key);

        services.AddSingleton(settings);
        services.AddSingleton<ISessionCredentials, JwtCredentials>();
        services.AddScoped<IPasswordHasher<User>, PasswordHasher<User>>();
        services.AddScoped<IPasswordService, IdentityPasswordService>();
        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(options =>
        {
            options.MapInboundClaims = false;
            options.TokenValidationParameters = new()
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = settings.SecurityKey,
                ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
                ValidateIssuer = true,
                ValidIssuer = settings.Issuer,
                ValidateAudience = true,
                ValidAudience = settings.Audience,
                RequireExpirationTime = true,
                ValidateLifetime = true,
                ClockSkew = TimeSpan.FromSeconds(15),
                NameClaimType = JwtRegisteredClaimNames.Sub,
                RoleClaimType = JwtSettings.RoleClaim
            };
            options.Events = new JwtBearerEvents
            {
                OnTokenValidated = ValidateAccountAsync,
                OnMessageReceived = context =>
                {
                    if (context.Request.Path == "/hubs/chat" &&
                        (context.HttpContext.WebSockets.IsWebSocketRequest ||
                         context.Request.Headers.Accept.ToString().Contains("text/event-stream", StringComparison.OrdinalIgnoreCase)) &&
                        context.Request.Query.TryGetValue("access_token", out var token))
                    {
                        context.Token = token;
                    }
                    return Task.CompletedTask;
                }
            };
        });
        services.AddAuthorization();
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.AddPolicy("account", context => RateLimitPartition.GetFixedWindowLimiter(
                context.Connection.RemoteIpAddress?.ToString() ?? "unknown", _ => new()
                {
                    Window = TimeSpan.FromMinutes(1), PermitLimit = 12, QueueLimit = 0
                }));
            options.AddPolicy("chat", context => RateLimitPartition.GetFixedWindowLimiter(
                context.User.FindFirstValue(JwtRegisteredClaimNames.Sub) ??
                context.Connection.RemoteIpAddress?.ToString() ?? "unknown", _ => new()
                {
                    Window = TimeSpan.FromMinutes(1), PermitLimit = 25, QueueLimit = 0
                }));
        });
        return services;
    }

    private static async Task ValidateAccountAsync(TokenValidatedContext context)
    {
        if (!Guid.TryParse(context.Principal?.FindFirstValue(JwtRegisteredClaimNames.Sub), out var userId) ||
            !int.TryParse(context.Principal?.FindFirstValue(JwtSettings.SessionVersionClaim), out var version))
        {
            context.Fail("Invalid session claims.");
            return;
        }

        var data = context.HttpContext.RequestServices.GetRequiredService<IAssistantData>();
        var role = context.Principal?.FindFirstValue(JwtSettings.RoleClaim);
        var expectedRole = role == nameof(Role.Admin) ? Role.Admin : Role.User;
        var active = await data.AnyAsync(data.Query<User>().Where(user => user.Id == userId &&
            !user.IsBlocked && user.SessionVersion == version && user.Role == expectedRole),
            context.HttpContext.RequestAborted);
        if (!active) context.Fail("Account session is no longer valid.");
    }
}
