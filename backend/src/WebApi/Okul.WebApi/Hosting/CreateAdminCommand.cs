using Okul.Core.Tenancy;
using Okul.Modules.Identity.Application;
using Okul.Modules.Identity.Domain;
using Okul.Modules.Institution.Contracts;

namespace Okul.WebApi.Hosting;

/// <summary>
/// Production's first administrator (spec 0003, Constraints; plan 0003, R8):
/// <c>Okul.WebApi create-admin --kurum-kodu ATATURK-IST --e-posta yonetici@okul.example</c>.
/// The password is read from standard input — never from the command line, so it stays out of shell history and
/// process lists — and it is never printed. The database must already be migrated.
/// </summary>
public static class CreateAdminCommand
{
    public const string Name = "create-admin";

    public static async Task<int> RunAsync(IServiceProvider services, string[] args, TextReader input, TextWriter output)
    {
        var code = Option(args, "--kurum-kodu");
        var email = EmailAddress.Normalize(Option(args, "--e-posta"));
        if (code is null || email is null)
        {
            await output.WriteLineAsync("Kullanım: create-admin --kurum-kodu <kod> --e-posta <adres>  (şifre standart girdiden)");
            return 2;
        }

        var password = await input.ReadLineAsync();
        if (!PasswordPolicy.IsAcceptable(password))
        {
            await output.WriteLineAsync($"Şifre en az {PasswordPolicy.MinLength} karakter olmalı.");
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
            var accounts = provider.GetRequiredService<IStaffAccountStore>();
            if (await accounts.FindByEmailAsync(email, CancellationToken.None) is not null)
            {
                await output.WriteLineAsync("Bu kurumda bu e-posta ile bir hesap zaten var.");
                return 1;
            }

            var hash = provider.GetRequiredService<IPasswordHasher>().Hash(password!);
            await accounts.AddAsync(
                StaffAccount.Create(Guid.CreateVersion7(), tenantId.Value, email, hash, [StaffRoles.Administrator]),
                CancellationToken.None);
            await accounts.SaveChangesAsync(CancellationToken.None);
        }

        await output.WriteLineAsync("Yönetici hesabı oluşturuldu.");
        return 0;
    }

    private static string? Option(string[] args, string name)
    {
        var index = Array.IndexOf(args, name);
        return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
    }
}
