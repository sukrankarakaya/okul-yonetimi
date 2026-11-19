using Okul.Core.Tenancy;
using Okul.Modules.Identity.Application;
using Okul.Modules.Identity.Domain;
using Okul.WebApi.Auth;

namespace Okul.WebApi.DevAuth;

/// <summary>
/// Test girişi — exists ONLY in the Development and Testing environments, for automated tests (spec 0001,
/// Requirement 6, AC-6; spec 0003, Requirement 9). In any other environment the route is never mapped, and it is
/// never part of the published API description (B-3), so the generated client and the app bundles do not know it.
/// It opens a REAL session (any role, a synthetic account), so the per-request session check has no back door.
/// </summary>
public static class DevAuthEndpoints
{
    public const string TestingEnvironment = "Testing";
    public const string Route = "/api/v1/dev/test-login";

    public static bool IsAllowedIn(IHostEnvironment environment) =>
        environment.IsDevelopment() || environment.IsEnvironment(TestingEnvironment);

    public static void MapDevAuth(this IEndpointRouteBuilder endpoints, IHostEnvironment environment)
    {
        if (!IsAllowedIn(environment))
        {
            return;
        }

        endpoints.MapPost(Route, async (
                TestLoginRequest request, TokenIssuer issuer, ITenantScope tenantScope, ISessionStore sessions,
                TimeProvider time, CancellationToken cancellationToken) =>
            {
                if (request.TenantId == Guid.Empty || !Roles.All.Contains(request.Role))
                {
                    return Results.Problem(
                        statusCode: StatusCodes.Status400BadRequest,
                        title: "Geçersiz istek",
                        detail: "Geçerli bir kurum ve rol (Administrator, Teacher, Guardian, Accountant) gerekli.");
                }

                var session = Session.Start(
                    Guid.CreateVersion7(), request.TenantId, Guid.CreateVersion7(), ClientKind.Mobile, time.GetUtcNow(),
                    RefreshCredential.HashOf(Guid.NewGuid().ToString("N")));
                using (tenantScope.Begin(request.TenantId))
                {
                    await sessions.AddAsync(session, cancellationToken);
                    await sessions.SaveChangesAsync(cancellationToken);
                }

                var (token, expiresAt) = issuer.Issue(session.AccountId, session.TenantId, session.Id, [request.Role], session.ExpiresAt);
                return Results.Ok(new TestLoginResponse(token, expiresAt));
            })
            .AllowAnonymous()
            .ExcludeFromDescription()
            .WithName("DevTestLogin");
    }
}

public sealed record TestLoginRequest(Guid TenantId, string Role);

public sealed record TestLoginResponse(string AccessToken, DateTimeOffset ExpiresAt);
