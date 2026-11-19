using Okul.WebApi.Auth;

namespace Okul.WebApi.DevAuth;

/// <summary>
/// Test girişi — exists ONLY in the Development and Testing environments (spec 0001, Requirement 6,
/// AC-6). In any other environment the route is never mapped. The real login flow is a later spec.
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

        endpoints.MapPost(Route, (TestLoginRequest request, TokenIssuer issuer) =>
            {
                if (request.TenantId == Guid.Empty || !Roles.All.Contains(request.Role))
                {
                    return Results.Problem(
                        statusCode: StatusCodes.Status400BadRequest,
                        title: "Geçersiz istek",
                        detail: "Geçerli bir kurum ve rol (Administrator, Teacher, Guardian, Accountant) gerekli.");
                }

                var (token, expiresAt) = issuer.Issue(Guid.CreateVersion7(), request.TenantId, request.Role);
                return Results.Ok(new TestLoginResponse(token, expiresAt));
            })
            .AllowAnonymous()
            .WithName("DevTestLogin")
            .WithTags("DevAuth")
            .Produces<TestLoginResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest);
    }
}

public sealed record TestLoginRequest(Guid TenantId, string Role);

public sealed record TestLoginResponse(string AccessToken, DateTimeOffset ExpiresAt);
