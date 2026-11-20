using Okul.Modules.Enrollment.Domain;
using Shouldly;

namespace Okul.Modules.Enrollment.UnitTests.Domain;

/// <summary>Spec 0007 — Requirement 6 (K6), AC-4: a rıza covers exactly one aydınlatma metni version.</summary>
public sealed class KvkkConsentTests
{
    [Fact]
    public void Consent_RecordsGuardianKurumVersionAndTime_AndCoversOnlyThatVersion()
    {
        var tenant = Guid.CreateVersion7();
        var guardian = Guid.CreateVersion7();
        var at = new DateTimeOffset(2026, 11, 20, 9, 0, 0, TimeSpan.Zero);

        var consent = KvkkConsent.Accept(Guid.CreateVersion7(), tenant, guardian, "taslak", at);

        consent.TenantId.ShouldBe(tenant);
        consent.GuardianId.ShouldBe(guardian);
        consent.TextVersion.ShouldBe("taslak");
        consent.AcceptedAt.ShouldBe(at);
        consent.Covers("taslak").ShouldBeTrue();
        consent.Covers("2026-12").ShouldBeFalse();
        consent.Covers("TASLAK").ShouldBeFalse();
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    public void Consent_NeedsAVersion(string version)
    {
        Should.Throw<ArgumentException>(() =>
            KvkkConsent.Accept(Guid.CreateVersion7(), Guid.CreateVersion7(), Guid.CreateVersion7(), version, DateTimeOffset.UnixEpoch));
    }

    [Fact]
    public void Consent_NeedsKurumAndGuardian()
    {
        Should.Throw<ArgumentException>(() =>
            KvkkConsent.Accept(Guid.CreateVersion7(), Guid.Empty, Guid.CreateVersion7(), "taslak", DateTimeOffset.UnixEpoch));
        Should.Throw<ArgumentException>(() =>
            KvkkConsent.Accept(Guid.CreateVersion7(), Guid.CreateVersion7(), Guid.Empty, "taslak", DateTimeOffset.UnixEpoch));
    }
}
