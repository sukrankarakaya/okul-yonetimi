using System.Security.Cryptography;
using System.Text;
using NArchitecture.Core.Mediator;
using Okul.Core.Tenancy;
using Okul.Modules.Identity.Domain;

namespace Okul.Modules.Identity.Application;

public enum TwoFactorStepStatus
{
    Succeeded,

    /// <summary>"Kod hatalı veya süresi geçmiş." — a wrong, reused or out-of-window code, or a used recovery code.</summary>
    InvalidCode,

    /// <summary>The login lock (spec 0003, Requirement 4) — wrong codes count towards it (spec 0004, Requirement 6).</summary>
    Locked,

    /// <summary>No usable step: expired (5 minutes), ended ("Vazgeç", success) or never issued.</summary>
    StepEnded,
}

/// <summary>Completes a login step (spec 0004, Requirement 4): a 6-digit code, or a kurtarma kodu instead (Requirement 3).</summary>
public sealed record VerifyLoginCommand(string? Step, string? Code, string? RecoveryCode) : IRequest<TwoFactorStepResult>;

/// <summary>Setup, part 1 (Requirement 2): a fresh secret for the QR code; a repeated call replaces it.</summary>
public sealed record StartSetupCommand(string? Step) : IRequest<SetupStartResult>;

/// <summary>Setup, part 2: the first code confirms it; the session opens and the kurtarma kodları are shown once.</summary>
public sealed record ConfirmSetupCommand(string? Step, string? Code) : IRequest<TwoFactorStepResult>;

/// <summary>"Vazgeç" (spec 0004, self-critique G3): the step can never be used again.</summary>
public sealed record CancelLoginCommand(string? Step) : IRequest;

public sealed record TwoFactorStepResult(
    TwoFactorStepStatus Status, IssuedSession? Session = null, IReadOnlyList<string>? RecoveryCodes = null);

/// <summary>The key as the user types it (base32) and the otpauth URI the QR code carries.</summary>
public sealed record SetupStartResult(bool Started, string? Key = null, string? ProvisioningUri = null);

/// <summary>What every step handler needs: locating the step, the lock, the secret, opening the session.</summary>
public sealed class TwoFactorLoginServices
{
    public const string Issuer = "Okul Yönetim";

    public TwoFactorLoginServices(
        ITenantScope tenantScope, ILoginChallengeStore challenges, IStaffAccountStore accounts, ISessionStore sessions,
        ILoginThrottleStore throttles, IRecoveryCodeStore recoveryCodes, ITwoFactorSecretProtector protector,
        IRecoveryCodeHasher recoveryHasher, TimeProvider time)
    {
        TenantScope = tenantScope;
        Challenges = challenges;
        Accounts = accounts;
        Sessions = sessions;
        Throttles = throttles;
        RecoveryCodes = recoveryCodes;
        Protector = protector;
        RecoveryHasher = recoveryHasher;
        Time = time;
    }

    public ITenantScope TenantScope { get; }

    public ILoginChallengeStore Challenges { get; }

    public IStaffAccountStore Accounts { get; }

    public ISessionStore Sessions { get; }

    public ILoginThrottleStore Throttles { get; }

    public IRecoveryCodeStore RecoveryCodes { get; }

    public ITwoFactorSecretProtector Protector { get; }

    public IRecoveryCodeHasher RecoveryHasher { get; }

    public TimeProvider Time { get; }

    /// <summary>
    /// The step and its account, when the credential matches a usable step of <paramref name="purpose"/>. The tenant id in
    /// the credential only locates the row (under a scope); nothing is trusted until the secret's hash matches.
    /// </summary>
    public async Task<(LoginChallenge Challenge, StaffAccount Account)?> FindAsync(
        string? step, LoginChallengePurpose purpose, DateTimeOffset now, CancellationToken cancellationToken)
    {
        if (!RefreshCredential.TryParse(step, out var credential))
        {
            return null;
        }

        using (TenantScope.Begin(credential.Value.TenantId))
        {
            var challenge = await Challenges.FindAsync(credential.Value.SessionId, cancellationToken);
            if (challenge is null
                || !CryptographicOperations.FixedTimeEquals(
                    Encoding.ASCII.GetBytes(challenge.CredentialHash), Encoding.ASCII.GetBytes(credential.Value.Hash))
                || challenge.Purpose != purpose
                || !challenge.IsUsable(now))
            {
                return null;
            }

            var account = await Accounts.FindByIdAsync(challenge.AccountId, cancellationToken);
            return account is null ? null : (challenge, account);
        }
    }

    /// <summary>The same kurum + e-posta key as the password step: one count for wrong passwords and wrong codes.</summary>
    public static string ThrottleKey(StaffAccount account) =>
        LoginCommandHandler.ThrottleKey(account.TenantId, null, account.Email);

    public async Task<bool> IsLockedAsync(StaffAccount account, DateTimeOffset now, CancellationToken cancellationToken) =>
        (await Throttles.FindAsync(ThrottleKey(account), cancellationToken))?.IsLocked(now) == true;

    /// <summary>A wrong code: counted; the answer is "locked" when this one locked it.</summary>
    public async Task<TwoFactorStepResult> FailAsync(StaffAccount account, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var locked = await Throttles.UpdateAsync(ThrottleKey(account), throttle => throttle.RegisterFailure(now), cancellationToken);
        return new TwoFactorStepResult(locked ? TwoFactorStepStatus.Locked : TwoFactorStepStatus.InvalidCode);
    }

    /// <summary>A complete login resets the count — unless a parallel failure locked it meanwhile.</summary>
    public Task<bool> CompleteAsync(StaffAccount account, DateTimeOffset now, CancellationToken cancellationToken) =>
        Throttles.UpdateAsync(ThrottleKey(account), throttle =>
        {
            if (throttle.IsLocked(now))
            {
                return false;
            }

            throttle.Reset();
            return true;
        }, cancellationToken);

    /// <summary>
    /// A valid code of the account's secret for a step later than the last accepted one, written with a concurrency check:
    /// two parallel requests with one code — only one wins (plan 0004, R6). Run inside the account's tenant scope.
    /// </summary>
    public async Task<bool> AcceptCodeAsync(StaffAccount account, string? code, DateTimeOffset now, CancellationToken cancellationToken)
    {
        if (account.TwoFactorSecret is not { } secret
            || Totp.MatchingStep(Protector.Unprotect(secret), code, now) is not { } matched
            || !account.TryAcceptStep(matched))
        {
            return false;
        }

        return await Accounts.TrySaveChangesAsync(cancellationToken);
    }

    public static IReadOnlyList<string> NewRecoveryCodes(out IReadOnlyList<string> hashes, IRecoveryCodeHasher hasher)
    {
        var codes = RecoveryCodeFormat.NewSet();
        hashes = codes.Select(code => hasher.Hash(RecoveryCodeFormat.Normalize(code)!)).ToList();
        return codes;
    }
}

public sealed class VerifyLoginCommandHandler : IRequestHandler<VerifyLoginCommand, TwoFactorStepResult>
{
    private readonly TwoFactorLoginServices _s;

    public VerifyLoginCommandHandler(TwoFactorLoginServices services)
    {
        _s = services;
    }

    public async Task<TwoFactorStepResult> Handle(VerifyLoginCommand request, CancellationToken cancellationToken)
    {
        var now = _s.Time.GetUtcNow();
        var found = await _s.FindAsync(request.Step, LoginChallengePurpose.Verify, now, cancellationToken);
        if (found is not { } step)
        {
            return new TwoFactorStepResult(TwoFactorStepStatus.StepEnded);
        }

        var (challenge, account) = step;
        if (await _s.IsLockedAsync(account, now, cancellationToken))
        {
            return new TwoFactorStepResult(TwoFactorStepStatus.Locked);
        }

        using (_s.TenantScope.Begin(account.TenantId))
        {
            var accepted = request.RecoveryCode is not null
                ? await UseRecoveryCodeAsync(account, request.RecoveryCode, now, cancellationToken)
                : await _s.AcceptCodeAsync(account, request.Code, now, cancellationToken);
            if (!accepted)
            {
                return await _s.FailAsync(account, now, cancellationToken);
            }

            if (!await _s.CompleteAsync(account, now, cancellationToken))
            {
                return new TwoFactorStepResult(TwoFactorStepStatus.Locked);
            }

            // One step, one session: a parallel request on the same step that also got here loses (review round 1 M-2).
            if (!await _s.Challenges.TryEndAsync(challenge.Id, now, cancellationToken))
            {
                return new TwoFactorStepResult(TwoFactorStepStatus.StepEnded);
            }

            var session = await SessionOpening.OpenAsync(_s.Sessions, _s.TenantScope, account, challenge.Client, now, cancellationToken);
            return new TwoFactorStepResult(TwoFactorStepStatus.Succeeded, session);
        }
    }

    private async Task<bool> UseRecoveryCodeAsync(
        StaffAccount account, string recoveryCode, DateTimeOffset now, CancellationToken cancellationToken)
    {
        if (RecoveryCodeFormat.Normalize(recoveryCode) is not { } normalized)
        {
            return false;
        }

        var hash = Encoding.ASCII.GetBytes(_s.RecoveryHasher.Hash(normalized));
        var codes = await _s.RecoveryCodes.ListUnusedAsync(account.Id, cancellationToken);
        var match = codes.FirstOrDefault(c => CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(c.CodeHash), hash));
        // A conditional UPDATE, not the tracked entity: of two parallel requests with one code only one uses it (M-2).
        return match is not null && await _s.RecoveryCodes.TryUseAsync(match.Id, now, cancellationToken);
    }
}

public sealed class StartSetupCommandHandler : IRequestHandler<StartSetupCommand, SetupStartResult>
{
    private readonly TwoFactorLoginServices _s;

    public StartSetupCommandHandler(TwoFactorLoginServices services)
    {
        _s = services;
    }

    public async Task<SetupStartResult> Handle(StartSetupCommand request, CancellationToken cancellationToken)
    {
        var now = _s.Time.GetUtcNow();
        if (await _s.FindAsync(request.Step, LoginChallengePurpose.Setup, now, cancellationToken) is not { } step)
        {
            return new SetupStartResult(false);
        }

        var (challenge, account) = step;
        var secret = Totp.NewSecret();
        using (_s.TenantScope.Begin(account.TenantId))
        {
            challenge.SetPendingSecret(_s.Protector.Protect(secret));
            await _s.Accounts.SaveChangesAsync(cancellationToken);
        }

        return new SetupStartResult(true, Totp.ToBase32(secret), Totp.ProvisioningUri(TwoFactorLoginServices.Issuer, account.Email, secret));
    }
}

public sealed class ConfirmSetupCommandHandler : IRequestHandler<ConfirmSetupCommand, TwoFactorStepResult>
{
    private readonly TwoFactorLoginServices _s;

    public ConfirmSetupCommandHandler(TwoFactorLoginServices services)
    {
        _s = services;
    }

    public async Task<TwoFactorStepResult> Handle(ConfirmSetupCommand request, CancellationToken cancellationToken)
    {
        var now = _s.Time.GetUtcNow();
        var found = await _s.FindAsync(request.Step, LoginChallengePurpose.Setup, now, cancellationToken);
        if (found is not { } step || step.Challenge.PendingProtectedSecret is not { } pending)
        {
            return new TwoFactorStepResult(TwoFactorStepStatus.StepEnded);
        }

        var (challenge, account) = step;
        if (account.TwoFactorEnabled)
        {
            return new TwoFactorStepResult(TwoFactorStepStatus.StepEnded); // nothing to set up (review round 1 M-1)
        }

        if (await _s.IsLockedAsync(account, now, cancellationToken))
        {
            return new TwoFactorStepResult(TwoFactorStepStatus.Locked);
        }

        // Wrong setup codes count towards the lock too (spec 0004, self-critique G1).
        if (Totp.MatchingStep(_s.Protector.Unprotect(pending), request.Code, now) is not { } matched)
        {
            return await _s.FailAsync(account, now, cancellationToken);
        }

        if (!await _s.CompleteAsync(account, now, cancellationToken))
        {
            return new TwoFactorStepResult(TwoFactorStepStatus.Locked);
        }

        var codes = TwoFactorLoginServices.NewRecoveryCodes(out var hashes, _s.RecoveryHasher);
        using (_s.TenantScope.Begin(account.TenantId))
        {
            // Claim the step first: a double confirm gets "step ended", never a second set of codes (review round 1 L-1).
            if (!await _s.Challenges.TryEndAsync(challenge.Id, now, cancellationToken))
            {
                return new TwoFactorStepResult(TwoFactorStepStatus.StepEnded);
            }

            account.EnableTwoFactor(pending, matched);
            await _s.RecoveryCodes.ReplaceAsync(account.TenantId, account.Id, hashes, cancellationToken);
            if (!await _s.Accounts.TrySaveChangesAsync(cancellationToken))
            {
                return new TwoFactorStepResult(TwoFactorStepStatus.StepEnded);
            }
        }

        var session = await SessionOpening.OpenAsync(_s.Sessions, _s.TenantScope, account, challenge.Client, now, cancellationToken);
        return new TwoFactorStepResult(TwoFactorStepStatus.Succeeded, session, codes);
    }
}

public sealed class CancelLoginCommandHandler : IRequestHandler<CancelLoginCommand>
{
    private readonly TwoFactorLoginServices _s;

    public CancelLoginCommandHandler(TwoFactorLoginServices services)
    {
        _s = services;
    }

    public async Task Handle(CancelLoginCommand request, CancellationToken cancellationToken)
    {
        var now = _s.Time.GetUtcNow();
        var found = await _s.FindAsync(request.Step, LoginChallengePurpose.Verify, now, cancellationToken)
            ?? await _s.FindAsync(request.Step, LoginChallengePurpose.Setup, now, cancellationToken);
        if (found is { } step)
        {
            using (_s.TenantScope.Begin(step.Account.TenantId))
            {
                step.Challenge.End(now);
                await _s.Accounts.SaveChangesAsync(cancellationToken);
            }
        }
    }
}
