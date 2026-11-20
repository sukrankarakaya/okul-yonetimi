using Microsoft.EntityFrameworkCore;
using Okul.Core.Persistence;
using Okul.Core.Tenancy;
using Okul.Modules.Identity.Domain;

namespace Okul.Modules.Identity.Infrastructure.Persistence;

/// <summary>
/// Kimlik module storage, schema <c>kimlik</c> (T-8). Accounts and sessions carry <c>TenantId</c>, so the tenant
/// filter applies to both (BR-1); before a session exists the tenant comes from an <c>ITenantScope</c> (plan 0003, R3).
/// </summary>
public sealed class IdentityDbContext : TenantDbContext
{
    public const string Schema = "kimlik";

    public IdentityDbContext(DbContextOptions<IdentityDbContext> options, ITenantContext tenantContext)
        : base(options, tenantContext)
    {
    }

    public DbSet<StaffAccount> StaffAccounts => Set<StaffAccount>();

    public DbSet<Session> Sessions => Set<Session>();

    public DbSet<LoginChallenge> LoginChallenges => Set<LoginChallenge>();

    public DbSet<RecoveryCode> RecoveryCodes => Set<RecoveryCode>();

    protected override void ConfigureModel(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);

        modelBuilder.Entity<StaffAccount>(account =>
        {
            account.ToTable("staff_accounts");
            account.HasKey(a => a.Id);
            account.Property(a => a.Id).HasColumnName("id").ValueGeneratedNever();
            account.Property(a => a.TenantId).HasColumnName("tenant_id");
            account.Property(a => a.Email).HasColumnName("email").IsRequired().HasMaxLength(EmailAddress.MaxLength);
            account.Property(a => a.PasswordHash).HasColumnName("password_hash").IsRequired().HasMaxLength(256);
            account.PrimitiveCollection(a => a.Roles).HasColumnName("roles").HasField("_roles")
                .UsePropertyAccessMode(PropertyAccessMode.Field);
            account.Ignore(a => a.CanLogInAsStaff);
            // Spec 0004: the secret only encrypted (R2); the last step is the concurrency token — one code, one session (R6).
            account.Property(a => a.TwoFactorSecret).HasColumnName("two_factor_secret").HasMaxLength(256);
            account.Property(a => a.TwoFactorLastStep).HasColumnName("two_factor_last_step").IsConcurrencyToken();
            account.Ignore(a => a.TwoFactorEnabled);
            account.Ignore(a => a.TwoFactorRequired);
            account.Ignore(a => a.CanDisableTwoFactor);

            // One e-mail per kurum; the same e-mail in another kurum is another account (spec 0003).
            account.HasIndex(a => new { a.TenantId, a.Email }).IsUnique().HasDatabaseName("ux_staff_accounts_tenant_email");
        });

        modelBuilder.Entity<Session>(session =>
        {
            session.ToTable("sessions");
            session.HasKey(s => s.Id);
            session.Property(s => s.Id).HasColumnName("id").ValueGeneratedNever();
            session.Property(s => s.TenantId).HasColumnName("tenant_id");
            session.Property(s => s.AccountId).HasColumnName("account_id");
            session.Property(s => s.Client).HasColumnName("client");
            // Spec 0007: rows before the column are staff sessions.
            session.Property(s => s.Kind).HasColumnName("account_kind").HasDefaultValue(AccountKind.Staff);
            session.Property(s => s.StartedAt).HasColumnName("started_at");
            session.Property(s => s.ExpiresAt).HasColumnName("expires_at");
            session.Property(s => s.ClosedAt).HasColumnName("closed_at");
            // Optimistic concurrency: a rotation writes only if the hash it read is still current (review round 1 #5).
            session.Property(s => s.CurrentRefreshHash).HasColumnName("current_refresh_hash").IsRequired().HasMaxLength(64)
                .IsConcurrencyToken();
            session.Property(s => s.PreviousRefreshHash).HasColumnName("previous_refresh_hash").HasMaxLength(64);
            session.Property(s => s.RotatedAt).HasColumnName("rotated_at");
            session.HasIndex(s => new { s.TenantId, s.AccountId }).HasDatabaseName("ix_sessions_tenant_account");
        });

        modelBuilder.Entity<LoginChallenge>(challenge =>
        {
            challenge.ToTable("login_challenges");
            challenge.HasKey(c => c.Id);
            challenge.Property(c => c.Id).HasColumnName("id").ValueGeneratedNever();
            challenge.Property(c => c.TenantId).HasColumnName("tenant_id");
            challenge.Property(c => c.AccountId).HasColumnName("account_id");
            challenge.Property(c => c.Purpose).HasColumnName("purpose");
            challenge.Property(c => c.Client).HasColumnName("client");
            challenge.Property(c => c.ExpiresAt).HasColumnName("expires_at");
            challenge.Property(c => c.EndedAt).HasColumnName("ended_at");
            challenge.Property(c => c.CredentialHash).HasColumnName("credential_hash").IsRequired().HasMaxLength(64);
            challenge.Property(c => c.PendingProtectedSecret).HasColumnName("pending_secret").HasMaxLength(256);
            challenge.HasIndex(c => new { c.TenantId, c.AccountId }).HasDatabaseName("ix_login_challenges_tenant_account");
        });

        modelBuilder.Entity<RecoveryCode>(code =>
        {
            code.ToTable("recovery_codes");
            code.HasKey(c => c.Id);
            code.Property(c => c.Id).HasColumnName("id").ValueGeneratedNever();
            code.Property(c => c.TenantId).HasColumnName("tenant_id");
            code.Property(c => c.AccountId).HasColumnName("account_id");
            code.Property(c => c.CodeHash).HasColumnName("code_hash").IsRequired().HasMaxLength(64);
            code.Property(c => c.UsedAt).HasColumnName("used_at");
            code.Ignore(c => c.IsUnused);
            code.HasIndex(c => new { c.TenantId, c.AccountId }).HasDatabaseName("ix_recovery_codes_tenant_account");
        });
    }
}
