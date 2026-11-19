using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using NArchitecture.Core.Mediator.DependencyInjection;
using Okul.Core.Tenancy;
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

// --- Authentication: JWT bearer; the tenant is a claim of the validated token ---
var tokenSettings = TokenSettings.FromConfiguration(builder.Configuration, builder.Environment);
services.AddSingleton(tokenSettings);
services.AddSingleton<TokenIssuer>();
services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.MapInboundClaims = false;
        options.TokenValidationParameters = tokenSettings.ValidationParameters();
    });

// --- Authorization: default deny (docs/security.md) — every endpoint needs a tenant member ---
services.AddAuthorizationBuilder()
    .SetFallbackPolicy(new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .RequireClaim(OkulClaims.TenantId)
        .RequireRole(Roles.All)
        .Build());

services.AddHttpContextAccessor();
services.AddScoped<ITenantContext, HttpTenantContext>();

// --- Modules ---
services.AddMediator(configuration => configuration.RegisterServicesFromAssembly(InstitutionApplicationAssembly.Assembly));
var connectionString = builder.Configuration.GetConnectionString("Okul");
services.AddInstitutionInfrastructure(connectionString ?? string.Empty);

// Development with a configured database only (user-secrets); build-time OpenAPI generation has none.
if (builder.Environment.IsDevelopment() && !string.IsNullOrWhiteSpace(connectionString))
{
    services.AddHostedService<DevelopmentDatabaseInitializer>();
}

// --- Clients (web panel, Expo web) ---
var corsOrigins = builder.Configuration.GetSection("Cors:Origins").Get<string[]>() ?? [];
services.AddCors(options => options.AddDefaultPolicy(policy =>
    policy.WithOrigins(corsOrigins).AllowAnyHeader().WithMethods("GET", "POST")));

services.AddOpenApi();

var app = builder.Build();

app.UseExceptionHandler();
app.UseOkulSecurityHeaders();
app.UseCors();
app.UseAuthentication();
app.UseAuthorization();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi().AllowAnonymous();
}

var api = app.MapGroup("/api/v1");
api.MapInstitutionEndpoints();
app.MapDevAuth(app.Environment);

app.Run();

/// <summary>Entry point marker for WebApplicationFactory (integration tests).</summary>
public partial class Program;
