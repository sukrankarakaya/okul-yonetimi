using System.Security.Cryptography;
using Microsoft.Extensions.Time.Testing;
using Okul.Core.Tenancy;
using Okul.Modules.Enrollment.Application;
using Okul.Modules.Enrollment.Contracts;
using Okul.Modules.Enrollment.Domain;
using Okul.Modules.Enrollment.Infrastructure;
using Okul.Modules.Enrollment.Infrastructure.Sms;
using Okul.Modules.Identity.Contracts;
using Okul.Modules.Institution.Contracts;

namespace Okul.Modules.Enrollment.UnitTests.Application;

/// <summary>In-memory ports that honour the tenant scope like the real tenant filter does.</summary>
internal sealed class FakeTenantScope : ITenantScope
{
    public Guid? Current { get; private set; }

    public IDisposable Begin(Guid tenantId)
    {
        var previous = Current;
        Current = tenantId;
        return new Restore(() => Current = previous);
    }

    private sealed class Restore(Action restore) : IDisposable
    {
        public void Dispose() => restore();
    }
}

internal sealed class FakeInstitutions : IInstitutionDirectory
{
    private readonly Dictionary<string, Guid> _codes = new(StringComparer.Ordinal);

    public FakeInstitutions Add(string code, Guid tenantId)
    {
        _codes[code] = tenantId;
        return this;
    }

    public Task<Guid?> FindIdByCodeAsync(string code, CancellationToken cancellationToken = default) =>
        Task.FromResult(InstitutionCodeFormat.Normalize(code) is { } normalized && _codes.TryGetValue(normalized, out var id)
            ? id : (Guid?)null);
}

internal sealed class FakeGuardians(FakeTenantScope scope) : IGuardianStore
{
    public List<Guardian> All { get; } = [];

    public Task<Guardian?> FindByPhoneAsync(PhoneNumber phone, CancellationToken cancellationToken) =>
        Task.FromResult(All.SingleOrDefault(g => g.TenantId == scope.Current && g.Phone == phone.E164));

    public Task<Guardian?> FindByIdAsync(Guid guardianId, CancellationToken cancellationToken) =>
        Task.FromResult(All.SingleOrDefault(g => g.TenantId == scope.Current && g.Id == guardianId));

    public Task SaveChangesAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}

internal sealed class FakeConsents(FakeTenantScope scope) : IKvkkConsentStore
{
    public List<KvkkConsent> All { get; } = [];

    public Task<KvkkConsent?> FindLatestAsync(Guid guardianId, CancellationToken cancellationToken) =>
        Task.FromResult(All.Where(c => c.TenantId == scope.Current && c.GuardianId == guardianId)
            .OrderByDescending(c => c.AcceptedAt).FirstOrDefault());

    public Task AddAsync(KvkkConsent consent, CancellationToken cancellationToken)
    {
        if (scope.Current != consent.TenantId)
        {
            throw new InvalidOperationException("Rıza kendi kurumunun dışında eklendi.");
        }

        All.Add(consent);
        return Task.CompletedTask;
    }
}

internal sealed class FakeLogin : IGuardianLoginStore
{
    public Dictionary<string, GuardianLoginThrottle> Throttles { get; } = new(StringComparer.Ordinal);

    public List<GuardianLoginCode> Codes { get; } = [];

    public Task<GuardianLoginThrottle?> FindThrottleAsync(string pairKey, CancellationToken cancellationToken) =>
        Task.FromResult(Throttles.GetValueOrDefault(pairKey));

    public Task<T> UpdateAsync<T>(string pairKey, Func<GuardianLoginPair, T> change, CancellationToken cancellationToken)
    {
        if (!Throttles.TryGetValue(pairKey, out var throttle))
        {
            throttle = GuardianLoginThrottle.Create(pairKey);
            Throttles[pairKey] = throttle;
        }

        var pair = new GuardianLoginPair(throttle, Codes.Where(c => c.PairKey == pairKey).OrderBy(c => c.IssuedAt).LastOrDefault());
        var result = change(pair);
        if (pair.IssuedCode is { } issued)
        {
            Codes.Add(issued);
        }

        return Task.FromResult(result);
    }
}

internal sealed class FakeGuardianSessions : IGuardianSessions
{
    public List<GuardianSessionStarted> Started { get; } = [];

    public List<(Guid TenantId, Guid GuardianId)> Closed { get; } = [];

    public Task<GuardianSessionStarted> StartAsync(
        Guid tenantId, Guid guardianId, SessionClient client, CancellationToken cancellationToken = default)
    {
        var started = new GuardianSessionStarted(
            Guid.CreateVersion7(), tenantId, guardianId, ["Guardian"], client, DateTimeOffset.UnixEpoch, "refresh");
        Started.Add(started);
        return Task.FromResult(started);
    }

    public Task<int> CloseAllAsync(Guid tenantId, Guid guardianId, CancellationToken cancellationToken = default)
    {
        var count = Started.Count(s => s.TenantId == tenantId && s.GuardianId == guardianId);
        Closed.Add((tenantId, guardianId));
        return Task.FromResult(count);
    }
}

/// <summary>One kurum with a registered veli, a second kurum, the ports and a fake clock — shared by the handler tests.</summary>
internal sealed class LoginWorld
{
    public const string KurumA = "OKUL-A";
    public const string KurumB = "OKUL-B";
    public const string AysePhone = "0532 123 45 67";

    public static readonly Guid TenantA = Guid.CreateVersion7();
    public static readonly Guid TenantB = Guid.CreateVersion7();

    public LoginWorld()
    {
        Guardians = new FakeGuardians(Scope);
        Consents = new FakeConsents(Scope);
        Ayse = Guardian.Register(Guid.CreateVersion7(), TenantA, PhoneNumber.Parse(AysePhone)!, "Ayşe", Clock.GetUtcNow());
        AyseInB = Guardian.Register(Guid.CreateVersion7(), TenantB, PhoneNumber.Parse(AysePhone)!, "Ayşe", Clock.GetUtcNow());
        Guardians.All.AddRange([Ayse, AyseInB]);
        Directory = new GuardianDirectory(Scope, Guardians, Consents, Text);
    }

    public FakeTimeProvider Clock { get; } = new(new DateTimeOffset(2026, 11, 20, 9, 0, 0, TimeSpan.Zero));

    public FakeTenantScope Scope { get; } = new();

    public FakeInstitutions Institutions { get; } = new FakeInstitutions().Add(KurumA, TenantA).Add(KurumB, TenantB);

    public FakeGuardians Guardians { get; }

    public FakeConsents Consents { get; }

    public FakeLogin Login { get; } = new();

    public FakeSmsSender Sms { get; } = new();

    public FakeGuardianSessions Sessions { get; } = new();

    public KvkkText Text { get; set; } = KvkkText.Create("taslak", "metin");

    public GuardianLoginTiming Timing { get; set; } = new(TimeSpan.Zero);

    public GuardianCodeHasher Hasher { get; } = new(new GuardianLoginKey(RandomNumberGenerator.GetBytes(GuardianLoginKey.Length)));

    public GuardianDirectory Directory { get; private set; }

    public Guardian Ayse { get; }

    public Guardian AyseInB { get; }

    public CancellationToken Ct => TestContext.Current.CancellationToken;

    public void ChangeTextVersion(string version)
    {
        Text = KvkkText.Create(version, "yeni metin");
        Directory = new GuardianDirectory(Scope, Guardians, Consents, Text);
    }

    public Task<RequestCodeStatus> Request(string? kurum = KurumA, string? phone = AysePhone) =>
        new RequestGuardianCodeCommandHandler(Institutions, Scope, Guardians, Login, Hasher, Sms, Timing, Clock)
            .Handle(new RequestGuardianCodeCommand(kurum, phone), Ct);

    public Task<VerifyCodeResult> Verify(string? code, string? kurum = KurumA, string? phone = AysePhone) =>
        new VerifyGuardianCodeCommandHandler(Institutions, Scope, Guardians, Login, Hasher, Sessions, Directory, Clock)
            .Handle(new VerifyGuardianCodeCommand(kurum, phone, code, SessionClient.Mobile), Ct);

    public Task<bool> Cancel(string? kurum = KurumA, string? phone = AysePhone) =>
        new CancelGuardianCodeCommandHandler(Institutions, Login, Hasher, Clock).Handle(new CancelGuardianCodeCommand(kurum, phone), Ct);

    public Task<ResetGuardianResult> Reset(string? kurum = KurumA, string? phone = AysePhone) =>
        new ResetGuardianCommandHandler(Institutions, Scope, Guardians, Login, Hasher, Sessions, Clock)
            .Handle(new ResetGuardianCommand(kurum, phone), Ct);

    public Task<GuardianConsentStatus> ConsentStatus(Guardian guardian) =>
        new GetGuardianConsentQueryHandler(Scope, Consents, Text).Handle(new GetGuardianConsentQuery(guardian.TenantId, guardian.Id), Ct);

    public Task<GuardianConsentStatus> Accept(Guardian guardian) =>
        new AcceptGuardianConsentCommandHandler(Scope, Guardians, Consents, Text, Clock)
            .Handle(new AcceptGuardianConsentCommand(guardian.TenantId, guardian.Id), Ct);

    public string? LastCode => Sms.LastCodeFor("+905321234567");

    public static string Other(string code) => code == "000000" ? "111111" : "000000";
}
