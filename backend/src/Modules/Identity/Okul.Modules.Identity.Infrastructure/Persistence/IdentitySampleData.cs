using Microsoft.EntityFrameworkCore;
using Okul.Modules.Identity.Domain;

namespace Okul.Modules.Identity.Infrastructure.Persistence;

/// <summary>
/// Sample staff accounts of the two sample kurums (spec 0001 seed; spec 0003). Development and Testing only — never in
/// production. The password is the caller's (user-secrets in Development, a random one per test run): it is never in
/// the repo (plan 0003, R8).
/// </summary>
public static class IdentitySampleData
{
    public static readonly Guid WithCampusesTenantId = Guid.Parse("019a8f00-0000-7000-8000-00000000000a");
    public static readonly Guid WithoutCampusesTenantId = Guid.Parse("019a8f00-0000-7000-8000-00000000000b");

    public static readonly SampleAccount Administrator = new(Id(1), WithCampusesTenantId, "yonetici@ataturk.test", [StaffRoles.Administrator]);
    public static readonly SampleAccount Teacher = new(Id(2), WithCampusesTenantId, "ogretmen@ataturk.test", [StaffRoles.Teacher]);
    public static readonly SampleAccount Accountant = new(Id(3), WithCampusesTenantId, "muhasebe@ataturk.test", [StaffRoles.Accountant]);
    public static readonly SampleAccount TeacherAndAccountant = new(Id(4), WithCampusesTenantId, "ogretmen-muhasebe@ataturk.test", [StaffRoles.Teacher, StaffRoles.Accountant]);
    public static readonly SampleAccount GuardianOnly = new(Id(5), WithCampusesTenantId, "veli@ataturk.test", [StaffRoles.Guardian]);

    /// <summary>Same e-mail as <see cref="Administrator"/> in the other kurum: two kurums, two accounts (spec 0003).</summary>
    public static readonly SampleAccount OtherKurumAdministrator = new(Id(6), WithoutCampusesTenantId, "yonetici@ataturk.test", [StaffRoles.Administrator]);

    public static readonly IReadOnlyList<SampleAccount> All =
        [Administrator, Teacher, Accountant, TeacherAndAccountant, GuardianOnly, OtherKurumAdministrator];

    public static async Task SeedAsync(IdentityDbContext db, string password, CancellationToken cancellationToken = default)
    {
        if (await db.StaffAccounts.IgnoreQueryFilters().AnyAsync(cancellationToken)) // system job: seeding spans tenants (T-2)
        {
            return;
        }

        var hasher = new StaffPasswordHasher();
        foreach (var sample in All)
        {
            db.StaffAccounts.Add(StaffAccount.Create(sample.Id, sample.TenantId, sample.Email, hasher.Hash(password), sample.Roles));
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    private static Guid Id(int n) => Guid.Parse($"019a8f00-0000-7000-8000-0000000002{n:D2}");

    public sealed record SampleAccount(Guid Id, Guid TenantId, string Email, IReadOnlyList<string> Roles);
}
