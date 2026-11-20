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
        if ((await _throttles.FindAsync(key, cancellationToken))?.IsLocked(now) == true)
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
            await _throttles.UpdateAsync(key, throttle => throttle.RegisterFailure(now), cancellationToken);
            return new LoginResult(LoginStatus.InvalidCredentials);
        }

        // A lock that landed while the password was being checked (a parallel failure) still wins over this success.
        var allowed = await _throttles.UpdateAsync(key, throttle =>
        {
            if (throttle.IsLocked(now))
            {
                return false;
            }

            throttle.Reset();
            return true;
        }, cancellationToken);
        if (!allowed)
        {
            return new LoginResult(LoginStatus.Locked);
        }

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
    /// The kurum part is the resolved tenant when the code exists (every spelling of a code shares one key); an unknown
    /// code keys on its <c>InstitutionCodeFormat</c> normalization — the same one a known code goes through — and only a
    /// code that can never be valid falls back to its trimmed upper-case text (it never resolves, so it tells nothing).
    /// Hashed: an unknown e-mail is never stored in clear (R4).
    /// </summary>
    internal static string ThrottleKey(Guid? tenantId, string? institutionCode, string? email)
    {
        // One normalization for every code, known or not (review round 1 #3): otherwise "XYZİ" and "XYZI" get two keys for
        // an unknown code but one for a known code, and the difference tells whether a kurum kodu exists.
        var kurum = tenantId?.ToString("N")
            ?? "kod:" + (InstitutionCodeFormat.Normalize(institutionCode) ?? institutionCode?.Trim().ToUpperInvariant() ?? string.Empty);
        var who = email?.Trim().ToLowerInvariant() ?? string.Empty;
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(kurum + "\n" + who)));
    }
}
