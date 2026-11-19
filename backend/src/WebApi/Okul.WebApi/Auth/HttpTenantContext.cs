using Okul.Core.Tenancy;

namespace Okul.WebApi.Auth;

/// <summary>
/// The tenant of the current request. It comes from the validated token's <c>tenant_id</c> claim; headers, query
/// strings and bodies are never consulted (BR-1, spec 0001 AC-3). Before a session exists (login, refresh) the tenant
/// resolved from the kurum kodu or the verified refresh credential is given through <see cref="ITenantScope"/>
/// (spec 0003; plan 0003, R3). A claim always wins over a scope.
/// </summary>
internal sealed class HttpTenantContext : ITenantContext, ITenantScope
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private Guid? _scoped;

    public HttpTenantContext(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public Guid? TenantId => ClaimTenant() ?? _scoped;

    public IDisposable Begin(Guid tenantId)
    {
        if (tenantId == Guid.Empty)
        {
            throw new ArgumentException("Tenant id cannot be empty.", nameof(tenantId));
        }

        var previous = _scoped;
        _scoped = tenantId;
        return new Restore(this, previous);
    }

    private Guid? ClaimTenant()
    {
        var user = _httpContextAccessor.HttpContext?.User;
        if (user?.Identity?.IsAuthenticated != true)
        {
            return null;
        }

        return Guid.TryParse(user.FindFirst(OkulClaims.TenantId)?.Value, out var tenantId) && tenantId != Guid.Empty
            ? tenantId
            : null;
    }

    private sealed class Restore(HttpTenantContext owner, Guid? previous) : IDisposable
    {
        public void Dispose() => owner._scoped = previous;
    }
}
