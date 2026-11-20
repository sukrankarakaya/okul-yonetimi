using NArchitecture.Core.Mediator;
using Okul.Core.Tenancy;
using Okul.Modules.Enrollment.Domain;
using Okul.Modules.Identity.Contracts;
using Okul.Modules.Institution.Contracts;

namespace Okul.Modules.Enrollment.Application;

public enum ResetGuardianStatus
{
    Reset,

    /// <summary>No such kurum kodu, no such telefon in it, or not a telefon at all: a clear error for the operator.</summary>
    NotFound,
}

/// <param name="ClosedSessions">How many open sessions were closed — the only number printed (no telefon, no code).</param>
public sealed record ResetGuardianResult(ResetGuardianStatus Status, int ClosedSessions = 0);

/// <summary>
/// Server command for a veli who lost the telefon (spec 0007, Requirement 10; K8): closes the veli's sessions, lifts the
/// lock and ends a pending code. Server access is the authorization (docs/security.md, as reset-2fa).
/// </summary>
public sealed record ResetGuardianCommand(string? InstitutionCode, string? Phone) : IRequest<ResetGuardianResult>;

public sealed class ResetGuardianCommandHandler : IRequestHandler<ResetGuardianCommand, ResetGuardianResult>
{
    private readonly IInstitutionDirectory _institutions;
    private readonly ITenantScope _tenantScope;
    private readonly IGuardianStore _guardians;
    private readonly IGuardianLoginStore _login;
    private readonly IGuardianLoginHasher _hasher;
    private readonly IGuardianSessions _sessions;
    private readonly TimeProvider _time;

    public ResetGuardianCommandHandler(
        IInstitutionDirectory institutions, ITenantScope tenantScope, IGuardianStore guardians, IGuardianLoginStore login,
        IGuardianLoginHasher hasher, IGuardianSessions sessions, TimeProvider time)
    {
        _institutions = institutions;
        _tenantScope = tenantScope;
        _guardians = guardians;
        _login = login;
        _hasher = hasher;
        _sessions = sessions;
        _time = time;
    }

    public async Task<ResetGuardianResult> Handle(ResetGuardianCommand request, CancellationToken cancellationToken)
    {
        var phone = PhoneNumber.Parse(request.Phone);
        var tenantId = await _institutions.FindIdByCodeAsync(request.InstitutionCode ?? string.Empty, cancellationToken);
        if (phone is null || tenantId is not { } tenant)
        {
            return new ResetGuardianResult(ResetGuardianStatus.NotFound);
        }

        Guardian? guardian;
        using (_tenantScope.Begin(tenant))
        {
            guardian = await _guardians.FindByPhoneAsync(phone, cancellationToken);
        }

        if (guardian is null)
        {
            return new ResetGuardianResult(ResetGuardianStatus.NotFound);
        }

        var closed = await _sessions.CloseAllAsync(tenant, guardian.Id, cancellationToken);
        var now = _time.GetUtcNow();
        await _login.UpdateAsync(_hasher.PairKey(tenant, request.InstitutionCode, phone), pair =>
        {
            pair.Throttle.ResetFailures();
            pair.LatestCode?.End(now);
            return true;
        }, cancellationToken);

        return new ResetGuardianResult(ResetGuardianStatus.Reset, closed);
    }
}
