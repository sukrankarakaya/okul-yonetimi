using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Npgsql.EntityFrameworkCore.PostgreSQL.Infrastructure;
using Okul.Modules.Identity.Application;
using Okul.Modules.Identity.Contracts;
using Okul.Modules.Identity.Infrastructure.Persistence;

namespace Okul.Modules.Identity.Infrastructure;

public static class IdentityInfrastructure
{
    public static IServiceCollection AddIdentityInfrastructure(this IServiceCollection services, string connectionString)
    {
        services.AddDbContext<IdentityDbContext>(options => options.UseNpgsql(connectionString, ConfigureNpgsql));
        services.AddDbContext<LoginThrottleDbContext>(options => options.UseNpgsql(connectionString, ConfigureThrottleNpgsql));
        services.AddScoped<IStaffAccountStore, StaffAccountStore>();
        services.AddScoped<ISessionStore, SessionStore>();
        services.AddScoped<ILoginThrottleStore, LoginThrottleStore>();
        services.AddScoped<ISessionValidator, SessionValidator>();
        services.AddSingleton<IPasswordHasher, StaffPasswordHasher>();
        return services;
    }

    internal static void ConfigureNpgsql(NpgsqlDbContextOptionsBuilder npgsql) =>
        npgsql.MigrationsHistoryTable(HistoryRepository.DefaultTableName, IdentityDbContext.Schema);

    internal static void ConfigureThrottleNpgsql(NpgsqlDbContextOptionsBuilder npgsql) =>
        npgsql.MigrationsHistoryTable(LoginThrottleDbContext.HistoryTable, IdentityDbContext.Schema);
}
