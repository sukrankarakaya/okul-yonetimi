namespace Okul.WebApi.Hosting;

/// <summary>
/// docs/security.md, Web: security headers on every response — error responses included. The API serves
/// JSON only, so the CSP allows nothing to load or frame it. Headers are written in
/// <c>Response.OnStarting</c>: <c>UseExceptionHandler</c> clears headers written earlier (T-12).
/// Outside Development/Testing: HTTPS redirection, and HSTS on HTTPS responses (proxy setup: backlog B-4).
/// </summary>
public static class SecurityHeaders
{
    public const string ContentSecurityPolicy = "default-src 'none'; frame-ancestors 'none'";
    public const string StrictTransportSecurity = "max-age=31536000; includeSubDomains";

    public static IApplicationBuilder UseOkulSecurityHeaders(this WebApplication app)
    {
        var enforceHttps = !DevAuth.DevAuthEndpoints.IsAllowedIn(app.Environment);

        // Registered before UseHttpsRedirection, so redirect responses carry the headers too.
        app.Use(async (context, next) =>
        {
            context.Response.OnStarting(() =>
            {
                var headers = context.Response.Headers;
                headers.XContentTypeOptions = "nosniff";
                headers.XFrameOptions = "DENY";
                headers["Referrer-Policy"] = "no-referrer";
                headers.ContentSecurityPolicy = ContentSecurityPolicy;
                if (enforceHttps && context.Request.IsHttps)
                {
                    headers.StrictTransportSecurity = StrictTransportSecurity;
                }

                return Task.CompletedTask;
            });
            await next(context);
        });

        if (enforceHttps)
        {
            app.UseHttpsRedirection();
        }

        return app;
    }
}
