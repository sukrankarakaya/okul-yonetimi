using Shouldly;

namespace Okul.Modules.Enrollment.UnitTests.Application;

/// <summary>Spec 0007 — Requirement 6 (K6), AC-4: rıza for the current text version; a new version asks again.</summary>
public sealed class GuardianConsentTests
{
    private readonly LoginWorld _w = new();

    [Fact]
    public async Task WithoutConsent_TheStatusSaysSo_AndShowsTheText()
    {
        var status = await _w.ConsentStatus(_w.Ayse);

        status.Accepted.ShouldBeFalse();
        status.TextVersion.ShouldBe("taslak");
        status.Text.ShouldBe("metin");
        (await _w.Directory.HasCurrentConsentAsync(LoginWorld.TenantA, _w.Ayse.Id, _w.Ct)).ShouldBeFalse();
    }

    [Fact]
    public async Task Accepting_RecordsVeliKurumVersionAndTime_Once()
    {
        await _w.Accept(_w.Ayse);
        await _w.Accept(_w.Ayse);

        var consent = _w.Consents.All.ShouldHaveSingleItem();
        consent.GuardianId.ShouldBe(_w.Ayse.Id);
        consent.TenantId.ShouldBe(LoginWorld.TenantA);
        consent.TextVersion.ShouldBe("taslak");
        consent.AcceptedAt.ShouldBe(_w.Clock.GetUtcNow());
        (await _w.ConsentStatus(_w.Ayse)).Accepted.ShouldBeTrue();
        (await _w.Directory.HasCurrentConsentAsync(LoginWorld.TenantA, _w.Ayse.Id, _w.Ct)).ShouldBeTrue();
    }

    [Fact]
    public async Task ANewTextVersion_AsksAgain_AndKeepsTheOldRecord()
    {
        await _w.Accept(_w.Ayse);
        _w.ChangeTextVersion("2026-12");

        (await _w.ConsentStatus(_w.Ayse)).Accepted.ShouldBeFalse();
        (await _w.Directory.HasCurrentConsentAsync(LoginWorld.TenantA, _w.Ayse.Id, _w.Ct)).ShouldBeFalse();

        await _w.Accept(_w.Ayse);
        _w.Consents.All.Select(c => c.TextVersion).ShouldBe(["taslak", "2026-12"]);
    }

    [Fact]
    public async Task ConsentInOneKurum_DoesNotCoverTheSameTelefonInAnother()
    {
        await _w.Accept(_w.Ayse);

        (await _w.ConsentStatus(_w.AyseInB)).Accepted.ShouldBeFalse();
    }
}
