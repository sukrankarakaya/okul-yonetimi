using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using NArchitecture.Core.Mediator.DependencyInjection;
using Okul.Core.Tenancy;
using Okul.Modules.Enrollment.Infrastructure;
using Okul.Modules.Identity.Application;
using Okul.Modules.Identity.Contracts;
using Okul.Modules.Identity.Infrastructure;
using Okul.Modules.Institution.Application;
using Okul.Modules.Institution.Infrastructure;
using Okul.WebApi.Auth;
using Okul.WebApi.DevAuth;
using Okul.WebApi.Endpoints;
using Okul.WebApi.Errors;
using Okul.WebApi.Hosting;

var builder = WebApplication.CreateBuilder(args);
var services = builder.Services;

// --- Time, errors ---
services.AddSingleton(TimeProvider.System);
services.AddProblemDetails();
services.AddExceptionHandler<OkulExceptionHandler>();

// --- Authentication: JWT — bearer header (mobile) or httpOnly cookie (web); the tenant is a claim of the validated
// token, and the session behind it must still be open on EVERY request (spec 0003, G5) ---
var tokenSettings = TokenSettings.FromConfiguration(builder.Configuration, builder.Environment);
services.AddSingleton(tokenSettings);
services.AddSingleton<TokenIssuer>();
services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();
services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
    .Configure<TimeProvider>((options, time) =>
    {
        options.MapInboundClaims = false;
        options.TokenValidationParameters = tokenSettings.ValidationParameters(time);
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                // Login and refresh never authenticate (plan 0003, A-2(b)): a still-valid cookie of kurum A must not
                // decide which kurum a login for kurum kodu B searches in (review round 1 #2).
                if (AuthEndpoints.IsPreSessionPath(context.Request.Path))
                {
                    context.NoResult();
                    return Task.CompletedTask;
                }

                if (string.IsNullOrEmpty(context.Request.Headers.Authorization)
                    && context.Request.Cookies.TryGetValue(SessionCookies.Access, out var cookie))
                {
                    context.Token = cookie;
                }

                return Task.CompletedTask;
            },
            OnTokenValidated = async context =>
            {
                var principal = context.Principal!;
                var open = Guid.TryParse(principal.FindFirst(OkulClaims.TenantId)?.Value, out var tenantId)
                    && Guid.TryParse(principal.FindFirst(OkulClaims.Session)?.Value, out var sessionId)
                    && await context.HttpContext.RequestServices.GetRequiredService<ISessionValidator>()
                        .IsActiveAsync(tenantId, sessionId, context.HttpContext.RequestAborted);
                if (!open)
                {
                    context.Fail("The session is closed or has ended.");
                }
            },
        };
    });

// --- Authorization: default deny (docs/security.md) — every endpoint needs a tenant member ---
services.AddAuthorizationBuilder()
    .SetFallbackPolicy(new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .RequireClaim(OkulClaims.TenantId)
        .RequireRole(Roles.All)
        .Build());

services.AddHttpContextAccessor();
services.AddScoped<HttpTenantContext>();
services.AddScoped<ITenantContext>(provider => provider.GetRequiredService<HttpTenantContext>());
services.AddScoped<ITenantScope>(provider => provider.GetRequiredService<HttpTenantContext>());
services.AddLoginRateLimit(builder.Configuration);

// --- Modules ---
services.AddMediator(configuration => configuration.RegisterServicesFromAssemblies(
    InstitutionApplicationAssembly.Assembly, IdentityApplicationAssembly.Assembly));
var connectionString = builder.Configuration.GetConnectionString("Okul");
services.AddInstitutionInfrastructure(connectionString ?? string.Empty);
services.AddEnrollmentInfrastructure(connectionString ?? string.Empty);
services.AddIdentityInfrastructure(
    connectionString ?? string.Empty, TwoFactorKeyFromConfiguration(builder.Configuration, builder.Environment));

// Development with a configured database only (user-secrets); build-time OpenAPI generation has none.
if (builder.Environment.IsDevelopment() && !string.IsNullOrWhiteSpace(connectionString))
{
    services.AddHostedService<DevelopmentDatabaseInitializer>();
}

// --- Clients (web panel, Expo web): credentials (session cookies) only for known origins ---
var corsOrigins = builder.Configuration.GetSection("Cors:Origins").Get<string[]>() ?? [];
services.AddCors(options => options.AddDefaultPolicy(policy =>
    policy.WithOrigins(corsOrigins).AllowAnyHeader().WithMethods("GET", "POST").AllowCredentials()));

services.AddOpenApi();

var app = builder.Build();

if (args.FirstOrDefault() == CreateAdminCommand.Name)
{
    return await CreateAdminCommand.RunAsync(app.Services, args, Console.In, Console.Out);
}

if (args.FirstOrDefault() == ResetTwoFactorCommand.Name)
{
    return await ResetTwoFactorCommand.RunAsync(app.Services, args, Console.Out);
}

app.UseExceptionHandler();
app.UseOkulSecurityHeaders();
app.UseCors();
app.UseMiddleware<CsrfGuard>();
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi().AllowAnonymous();
}

var api = app.MapGroup("/api/v1");
api.MapInstitutionEndpoints();
api.MapAuthEndpoints();
app.MapDevAuth(app.Environment);

await app.RunAsync();
return 0;

/// <summary>
/// <c>Auth:TwoFactorKey</c> (base64, 32 bytes — spec 0004, plan R2), like <c>Auth:SigningKey</c>. Missing: Development and
/// Testing get an ephemeral key (2FA set up there does not survive a restart); any other environment refuses to start —
/// a lost key would lock every mandatory account out.
/// </summary>
static TwoFactorKey TwoFactorKeyFromConfiguration(IConfiguration configuration, IHostEnvironment environment)
{
    var configured = configuration["Auth:TwoFactorKey"];
    if (!string.IsNullOrWhiteSpace(configured))
    {
        return new TwoFactorKey(Convert.FromBase64String(configured));
    }

    if (DevAuthEndpoints.IsAllowedIn(environment))
    {
        return new TwoFactorKey(System.Security.Cryptography.RandomNumberGenerator.GetBytes(TwoFactorKey.Length));
    }

    throw new InvalidOperationException("Auth:TwoFactorKey is not configured.");
}

/// <summary>Entry point marker for WebApplicationFactory (integration tests).</summary>
public partial class Program;
