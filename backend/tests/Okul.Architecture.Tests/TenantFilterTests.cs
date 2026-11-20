using Microsoft.EntityFrameworkCore;
using Okul.Core.Persistence;
using Okul.Core.Tenancy;
using Shouldly;

namespace Okul.Architecture.Tests;

/// <summary>
/// Sensitive area: Tenant izolasyonu. Every entity of every module DbContext carries the named tenant
/// filter (BR-1) — a new entity without <c>TenantId</c> and without its own tenant filter fails here.
/// </summary>
public sealed class TenantFilterTests
{
    public static TheoryData<string> ModuleDbContexts() => new(DbContextTypes().Select(t => t.FullName!));

    [Fact]
    public void ModuleDbContextsAreDiscovered() => DbContextTypes().ShouldNotBeEmpty();

    [Theory]
    [MemberData(nameof(ModuleDbContexts))]
    public void EveryEntity_HasTheTenantFilter(string contextTypeName)
    {
        var type = DbContextTypes().Single(t => t.FullName == contextTypeName);
        using var db = Create(type);

        var unfiltered = db.Model.GetEntityTypes()
            .Where(e => e.FindDeclaredQueryFilter(TenantDbContext.TenantFilterKey) is null)
            .Select(e => e.DisplayName())
            .ToList();

        unfiltered.ShouldBeEmpty($"{type.Name}: entities without the '{TenantDbContext.TenantFilterKey}' query filter");
    }

    /// <summary>
    /// The module DbContexts allowed to be tenant-free, BY NAME (plan 0003, R4): failed-login state must exist before
    /// any tenant is known. Anything else that derives from plain <see cref="DbContext"/> fails here — a new tenant-free
    /// context needs its own decision.
    /// </summary>
    private static readonly string[] TenantFreeAllowList =
        ["Okul.Modules.Identity.Infrastructure.Persistence.LoginThrottleDbContext"];

    [Fact]
    public void EveryModuleDbContext_IsTenantScoped_OrNamedInTheAllowList()
    {
        var tenantFree = ModuleTypes()
            .Where(t => t.IsSubclassOf(typeof(DbContext)) && !t.IsSubclassOf(typeof(TenantDbContext)))
            .Select(t => t.FullName!)
            .ToList();

        tenantFree.ShouldBe(TenantFreeAllowList, ignoreOrder: true);
    }

    private static IEnumerable<Type> DbContextTypes() =>
        ModuleTypes().Where(t => t.IsSubclassOf(typeof(TenantDbContext)));

    private static IEnumerable<Type> ModuleTypes() =>
        AppDomain.CurrentDomain.GetAssemblies()
            .Concat(Directory.GetFiles(AppContext.BaseDirectory, "Okul.Modules.*.Infrastructure.dll")
                .Select(System.Reflection.Assembly.LoadFrom))
            .Distinct()
            .Where(a => a.GetName().Name!.StartsWith("Okul.Modules.", StringComparison.Ordinal))
            .SelectMany(a => a.GetTypes())
            .Where(t => t is { IsAbstract: false });

    private static TenantDbContext Create(Type contextType)
    {
        var builder = (DbContextOptionsBuilder)Activator.CreateInstance(
            typeof(DbContextOptionsBuilder<>).MakeGenericType(contextType))!;
        var options = builder.UseNpgsql("Host=unused").Options;
        return (TenantDbContext)Activator.CreateInstance(contextType, options, new NoTenant())!;
    }

    private sealed class NoTenant : ITenantContext
    {
        public Guid? TenantId => null;
    }
}
