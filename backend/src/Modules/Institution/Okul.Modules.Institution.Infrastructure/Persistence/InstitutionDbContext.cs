using Microsoft.EntityFrameworkCore;
using Okul.Core.Persistence;
using Okul.Core.Tenancy;
using Okul.Modules.Institution.Domain;
using CampusEntity = Okul.Modules.Institution.Domain.Campus;
using InstitutionEntity = Okul.Modules.Institution.Domain.Institution;

namespace Okul.Modules.Institution.Infrastructure.Persistence;

/// <summary>Kurum module storage: schema <c>kurum</c> (T-8: one DbContext + schema per module).</summary>
public sealed class InstitutionDbContext : TenantDbContext
{
    public const string Schema = "kurum";

    /// <summary>PostgreSQL ICU Turkish collation: ç, ğ, ı, İ, ö, ş, ü sort correctly (T-1, AC-7).</summary>
    public const string TurkishCollation = "tr-TR-x-icu";

    public InstitutionDbContext(DbContextOptions<InstitutionDbContext> options, ITenantContext tenantContext)
        : base(options, tenantContext)
    {
    }

    public DbSet<InstitutionEntity> Institutions => Set<InstitutionEntity>();

    public DbSet<CampusEntity> Campuses => Set<CampusEntity>();

    protected override void ConfigureModel(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);

        modelBuilder.Entity<InstitutionEntity>(institution =>
        {
            institution.ToTable("institutions");
            institution.HasKey(i => i.Id);
            institution.Property(i => i.Id).HasColumnName("id").ValueGeneratedNever();
            institution.Property(i => i.Name).HasColumnName("name")
                .IsRequired().HasMaxLength(InstitutionEntity.NameMaxLength).UseCollation(TurkishCollation);
            institution.Property(i => i.Code).HasColumnName("code")
                .IsRequired().HasMaxLength(InstitutionCode.MaxLength);
            institution.HasIndex(i => i.Code).IsUnique().HasDatabaseName("ux_institutions_code");

            institution.HasMany(i => i.Campuses).WithOne().HasForeignKey(c => c.TenantId).OnDelete(DeleteBehavior.Restrict);
            institution.Navigation(i => i.Campuses).UsePropertyAccessMode(PropertyAccessMode.Field);

            // The kurum IS the tenant: its own row is visible only to its own users (BR-1).
            institution.HasQueryFilter(TenantFilterKey, i => i.Id == CurrentTenantId);
        });

        modelBuilder.Entity<CampusEntity>(campus =>
        {
            campus.ToTable("campuses");
            campus.HasKey(c => c.Id);
            campus.Property(c => c.Id).HasColumnName("id").ValueGeneratedNever();
            campus.Property(c => c.TenantId).HasColumnName("tenant_id");
            campus.Property(c => c.Name).HasColumnName("name")
                .IsRequired().HasMaxLength(CampusEntity.NameMaxLength).UseCollation(TurkishCollation);
            campus.HasIndex(c => c.TenantId).HasDatabaseName("ix_campuses_tenant_id");
        });
    }
}
