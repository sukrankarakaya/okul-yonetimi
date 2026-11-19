using Microsoft.EntityFrameworkCore;
using Okul.Modules.Institution.Contracts;
using Okul.Modules.Institution.Domain;

namespace Okul.Modules.Institution.Infrastructure.Persistence;

/// <summary>
/// The pre-session kurum kodu lookup (plan 0003, R2). It must bypass the tenant filter — no tenant is known yet —
/// so it is the module's single <c>IgnoreQueryFilters</c> read outside seeding (T-2), projected to the id only:
/// no name, no campus, nothing a caller could leak.
/// </summary>
internal sealed class InstitutionDirectory : IInstitutionDirectory
{
    private readonly InstitutionDbContext _db;

    public InstitutionDirectory(InstitutionDbContext db)
    {
        _db = db;
    }

    public async Task<Guid?> FindIdByCodeAsync(string code, CancellationToken cancellationToken = default)
    {
        var normalized = InstitutionCode.Normalize(code);
        if (normalized is null)
        {
            return null;
        }

        return await _db.Institutions
            .IgnoreQueryFilters()
            .Where(i => i.Code == normalized)
            .Select(i => (Guid?)i.Id)
            .SingleOrDefaultAsync(cancellationToken);
    }
}
