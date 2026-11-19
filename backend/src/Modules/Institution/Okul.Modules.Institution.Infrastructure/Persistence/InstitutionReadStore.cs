using Microsoft.EntityFrameworkCore;
using Okul.Modules.Institution.Application;
using Okul.Modules.Institution.Contracts;

namespace Okul.Modules.Institution.Infrastructure.Persistence;

/// <summary>Projection queries (T-7); tenant filtering comes from <see cref="InstitutionDbContext"/>.</summary>
internal sealed class InstitutionReadStore : IInstitutionReadStore
{
    private readonly InstitutionDbContext _db;

    public InstitutionReadStore(InstitutionDbContext db)
    {
        _db = db;
    }

    public Task<InstitutionDto?> GetCurrentInstitutionAsync(CancellationToken cancellationToken) =>
        _db.Institutions
            .AsNoTracking()
            .Select(i => new InstitutionDto(
                i.Id,
                i.Name,
                i.Campuses.OrderBy(c => c.Name).Select(c => new CampusDto(c.Id, c.Name)).ToList()))
            .FirstOrDefaultAsync(cancellationToken);

    public Task<CampusDto?> GetCampusAsync(Guid campusId, CancellationToken cancellationToken) =>
        _db.Campuses
            .AsNoTracking()
            .Where(c => c.Id == campusId)
            .Select(c => new CampusDto(c.Id, c.Name))
            .FirstOrDefaultAsync(cancellationToken);
}
