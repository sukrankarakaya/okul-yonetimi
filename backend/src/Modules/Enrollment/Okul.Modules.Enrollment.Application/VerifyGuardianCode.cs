using NArchitecture.Core.Mediator;
using Okul.Core.Tenancy;
using Okul.Modules.Enrollment.Contracts;
using Okul.Modules.Enrollment.Domain;
using Okul.Modules.Identity.Contracts;
using Okul.Modules.Institution.Contracts;

namespace Okul.Modules.Enrollment.Application;

public enum VerifyCodeStatus
{
    Succeeded,

    /// <summary>"Kod hatalı veya süresi geçmiş." — wrong, used, replaced, cancelled, or no code at all (AC-2).</summary>
    Wrong,

    /// <summary>"Kodun süresi doldu. Yeni kod isteyin."</summary>
    Expired,

    /// <summary>"Çok fazla başarısız deneme. 15 dakika sonra tekrar deneyin." — for unregistered telefonlar too (G-3).</summary>
    Locked,
}

/// <param name="ConsentRequired">The veli has not accepted the current aydınlatma metni: KVKK first (Requirement 6).</param>
public sealed record VerifyCodeResult(
    VerifyCodeStatus Status, GuardianSessionStarted? Session = null, bool ConsentRequired = false);

/// <summary>Kodu doğrula (spec 0007, Requirements 3, 4, 7, 9): a right code opens a veli session through Kimlik.</summary>
public sealed record VerifyGuardianCodeCommand(string? InstitutionCode, string? Phone, string? Code, SessionClient Client)
    : IRequest<VerifyCodeResult>;

public sealed class VerifyGuardianCodeCommandHandler : IRequestHandler<VerifyGuardianCodeCommand, VerifyCodeResult>
{
    private readonly IInstitutionDirectory _institutions;
    private readonly ITenantScope _tenantScope;
    private readonly IGuardianStore _guardians;
    private readonly IGuardianLoginStore _login;
    private readonly IGuardianLoginHasher _hasher;
    private readonly IGuardianSessions _sessions;
    private readonly IGuardianDirectory _directory;
    private readonly TimeProvider _time;

    public VerifyGuardianCodeCommandHandler(
        IInstitutionDirectory institutions, ITenantScope tenantScope, IGuardianStore guardians, IGuardianLoginStore login,
        IGuardianLoginHasher hasher, IGuardianSessions sessions, IGuardianDirectory directory, TimeProvider time)
    {
        _institutions = institutions;
        _tenantScope = tenantScope;
        _guardians = guardians;
        _login = login;
        _hasher = hasher;
        _sessions = sessions;
        _directory = directory;
        _time = time;
    }

    public async Task<VerifyCodeResult> Handle(VerifyGuardianCodeCommand request, CancellationToken cancellationToken)
    {
        var phone = PhoneNumber.Parse(request.Phone);
        if (phone is null)
        {
            return new VerifyCodeResult(VerifyCodeStatus.Wrong);
        }

        var now = _time.GetUtcNow();
        var tenantId = await _institutions.FindIdByCodeAsync(request.InstitutionCode ?? string.Empty, cancellationToken);
        var pairKey = _hasher.PairKey(tenantId, request.InstitutionCode, phone);
        if ((await _login.FindThrottleAsync(pairKey, cancellationToken))?.IsLocked(now) == true)
        {
            return new VerifyCodeResult(VerifyCodeStatus.Locked);
        }

        var code = GuardianLoginCodeFormat.Normalize(request.Code);
        var presented = code is null ? string.Empty : _hasher.CodeHash(pairKey, code);
        var (status, guardianId) = await _login.UpdateAsync(pairKey, pair =>
        {
            if (pair.Throttle.IsLocked(now))
            {
                return (VerifyCodeStatus.Locked, (Guid?)null);
            }

            var check = pair.LatestCode?.Check(presented, now) ?? CodeCheck.Wrong;
            switch (check)
            {
                case CodeCheck.Accepted:
                    pair.Throttle.ResetFailures();
                    return (VerifyCodeStatus.Succeeded, pair.LatestCode!.GuardianId);
                case CodeCheck.Expired:
                    return (VerifyCodeStatus.Expired, null);
                default:
                    return (pair.Throttle.RegisterFailure(now) ? VerifyCodeStatus.Locked : VerifyCodeStatus.Wrong, null);
            }
        }, cancellationToken);

        if (status != VerifyCodeStatus.Succeeded)
        {
            return new VerifyCodeResult(status);
        }

        // Only a registered veli's code is ever accepted, so the tenant and the veli exist here.
        var tenant = tenantId!.Value;
        using (_tenantScope.Begin(tenant))
        {
            var guardian = await _guardians.FindByIdAsync(guardianId!.Value, cancellationToken)
                ?? throw new InvalidOperationException("Kabul edilen kodun velisi bulunamadı.");
            guardian.RecordLogin(now);
            await _guardians.SaveChangesAsync(cancellationToken);
        }

        var session = await _sessions.StartAsync(tenant, guardianId.Value, request.Client, cancellationToken);
        var consented = await _directory.HasCurrentConsentAsync(tenant, guardianId.Value, cancellationToken);
        return new VerifyCodeResult(VerifyCodeStatus.Succeeded, session, ConsentRequired: !consented);
    }
}
