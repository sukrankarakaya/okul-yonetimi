using NArchitecture.Core.Mediator;
using Okul.Modules.Identity.Domain;

namespace Okul.Modules.Identity.Application;

public enum ChangePasswordStatus
{
    Changed,
    CurrentPasswordWrong,
    TooShort,
    Mismatch,

    /// <summary>Spec 0004 (B-11): the login lock — wrong current passwords count towards it; while locked nothing changes.</summary>
    Locked,
}

/// <summary>
/// The signed-in user changes their own password (spec 0003, Requirement 7). Account and session come from the
/// validated access claims; the tenant filter applies. Success closes the account's OTHER sessions at once.
/// </summary>
public sealed record ChangePasswordCommand(
    Guid AccountId, Guid SessionId, string? CurrentPassword, string? NewPassword, string? NewPasswordAgain)
    : IRequest<ChangePasswordStatus>;

public sealed class ChangePasswordCommandHandler : IRequestHandler<ChangePasswordCommand, ChangePasswordStatus>
{
    private readonly IStaffAccountStore _accounts;
    private readonly ISessionStore _sessions;
    private readonly IPasswordHasher _hasher;
    private readonly ILoginThrottleStore _throttles;
    private readonly TimeProvider _time;

    public ChangePasswordCommandHandler(
        IStaffAccountStore accounts, ISessionStore sessions, IPasswordHasher hasher, ILoginThrottleStore throttles, TimeProvider time)
    {
        _throttles = throttles;
        _accounts = accounts;
        _sessions = sessions;
        _hasher = hasher;
        _time = time;
    }

    public async Task<ChangePasswordStatus> Handle(ChangePasswordCommand request, CancellationToken cancellationToken)
    {
        var account = await _accounts.FindByIdAsync(request.AccountId, cancellationToken)
            ?? throw new InvalidOperationException("The signed-in account was not found in its own tenant.");

        // B-11 (spec 0004, Requirement 6): the same kurum + e-posta count as the login — a stolen session cannot guess the
        // password unlimited; while locked even the right current password changes nothing.
        var now = _time.GetUtcNow();
        var key = TwoFactorLoginServices.ThrottleKey(account);
        if ((await _throttles.FindAsync(key, cancellationToken))?.IsLocked(now) == true)
        {
            return ChangePasswordStatus.Locked;
        }

        if (!_hasher.Verify(account.PasswordHash, request.CurrentPassword ?? string.Empty))
        {
            await _throttles.UpdateAsync(key, throttle => throttle.RegisterFailure(now), cancellationToken);
            return ChangePasswordStatus.CurrentPasswordWrong;
        }

        if (!PasswordPolicy.IsAcceptable(request.NewPassword))
        {
            return ChangePasswordStatus.TooShort;
        }

        if (!string.Equals(request.NewPassword, request.NewPasswordAgain, StringComparison.Ordinal))
        {
            return ChangePasswordStatus.Mismatch;
        }

        account.ChangePasswordHash(_hasher.Hash(request.NewPassword!));
        await _accounts.SaveChangesAsync(cancellationToken);
        await _sessions.CloseOthersAsync(account.Id, request.SessionId, now, cancellationToken);
        return ChangePasswordStatus.Changed;
    }
}
