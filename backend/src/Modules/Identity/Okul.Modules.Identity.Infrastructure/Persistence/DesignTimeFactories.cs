using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Okul.Core.Tenancy;

namespace Okul.Modules.Identity.Infrastructure.Persistence;

/// <summary>Used only by <c>dotnet ef migrations</c>; never connects (no secret needed).</summary>
internal sealed class IdentityDesignTimeDbContextFactory : IDesignTimeDbContextFactory<IdentityDbContext>
{
    public IdentityDbContext CreateDbContext(string[] args) =>
        new(new DbContextOptionsBuilder<IdentityDbContext>()
            .UseNpgsql("Host=design-time-only", IdentityInfrastructure.ConfigureNpgsql).Options, new NoTenant());

    private sealed class NoTenant : ITenantContext
    {
        public Guid? TenantId => null;
    }
}

internal sealed class LoginThrottleDesignTimeDbContextFactory : IDesignTimeDbContextFactory<LoginThrottleDbContext>
{
    public LoginThrottleDbContext CreateDbContext(string[] args) =>
        new(new DbContextOptionsBuilder<LoginThrottleDbContext>()
            .UseNpgsql("Host=design-time-only", IdentityInfrastructure.ConfigureThrottleNpgsql).Options);
}
