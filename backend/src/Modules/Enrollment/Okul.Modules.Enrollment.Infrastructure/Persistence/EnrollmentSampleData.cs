using Microsoft.EntityFrameworkCore;
using Okul.Modules.Enrollment.Domain;

namespace Okul.Modules.Enrollment.Infrastructure.Persistence;

/// <summary>
/// Sample veliler of the two sample kurums (spec 0007, Requirement 9; K5): the kurum's veli telefon list until the
/// Kayıt management screens exist. Development and Testing only — never in production. No secret is involved.
/// </summary>
public static class EnrollmentSampleData
{
    public static readonly Guid WithCampusesTenantId = Guid.Parse("019a8f00-0000-7000-8000-00000000000a");
    public static readonly Guid WithoutCampusesTenantId = Guid.Parse("019a8f00-0000-7000-8000-00000000000b");

    public static readonly SampleGuardian Ayse = new(Id(1), WithCampusesTenantId, "+905321234567", "Ayşe Yılmaz");

    /// <summary>No name recorded: shown as "Veli".</summary>
    public static readonly SampleGuardian Unnamed = new(Id(2), WithCampusesTenantId, "+905329876543", null);

    /// <summary>Same telefon as <see cref="Ayse"/> in the other kurum: two kurums, two veliler (Requirement 8).</summary>
    public static readonly SampleGuardian AyseOtherKurum = new(Id(3), WithoutCampusesTenantId, "+905321234567", "Ayşe Yılmaz");

    public static readonly IReadOnlyList<SampleGuardian> All = [Ayse, Unnamed, AyseOtherKurum];

    public static async Task SeedAsync(EnrollmentDbContext db, TimeProvider time, CancellationToken cancellationToken = default)
    {
        if (await db.Guardians.IgnoreQueryFilters().AnyAsync(cancellationToken)) // system job: seeding spans tenants (T-2)
        {
            return;
        }

        var now = time.GetUtcNow();
        foreach (var sample in All)
        {
            db.Guardians.Add(Guardian.Register(sample.Id, sample.TenantId, PhoneNumber.FromE164(sample.Phone), sample.Name, now));
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    private static Guid Id(int n) => Guid.Parse($"019a8f00-0000-7000-8000-0000000003{n:D2}");

    public sealed record SampleGuardian(Guid Id, Guid TenantId, string Phone, string? Name);
}
