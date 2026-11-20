using NArchitecture.Core.Mediator;
using Okul.Core.Tenancy;
using Okul.Modules.Enrollment.Domain;
using Okul.Modules.Institution.Contracts;

namespace Okul.Modules.Enrollment.Application;

public enum RequestCodeStatus
{
    /// <summary>
    /// "Numara kayıtlıysa SMS kodu gönderildi." — the SAME answer for a registered veli, an unregistered telefon and an
    /// unknown kurum kodu (spec 0007, Requirement 2; K3). Only a registered veli gets an SMS.
    /// </summary>
    Accepted,

    /// <summary>"Lütfen geçerli bir telefon numarası girin." — a format check, independent of any record.</summary>
    InvalidPhone,

    /// <summary>The 4th request of the pair in a minute, "tekrar gönder" included (Requirement 4; G-2).</summary>
    RateLimited,

    /// <summary>The SMS could not be sent within the retries (Requirement 5; G-4).</summary>
    SmsFailed,
}

/// <summary>
/// Every code request answers after at least <see cref="MinimumResponse"/> (plan 0007, decision A, 2026-11-20): the SMS
/// to a registered veli takes time, an unregistered telefon none — without this the delay would tell them apart (K3).
/// Only a provider slower than this, or down, still shows. Zero in tests.
/// </summary>
public sealed record GuardianLoginTiming(TimeSpan MinimumResponse)
{
    public static readonly GuardianLoginTiming Default = new(TimeSpan.FromMilliseconds(1500));
}

/// <summary>Kod iste (spec 0007, Requirements 1–5): kurum kodu + telefon.</summary>
public sealed record RequestGuardianCodeCommand(string? InstitutionCode, string? Phone) : IRequest<RequestCodeStatus>;

public sealed class RequestGuardianCodeCommandHandler : IRequestHandler<RequestGuardianCodeCommand, RequestCodeStatus>
{
    private readonly IInstitutionDirectory _institutions;
    private readonly ITenantScope _tenantScope;
    private readonly IGuardianStore _guardians;
    private readonly IGuardianLoginStore _login;
    private readonly IGuardianLoginHasher _hasher;
    private readonly ISmsSender _sms;
    private readonly GuardianLoginTiming _timing;
    private readonly TimeProvider _time;

    public RequestGuardianCodeCommandHandler(
        IInstitutionDirectory institutions, ITenantScope tenantScope, IGuardianStore guardians, IGuardianLoginStore login,
        IGuardianLoginHasher hasher, ISmsSender sms, GuardianLoginTiming timing, TimeProvider time)
    {
        _timing = timing;
        _institutions = institutions;
        _tenantScope = tenantScope;
        _guardians = guardians;
        _login = login;
        _hasher = hasher;
        _sms = sms;
        _time = time;
    }

    public async Task<RequestCodeStatus> Handle(RequestGuardianCodeCommand request, CancellationToken cancellationToken)
    {
        var started = _time.GetTimestamp();
        var status = await RequestAsync(request, cancellationToken);
        var remaining = _timing.MinimumResponse - _time.GetElapsedTime(started);
        if (remaining > TimeSpan.Zero)
        {
            await Task.Delay(remaining, _time, cancellationToken);
        }

        return status;
    }

    private async Task<RequestCodeStatus> RequestAsync(RequestGuardianCodeCommand request, CancellationToken cancellationToken)
    {
        var phone = PhoneNumber.Parse(request.Phone);
        if (phone is null)
        {
            return RequestCodeStatus.InvalidPhone;
        }

        var now = _time.GetUtcNow();
        var tenantId = await _institutions.FindIdByCodeAsync(request.InstitutionCode ?? string.Empty, cancellationToken);
        Guardian? guardian = null;
        if (tenantId is { } tenant)
        {
            using (_tenantScope.Begin(tenant))
            {
                guardian = await _guardians.FindByPhoneAsync(phone, cancellationToken);
            }
        }

        // A code for every pair — never sent when the pair is not a registered veli (K3): expiry, "Vazgeç" and wrong-code
        // answers are then the same for both.
        var pairKey = _hasher.PairKey(tenantId, request.InstitutionCode, phone);
        var code = GuardianLoginCodeFormat.New();
        var issued = await _login.UpdateAsync(pairKey, pair =>
        {
            if (!pair.Throttle.TryRegisterCodeRequest(now))
            {
                return false;
            }

            pair.Issue(GuardianLoginCode.Issue(Guid.CreateVersion7(), pairKey, guardian?.Id, _hasher.CodeHash(pairKey, code), now), now);
            return true;
        }, cancellationToken);

        if (!issued)
        {
            return RequestCodeStatus.RateLimited;
        }

        if (guardian is not null && !await _sms.SendLoginCodeAsync(phone, code, cancellationToken))
        {
            return RequestCodeStatus.SmsFailed;
        }

        return RequestCodeStatus.Accepted;
    }
}

/// <summary>"Vazgeç" (spec 0007, G-1): the pending code of the pair is cancelled. Always succeeds; tells nothing.</summary>
public sealed record CancelGuardianCodeCommand(string? InstitutionCode, string? Phone) : IRequest<bool>;

public sealed class CancelGuardianCodeCommandHandler : IRequestHandler<CancelGuardianCodeCommand, bool>
{
    private readonly IInstitutionDirectory _institutions;
    private readonly IGuardianLoginStore _login;
    private readonly IGuardianLoginHasher _hasher;
    private readonly TimeProvider _time;

    public CancelGuardianCodeCommandHandler(
        IInstitutionDirectory institutions, IGuardianLoginStore login, IGuardianLoginHasher hasher, TimeProvider time)
    {
        _institutions = institutions;
        _login = login;
        _hasher = hasher;
        _time = time;
    }

    public async Task<bool> Handle(CancelGuardianCodeCommand request, CancellationToken cancellationToken)
    {
        var phone = PhoneNumber.Parse(request.Phone);
        if (phone is null)
        {
            return true;
        }

        var now = _time.GetUtcNow();
        var tenantId = await _institutions.FindIdByCodeAsync(request.InstitutionCode ?? string.Empty, cancellationToken);
        var pairKey = _hasher.PairKey(tenantId, request.InstitutionCode, phone);
        await _login.UpdateAsync(pairKey, pair =>
        {
            pair.LatestCode?.End(now);
            return true;
        }, cancellationToken);
        return true;
    }
}
