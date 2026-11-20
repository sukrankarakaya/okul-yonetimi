using Microsoft.EntityFrameworkCore;
using Okul.Core.Persistence;
using Okul.Core.Tenancy;
using Okul.Modules.Enrollment.Domain;

namespace Okul.Modules.Enrollment.Infrastructure.Persistence;

/// <summary>
/// Kayıt module storage, schema <c>kayit</c> (T-8; ADR 0002). Veliler and rıza records carry <c>TenantId</c>, so the
/// tenant filter applies (BR-1); before a session exists the tenant comes from an <c>ITenantScope</c>.
/// </summary>
public sealed class EnrollmentDbContext : TenantDbContext
{
    public const string Schema = "kayit";

    public EnrollmentDbContext(DbContextOptions<EnrollmentDbContext> options, ITenantContext tenantContext)
        : base(options, tenantContext)
    {
    }

    public DbSet<Guardian> Guardians => Set<Guardian>();

    public DbSet<KvkkConsent> KvkkConsents => Set<KvkkConsent>();

    protected override void ConfigureModel(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);

        modelBuilder.Entity<Guardian>(guardian =>
        {
            guardian.ToTable("guardians");
            guardian.HasKey(g => g.Id);
            guardian.Property(g => g.Id).HasColumnName("id").ValueGeneratedNever();
            guardian.Property(g => g.TenantId).HasColumnName("tenant_id");
            guardian.Property(g => g.Phone).HasColumnName("phone").IsRequired().HasMaxLength(16);
            guardian.Property(g => g.Name).HasColumnName("name").HasMaxLength(200);
            guardian.Property(g => g.RegisteredAt).HasColumnName("registered_at");
            guardian.Property(g => g.AccountCreatedAt).HasColumnName("account_created_at");
            guardian.Ignore(g => g.HasAccount);
            guardian.Ignore(g => g.DisplayName);

            // One telefon per kurum; the same telefon in another kurum is another veli (spec 0007, Requirement 8).
            guardian.HasIndex(g => new { g.TenantId, g.Phone }).IsUnique().HasDatabaseName("ux_guardians_tenant_phone");
        });

        modelBuilder.Entity<KvkkConsent>(consent =>
        {
            consent.ToTable("kvkk_consents");
            consent.HasKey(c => c.Id);
            consent.Property(c => c.Id).HasColumnName("id").ValueGeneratedNever();
            consent.Property(c => c.TenantId).HasColumnName("tenant_id");
            consent.Property(c => c.GuardianId).HasColumnName("guardian_id");
            consent.Property(c => c.TextVersion).HasColumnName("text_version").IsRequired().HasMaxLength(64);
            consent.Property(c => c.AcceptedAt).HasColumnName("accepted_at");
            consent.HasIndex(c => new { c.TenantId, c.GuardianId, c.AcceptedAt }).HasDatabaseName("ix_kvkk_consents_guardian");
        });
    }
}
