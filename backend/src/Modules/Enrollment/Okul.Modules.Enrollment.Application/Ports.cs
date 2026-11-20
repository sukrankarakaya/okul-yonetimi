using Okul.Modules.Enrollment.Domain;

namespace Okul.Modules.Enrollment.Application;

/// <summary>Registered veliler of the CURRENT tenant (tenant filter — from the session or an <c>ITenantScope</c>).</summary>
public interface IGuardianStore
{
    Task<Guardian?> FindByPhoneAsync(PhoneNumber phone, CancellationToken cancellationToken);

    Task<Guardian?> FindByIdAsync(Guid guardianId, CancellationToken cancellationToken);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}

/// <summary>KVKK rıza records of the CURRENT tenant; only added, never changed (spec 0007, Requirement 6).</summary>
public interface IKvkkConsentStore
{
    Task<KvkkConsent?> FindLatestAsync(Guid guardianId, CancellationToken cancellationToken);

    Task AddAsync(KvkkConsent consent, CancellationToken cancellationToken);
}

/// <summary>
/// Login codes and failure state by kurum kodu + telefon pair key — not tenant-scoped by design (plan 0007, A-1): an
/// unknown kurum kodu or an unregistered telefon behaves exactly like a registered one (K3, G-3).
/// </summary>
public interface IGuardianLoginStore
{
    /// <summary>A cheap, unlocked read — only to refuse early while locked.</summary>
    Task<GuardianLoginThrottle?> FindThrottleAsync(string pairKey, CancellationToken cancellationToken);

    /// <summary>
    /// Read-modify-write of one pair, serialized per key (as Kimlik's login throttle, spec 0003 review round 1 #1): the
    /// throttle (created when missing) and the pair's latest code; a code issued through the pair is added. Persisted.
    /// </summary>
    Task<T> UpdateAsync<T>(string pairKey, Func<GuardianLoginPair, T> change, CancellationToken cancellationToken);
}

/// <summary>One kurum kodu + telefon pair inside <see cref="IGuardianLoginStore.UpdateAsync{T}"/>.</summary>
public sealed class GuardianLoginPair
{
    public GuardianLoginPair(GuardianLoginThrottle throttle, GuardianLoginCode? latestCode)
    {
        Throttle = throttle;
        LatestCode = latestCode;
    }

    public GuardianLoginThrottle Throttle { get; }

    public GuardianLoginCode? LatestCode { get; private set; }

    /// <summary>The code issued in this change, if any — the store adds it.</summary>
    public GuardianLoginCode? IssuedCode { get; private set; }

    /// <summary>A new code ends the previous one (Requirement 3).</summary>
    public void Issue(GuardianLoginCode code, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(code);
        LatestCode?.End(now);
        LatestCode = code;
        IssuedCode = code;
    }
}
