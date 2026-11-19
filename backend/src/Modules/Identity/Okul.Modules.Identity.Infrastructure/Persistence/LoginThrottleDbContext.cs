using Microsoft.EntityFrameworkCore;
using Okul.Modules.Identity.Domain;

namespace Okul.Modules.Identity.Infrastructure.Persistence;

/// <summary>
/// THE one module DbContext without the tenant filter (plan 0003, R4; architecture test allow-list by name).
/// Failed-login state must exist before — and without — any tenant: an unknown kurum kodu or e-mail locks exactly
/// like a real one (spec 0003, Requirement 4). It holds only hashed keys, counters and times — no tenant data, no
/// e-mail in clear.
/// </summary>
public sealed class LoginThrottleDbContext : DbContext
{
    public const string HistoryTable = "__EFMigrationsHistory_throttle";

    public LoginThrottleDbContext(DbContextOptions<LoginThrottleDbContext> options)
        : base(options)
    {
    }

    public DbSet<LoginThrottle> LoginThrottles => Set<LoginThrottle>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(IdentityDbContext.Schema);

        modelBuilder.Entity<LoginThrottle>(throttle =>
        {
            throttle.ToTable("login_throttles");
            throttle.HasKey(t => t.Key);
            throttle.Property(t => t.Key).HasColumnName("key").HasMaxLength(64);
            throttle.Property(t => t.FailureCount).HasColumnName("failure_count");
            throttle.Property(t => t.LastFailureAt).HasColumnName("last_failure_at");
            throttle.Property(t => t.LockedUntil).HasColumnName("locked_until");
        });
    }
}
