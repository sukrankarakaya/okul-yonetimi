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

    public const string InvalidCredentialsMessage = "Kurum kodu, e-posta veya şifre hatalı.";
    public const string TooManyAttemptsMessage = "Çok fazla başarısız deneme. 15 dakika sonra tekrar deneyin.";
    public const string SessionEndedMessage = "Oturumunuzun süresi doldu, tekrar giriş yapın.";
    public const string CurrentPasswordWrongMessage = "Mevcut şifre hatalı.";
    public const string PasswordTooShortMessage = "Yeni şifre en az 10 karakter olmalı.";
    public const string PasswordMismatchMessage = "Yeni şifreler eşleşmiyor.";

    public static void MapAuthEndpoints(this IEndpointRouteBuilder api)
    {
        var auth = api.MapGroup("/auth").WithTags("Auth");

        auth.MapPost("/login", LoginAsync)
            .AllowAnonymous()
            .RequireRateLimiting(LoginRateLimitPolicy)
            .WithName("Login")
            .Produces<SessionResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status429TooManyRequests);

        auth.MapPost("/refresh", RefreshAsync)
            .AllowAnonymous()
            .WithName("RefreshSession")
            .Produces<SessionResponse>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status409Conflict);

        auth.MapPost("/logout", LogoutAsync)
            .WithName("Logout")
            .Produces(StatusCodes.Status204NoContent);

        auth.MapGet("/session", GetSession)
            .WithName("GetSession")
            .Produces<SessionInfo>();

        auth.MapPost("/password", ChangePasswordAsync)
            .WithName("ChangePassword")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status400BadRequest);
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
            LoginStatus.Locked => TooManyAttempts(),
            _ => Problem(401, "Giriş başarısız", InvalidCredentialsMessage, "invalid_credentials"),
        };
    }

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
            _ => Problem(400, "Şifre değiştirilemedi", PasswordMismatchMessage, "password_mismatch"),
        };
    }

    /// <summary>Web: both credentials as httpOnly cookies, none in the body (AC-8). Mobile: in the body, for SecureStore.</summary>
    private static IResult Issue(IssuedSession session, TokenIssuer issuer, HttpResponse response)
    {
        var (access, accessExpiresAt) = issuer.Issue(session.AccountId, session.TenantId, session.SessionId, session.Roles, session.ExpiresAt);
        if (session.Client == ClientKind.Web)
        {
            SessionCookies.Write(response, access, accessExpiresAt, session.RefreshToken, session.ExpiresAt);
            return Results.Ok(new SessionResponse(session.Roles, accessExpiresAt, session.ExpiresAt, null, null));
        }

        return Results.Ok(new SessionResponse(session.Roles, accessExpiresAt, session.ExpiresAt, access, session.RefreshToken));
    }

    private static IResult TooManyAttempts() =>
        Problem(429, "Çok fazla deneme", TooManyAttemptsMessage, "too_many_attempts");

    private static IResult Problem(int status, string title, string detail, string code) =>
        Results.Problem(statusCode: status, title: title, detail: detail, extensions: new Dictionary<string, object?> { ["code"] = code });

    private static bool TryParseClient(string? value, out ClientKind client)
    {
        client = value switch
        {
            "web" => ClientKind.Web,
            "mobile" => ClientKind.Mobile,
            _ => default,
        };
        return client != default;
    }

    private static Guid ClaimGuid(ClaimsPrincipal user, string type) =>
        Guid.Parse(user.FindFirst(type)?.Value ?? throw new InvalidOperationException($"Claim '{type}' is missing."));
}

public sealed record LoginRequest(string? InstitutionCode, string? Email, string? Password, string? Client);

public sealed record RefreshRequest(string? RefreshToken);

public sealed record ChangePasswordRequest(string? CurrentPassword, string? NewPassword, string? NewPasswordAgain);

/// <summary>Web: <see cref="AccessToken"/> and <see cref="RefreshToken"/> are null — they are httpOnly cookies.</summary>
public sealed record SessionResponse(
    IReadOnlyList<string> Roles, DateTimeOffset AccessExpiresAt, DateTimeOffset SessionExpiresAt,
    string? AccessToken, string? RefreshToken);

public sealed record SessionInfo(IReadOnlyList<string> Roles, DateTimeOffset SessionExpiresAt);
