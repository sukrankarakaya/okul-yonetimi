using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Npgsql.EntityFrameworkCore.PostgreSQL.Infrastructure;
using Okul.Core.Tenancy;
using Okul.Modules.School.Application;
using Okul.Modules.School.Infrastructure.Persistence;

namespace Okul.Modules.School.Infrastructure;

public static class SchoolInfrastructure
{
    public static IServiceCollection AddSchoolInfrastructure(this IServiceCollection services, string connectionString)
    {
        services.AddDbContext<SchoolDbContext>(options => options.UseNpgsql(connectionString, ConfigureNpgsql));
        services.AddScoped<ISchoolService, SchoolService>();
        services.AddSingleton<IMessageDrafter, MessageDrafter>();
        return services;
    }

    internal static void ConfigureNpgsql(NpgsqlDbContextOptionsBuilder npgsql) =>
        npgsql.MigrationsHistoryTable(HistoryRepository.DefaultTableName, SchoolDbContext.Schema);
}

/// <summary>Used only by <c>dotnet ef migrations</c>; never connects.</summary>
internal sealed class SchoolDesignTimeDbContextFactory : IDesignTimeDbContextFactory<SchoolDbContext>
{
    public SchoolDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<SchoolDbContext>()
            .UseNpgsql("Host=design-time-only", SchoolInfrastructure.ConfigureNpgsql)
            .Options;
        return new SchoolDbContext(options, new NoTenant());
    }

    private sealed class NoTenant : ITenantContext
    {
        public Guid? TenantId => null;
    }
}
