using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Okul.Core.Tenancy;

namespace Okul.Modules.Institution.Infrastructure.Persistence;

/// <summary>Used only by <c>dotnet ef migrations</c>; never connects (no secret needed).</summary>
internal sealed class InstitutionDesignTimeDbContextFactory : IDesignTimeDbContextFactory<InstitutionDbContext>
{
    public InstitutionDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<InstitutionDbContext>()
            .UseNpgsql("Host=design-time-only", InstitutionInfrastructure.ConfigureNpgsql)
            .Options;
        return new InstitutionDbContext(options, new NoTenant());
    }

    private sealed class NoTenant : ITenantContext
    {
        public Guid? TenantId => null;
    }
}
