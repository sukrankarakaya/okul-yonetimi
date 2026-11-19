using System.Security.Cryptography;
using System.Text;
using NArchitecture.Core.Mediator;
using Okul.Core.Tenancy;
using Okul.Modules.Identity.Domain;
using Okul.Modules.Institution.Contracts;

namespace Okul.Modules.Identity.Application;

public enum LoginStatus
{
    Succeeded,

    /// <summary>One answer for every wrong input — which part was wrong is never told (Requirement 3, AC-2).</summary>
    InvalidCredentials,

    /// <summary>5 failures within the window: 15 minutes, for known and unknown accounts alike (Requirement 4).</summary>
    Locked,
}

/// <summary>A session just opened or renewed: what the API needs to issue the access and refresh credentials.</summary>
public sealed record IssuedSession(
    Guid SessionId, Guid TenantId, Guid AccountId, IReadOnlyList<string> Roles, ClientKind Client,
    DateTimeOffset ExpiresAt, string RefreshToken);

public sealed record LoginResult(LoginStatus Status, IssuedSession? Session = null);

/// <summary>Staff login: kurum kodu + e-posta + şifre (spec 0003, Requirements 1–4).</summary>
public sealed record LoginCommand(string? InstitutionCode, string? Email, string? Password, ClientKind Client)
    : IRequest<LoginResult>;

public sealed class LoginCommandHandler : IRequestHandler<LoginCommand, LoginResult>
{
    private readonly IInstitutionDirectory _directory;
    private readonly ITenantScope _tenantScope;
    private readonly IStaffAccountStore _accounts;
    private readonly ISessionStore _sessions;
    private readonly ILoginThrottleStore _throttles;
    private readonly IPasswordHasher _hasher;
    private readonly TimeProvider _time;

    public LoginCommandHandler(
        IInstitutionDirectory directory, ITenantScope tenantScope, IStaffAccountStore accounts, ISessionStore sessions,
        ILoginThrottleStore throttles, IPasswordHasher hasher, TimeProvider time)
    {
        _directory = directory;
        _tenantScope = tenantScope;
        _accounts = accounts;
        _sessions = sessions;
        _throttles = throttles;
        _hasher = hasher;
        _time = time;
    }

    public async Task<LoginResult> Handle(LoginCommand request, CancellationToken cancellationToken)
    {
        var now = _time.GetUtcNow();
        var tenantId = await _directory.FindIdByCodeAsync(request.InstitutionCode ?? string.Empty, cancellationToken);
        var email = EmailAddress.Normalize(request.Email);

        var key = ThrottleKey(tenantId, request.InstitutionCode, email ?? request.Email);
        var throttle = await _throttles.FindAsync(key, cancellationToken) ?? LoginThrottle.Create(key);
        if (throttle.IsLocked(now))
        {
            return new LoginResult(LoginStatus.Locked);
        }

        StaffAccount? account = null;
        if (tenantId is { } tenant && email is not null)
        {
            using (_tenantScope.Begin(tenant))
            {
                account = await _accounts.FindByEmailAsync(email, cancellationToken);
            }
        }

        // Always one hash verification, also for an unknown account: timing must not tell which part was wrong.
        var passwordOk = _hasher.Verify(account?.PasswordHash, request.Password ?? string.Empty);
        if (account is null || !passwordOk || !account.CanLogInAsStaff)
        {
            throttle.RegisterFailure(now);
            await _throttles.SaveAsync(throttle, cancellationToken);
            return new LoginResult(LoginStatus.InvalidCredentials);
        }

        throttle.Reset();
        await _throttles.SaveAsync(throttle, cancellationToken);

        var sessionId = Guid.CreateVersion7();
        var credential = RefreshCredential.New(account.TenantId, sessionId);
        var session = Session.Start(sessionId, account.TenantId, account.Id, request.Client, now, credential.Hash);
        using (_tenantScope.Begin(account.TenantId))
        {
            await _sessions.AddAsync(session, cancellationToken);
            await _sessions.SaveChangesAsync(cancellationToken);
        }

        return new LoginResult(LoginStatus.Succeeded, new IssuedSession(
            session.Id, session.TenantId, account.Id, account.Roles, session.Client, session.ExpiresAt, credential.ToString()));
    }

    /// <summary>
    /// The kurum part is the resolved tenant when the code exists (every spelling of a code shares one key); an
    /// unknown code keys on its trimmed upper-case text. Hashed: an unknown e-mail is never stored in clear (R4).
    /// </summary>
    internal static string ThrottleKey(Guid? tenantId, string? institutionCode, string? email)
    {
        var kurum = tenantId?.ToString("N") ?? "kod:" + (institutionCode?.Trim().ToUpperInvariant() ?? string.Empty);
        var who = email?.Trim().ToLowerInvariant() ?? string.Empty;
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(kurum + "\n" + who)));
    }
}
