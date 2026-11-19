namespace Okul.Modules.Institution.Contracts;

/// <summary>
/// Kurum kodu → kurum kimliği, BEFORE any session exists (spec 0003, login). The only cross-tenant lookup of the
/// module (plan 0003, R2): it returns the identifier and nothing else, and an unknown or malformed code gives null.
/// </summary>
public interface IInstitutionDirectory
{
    Task<Guid?> FindIdByCodeAsync(string code, CancellationToken cancellationToken = default);
}
