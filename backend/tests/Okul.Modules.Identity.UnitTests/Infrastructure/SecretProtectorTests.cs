using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.DependencyInjection;
using Okul.Modules.Identity.Application;
using Okul.Modules.Identity.Domain;
using Okul.Modules.Identity.Infrastructure;
using Shouldly;

namespace Okul.Modules.Identity.UnitTests.Infrastructure;

/// <summary>
/// Spec 0004, Requirement 10 / AC-11 — the secret is stored encrypted and the recovery codes as keyed hashes
/// (plan 0004, R2–R3). Resolved through the module's registration; nothing connects to a database.
/// </summary>
public sealed class SecretProtectorTests
{
    [Fact]
    public void Secret_RoundTrips_ButIsNeverReadableAtRest()
    {
        var protector = Resolve<ITwoFactorSecretProtector>(Key());
        var secret = Totp.NewSecret();

        var stored = protector.Protect(secret);

        protector.Unprotect(stored).ShouldBe(secret);
        stored.ShouldStartWith("v1:");
        stored.ShouldNotContain(Totp.ToBase32(secret));
        stored.ShouldNotContain(Convert.ToBase64String(secret));
        protector.Protect(secret).ShouldNotBe(stored); // a fresh nonce every time
    }

    [Fact]
    public void AnotherKey_OrTamperedData_DoesNotDecrypt()
    {
        var key = Key();
        var stored = Resolve<ITwoFactorSecretProtector>(key).Protect(Totp.NewSecret());
        var bytes = Convert.FromBase64String(stored[3..]);
        bytes[^1] ^= 1;

        Should.Throw<CryptographicException>(() => Resolve<ITwoFactorSecretProtector>(Key()).Unprotect(stored));
        Should.Throw<CryptographicException>(() =>
            Resolve<ITwoFactorSecretProtector>(key).Unprotect("v1:" + Convert.ToBase64String(bytes)));
        Should.Throw<CryptographicException>(() => Resolve<ITwoFactorSecretProtector>(key).Unprotect("v2:AAAA"));
    }

    [Fact]
    public void RecoveryCodeHash_IsKeyed_AndNotThePlainCode()
    {
        var key = Key();
        var hasher = Resolve<IRecoveryCodeHasher>(key);

        var hash = hasher.Hash("K7Q29XMD");

        hash.ShouldBe(hasher.Hash("K7Q29XMD"));
        hash.ShouldNotContain("K7Q29XMD");
        hash.ShouldNotBe(Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes("K7Q29XMD"))));
        Resolve<IRecoveryCodeHasher>(Key()).Hash("K7Q29XMD").ShouldNotBe(hash);
    }

    [Fact]
    public void A_KeyOfTheWrongLength_IsRefused()
    {
        Should.Throw<InvalidOperationException>(() => new TwoFactorKey(new byte[16]));
    }

    private static TwoFactorKey Key() => new(RandomNumberGenerator.GetBytes(TwoFactorKey.Length));

    private static T Resolve<T>(TwoFactorKey key)
        where T : notnull =>
        new ServiceCollection().AddIdentityInfrastructure("Host=never-connected", key).BuildServiceProvider()
            .GetRequiredService<T>();
}
