using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Npgsql.EntityFrameworkCore.PostgreSQL.Infrastructure;
using Okul.Modules.Enrollment.Application;
using Okul.Modules.Enrollment.Contracts;
using Okul.Modules.Enrollment.Infrastructure.Persistence;

namespace Okul.Modules.Enrollment.Infrastructure;

public static class EnrollmentInfrastructure
{
    public static IServiceCollection AddEnrollmentInfrastructure(
        this IServiceCollection services, string connectionString, KvkkText kvkkText)
    {
        services.AddDbContext<EnrollmentDbContext>(options => options.UseNpgsql(connectionString, ConfigureNpgsql));
        services.AddDbContext<GuardianLoginDbContext>(options => options.UseNpgsql(connectionString, ConfigureLoginNpgsql));
        services.AddScoped<IGuardianStore, GuardianStore>();
        services.AddScoped<IKvkkConsentStore, KvkkConsentStore>();
        services.AddScoped<IGuardianLoginStore, GuardianLoginStore>();
        services.AddScoped<IGuardianDirectory, GuardianDirectory>();
        services.AddSingleton(kvkkText);
        return services;
    }

    internal static void ConfigureNpgsql(NpgsqlDbContextOptionsBuilder npgsql) =>
        npgsql.MigrationsHistoryTable(HistoryRepository.DefaultTableName, EnrollmentDbContext.Schema);

    internal static void ConfigureLoginNpgsql(NpgsqlDbContextOptionsBuilder npgsql) =>
        npgsql.MigrationsHistoryTable(GuardianLoginDbContext.HistoryTable, EnrollmentDbContext.Schema);
}
