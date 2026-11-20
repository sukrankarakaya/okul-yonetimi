using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Okul.Core.Tenancy;

namespace Okul.Modules.Enrollment.Infrastructure.Persistence;

/// <summary>Used only by <c>dotnet ef migrations</c>; never connects (no secret needed).</summary>
internal sealed class EnrollmentDesignTimeDbContextFactory : IDesignTimeDbContextFactory<EnrollmentDbContext>
{
    public EnrollmentDbContext CreateDbContext(string[] args) =>
        new(new DbContextOptionsBuilder<EnrollmentDbContext>()
            .UseNpgsql("Host=design-time-only", EnrollmentInfrastructure.ConfigureNpgsql).Options, new NoTenant());

    private sealed class NoTenant : ITenantContext
    {
        public Guid? TenantId => null;
    }
}

internal sealed class GuardianLoginDesignTimeDbContextFactory : IDesignTimeDbContextFactory<GuardianLoginDbContext>
{
    public GuardianLoginDbContext CreateDbContext(string[] args) =>
        new(new DbContextOptionsBuilder<GuardianLoginDbContext>()
            .UseNpgsql("Host=design-time-only", EnrollmentInfrastructure.ConfigureLoginNpgsql).Options);
}
