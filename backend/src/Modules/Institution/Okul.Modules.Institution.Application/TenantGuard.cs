using Okul.Core.Tenancy;

namespace Okul.Modules.Institution.Application;

internal static class TenantGuard
{
    /// <summary>
    /// A tenant-scoped query without a tenant is a programming error (e.g. a background job without
    /// tenant context, T-3), never a user error: fail loudly instead of returning empty data.
    /// </summary>
    public static void EnsureTenant(ITenantContext tenantContext)
    {
        if (tenantContext.TenantId is null || tenantContext.TenantId == Guid.Empty)
        {
            throw new InvalidOperationException("Tenant context is missing for a tenant-scoped query.");
        }
    }
}
