using NArchitecture.Core.Mediator;
using Okul.Core.Tenancy;
using Okul.Modules.Identity.Application;
using Okul.Modules.Identity.Domain;
using Okul.Modules.Institution.Contracts;

namespace Okul.WebApi.Hosting;

/// <summary>
/// For a user who lost both the phone and the kurtarma kodları (spec 0004, Requirement 9):
/// <c>Okul.WebApi reset-2fa --kurum-kodu ATATURK-IST --e-posta yonetici@okul.example</c>. Access to the server is the
/// authorisation; no secret is asked or printed — only that it was reset and how many sessions closed (self-critique G6).
/// </summary>
public static class ResetTwoFactorCommand
{
    public const string Name = "reset-2fa";

    public static async Task<int> RunAsync(IServiceProvider services, string[] args, TextWriter output)
    {
        var code = Option(args, "--kurum-kodu");
        var email = EmailAddress.Normalize(Option(args, "--e-posta"));
        if (code is null || email is null)
        {
            await output.WriteLineAsync("Kullanım: reset-2fa --kurum-kodu <kod> --e-posta <adres>");
            return 2;
        }

        await using var scope = services.CreateAsyncScope();
        var provider = scope.ServiceProvider;
        var tenantId = await provider.GetRequiredService<IInstitutionDirectory>().FindIdByCodeAsync(code);
        if (tenantId is null)
        {
            await output.WriteLineAsync("Bu kurum kodu bulunamadı.");
            return 1;
        }

        using (provider.GetRequiredService<ITenantScope>().Begin(tenantId.Value))
        {
            var account = await provider.GetRequiredService<IStaffAccountStore>().FindByEmailAsync(email, CancellationToken.None);
            if (account is null)
            {
                await output.WriteLineAsync("Bu kurumda bu e-posta ile bir hesap yok.");
                return 1;
            }

            var closed = await provider.GetRequiredService<ISender>().Send(new ResetAccountTwoFactorCommand(account.Id));
            await output.WriteLineAsync($"İki adımlı doğrulama sıfırlandı. Kapanan oturum: {closed}.");
            return 0;
        }
    }

    private static string? Option(string[] args, string name)
    {
        var index = Array.IndexOf(args, name);
        return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
    }
}
