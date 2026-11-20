using NArchitecture.Core.Mediator;
using Okul.Modules.Enrollment.Application;

namespace Okul.WebApi.Hosting;

/// <summary>
/// For a veli who lost the telefon (spec 0007, Requirement 10):
/// <c>Okul.WebApi reset-veli --kurum-kodu ATATURK-IST --telefon "0532 123 45 67"</c>. Access to the server is the
/// authorisation (as reset-2fa); nothing secret is printed — only that it was reset and how many sessions closed.
/// </summary>
public static class ResetGuardianCommand
{
    public const string Name = "reset-veli";

    public static async Task<int> RunAsync(IServiceProvider services, string[] args, TextWriter output)
    {
        var code = Option(args, "--kurum-kodu");
        var phone = Option(args, "--telefon");
        if (code is null || phone is null)
        {
            await output.WriteLineAsync("Kullanım: reset-veli --kurum-kodu <kod> --telefon <numara>");
            return 2;
        }

        await using var scope = services.CreateAsyncScope();
        var result = await scope.ServiceProvider.GetRequiredService<ISender>()
            .Send(new Modules.Enrollment.Application.ResetGuardianCommand(code, phone));
        if (result.Status == ResetGuardianStatus.NotFound)
        {
            await output.WriteLineAsync("Bu kurumda bu telefonla kayıtlı bir veli yok.");
            return 1;
        }

        await output.WriteLineAsync($"Veli girişi sıfırlandı. Kapanan oturum: {result.ClosedSessions}.");
        return 0;
    }

    private static string? Option(string[] args, string name)
    {
        var index = Array.IndexOf(args, name);
        return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
    }
}
