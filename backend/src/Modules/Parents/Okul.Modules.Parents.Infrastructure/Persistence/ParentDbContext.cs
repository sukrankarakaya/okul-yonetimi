using Microsoft.EntityFrameworkCore;
using Okul.Core.Persistence;
using Okul.Core.Tenancy;
using Okul.Modules.Parents.Domain;

namespace Okul.Modules.Parents.Infrastructure.Persistence;

public sealed class ParentDbContext : TenantDbContext
{
    public DbSet<Parent> Parents => Set<Parent>();
    public DbSet<SmsCode> SmsCodes => Set<SmsCode>();
    public DbSet<KvkkConsent> KvkkConsents => Set<KvkkConsent>();

    public ParentDbContext(DbContextOptions<ParentDbContext> options, ITenantContext tenantContext)
        : base(options, tenantContext) { }

    protected override void ConfigureModel(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("parent");

        modelBuilder.Entity<Parent>(b =>
        {
            b.ToTable("accounts");
            b.HasKey(x => x.Id);
            b.Property(x => x.Id).ValueGeneratedNever();
            b.Property(x => x.TenantId).IsRequired();
            b.Property(x => x.PhoneNumber).IsRequired().HasMaxLength(20);
            b.Property(x => x.Name).HasMaxLength(255);
            b.Property(x => x.FailureCount).IsRequired();
            b.Property(x => x.LastFailureAt);
            b.Property(x => x.CreatedAt).IsRequired();
            b.HasIndex(x => new { x.TenantId, x.PhoneNumber }).IsUnique();
            b.HasIndex(x => x.TenantId);
        });

        modelBuilder.Entity<SmsCode>(b =>
        {
            b.ToTable("sms_codes");
            b.HasKey(x => x.Id);
            b.Property(x => x.Id).ValueGeneratedNever();
            b.Property(x => x.ParentId).IsRequired();
            b.Property(x => x.TenantId).IsRequired();
            b.Property(x => x.Code).IsRequired().HasMaxLength(6);
            b.Property(x => x.ExpiresAt).IsRequired();
            b.Property(x => x.AttemptCount).IsRequired();
            b.HasIndex(x => new { x.TenantId, x.ParentId });
            b.HasIndex(x => x.ExpiresAt);
        });

        modelBuilder.Entity<KvkkConsent>(b =>
        {
            b.ToTable("kvkk_consents");
            b.HasKey(x => x.Id);
            b.Property(x => x.Id).ValueGeneratedNever();
            b.Property(x => x.ParentId).IsRequired();
            b.Property(x => x.TenantId).IsRequired();
            b.Property(x => x.Accepted).IsRequired();
            b.Property(x => x.RecordedAt).IsRequired();
            b.HasIndex(x => new { x.TenantId, x.ParentId });
        });
    }
}
