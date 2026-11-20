using Okul.Modules.Enrollment.Domain;

namespace Okul.Modules.Enrollment.Application;

/// <summary>
/// Sends the veli login code (spec 0007, Requirement 5): Netgsm in production; in Development and Testing nothing leaves
/// the machine. Behind this interface only (docs/architecture.md, Forbidden dependencies 6). False when it could not
/// send within its retries (at most 10 seconds in total, G-4). Implementations never log the code or the full telefon.
/// </summary>
public interface ISmsSender
{
    Task<bool> SendLoginCodeAsync(PhoneNumber phone, string code, CancellationToken cancellationToken);
}

/// <summary>Keyed hashes for the login state (spec 0007, Requirement 11): the database alone gives neither telefon nor code.</summary>
public interface IGuardianLoginHasher
{
    /// <summary>
    /// The key of one kurum kodu + telefon pair. The kurum part is the tenant when the code exists (every spelling of a
    /// code shares one key), else the normalized code text — an unknown kurum behaves like a known one (K3).
    /// </summary>
    string PairKey(Guid? tenantId, string? institutionCode, PhoneNumber phone);

    string CodeHash(string pairKey, string code);
}
