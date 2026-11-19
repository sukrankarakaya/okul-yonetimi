using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Okul.Core.Tenancy;

namespace Okul.Core.Persistence;

/// <summary>
/// Base for every module DbContext. Every entity with a <c>Guid TenantId</c> property gets a global
/// query filter on the current tenant (docs/conventions.md, Data rules). Without a tenant the filter
/// compares with <see cref="Guid.Empty"/>, so nothing is returned (fail closed).
/// Bypassing the filter (<c>IgnoreQueryFilters</c>, raw SQL) is a known trap (T-2).
/// The filter is named <see cref="TenantFilterKey"/>, so a module's own filters (e.g. soft delete) live
/// beside it instead of overwriting it; a <c>TenantId</c> that is not a non-nullable <see cref="Guid"/>
/// fails model building instead of silently going unfiltered.
/// </summary>
public abstract class TenantDbContext : DbContext
{
    public const string TenantIdProperty = "TenantId";

    /// <summary>Query filter key of the tenant filter; a tenant entity without a <c>TenantId</c> (the kurum itself) uses it too.</summary>
    public const string TenantFilterKey = "Tenant";

    private readonly ITenantContext _tenantContext;

    protected TenantDbContext(DbContextOptions options, ITenantContext tenantContext)
        : base(options)
    {
        _tenantContext = tenantContext;
    }

    /// <summary>Read by the query filters on every query execution (EF parameterizes it).</summary>
    public Guid CurrentTenantId => _tenantContext.TenantId ?? Guid.Empty;

    protected sealed override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ConfigureModel(modelBuilder);
        ApplyTenantFilters(modelBuilder);
    }

    /// <summary>Module model configuration. Tenant filters are applied afterwards, always.</summary>
    protected abstract void ConfigureModel(ModelBuilder modelBuilder);

    private void ApplyTenantFilters(ModelBuilder modelBuilder)
    {
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            var tenantProperty = entityType.FindProperty(TenantIdProperty);
            if (tenantProperty is null)
            {
                continue;
            }

            if (tenantProperty.ClrType != typeof(Guid))
            {
                throw new InvalidOperationException(
                    $"{entityType.DisplayName()}.{TenantIdProperty} must be a non-nullable Guid "
                    + $"(found {tenantProperty.ClrType.Name}); tenant data is never left unfiltered.");
            }

            var entity = Expression.Parameter(entityType.ClrType, "e");
            var propertyAccess = Expression.Call(
                typeof(EF), nameof(EF.Property), [typeof(Guid)], entity, Expression.Constant(TenantIdProperty));
            var currentTenant = Expression.Property(Expression.Constant(this), nameof(CurrentTenantId));
            var filter = Expression.Lambda(Expression.Equal(propertyAccess, currentTenant), entity);

            modelBuilder.Entity(entityType.ClrType).HasQueryFilter(TenantFilterKey, filter);
        }
    }
}
