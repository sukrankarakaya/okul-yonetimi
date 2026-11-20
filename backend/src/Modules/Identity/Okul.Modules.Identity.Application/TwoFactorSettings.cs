using NArchitecture.Core.Mediator;
using Okul.Modules.Identity.Domain;

namespace Okul.Modules.Identity.Application;

/// <summary>Güvenlik ekranı (spec 0004): on/off, mandatory or not, may it be turned off, kurtarma kodları left (Requirement 8).</summary>
public sealed record TwoFactorStatus(bool Enabled, bool Required, bool CanDisable, int RemainingRecoveryCodes);

public sealed record GetTwoFactorStatusQuery(Guid AccountId) : IRequest<TwoFactorStatus>;

/// <summary>A signed-in Öğretmen turns 2FA on (Requirement 7): the same setup as at login, without a new session.</summary>
public sealed record StartEnableTwoFactorCommand(Guid AccountId) : IRequest<EnableStartResult>;

public sealed record EnableStartResult(bool Started, string? Step = null, string? Key = null, string? ProvisioningUri = null);

public sealed record ConfirmEnableTwoFactorCommand(Guid AccountId, string? Step, string? Code) : IRequest<TwoFactorChangeResult>;

/// <summary>Öğretmen only, with a valid code (Requirement 7).</summary>
public sealed record DisableTwoFactorCommand(Guid AccountId, string? Code) : IRequest<TwoFactorChangeResult>;

/// <summary>A valid code; the old codes stop at once (Requirement 8).</summary>
public sealed record RenewRecoveryCodesCommand(Guid AccountId, string? Code) : IRequest<TwoFactorChangeResult>;

public enum TwoFactorChangeStatus
{
    Succeeded,
    InvalidCode,
    Locked,

    /// <summary>Mandatory for the account's roles — it cannot be turned off; or nothing to turn off / already on.</summary>
    NotAllowed,

    /// <summary>The setup step expired or was never started.</summary>
    StepEnded,
}

public sealed record TwoFactorChangeResult(TwoFactorChangeStatus Status, IReadOnlyList<string>? RecoveryCodes = null);

/// <summary>Account and claims come from the validated session: the tenant filter applies (BR-1).</summary>
public sealed class TwoFactorSettingsHandlers :
    IRequestHandler<GetTwoFactorStatusQuery, TwoFactorStatus>,
    IRequestHandler<StartEnableTwoFactorCommand, EnableStartResult>,
    IRequestHandler<ConfirmEnableTwoFactorCommand, TwoFactorChangeResult>,
    IRequestHandler<DisableTwoFactorCommand, TwoFactorChangeResult>,
    IRequestHandler<RenewRecoveryCodesCommand, TwoFactorChangeResult>
{
    private readonly TwoFactorLoginServices _s;

    public TwoFactorSettingsHandlers(TwoFactorLoginServices services)
    {
        _s = services;
    }

    public async Task<TwoFactorStatus> Handle(GetTwoFactorStatusQuery request, CancellationToken cancellationToken)
    {
        var account = await AccountAsync(request.AccountId, cancellationToken);
        var remaining = account.TwoFactorEnabled ? await _s.RecoveryCodes.CountUnusedAsync(account.Id, cancellationToken) : 0;
        return new TwoFactorStatus(account.TwoFactorEnabled, account.TwoFactorRequired, account.CanDisableTwoFactor, remaining);
    }

    public async Task<EnableStartResult> Handle(StartEnableTwoFactorCommand request, CancellationToken cancellationToken)
    {
        var account = await AccountAsync(request.AccountId, cancellationToken);
        if (account.TwoFactorEnabled)
        {
            return new EnableStartResult(false);
        }

        var now = _s.Time.GetUtcNow();
        var challengeId = Guid.CreateVersion7();
        var credential = RefreshCredential.New(account.TenantId, challengeId);
        var challenge = LoginChallenge.Start(
            challengeId, account.TenantId, account.Id, LoginChallengePurpose.Enable, ClientKind.Web, now, credential.Hash);
        var secret = Totp.NewSecret();
        challenge.SetPendingSecret(_s.Protector.Protect(secret));
        await _s.Challenges.AddAsync(challenge, cancellationToken);
        await _s.Accounts.SaveChangesAsync(cancellationToken);
        return new EnableStartResult(
            true, credential.ToString(), Totp.ToBase32(secret), Totp.ProvisioningUri(TwoFactorLoginServices.Issuer, account.Email, secret));
    }

    public async Task<TwoFactorChangeResult> Handle(ConfirmEnableTwoFactorCommand request, CancellationToken cancellationToken)
    {
        var now = _s.Time.GetUtcNow();
        var found = await _s.FindAsync(request.Step, LoginChallengePurpose.Enable, now, cancellationToken);
        if (found is not { } step || step.Account.Id != request.AccountId || step.Challenge.PendingProtectedSecret is not { } pending
            || step.Account.TwoFactorEnabled)
        {
            return new TwoFactorChangeResult(TwoFactorChangeStatus.StepEnded);
        }

        var account = await AccountAsync(request.AccountId, cancellationToken);
        if (await _s.IsLockedAsync(account, now, cancellationToken))
        {
            return new TwoFactorChangeResult(TwoFactorChangeStatus.Locked);
        }

        if (Totp.MatchingStep(_s.Protector.Unprotect(pending), request.Code, now) is not { } matched)
        {
            return await FailAsync(account, now, cancellationToken);
        }

        if (!await _s.Challenges.TryEndAsync(step.Challenge.Id, now, cancellationToken))
        {
            return new TwoFactorChangeResult(TwoFactorChangeStatus.StepEnded);
        }

        var codes = TwoFactorLoginServices.NewRecoveryCodes(out var hashes, _s.RecoveryHasher);
        account.EnableTwoFactor(pending, matched);
        await _s.RecoveryCodes.ReplaceAsync(account.TenantId, account.Id, hashes, cancellationToken);
        return await _s.Accounts.TrySaveChangesAsync(cancellationToken)
            ? new TwoFactorChangeResult(TwoFactorChangeStatus.Succeeded, codes)
            : new TwoFactorChangeResult(TwoFactorChangeStatus.StepEnded);
    }

    public async Task<TwoFactorChangeResult> Handle(DisableTwoFactorCommand request, CancellationToken cancellationToken)
    {
        var account = await AccountAsync(request.AccountId, cancellationToken);
        if (!account.CanDisableTwoFactor)
        {
            return new TwoFactorChangeResult(TwoFactorChangeStatus.NotAllowed);
        }

        return await WithCodeAsync(account, request.Code, cancellationToken, async () =>
        {
            account.ClearTwoFactor();
            await _s.RecoveryCodes.DeleteAllAsync(account.Id, cancellationToken);
            await _s.Accounts.SaveChangesAsync(cancellationToken);
            return new TwoFactorChangeResult(TwoFactorChangeStatus.Succeeded);
        });
    }

    public async Task<TwoFactorChangeResult> Handle(RenewRecoveryCodesCommand request, CancellationToken cancellationToken)
    {
        var account = await AccountAsync(request.AccountId, cancellationToken);
        if (!account.TwoFactorEnabled)
        {
            return new TwoFactorChangeResult(TwoFactorChangeStatus.NotAllowed);
        }

        return await WithCodeAsync(account, request.Code, cancellationToken, async () =>
        {
            var codes = TwoFactorLoginServices.NewRecoveryCodes(out var hashes, _s.RecoveryHasher);
            await _s.RecoveryCodes.ReplaceAsync(account.TenantId, account.Id, hashes, cancellationToken);
            return await _s.Accounts.TrySaveChangesAsync(cancellationToken) // one transaction (review round 1 L-1)
                ? new TwoFactorChangeResult(TwoFactorChangeStatus.Succeeded, codes)
                : new TwoFactorChangeResult(TwoFactorChangeStatus.InvalidCode);
        });
    }

    /// <summary>A valid code first — wrong codes count towards the login lock here too (Requirement 6).</summary>
    private async Task<TwoFactorChangeResult> WithCodeAsync(
        StaffAccount account, string? code, CancellationToken cancellationToken, Func<Task<TwoFactorChangeResult>> change)
    {
        var now = _s.Time.GetUtcNow();
        if (await _s.IsLockedAsync(account, now, cancellationToken))
        {
            return new TwoFactorChangeResult(TwoFactorChangeStatus.Locked);
        }

        return await _s.AcceptCodeAsync(account, code, now, cancellationToken)
            ? await change()
            : await FailAsync(account, now, cancellationToken);
    }

    private async Task<TwoFactorChangeResult> FailAsync(StaffAccount account, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var result = await _s.FailAsync(account, now, cancellationToken);
        return new TwoFactorChangeResult(
            result.Status == TwoFactorStepStatus.Locked ? TwoFactorChangeStatus.Locked : TwoFactorChangeStatus.InvalidCode);
    }

    private async Task<StaffAccount> AccountAsync(Guid accountId, CancellationToken cancellationToken) =>
        await _s.Accounts.FindByIdAsync(accountId, cancellationToken)
            ?? throw new InvalidOperationException("The signed-in account was not found in its own tenant.");
}

/// <summary>
/// The operations reset (spec 0004, Requirement 9): 2FA off, kurtarma kodları gone, every open session and login step
/// closed — a mandatory account sets it up again at its next login. Run inside the account's tenant scope.
/// </summary>
public sealed record ResetAccountTwoFactorCommand(Guid AccountId) : IRequest<int>;

public sealed class ResetAccountTwoFactorCommandHandler : IRequestHandler<ResetAccountTwoFactorCommand, int>
{
    private readonly TwoFactorLoginServices _s;

    public ResetAccountTwoFactorCommandHandler(TwoFactorLoginServices services)
    {
        _s = services;
    }

    public async Task<int> Handle(ResetAccountTwoFactorCommand request, CancellationToken cancellationToken)
    {
        var now = _s.Time.GetUtcNow();
        var account = await _s.Accounts.FindByIdAsync(request.AccountId, cancellationToken)
            ?? throw new InvalidOperationException("No such account in this tenant.");
        account.ClearTwoFactor();
        await _s.RecoveryCodes.DeleteAllAsync(account.Id, cancellationToken);
        await _s.Challenges.EndAllAsync(account.Id, now, cancellationToken);
        await _s.Accounts.SaveChangesAsync(cancellationToken);
        return await _s.Sessions.CloseAllAsync(account.Id, now, cancellationToken);
    }
}
