using Okul.Modules.Institution.Contracts;

namespace Okul.Modules.Institution.Application;

/// <summary>
/// Read access for the current tenant. Implementations must be tenant-filtered: they never return
/// another kurum's data (BR-1).
/// </summary>
public interface IInstitutionReadStore
{
    Task<InstitutionDto?> GetCurrentInstitutionAsync(CancellationToken cancellationToken);

    Task<CampusDto?> GetCampusAsync(Guid campusId, CancellationToken cancellationToken);
}
