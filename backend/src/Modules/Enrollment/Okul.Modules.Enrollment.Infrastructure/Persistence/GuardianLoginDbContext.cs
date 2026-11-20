using Microsoft.EntityFrameworkCore;
using Okul.Modules.Enrollment.Domain;

namespace Okul.Modules.Enrollment.Infrastructure.Persistence;

/// <summary>
/// The Kayıt module's tenant-free context (plan 0007, A-1; architecture test allow-list by name), like Kimlik's
/// <c>LoginThrottleDbContext</c>: veli login codes and failure state exist before — and without — any tenant, so an
/// unknown kurum kodu or an unregistered telefon behaves exactly like a registered one (spec 0007, K3, G-3). It holds
/// only hashed pair keys, code hashes, counters and times — no tenant data, no telefon in clear.
/// </summary>
public sealed class GuardianLoginDbContext : DbContext
{
    public const string HistoryTable = "__EFMigrationsHistory_guardian_login";

    public GuardianLoginDbContext(DbContextOptions<GuardianLoginDbContext> options)
        : base(options)
    {
    }

    public DbSet<GuardianLoginCode> Codes => Set<GuardianLoginCode>();

    public DbSet<GuardianLoginThrottle> Throttles => Set<GuardianLoginThrottle>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(EnrollmentDbContext.Schema);

        modelBuilder.Entity<GuardianLoginCode>(code =>
        {
            code.ToTable("guardian_login_codes");
            code.HasKey(c => c.Id);
            code.Property(c => c.Id).HasColumnName("id").ValueGeneratedNever();
            code.Property(c => c.PairKey).HasColumnName("pair_key").IsRequired().HasMaxLength(64);
            code.Property(c => c.GuardianId).HasColumnName("guardian_id");
            code.Property(c => c.CodeHash).HasColumnName("code_hash").IsRequired().HasMaxLength(64);
            code.Property(c => c.IssuedAt).HasColumnName("issued_at");
            code.Property(c => c.ExpiresAt).HasColumnName("expires_at");
            code.Property(c => c.EndedAt).HasColumnName("ended_at");
            code.HasIndex(c => new { c.PairKey, c.IssuedAt }).HasDatabaseName("ix_guardian_login_codes_pair");
        });

        modelBuilder.Entity<GuardianLoginThrottle>(throttle =>
        {
            throttle.ToTable("guardian_login_throttles");
            throttle.HasKey(t => t.Key);
            throttle.Property(t => t.Key).HasColumnName("key").HasMaxLength(64);
            throttle.Property(t => t.FailureCount).HasColumnName("failure_count");
            throttle.Property(t => t.LastFailureAt).HasColumnName("last_failure_at");
            throttle.Property(t => t.LockedUntil).HasColumnName("locked_until");
            throttle.Property(t => t.RequestWindowStartedAt).HasColumnName("request_window_started_at");
            throttle.Property(t => t.RequestCount).HasColumnName("request_count");
        });
    }
}
