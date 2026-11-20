using Okul.Modules.Identity.Domain;
using Shouldly;

namespace Okul.Modules.Identity.UnitTests.Domain;

/// <summary>Spec 0007 — Requirement 7 (K4), AC-5: a veli session is a session with the staff rules and its own kind.</summary>
public sealed class SessionKindTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 20, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public void StaffSession_IsTheDefaultKind()
    {
        var session = Session.Start(Guid.CreateVersion7(), Guid.CreateVersion7(), Guid.CreateVersion7(), ClientKind.Web, Now, "h");

        session.Kind.ShouldBe(AccountKind.Staff);
    }

    [Theory]
    [InlineData(ClientKind.Web, 12 * 60)]
    [InlineData(ClientKind.Mobile, 30 * 24 * 60)]
    public void GuardianSession_HasTheSameAbsoluteLimits(ClientKind client, int minutes)
    {
        var session = Session.Start(
            Guid.CreateVersion7(), Guid.CreateVersion7(), Guid.CreateVersion7(), client, Now, "h", AccountKind.Guardian);

        session.Kind.ShouldBe(AccountKind.Guardian);
        session.ExpiresAt.ShouldBe(Now.AddMinutes(minutes));
        session.Refresh("h", "h2", Now.AddMinutes(1)).ShouldBe(RefreshOutcome.Rotated);
        session.Refresh("h", "h3", Now.AddMinutes(2)).ShouldBe(RefreshOutcome.Reused);
        session.IsActive(Now.AddMinutes(2)).ShouldBeFalse();
    }

    [Fact]
    public void UnknownKind_IsRefused()
    {
        Should.Throw<ArgumentOutOfRangeException>(() => Session.Start(
            Guid.CreateVersion7(), Guid.CreateVersion7(), Guid.CreateVersion7(), ClientKind.Web, Now, "h", (AccountKind)9));
    }
}
