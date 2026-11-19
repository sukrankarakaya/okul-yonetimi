using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Okul.Modules.Institution.Infrastructure.Persistence;
using Okul.WebApi.DevAuth;
using Shouldly;
using Testcontainers.PostgreSql;

[assembly: AssemblyFixture(typeof(Okul.WebApi.IntegrationTests.Infrastructure.ApiFixture))]

namespace Okul.WebApi.IntegrationTests.Infrastructure;

/// <summary>
/// One fresh PostgreSQL 18 container per test run, disposed at the end — no data reaches the next
/// run (docs/testing.md, Resource hygiene 5). Seeded with the two sample kurums of spec 0001.
/// </summary>
public sealed class ApiFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _database = new PostgreSqlBuilder("postgres:18").Build();

    public byte[] SigningKey { get; } = RandomNumberGenerator.GetBytes(32);

    public OkulApiFactory Testing { get; private set; } = null!;

    public string ConnectionString => _database.GetConnectionString();

    public async ValueTask InitializeAsync()
    {
        await _database.StartAsync();
        Testing = CreateFactory(DevAuthEndpoints.TestingEnvironment);

        await using var scope = Testing.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<InstitutionDbContext>();
        await db.Database.MigrateAsync();
        await InstitutionSampleData.SeedAsync(db);
    }

    public OkulApiFactory CreateFactory(string environment) =>
        new(environment, ConnectionString, Convert.ToBase64String(SigningKey));

    /// <summary>A client signed in through the Testing-only test login (the path the apps use).</summary>
    public async Task<HttpClient> ClientForAsync(Guid tenantId, string role)
    {
        var client = Testing.CreateClient();
        var response = await client.PostAsJsonAsync(DevAuthEndpoints.Route, new TestLoginRequest(tenantId, role));
        response.EnsureSuccessStatusCode();
        var login = await response.Content.ReadFromJsonAsync<TestLoginResponse>();
        login.ShouldNotBeNull();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", login.AccessToken);
        return client;
    }

    public async ValueTask DisposeAsync()
    {
        if (Testing is not null)
        {
            await Testing.DisposeAsync();
        }

        await _database.DisposeAsync();
    }
}

public sealed class OkulApiFactory : WebApplicationFactory<Program>
{
    private readonly string _environment;
    private readonly string _connectionString;
    private readonly string _signingKey;

    public OkulApiFactory(string environment, string connectionString, string signingKey)
    {
        _environment = environment;
        _connectionString = connectionString;
        _signingKey = signingKey;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment(_environment);
        builder.UseSetting("ConnectionStrings:Okul", _connectionString);
        builder.UseSetting("Auth:SigningKey", _signingKey);
    }
}
