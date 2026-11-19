using System.Security.Cryptography;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Hosting.Internal;
using Okul.WebApi.Auth;
using Shouldly;

namespace Okul.WebApi.UnitTests.Auth;

/// <summary>Sensitive area: Kimlik ve yetki — the signing key never falls back outside Development/Testing.</summary>
public sealed class TokenSettingsTests
{
    [Theory]
    [InlineData("Production")]
    [InlineData("Staging")]
    public void MissingKey_OutsideDevelopmentAndTesting_RefusesToStart(string environment)
    {
        Should.Throw<InvalidOperationException>(() => TokenSettings.FromConfiguration(Config(key: null), Env(environment)))
            .Message.ShouldContain("Auth:SigningKey");
    }

    [Fact]
    public void ShortKey_IsRejected_InEveryEnvironment()
    {
        var shortKey = Convert.ToBase64String(RandomNumberGenerator.GetBytes(16));

        Should.Throw<InvalidOperationException>(() => TokenSettings.FromConfiguration(Config(shortKey), Env("Testing")));
    }

    [Theory]
    [InlineData("Development")]
    [InlineData("Testing")]
    public void MissingKey_InDevelopmentAndTesting_GetsEphemeralKey(string environment)
    {
        var first = TokenSettings.FromConfiguration(Config(key: null), Env(environment));
        var second = TokenSettings.FromConfiguration(Config(key: null), Env(environment));

        first.SigningKey.Key.Length.ShouldBe(32);
        first.SigningKey.Key.ShouldNotBe(second.SigningKey.Key);
    }

    [Fact]
    public void ConfiguredKey_IsUsed()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);

        var settings = TokenSettings.FromConfiguration(Config(Convert.ToBase64String(bytes)), Env("Production"));

        settings.SigningKey.Key.ShouldBe(bytes);
    }

    private static IConfiguration Config(string? key) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(key is null ? [] : [new KeyValuePair<string, string?>("Auth:SigningKey", key)])
            .Build();

    private static IHostEnvironment Env(string name) => new HostingEnvironment { EnvironmentName = name };
}
