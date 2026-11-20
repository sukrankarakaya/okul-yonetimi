namespace Okul.Core.Tenancy;

/// <summary>
/// The one way to act for a tenant BEFORE a session exists (spec 0003, login and refresh; plan 0003, R3): the tenant
/// resolved from the kurum kodu — or carried inside a verified refresh credential — is set for the current operation,
/// so every query still runs under the tenant filter and no <c>IgnoreQueryFilters</c> is needed. An authenticated
/// user's tenant claim always wins over a scope.
/// </summary>
public interface ITenantScope
{
    IDisposable Begin(Guid tenantId);
}
