using Microsoft.EntityFrameworkCore;
using InstitutionEntity = Okul.Modules.Institution.Domain.Institution;

namespace Okul.Modules.Institution.Infrastructure.Persistence;

/// <summary>
/// The two sample kurums of spec 0001 (one with campuses, one without). Written only in the
/// Development and Testing environments — never in production.
/// Campus names are inserted out of order on purpose: only a Turkish collation sorts them as
/// Cevizli, Çankaya, Ilgaz, İkitelli, Sultanbeyli, Şişli, Üsküdar (AC-7).
/// </summary>
public static class InstitutionSampleData
{
    public static readonly Guid WithCampusesId = Guid.Parse("019a8f00-0000-7000-8000-00000000000a");
    public static readonly Guid WithoutCampusesId = Guid.Parse("019a8f00-0000-7000-8000-00000000000b");

    public const string WithCampusesName = "Atatürk Koleji";
    public const string WithoutCampusesName = "Bilge Etüt Merkezi";

    /// <summary>Kurum kodları of the sample kurums (spec 0003). The first holds an <c>I</c> on purpose (T-1).</summary>
    public const string WithCampusesCode = "ATATURK-IST";
    public const string WithoutCampusesCode = "BILGE";

    public static readonly string[] CampusNamesInsertOrder =
        ["Üsküdar", "Şişli", "Sultanbeyli", "İkitelli", "Ilgaz", "Çankaya", "Cevizli"];

    public static readonly string[] CampusNamesTurkishOrder =
        ["Cevizli", "Çankaya", "Ilgaz", "İkitelli", "Sultanbeyli", "Şişli", "Üsküdar"];

    public static async Task SeedAsync(InstitutionDbContext db, CancellationToken cancellationToken = default)
    {
        if (await db.Institutions.IgnoreQueryFilters().AnyAsync(cancellationToken)) // system job: seeding spans tenants (T-2)
        {
            return;
        }

        var withCampuses = InstitutionEntity.Create(WithCampusesId, WithCampusesName, WithCampusesCode);
        for (var i = 0; i < CampusNamesInsertOrder.Length; i++)
        {
            withCampuses.AddCampus(Guid.Parse($"019a8f00-0000-7000-8000-0000000001{i:D2}"), CampusNamesInsertOrder[i]);
        }

        db.Institutions.Add(withCampuses);
        db.Institutions.Add(InstitutionEntity.Create(WithoutCampusesId, WithoutCampusesName, WithoutCampusesCode));
        await db.SaveChangesAsync(cancellationToken);
    }
}
