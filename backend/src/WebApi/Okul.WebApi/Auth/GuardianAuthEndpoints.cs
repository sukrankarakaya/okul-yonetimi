using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.HttpResults;
using NArchitecture.Core.Mediator;
using Okul.Modules.Enrollment.Application;
using Okul.Modules.Enrollment.Contracts;
using Okul.Modules.Identity.Application;
using Okul.Modules.Identity.Contracts;
using Okul.Modules.Identity.Domain;

namespace Okul.WebApi.Auth;

/// <summary>
/// Veli girişi (spec 0007): kurum kodu + telefon → SMS kodu → session; KVKK rıza; the veli's own profile. The texts are
/// the spec's (B-7). Anonymous steps run under the login source limit (Requirement 4: 20 a minute per source).
/// </summary>
public static class GuardianAuthEndpoints
{
    public const string GuardianPolicy = "guardian";
    public const string GuardianWithConsentPolicy = "guardian-consented";

    public const string InvalidPhoneMessage = "Lütfen geçerli bir telefon numarası girin.";
    public const string CodeSentMessage = "Numara kayıtlıysa SMS kodu gönderildi. Kod 5 dakika geçerli.";
    public const string RateLimitedMessage = "Çok sık kod istediniz. 1 dakika sonra tekrar deneyin.";
    public const string SmsFailedMessage = "SMS gönderilemedi. Tekrar deneyin.";
    public const string WrongCodeMessage = "Kod hatalı veya süresi geçmiş.";
    public const string ExpiredCodeMessage = "Kodun süresi doldu. Yeni kod isteyin.";

    public static void MapGuardianAuthEndpoints(this IEndpointRouteBuilder api)
    {
        var auth = api.MapGroup("/auth/guardian").WithTags("GuardianAuth");

        auth.MapPost("/code", RequestCodeAsync)
            .AllowAnonymous()
            .RequireRateLimiting(AuthEndpoints.LoginRateLimitPolicy)
            .WithName("RequestGuardianCode")
            .Produces<GuardianCodeResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status429TooManyRequests)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable);

        auth.MapPost("/verify", VerifyCodeAsync)
            .AllowAnonymous()
            .RequireRateLimiting(AuthEndpoints.LoginRateLimitPolicy)
            .WithName("VerifyGuardianCode")
            .Produces<GuardianSessionResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status429TooManyRequests);

        auth.MapPost("/cancel", CancelAsync)
            .AllowAnonymous()
            .RequireRateLimiting(AuthEndpoints.LoginRateLimitPolicy)
            .WithName("CancelGuardianCode")
            .Produces(StatusCodes.Status204NoContent);

        var guardian = api.MapGroup("/guardian").WithTags("Guardian");

        // Rıza itself needs a veli session, not a rıza (Requirement 6).
        guardian.MapGet("/consent", GetConsentAsync)
            .RequireAuthorization(GuardianPolicy)
            .WithName("GetGuardianConsent")
            .Produces<GuardianConsentResponse>();

        guardian.MapPost("/consent", AcceptConsentAsync)
            .RequireAuthorization(GuardianPolicy)
            .WithName("AcceptGuardianConsent")
            .Produces<GuardianConsentResponse>();

        // Veli data: only with the current rıza (AC-4). The first such endpoint; 0006 adds the çocuk listesi beside it.
        guardian.MapGet("/me", GetMeAsync)
            .RequireAuthorization(GuardianWithConsentPolicy)
            .WithName("GetGuardianProfile")
            .Produces<GuardianProfileResponse>()
            .ProducesProblem(StatusCodes.Status403Forbidden);
    }

    public static void AddGuardianPolicies(this AuthorizationBuilder authorization) => authorization
        .AddPolicy(GuardianPolicy, policy => policy
            .RequireAuthenticatedUser().RequireClaim(OkulClaims.TenantId).RequireRole(Roles.Guardian))
        .AddPolicy(GuardianWithConsentPolicy, policy => policy
            .RequireAuthenticatedUser().RequireClaim(OkulClaims.TenantId).RequireRole(Roles.Guardian)
            .AddRequirements(new CurrentConsentRequirement()));

    private static async Task<IResult> RequestCodeAsync(
        GuardianCodeRequest request, ISender sender, CancellationToken cancellationToken)
    {
        var status = await sender.Send(new RequestGuardianCodeCommand(request.InstitutionCode, request.Phone), cancellationToken);
        return status switch
        {
            RequestCodeStatus.Accepted => Results.Ok(new GuardianCodeResponse(CodeSentMessage)),
            RequestCodeStatus.InvalidPhone => AuthEndpoints.Problem(400, "Geçersiz telefon", InvalidPhoneMessage, "invalid_phone"),
            RequestCodeStatus.RateLimited => AuthEndpoints.Problem(429, "Çok sık kod isteği", RateLimitedMessage, "code_rate_limited"),
            _ => AuthEndpoints.Problem(503, "SMS gönderilemedi", SmsFailedMessage, "sms_failed"),
        };
    }

    private static async Task<IResult> VerifyCodeAsync(
        GuardianVerifyRequest request, ISender sender, TokenIssuer issuer, HttpResponse response,
        CancellationToken cancellationToken)
    {
        if (!AuthEndpoints.TryParseClient(request.Client, out var client))
        {
            return Results.Problem(statusCode: 400, title: "Geçersiz istek", detail: "İstemci türü 'web' veya 'mobile' olmalı.");
        }

        var result = await sender.Send(new VerifyGuardianCodeCommand(
            request.InstitutionCode, request.Phone, request.Code, (SessionClient)client), cancellationToken);
        switch (result.Status)
        {
            case VerifyCodeStatus.Succeeded:
                var started = result.Session!;
                var issued = new IssuedSession(
                    started.SessionId, started.TenantId, started.GuardianId, started.Roles, (ClientKind)started.Client,
                    started.ExpiresAt, started.RefreshToken);
                response.Headers.CacheControl = "no-store";
                return Results.Ok(new GuardianSessionResponse(
                    AuthEndpoints.Session(issued, issuer, response), result.ConsentRequired));
            case VerifyCodeStatus.Locked:
                return AuthEndpoints.TooManyAttempts();
            case VerifyCodeStatus.Expired:
                return AuthEndpoints.Problem(401, "Kodun süresi doldu", ExpiredCodeMessage, "code_expired");
            default:
                return AuthEndpoints.Problem(401, "Doğrulama başarısız", WrongCodeMessage, "invalid_code");
        }
    }

    private static async Task<NoContent> CancelAsync(GuardianCodeRequest request, ISender sender, CancellationToken cancellationToken)
    {
        await sender.Send(new CancelGuardianCodeCommand(request.InstitutionCode, request.Phone), cancellationToken);
        return TypedResults.NoContent();
    }

    private static async Task<Ok<GuardianConsentResponse>> GetConsentAsync(
        ClaimsPrincipal user, ISender sender, CancellationToken cancellationToken)
    {
        var status = await sender.Send(new GetGuardianConsentQuery(
            AuthEndpoints.ClaimGuid(user, OkulClaims.TenantId), AuthEndpoints.ClaimGuid(user, OkulClaims.Subject)), cancellationToken);
        return TypedResults.Ok(new GuardianConsentResponse(status.Accepted, status.TextVersion, status.Text));
    }

    private static async Task<Ok<GuardianConsentResponse>> AcceptConsentAsync(
        ClaimsPrincipal user, ISender sender, CancellationToken cancellationToken)
    {
        var status = await sender.Send(new AcceptGuardianConsentCommand(
            AuthEndpoints.ClaimGuid(user, OkulClaims.TenantId), AuthEndpoints.ClaimGuid(user, OkulClaims.Subject)), cancellationToken);
        return TypedResults.Ok(new GuardianConsentResponse(status.Accepted, status.TextVersion, status.Text));
    }

    private static async Task<Ok<GuardianProfileResponse>> GetMeAsync(
        ClaimsPrincipal user, ISender sender, CancellationToken cancellationToken)
    {
        var profile = await sender.Send(new GetGuardianProfileQuery(
            AuthEndpoints.ClaimGuid(user, OkulClaims.TenantId), AuthEndpoints.ClaimGuid(user, OkulClaims.Subject)), cancellationToken);
        return TypedResults.Ok(new GuardianProfileResponse(profile.DisplayName, profile.MaskedPhone));
    }
}

/// <summary>Veli data needs the current aydınlatma metni rıza (spec 0007, Requirement 6) — checked on the server.</summary>
public sealed class CurrentConsentRequirement : IAuthorizationRequirement;

public sealed class CurrentConsentHandler : AuthorizationHandler<CurrentConsentRequirement>
{
    private readonly IGuardianDirectory _guardians;

    public CurrentConsentHandler(IGuardianDirectory guardians)
    {
        _guardians = guardians;
    }

    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, CurrentConsentRequirement requirement)
    {
        if (Guid.TryParse(context.User.FindFirst(OkulClaims.TenantId)?.Value, out var tenantId)
            && Guid.TryParse(context.User.FindFirst(OkulClaims.Subject)?.Value, out var guardianId)
            && await _guardians.HasCurrentConsentAsync(tenantId, guardianId))
        {
            context.Succeed(requirement);
        }
    }
}

public sealed record GuardianCodeRequest(string? InstitutionCode, string? Phone);

public sealed record GuardianCodeResponse(string Message);

public sealed record GuardianVerifyRequest(string? InstitutionCode, string? Phone, string? Code, string? Client);

/// <summary>The session as for staff, and whether the KVKK screen comes first.</summary>
public sealed record GuardianSessionResponse(SessionResponse Session, bool ConsentRequired);

public sealed record GuardianConsentResponse(bool Accepted, string TextVersion, string Text);

/// <summary>Only the last two digits of the telefon (Requirement 11).</summary>
public sealed record GuardianProfileResponse(string DisplayName, string MaskedPhone);
