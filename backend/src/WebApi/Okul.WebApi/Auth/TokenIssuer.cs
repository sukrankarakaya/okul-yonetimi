using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Okul.WebApi.Auth;

/// <summary>Signing settings. The key never lives in the repo (docs/security.md, Secrets).</summary>
public sealed class TokenSettings
{
    public const string Issuer = "okul-api";
    public const string Audience = "okul-clients";
    public static readonly TimeSpan AccessTokenLifetime = TimeSpan.FromMinutes(15);
    public static readonly TimeSpan ClockSkew = TimeSpan.FromSeconds(30);

    public TokenSettings(SymmetricSecurityKey signingKey)
    {
        SigningKey = signingKey;
    }

    public SymmetricSecurityKey SigningKey { get; }

    /// <summary>
    /// <c>Auth:SigningKey</c> (base64, ≥ 32 bytes) from configuration (user-secrets / secret manager).
    /// Missing: Development and Testing get an ephemeral random key; any other environment refuses to start.
    /// </summary>
    public static TokenSettings FromConfiguration(IConfiguration configuration, IHostEnvironment environment)
    {
        var configured = configuration["Auth:SigningKey"];
        if (!string.IsNullOrWhiteSpace(configured))
        {
            var bytes = Convert.FromBase64String(configured);
            if (bytes.Length < 32)
            {
                throw new InvalidOperationException("Auth:SigningKey must be at least 32 bytes.");
            }

            return new TokenSettings(new SymmetricSecurityKey(bytes));
        }

        if (DevAuth.DevAuthEndpoints.IsAllowedIn(environment))
        {
            return new TokenSettings(new SymmetricSecurityKey(RandomNumberGenerator.GetBytes(32)));
        }

        throw new InvalidOperationException("Auth:SigningKey is not configured.");
    }

    public TokenValidationParameters ValidationParameters() => new()
    {
        ValidIssuer = Issuer,
        ValidAudience = Audience,
        IssuerSigningKey = SigningKey,
        ValidateIssuerSigningKey = true,
        ValidateLifetime = true,
        RequireExpirationTime = true,
        ClockSkew = ClockSkew,
        NameClaimType = OkulClaims.Subject,
        RoleClaimType = OkulClaims.Role,
    };
}

public sealed class TokenIssuer
{
    private readonly TokenSettings _settings;
    private readonly TimeProvider _timeProvider;

    public TokenIssuer(TokenSettings settings, TimeProvider timeProvider)
    {
        _settings = settings;
        _timeProvider = timeProvider;
    }

    public (string Token, DateTimeOffset ExpiresAt) Issue(Guid userId, Guid tenantId, string role)
    {
        var now = _timeProvider.GetUtcNow();
        var expiresAt = now.Add(TokenSettings.AccessTokenLifetime);
        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = TokenSettings.Issuer,
            Audience = TokenSettings.Audience,
            IssuedAt = now.UtcDateTime,
            NotBefore = now.UtcDateTime,
            Expires = expiresAt.UtcDateTime,
            Subject = new ClaimsIdentity(
            [
                new Claim(OkulClaims.Subject, userId.ToString()),
                new Claim(OkulClaims.TenantId, tenantId.ToString()),
                new Claim(OkulClaims.Role, role),
            ]),
            SigningCredentials = new SigningCredentials(_settings.SigningKey, SecurityAlgorithms.HmacSha256),
        };

        return (new JsonWebTokenHandler().CreateToken(descriptor), expiresAt);
    }
}
