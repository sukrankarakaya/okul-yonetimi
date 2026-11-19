using Okul.Core.Tenancy;

namespace Okul.WebApi.Auth;

/// <summary>
/// The tenant comes ONLY from the validated token's <c>tenant_id</c> claim. Headers, query strings
/// and bodies are never consulted (BR-1, spec 0001 AC-3).
/// </summary>
internal sealed class HttpTenantContext : ITenantContext
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public HttpTenantContext(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public Guid? TenantId
    {
        get
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
    }
}
