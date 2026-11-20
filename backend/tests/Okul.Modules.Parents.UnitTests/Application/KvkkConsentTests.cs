using Okul.Modules.Parents.Application;
using Okul.Modules.Parents.Domain;
using Shouldly;

namespace Okul.Modules.Parents.UnitTests.Application;

public sealed class KvkkConsentTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid ParentId = Guid.NewGuid();

    [Fact]
    public async Task RecordConsentAsync_ValidParent_Success()
    {
        var accountStore = new InMemoryAccountStore(ParentId, TenantId);
        var consentStore = new InMemoryConsentStore();
        var services = new KvkkConsentServices(accountStore, consentStore);

        var result = await services.RecordConsentAsync(ParentId, TenantId, accepted: true, CancellationToken.None);

        result.Success.ShouldBeTrue();
        result.ErrorMessage.ShouldBeNull();
    }

    [Fact]
    public async Task RecordConsentAsync_UnknownParent_Fails()
    {
        var accountStore = new InMemoryAccountStore();
        var consentStore = new InMemoryConsentStore();
        var services = new KvkkConsentServices(accountStore, consentStore);

        var result = await services.RecordConsentAsync(Guid.NewGuid(), TenantId, accepted: true, CancellationToken.None);

        result.Success.ShouldBeFalse();
        result.ErrorMessage.ShouldNotBeNull();
    }

    [Fact]
    public async Task RecordConsentAsync_WrongTenant_Fails()
    {
        var accountStore = new InMemoryAccountStore(ParentId, TenantId);
        var consentStore = new InMemoryConsentStore();
        var services = new KvkkConsentServices(accountStore, consentStore);

        var result = await services.RecordConsentAsync(ParentId, Guid.NewGuid(), accepted: true, CancellationToken.None);

        result.Success.ShouldBeFalse();
    }

    [Fact]
    public async Task RecordConsentAsync_Rejected_Stored()
    {
        var accountStore = new InMemoryAccountStore(ParentId, TenantId);
        var consentStore = new InMemoryConsentStore();
        var services = new KvkkConsentServices(accountStore, consentStore);

        await services.RecordConsentAsync(ParentId, TenantId, accepted: false, CancellationToken.None);
        var latest = await consentStore.FindLatestByParentAsync(ParentId, CancellationToken.None);

        latest.ShouldNotBeNull();
        latest.Accepted.ShouldBeFalse();
    }

    private sealed class InMemoryAccountStore : IParentAccountStore
    {
        private readonly Dictionary<Guid, Parent> _parents = new();

        public InMemoryAccountStore() { }

        public InMemoryAccountStore(Guid parentId, Guid tenantId)
        {
            var parent = Parent.Create(tenantId, "+905551234567");
            var prop = typeof(Parent).GetProperty("Id")?.GetSetMethod(true);
            prop?.Invoke(parent, new object[] { parentId });
            _parents[parentId] = parent;
        }

        public Task<Parent?> FindByPhoneAsync(Guid tenantId, string phoneNumber, CancellationToken cancellationToken) =>
            Task.FromResult(_parents.Values.FirstOrDefault(p => p.TenantId == tenantId && p.PhoneNumber == phoneNumber));

        public Task<Parent?> FindByIdAsync(Guid id, CancellationToken cancellationToken) =>
            Task.FromResult(_parents.TryGetValue(id, out var parent) ? parent : null);

        public async Task AddAsync(Parent parent, CancellationToken cancellationToken)
        {
            _parents[parent.Id] = parent;
            await Task.CompletedTask;
        }

        public Task SaveChangesAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class InMemoryConsentStore : IKvkkConsentStore
    {
        private readonly List<KvkkConsent> _consents = new();

        public Task<KvkkConsent?> FindLatestByParentAsync(Guid parentId, CancellationToken cancellationToken) =>
            Task.FromResult(_consents.Where(c => c.ParentId == parentId).OrderByDescending(c => c.RecordedAt).FirstOrDefault());

        public async Task AddAsync(KvkkConsent consent, CancellationToken cancellationToken)
        {
            _consents.Add(consent);
            await Task.CompletedTask;
        }

        public Task SaveChangesAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
