using System.Globalization;
using System.Security.Claims;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.RateLimiting;
using NArchitecture.Core.Mediator;
using Okul.Modules.Identity.Application;
using Okul.Modules.Identity.Domain;

namespace Okul.WebApi.Auth;

/// <summary>
/// Staff login and session (spec 0003). Messages are the spec's exact texts (User experience). Wrong inputs of any
/// kind get one answer (Requirement 3); lock and source throttling share one answer (Requirement 4).
/// </summary>
public static class AuthEndpoints
{
    public const string LoginRateLimitPolicy = "login";

    /// <summary>Spec 0007: a staff or a veli session (the fallback policy is staff only — plan 0007, R-3).</summary>
    public const string AnySessionPolicy = "any-session";

    public const string InvalidCredentialsMessage = "Kurum kodu, e-posta veya şifre hatalı.";
    public const string TooManyAttemptsMessage = "Çok fazla başarısız deneme. 15 dakika sonra tekrar deneyin.";
    public const string SessionEndedMessage = "Oturumunuzun süresi doldu, tekrar giriş yapın.";
    public const string CurrentPasswordWrongMessage = "Mevcut şifre hatalı.";
    public const string PasswordTooShortMessage = "Yeni şifre en az 10 karakter olmalı.";
    public const string PasswordMismatchMessage = "Yeni şifreler eşleşmiyor.";
    public const string InvalidCodeMessage = "Kod hatalı veya süresi geçmiş.";
    public const string StepEndedMessage = "Doğrulama süresi doldu, tekrar giriş yapın.";
    public const string TwoFactorMandatoryMessage = "Rolünüz gereği zorunlu.";

    /// <summary>The routes that run BEFORE a session exists; no credential is read on them.</summary>
    public static bool IsPreSessionPath(PathString path) =>
        path.StartsWithSegments("/api/v1/auth/login", StringComparison.OrdinalIgnoreCase)
        || path.StartsWithSegments("/api/v1/auth/refresh", StringComparison.OrdinalIgnoreCase);

    public static void MapAuthEndpoints(this IEndpointRouteBuilder api)
    {
        var auth = api.MapGroup("/auth").WithTags("Auth");

        auth.MapPost("/login", LoginAsync)
            .AllowAnonymous()
            .RequireRateLimiting(LoginRateLimitPolicy)
            .WithName("Login")
            .Produces<SessionResponse>()
            .Produces<LoginStepResponse>(StatusCodes.Status202Accepted)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status429TooManyRequests);

        // Spec 0004 — the step between a correct password and a session. Anonymous like the login (no credential is read,
        // IsPreSessionPath), under the same source limit (self-critique G2).
        auth.MapPost("/login/verify", VerifyLoginAsync)
            .AllowAnonymous()
            .RequireRateLimiting(LoginRateLimitPolicy)
            .WithName("VerifyLogin")
            .Produces<SessionResponse>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status429TooManyRequests);

        auth.MapPost("/login/setup", StartSetupAsync)
            .AllowAnonymous()
            .RequireRateLimiting(LoginRateLimitPolicy)
            .WithName("StartTwoFactorSetup")
            .Produces<TwoFactorSetupResponse>()
            .ProducesProblem(StatusCodes.Status401Unauthorized);

        auth.MapPost("/login/setup/confirm", ConfirmSetupAsync)
            .AllowAnonymous()
            .RequireRateLimiting(LoginRateLimitPolicy)
            .WithName("ConfirmTwoFactorSetup")
            .Produces<SetupCompletedResponse>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status429TooManyRequests);

        auth.MapPost("/login/cancel", CancelLoginAsync)
            .AllowAnonymous()
            .RequireRateLimiting(LoginRateLimitPolicy)
            .WithName("CancelLogin")
            .Produces(StatusCodes.Status204NoContent);

        auth.MapPost("/refresh", RefreshAsync)
            .AllowAnonymous()
            .WithName("RefreshSession")
            .Produces<SessionResponse>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status409Conflict);

        // Spec 0007: staff and veli sessions both log out and read their session (every other endpoint is staff only).
        auth.MapPost("/logout", LogoutAsync)
            .RequireAuthorization(AnySessionPolicy)
            .WithName("Logout")
            .Produces(StatusCodes.Status204NoContent);

        auth.MapGet("/session", GetSession)
            .RequireAuthorization(AnySessionPolicy)
            .WithName("GetSession")
            .Produces<SessionInfo>();

        auth.MapPost("/password", ChangePasswordAsync)
            .WithName("ChangePassword")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status429TooManyRequests);

        // Spec 0004 — Güvenlik (signed in; the default policy requires a session).
        var twoFactor = auth.MapGroup("/two-factor");
        twoFactor.MapGet("", GetTwoFactorAsync).WithName("GetTwoFactor").Produces<TwoFactorStatusResponse>();
        twoFactor.MapPost("/enable", StartEnableAsync)
            .WithName("StartEnableTwoFactor")
            .Produces<TwoFactorEnableResponse>()
            .ProducesProblem(StatusCodes.Status409Conflict);
        twoFactor.MapPost("/enable/confirm", ConfirmEnableAsync)
            .WithName("ConfirmEnableTwoFactor")
            .Produces<RecoveryCodesResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status429TooManyRequests);
        twoFactor.MapPost("/disable", DisableAsync)
            .WithName("DisableTwoFactor")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status429TooManyRequests);
        twoFactor.MapPost("/recovery-codes", RenewRecoveryCodesAsync)
            .WithName("RenewRecoveryCodes")
            .Produces<RecoveryCodesResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status429TooManyRequests);
    }

    private static async Task<Ok<TwoFactorStatusResponse>> GetTwoFactorAsync(
        ClaimsPrincipal user, ISender sender, CancellationToken cancellationToken)
    {
        var status = await sender.Send(new GetTwoFactorStatusQuery(ClaimGuid(user, OkulClaims.Subject)), cancellationToken);
        return TypedResults.Ok(new TwoFactorStatusResponse(status.Enabled, status.Required, status.CanDisable, status.RemainingRecoveryCodes));
    }

    private static async Task<IResult> StartEnableAsync(
        ClaimsPrincipal user, ISender sender, HttpResponse response, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new StartEnableTwoFactorCommand(ClaimGuid(user, OkulClaims.Subject)), cancellationToken);
        if (!result.Started)
        {
            return Problem(409, "Zaten açık", "İki adımlı doğrulama zaten açık.", "two_factor_enabled");
        }

        response.Headers.CacheControl = "no-store";
        return Results.Ok(new TwoFactorEnableResponse(result.Step!, result.Key!, QrCodes.PngDataUri(result.ProvisioningUri!)));
    }

    private static async Task<IResult> ConfirmEnableAsync(
        VerifyLoginRequest request, ClaimsPrincipal user, ISender sender, HttpResponse response, CancellationToken cancellationToken) =>
        ChangeAnswer(await sender.Send(
            new ConfirmEnableTwoFactorCommand(ClaimGuid(user, OkulClaims.Subject), request.Step, request.Code), cancellationToken), response);

    private static async Task<IResult> DisableAsync(
        TwoFactorCodeRequest request, ClaimsPrincipal user, ISender sender, HttpResponse response, CancellationToken cancellationToken) =>
        ChangeAnswer(await sender.Send(new DisableTwoFactorCommand(ClaimGuid(user, OkulClaims.Subject), request.Code), cancellationToken), response);

    private static async Task<IResult> RenewRecoveryCodesAsync(
        TwoFactorCodeRequest request, ClaimsPrincipal user, ISender sender, HttpResponse response, CancellationToken cancellationToken) =>
        ChangeAnswer(await sender.Send(new RenewRecoveryCodesCommand(ClaimGuid(user, OkulClaims.Subject), request.Code), cancellationToken), response);

    /// <summary>A wrong code is 400 here, not 401: the session is valid, only the code is not.</summary>
    private static IResult ChangeAnswer(TwoFactorChangeResult result, HttpResponse response)
    {
        switch (result.Status)
        {
            case TwoFactorChangeStatus.Succeeded when result.RecoveryCodes is { } codes:
                response.Headers.CacheControl = "no-store";
                return Results.Ok(new RecoveryCodesResponse(codes));
            case TwoFactorChangeStatus.Succeeded:
                return Results.NoContent();
            case TwoFactorChangeStatus.Locked:
                return TooManyAttempts();
            case TwoFactorChangeStatus.NotAllowed:
                return Problem(403, "İzin verilmiyor", TwoFactorMandatoryMessage, "two_factor_mandatory");
            case TwoFactorChangeStatus.StepEnded:
                return Problem(400, "Kurulum bitti", StepEndedMessage, "step_ended");
            default:
                return Problem(400, "Doğrulama başarısız", InvalidCodeMessage, "invalid_code");
        }
    }

    public static void AddLoginRateLimit(this IServiceCollection services, IConfiguration configuration)
    {
        var permitsPerMinute = configuration.GetValue("Auth:LoginRateLimitPerMinute", 20);
        services.AddRateLimiter(options =>
        {
            options.AddPolicy(LoginRateLimitPolicy, context => RateLimitPartition.GetFixedWindowLimiter(
                context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = permitsPerMinute,
                    Window = TimeSpan.FromMinutes(1),
                    QueueLimit = 0,
                }));
            options.OnRejected = async (context, cancellationToken) =>
            {
                context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
                await TooManyAttempts().ExecuteAsync(context.HttpContext);
            };
        });
    }

    private static async Task<IResult> LoginAsync(
        LoginRequest request, ISender sender, TokenIssuer issuer, HttpResponse response, CancellationToken cancellationToken)
    {
        if (!TryParseClient(request.Client, out var client))
        {
            return Results.Problem(statusCode: 400, title: "Geçersiz istek", detail: "İstemci türü 'web' veya 'mobile' olmalı.");
        }

        var result = await sender.Send(
            new LoginCommand(request.InstitutionCode, request.Email, request.Password, client), cancellationToken);

        return result.Status switch
        {
            LoginStatus.Succeeded => Issue(result.Session!, issuer, response),
            LoginStatus.TwoFactorRequired => Results.Json(
                new LoginStepResponse("verify", result.Step!.Credential, result.Step.ExpiresAt), statusCode: StatusCodes.Status202Accepted),
            LoginStatus.TwoFactorSetupRequired => Results.Json(
                new LoginStepResponse("setup", result.Step!.Credential, result.Step.ExpiresAt), statusCode: StatusCodes.Status202Accepted),
            LoginStatus.Locked => TooManyAttempts(),
            _ => Problem(401, "Giriş başarısız", InvalidCredentialsMessage, "invalid_credentials"),
        };
    }

    private static async Task<IResult> VerifyLoginAsync(
        VerifyLoginRequest request, ISender sender, TokenIssuer issuer, HttpResponse response, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new VerifyLoginCommand(request.Step, request.Code, request.RecoveryCode), cancellationToken);
        return StepAnswer(result) ?? Issue(result.Session!, issuer, response);
    }

    private static async Task<IResult> StartSetupAsync(
        LoginStepRequest request, ISender sender, HttpResponse response, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new StartSetupCommand(request.Step), cancellationToken);
        if (!result.Started)
        {
            return StepEnded();
        }

        response.Headers.CacheControl = "no-store";
        return Results.Ok(new TwoFactorSetupResponse(result.Key!, QrCodes.PngDataUri(result.ProvisioningUri!)));
    }

    private static async Task<IResult> ConfirmSetupAsync(
        VerifyLoginRequest request, ISender sender, TokenIssuer issuer, HttpResponse response, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new ConfirmSetupCommand(request.Step, request.Code), cancellationToken);
        if (StepAnswer(result) is { } refused)
        {
            return refused;
        }

        response.Headers.CacheControl = "no-store";
        return Results.Ok(new SetupCompletedResponse(Session(result.Session!, issuer, response), result.RecoveryCodes!));
    }

    private static async Task<NoContent> CancelLoginAsync(LoginStepRequest request, ISender sender, CancellationToken cancellationToken)
    {
        await sender.Send(new CancelLoginCommand(request.Step), cancellationToken);
        return TypedResults.NoContent();
    }

    /// <summary>Null when the step succeeded; otherwise the spec's answer for it.</summary>
    private static IResult? StepAnswer(TwoFactorStepResult result) => result.Status switch
    {
        TwoFactorStepStatus.Succeeded => null,
        TwoFactorStepStatus.Locked => TooManyAttempts(),
        TwoFactorStepStatus.InvalidCode => Problem(401, "Doğrulama başarısız", InvalidCodeMessage, "invalid_code"),
        _ => StepEnded(),
    };

    private static IResult StepEnded() => Problem(401, "Doğrulama adımı bitti", StepEndedMessage, "step_ended");

    private static async Task<IResult> RefreshAsync(
        RefreshRequest? request, ISender sender, TokenIssuer issuer, HttpRequest httpRequest, HttpResponse response,
        CancellationToken cancellationToken)
    {
        var fromCookie = httpRequest.Cookies.TryGetValue(SessionCookies.Refresh, out var cookie);
        var token = fromCookie ? cookie : request?.RefreshToken;

        var result = await sender.Send(new RefreshSessionCommand(token), cancellationToken);
        switch (result.Status)
        {
            case RefreshStatus.Renewed:
                return Issue(result.Session!, issuer, response);
            case RefreshStatus.Conflict:
                return Problem(409, "Oturum yenileniyor", "Oturum başka bir istekte yenilendi; isteği tekrarlayın.", "refresh_conflict");
            default:
                SessionCookies.Clear(response);
                return Problem(401, "Oturum sona erdi", SessionEndedMessage, "session_ended");
        }
    }

    private static async Task<NoContent> LogoutAsync(
        ClaimsPrincipal user, ISender sender, HttpResponse response, CancellationToken cancellationToken)
    {
        await sender.Send(new LogoutCommand(ClaimGuid(user, OkulClaims.Session)), cancellationToken);
        SessionCookies.Clear(response);
        return TypedResults.NoContent();
    }

    private static Ok<SessionInfo> GetSession(ClaimsPrincipal user) =>
        TypedResults.Ok(new SessionInfo(
            user.FindAll(OkulClaims.Role).Select(c => c.Value).ToList(),
            DateTimeOffset.FromUnixTimeSeconds(long.Parse(user.FindFirst(OkulClaims.SessionExpiresAt)!.Value, CultureInfo.InvariantCulture))));

    private static async Task<IResult> ChangePasswordAsync(
        ChangePasswordRequest request, ClaimsPrincipal user, ISender sender, CancellationToken cancellationToken)
    {
        var status = await sender.Send(new ChangePasswordCommand(
            ClaimGuid(user, OkulClaims.Subject), ClaimGuid(user, OkulClaims.Session),
            request.CurrentPassword, request.NewPassword, request.NewPasswordAgain), cancellationToken);

        return status switch
        {
            ChangePasswordStatus.Changed => Results.NoContent(),
            ChangePasswordStatus.CurrentPasswordWrong => Problem(400, "Şifre değiştirilemedi", CurrentPasswordWrongMessage, "current_password_wrong"),
            ChangePasswordStatus.TooShort => Problem(400, "Şifre değiştirilemedi", PasswordTooShortMessage, "password_too_short"),
            ChangePasswordStatus.Locked => TooManyAttempts(),
            _ => Problem(400, "Şifre değiştirilemedi", PasswordMismatchMessage, "password_mismatch"),
        };
    }

    /// <summary>Web: both credentials as httpOnly cookies, none in the body (AC-8). Mobile: in the body, for SecureStore.</summary>
    private static IResult Issue(IssuedSession session, TokenIssuer issuer, HttpResponse response) =>
        Results.Ok(Session(session, issuer, response));

    internal static SessionResponse Session(IssuedSession session, TokenIssuer issuer, HttpResponse response)
    {
        var (access, accessExpiresAt) = issuer.Issue(session.AccountId, session.TenantId, session.SessionId, session.Roles, session.ExpiresAt);
        if (session.Client == ClientKind.Web)
        {
            SessionCookies.Write(response, access, accessExpiresAt, session.RefreshToken, session.ExpiresAt);
            return new SessionResponse(session.Roles, accessExpiresAt, session.ExpiresAt, null, null);
        }

        return new SessionResponse(session.Roles, accessExpiresAt, session.ExpiresAt, access, session.RefreshToken);
    }

    internal static IResult TooManyAttempts() =>
        Problem(429, "Çok fazla deneme", TooManyAttemptsMessage, "too_many_attempts");

    internal static IResult Problem(int status, string title, string detail, string code) =>
        Results.Problem(statusCode: status, title: title, detail: detail, extensions: new Dictionary<string, object?> { ["code"] = code });

    internal static bool TryParseClient(string? value, out ClientKind client)
    {
        client = value switch
        {
            "web" => ClientKind.Web,
            "mobile" => ClientKind.Mobile,
            _ => default,
        };
        return client != default;
    }

    internal static Guid ClaimGuid(ClaimsPrincipal user, string type) =>
        Guid.Parse(user.FindFirst(type)?.Value ?? throw new InvalidOperationException($"Claim '{type}' is missing."));
}

public sealed record LoginRequest(string? InstitutionCode, string? Email, string? Password, string? Client);

public sealed record RefreshRequest(string? RefreshToken);

/// <summary>Spec 0004: <see cref="Next"/> is "verify" (enter a code) or "setup" (set 2FA up first). Not a session.</summary>
public sealed record LoginStepResponse(string Next, string Step, DateTimeOffset StepExpiresAt);

public sealed record LoginStepRequest(string? Step);

/// <summary>A 6-digit <see cref="Code"/>, or a <see cref="RecoveryCode"/> instead (verify only).</summary>
public sealed record VerifyLoginRequest(string? Step, string? Code, string? RecoveryCode);

/// <summary>The key as typed into an authenticator app, and the same as a QR code (PNG data URI).</summary>
public sealed record TwoFactorSetupResponse(string Key, string QrCode);

/// <summary>The session, and the 10 kurtarma kodları — shown this once, never again (Requirement 3).</summary>
public sealed record SetupCompletedResponse(SessionResponse Session, IReadOnlyList<string> RecoveryCodes);

public sealed record TwoFactorStatusResponse(bool Enabled, bool Required, bool CanDisable, int RemainingRecoveryCodes);

/// <summary>Turning 2FA on while signed in: the setup step, the key and its QR code.</summary>
public sealed record TwoFactorEnableResponse(string Step, string Key, string QrCode);

public sealed record TwoFactorCodeRequest(string? Code);

public sealed record RecoveryCodesResponse(IReadOnlyList<string> RecoveryCodes);

public sealed record ChangePasswordRequest(string? CurrentPassword, string? NewPassword, string? NewPasswordAgain);

/// <summary>Web: <see cref="AccessToken"/> and <see cref="RefreshToken"/> are null — they are httpOnly cookies.</summary>
public sealed record SessionResponse(
    IReadOnlyList<string> Roles, DateTimeOffset AccessExpiresAt, DateTimeOffset SessionExpiresAt,
    string? AccessToken, string? RefreshToken);

public sealed record SessionInfo(IReadOnlyList<string> Roles, DateTimeOffset SessionExpiresAt);
