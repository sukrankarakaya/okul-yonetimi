using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Npgsql.EntityFrameworkCore.PostgreSQL.Infrastructure;
using Okul.Modules.Enrollment.Application;
using Okul.Modules.Enrollment.Contracts;
using Okul.Modules.Enrollment.Infrastructure.Persistence;
using Okul.Modules.Enrollment.Infrastructure.Sms;

namespace Okul.Modules.Enrollment.Infrastructure;

public static class EnrollmentInfrastructure
{
    /// <param name="netgsm">Null: the fake sender (Development and Testing only — the API decides, plan 0007 step 6).</param>
    public static IServiceCollection AddEnrollmentInfrastructure(
        this IServiceCollection services, string connectionString, KvkkText kvkkText, GuardianLoginKey loginKey,
        NetgsmOptions? netgsm, GuardianLoginTiming? timing = null)
    {
        services.AddSingleton(timing ?? GuardianLoginTiming.Default);
        services.AddDbContext<EnrollmentDbContext>(options => options.UseNpgsql(connectionString, ConfigureNpgsql));
        services.AddDbContext<GuardianLoginDbContext>(options => options.UseNpgsql(connectionString, ConfigureLoginNpgsql));
        services.AddScoped<IGuardianStore, GuardianStore>();
        services.AddScoped<IKvkkConsentStore, KvkkConsentStore>();
        services.AddScoped<IGuardianLoginStore, GuardianLoginStore>();
        services.AddScoped<IGuardianDirectory, GuardianDirectory>();
        services.AddSingleton(kvkkText);
        services.AddSingleton(loginKey);
        services.AddSingleton<IGuardianLoginHasher, GuardianCodeHasher>();

        if (netgsm is null)
        {
            services.AddSingleton<FakeSmsSender>();
            services.AddSingleton<ISmsSender>(provider => provider.GetRequiredService<FakeSmsSender>());
        }
        else
        {
            services.AddSingleton(netgsm);
            services.AddSingleton<ISmsSender>(provider => new NetgsmSmsSender(
                new HttpClient { Timeout = Timeout.InfiniteTimeSpan }, provider.GetRequiredService<NetgsmOptions>()));
        }

        return services;
    }

    internal static void ConfigureNpgsql(NpgsqlDbContextOptionsBuilder npgsql) =>
        npgsql.MigrationsHistoryTable(HistoryRepository.DefaultTableName, EnrollmentDbContext.Schema);

    internal static void ConfigureLoginNpgsql(NpgsqlDbContextOptionsBuilder npgsql) =>
        npgsql.MigrationsHistoryTable(GuardianLoginDbContext.HistoryTable, EnrollmentDbContext.Schema);
}
