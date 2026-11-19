using Microsoft.EntityFrameworkCore;
using Okul.Core.Persistence;
using Okul.Core.Tenancy;
using Shouldly;

namespace Okul.WebApi.UnitTests.Core;

/// <summary>Sensitive area: Tenant izolasyonu — the base filter fails closed and never overwrites module filters.</summary>
public sealed class TenantDbContextTests
{
    [Fact]
    public void NullableTenantId_FailsModelBuilding()
    {
        using var db = new NullableTenantContext();

        Should.Throw<InvalidOperationException>(() => db.Model).Message.ShouldContain("non-nullable Guid");
    }

    [Fact]
    public void TenantFilter_IsNamed_AndKeepsTheModulesOwnFilter()
    {
        using var db = new SoftDeleteContext();

        var entity = db.Model.FindEntityType(typeof(SoftDeletable)).ShouldNotBeNull();
        entity.GetDeclaredQueryFilters().Select(f => f.Key).ShouldBe(["SoftDelete", TenantDbContext.TenantFilterKey], ignoreOrder: true);
    }

    private sealed class NoTenant : ITenantContext
    {
        public Guid? TenantId => null;
    }

    private sealed class NullableTenant
    {
        public Guid Id { get; set; }

        public Guid? TenantId { get; set; }
    }

    private sealed class SoftDeletable
    {
        public Guid Id { get; set; }

        public Guid TenantId { get; set; }

        public bool IsDeleted { get; set; }
    }

    private abstract class FakeContext(ITenantContext tenant) : TenantDbContext(
        new DbContextOptionsBuilder().UseNpgsql("Host=unused").Options, tenant);

    private sealed class NullableTenantContext() : FakeContext(new NoTenant())
    {
        protected override void ConfigureModel(ModelBuilder modelBuilder) => modelBuilder.Entity<NullableTenant>();
    }

    private sealed class SoftDeleteContext() : FakeContext(new NoTenant())
    {
        protected override void ConfigureModel(ModelBuilder modelBuilder) =>
            modelBuilder.Entity<SoftDeletable>().HasQueryFilter("SoftDelete", e => !e.IsDeleted);
    }
}
