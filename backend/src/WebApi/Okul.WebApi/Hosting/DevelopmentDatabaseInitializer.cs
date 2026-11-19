using Microsoft.EntityFrameworkCore;
using Okul.Modules.Institution.Infrastructure.Persistence;

namespace Okul.WebApi.Hosting;

/// <summary>
/// Development only: applies migrations and writes the sample kurums. A hosted service (not code
/// before <c>app.Run</c>) so build-time OpenAPI generation never touches a database.
/// </summary>
internal sealed class DevelopmentDatabaseInitializer : IHostedService
{
    private readonly IServiceScopeFactory _scopeFactory;

    public DevelopmentDatabaseInitializer(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<InstitutionDbContext>();
        await db.Database.MigrateAsync(cancellationToken);
        await InstitutionSampleData.SeedAsync(db, cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
