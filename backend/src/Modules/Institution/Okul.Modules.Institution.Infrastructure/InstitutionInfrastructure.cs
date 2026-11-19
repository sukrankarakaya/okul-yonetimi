using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Npgsql.EntityFrameworkCore.PostgreSQL.Infrastructure;
using Okul.Modules.Institution.Application;
using Okul.Modules.Institution.Contracts;
using Okul.Modules.Institution.Infrastructure.Persistence;

namespace Okul.Modules.Institution.Infrastructure;

public static class InstitutionInfrastructure
{
    public static IServiceCollection AddInstitutionInfrastructure(this IServiceCollection services, string connectionString)
    {
        services.AddDbContext<InstitutionDbContext>(options => options.UseNpgsql(connectionString, ConfigureNpgsql));
        services.AddScoped<IInstitutionReadStore, InstitutionReadStore>();
        services.AddScoped<IInstitutionDirectory, InstitutionDirectory>();
        return services;
    }

    internal static void ConfigureNpgsql(NpgsqlDbContextOptionsBuilder npgsql) =>
        npgsql.MigrationsHistoryTable(HistoryRepository.DefaultTableName, InstitutionDbContext.Schema);
}
