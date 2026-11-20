using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Okul.Core.Tenancy;

namespace Okul.Modules.Parents.Infrastructure.Persistence;

/// <summary>Used only by <c>dotnet ef migrations</c>; never connects (no secret needed).</summary>
internal sealed class ParentDesignTimeDbContextFactory : IDesignTimeDbContextFactory<ParentDbContext>
{
    public ParentDbContext CreateDbContext(string[] args) =>
        new(new DbContextOptionsBuilder<ParentDbContext>()
            .UseNpgsql("Host=design-time-only").Options, new NoTenant());

    private sealed class NoTenant : ITenantContext
    {
        public Guid? TenantId => null;
    }
}
