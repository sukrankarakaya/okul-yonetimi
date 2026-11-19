using NArchitecture.Core.Mediator;
using Okul.Modules.Identity.Domain;

namespace Okul.Modules.Identity.Application;

public enum ChangePasswordStatus
{
    Changed,
    CurrentPasswordWrong,
    TooShort,
    Mismatch,
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
    private readonly TimeProvider _time;

    public ChangePasswordCommandHandler(
        IStaffAccountStore accounts, ISessionStore sessions, IPasswordHasher hasher, TimeProvider time)
    {
        _accounts = accounts;
        _sessions = sessions;
        _hasher = hasher;
        _time = time;
    }

    public async Task<ChangePasswordStatus> Handle(ChangePasswordCommand request, CancellationToken cancellationToken)
    {
        var account = await _accounts.FindByIdAsync(request.AccountId, cancellationToken)
            ?? throw new InvalidOperationException("The signed-in account was not found in its own tenant.");

        if (!_hasher.Verify(account.PasswordHash, request.CurrentPassword ?? string.Empty))
        {
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
        await _sessions.CloseOthersAsync(account.Id, request.SessionId, _time.GetUtcNow(), cancellationToken);
        return ChangePasswordStatus.Changed;
    }
}
