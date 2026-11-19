using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Okul.Core.Persistence;
using Okul.Core.Tenancy;
using Okul.Modules.School.Domain;

namespace Okul.Modules.School.Infrastructure.Persistence;

/// <summary>Okul (demo) modülü: şema <c>okul</c>. Tüm varlıklar TenantId taşır → kurum filtresi otomatik (BR-1).</summary>
public sealed class SchoolDbContext : TenantDbContext
{
    public const string Schema = "okul";
    public const string TurkishCollation = "tr-TR-x-icu";

    public SchoolDbContext(DbContextOptions<SchoolDbContext> options, ITenantContext tenantContext)
        : base(options, tenantContext)
    {
    }

    public DbSet<ClassGroup> ClassGroups => Set<ClassGroup>();
    public DbSet<Student> Students => Set<Student>();
    public DbSet<AttendanceRecord> Attendance => Set<AttendanceRecord>();
    public DbSet<Announcement> Announcements => Set<Announcement>();
    public DbSet<Installment> Installments => Set<Installment>();
    public DbSet<GuardianMessage> Messages => Set<GuardianMessage>();

    /// <summary>Npgsql timestamptz yalnızca UTC yazar: Türkiye saatli değerler yazarken UTC'ye çevrilir.</summary>
    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder) =>
        configurationBuilder.Properties<DateTimeOffset>().HaveConversion<UtcConverter>();

    private sealed class UtcConverter() : ValueConverter<DateTimeOffset, DateTimeOffset>(v => v.ToUniversalTime(), v => v);

    protected override void ConfigureModel(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);

        modelBuilder.Entity<ClassGroup>(e =>
        {
            e.ToTable("class_groups");
            e.Property(x => x.Id).ValueGeneratedNever();
            e.Property(x => x.Name).HasMaxLength(20).UseCollation(TurkishCollation);
            e.Property(x => x.TeacherName).HasMaxLength(120);
            e.HasIndex(x => x.TenantId);
        });

        modelBuilder.Entity<Student>(e =>
        {
            e.ToTable("students");
            e.Property(x => x.Id).ValueGeneratedNever();
            e.Property(x => x.FirstName).HasMaxLength(80).UseCollation(TurkishCollation);
            e.Property(x => x.LastName).HasMaxLength(80).UseCollation(TurkishCollation);
            e.Property(x => x.GuardianName).HasMaxLength(120);
            e.Property(x => x.GuardianRelation).HasMaxLength(40);
            e.Property(x => x.GuardianPhone).HasMaxLength(20);
            e.HasIndex(x => new { x.TenantId, x.ClassGroupId });
        });

        modelBuilder.Entity<AttendanceRecord>(e =>
        {
            e.ToTable("attendance_records");
            e.Property(x => x.Id).ValueGeneratedNever();
            e.Property(x => x.Status).HasMaxLength(10);
            e.HasIndex(x => new { x.TenantId, x.StudentId, x.Date }).IsUnique();
            e.HasIndex(x => new { x.TenantId, x.Date });
        });

        modelBuilder.Entity<Announcement>(e =>
        {
            e.ToTable("announcements");
            e.Property(x => x.Id).ValueGeneratedNever();
            e.Property(x => x.Title).HasMaxLength(160);
            e.Property(x => x.Body).HasMaxLength(4000);
            e.HasIndex(x => new { x.TenantId, x.CreatedAt });
        });

        modelBuilder.Entity<Installment>(e =>
        {
            e.ToTable("installments");
            e.Property(x => x.Id).ValueGeneratedNever();
            e.Property(x => x.ReceiptNo).HasMaxLength(30);
            e.Ignore(x => x.RemainingKurus);
            e.HasIndex(x => new { x.TenantId, x.StudentId });
            e.HasIndex(x => new { x.TenantId, x.DueDate });
        });

        modelBuilder.Entity<GuardianMessage>(e =>
        {
            e.ToTable("guardian_messages");
            e.Property(x => x.Id).ValueGeneratedNever();
            e.Property(x => x.Body).HasMaxLength(4000);
            e.Property(x => x.SentBy).HasMaxLength(200);
            e.HasIndex(x => new { x.TenantId, x.SentAt });
        });
    }
}
