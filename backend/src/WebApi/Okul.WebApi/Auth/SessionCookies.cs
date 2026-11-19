namespace Okul.WebApi.Auth;

/// <summary>
/// Web session storage (spec 0003, Requirement 8; plan 0003, R5): access AND refresh live in httpOnly, Secure,
/// SameSite=Strict cookies — page scripts can read neither, and the browser storage holds nothing. Mobile uses bearer
/// tokens kept in the device's secure store instead.
/// </summary>
public static class SessionCookies
{
    public const string Access = "okul_access";
    public const string Refresh = "okul_refresh";
    public const string AccessPath = "/api";
    public const string RefreshPath = "/api/v1/auth";

    /// <summary>Cookie-authenticated requests that change state must carry this header (CSRF; plan 0003, R5).</summary>
    public const string CsrfHeader = "X-Okul-Csrf";

    public static void Write(HttpResponse response, string accessToken, DateTimeOffset accessExpiresAt,
        string refreshToken, DateTimeOffset sessionExpiresAt)
    {
        response.Cookies.Append(Access, accessToken, Options(AccessPath, accessExpiresAt));
        response.Cookies.Append(Refresh, refreshToken, Options(RefreshPath, sessionExpiresAt));
    }

    public static void Clear(HttpResponse response)
    {
        response.Cookies.Delete(Access, Options(AccessPath, null));
        response.Cookies.Delete(Refresh, Options(RefreshPath, null));
    }

    public static bool HasAny(HttpRequest request) =>
        request.Cookies.ContainsKey(Access) || request.Cookies.ContainsKey(Refresh);

    private static CookieOptions Options(string path, DateTimeOffset? expires) => new()
    {
        HttpOnly = true,
        Secure = true,
        SameSite = SameSiteMode.Strict,
        Path = path,
        Expires = expires,
        IsEssential = true,
    };
}

/// <summary>
/// A state-changing request that carries a session cookie must also carry <see cref="SessionCookies.CsrfHeader"/>.
/// Another site cannot add a custom header without a CORS preflight, which only known origins pass.
/// </summary>
internal sealed class CsrfGuard
{
    private readonly RequestDelegate _next;

    public CsrfGuard(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, IProblemDetailsService problemDetails)
    {
        var request = context.Request;
        var safe = HttpMethods.IsGet(request.Method) || HttpMethods.IsHead(request.Method) || HttpMethods.IsOptions(request.Method);
        if (!safe && SessionCookies.HasAny(request) && !request.Headers.ContainsKey(SessionCookies.CsrfHeader))
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            await problemDetails.WriteAsync(new ProblemDetailsContext
            {
                HttpContext = context,
                ProblemDetails = { Status = 403, Title = "Yetkisiz işlem", Detail = "İstek doğrulanamadı." },
            });
            return;
        }

        await _next(context);
    }
}
