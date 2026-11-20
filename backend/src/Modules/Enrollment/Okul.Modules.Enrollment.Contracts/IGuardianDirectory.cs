namespace Okul.Modules.Enrollment.Contracts;

/// <summary>
/// What other modules may ask about a veli (ADR 0002): Kimlik, when it renews a veli session (spec 0007, Requirement 7);
/// the API, before it serves veli data (Requirement 6 — no data without the current KVKK rıza).
/// </summary>
public interface IGuardianDirectory
{
    /// <summary>Is this a registered veli of the kurum with a login account? False for anything unknown.</summary>
    Task<bool> IsActiveAsync(Guid tenantId, Guid guardianId, CancellationToken cancellationToken = default);

    /// <summary>Has the veli accepted the CURRENT aydınlatma metni version?</summary>
    Task<bool> HasCurrentConsentAsync(Guid tenantId, Guid guardianId, CancellationToken cancellationToken = default);
}
