using Microsoft.EntityFrameworkCore;
using Okul.Modules.Identity.Infrastructure.Persistence;
using Okul.Modules.Institution.Infrastructure.Persistence;
using Okul.Modules.School.Infrastructure.Persistence;

namespace Okul.WebApi.Hosting;

/// <summary>
/// Development only: applies migrations and writes the sample kurums and — when <c>Dev:SamplePassword</c> is set in
/// user-secrets — the sample staff accounts (spec 0003; plan 0003, R8: no password in the repo, none → no accounts).
/// A hosted service (not code before <c>app.Run</c>) so build-time OpenAPI generation never touches a database.
/// </summary>
internal sealed class DevelopmentDatabaseInitializer : IHostedService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IConfiguration _configuration;

    public DevelopmentDatabaseInitializer(IServiceScopeFactory scopeFactory, IConfiguration configuration)
    {
        _scopeFactory = scopeFactory;
        _configuration = configuration;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        await MigrateAsync(scope.ServiceProvider, cancellationToken);

        var institutions = scope.ServiceProvider.GetRequiredService<InstitutionDbContext>();
        await InstitutionSampleData.SeedAsync(institutions, cancellationToken);

        // Demo okul verisi (AGENTS.md, Demo modu): sınıflar, öğrenciler, yoklama, taksitler, duyurular.
        var today = DateOnly.FromDateTime(TimeProvider.System.GetUtcNow().ToOffset(TimeSpan.FromHours(3)).DateTime);
        await SchoolSampleData.SeedAsync(scope.ServiceProvider.GetRequiredService<SchoolDbContext>(), today, cancellationToken);

        var samplePassword = _configuration["Dev:SamplePassword"];
        if (!string.IsNullOrWhiteSpace(samplePassword))
        {
            await IdentitySampleData.SeedAsync(scope.ServiceProvider.GetRequiredService<IdentityDbContext>(), samplePassword, cancellationToken);
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    /// <summary>Every module context, in dependency order (Kimlik needs Kurum's codes only at run time).</summary>
    public static async Task MigrateAsync(IServiceProvider services, CancellationToken cancellationToken)
    {
        await services.GetRequiredService<InstitutionDbContext>().Database.MigrateAsync(cancellationToken);
        await services.GetRequiredService<IdentityDbContext>().Database.MigrateAsync(cancellationToken);
        await services.GetRequiredService<LoginThrottleDbContext>().Database.MigrateAsync(cancellationToken);
        await services.GetRequiredService<SchoolDbContext>().Database.MigrateAsync(cancellationToken);
    }
}
