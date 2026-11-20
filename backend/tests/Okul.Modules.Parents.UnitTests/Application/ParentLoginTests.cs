using Microsoft.Extensions.Time.Testing;
using Okul.Modules.Parents.Application;
using Okul.Modules.Parents.Domain;
using Okul.Modules.Parents.Infrastructure;
using Okul.Modules.Parents.Infrastructure.Persistence;
using Shouldly;

namespace Okul.Modules.Parents.UnitTests.Application;

public sealed class ParentLoginTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly string PhoneNumber = "+905551234567";
    private static readonly TimeSpan Validity = TimeSpan.FromMinutes(5);

    [Fact]
    public async Task VerifyCodeAsync_ValidCode_Success()
    {
        var smsCode = "123456";
        var now = DateTimeOffset.UtcNow;
        var timeProvider = new FakeTimeProvider(now);

        var accountStore = new InMemoryParentAccountStore();
        var smsCodeStore = new InMemorySmsCodeStore();
        var services = new ParentLoginServices(accountStore, smsCodeStore, timeProvider);

        // Create parent and SMS code
        var parent = Parent.Create(TenantId, PhoneNumber);
        await accountStore.AddAsync(parent, CancellationToken.None);

        parent = await accountStore.FindByPhoneAsync(TenantId, PhoneNumber, CancellationToken.None)
            ?? throw new InvalidOperationException();

        var code = SmsCode.Create(parent.Id, TenantId, smsCode, Validity);
        await smsCodeStore.AddAsync(code, CancellationToken.None);

        // Verify
        var result = await services.VerifyCodeAsync(TenantId, PhoneNumber, smsCode, CancellationToken.None);

        result.Success.ShouldBeTrue();
        result.ErrorMessage.ShouldBeNull();
    }

    [Fact]
    public async Task VerifyCodeAsync_WrongCode_Fails()
    {
        var now = DateTimeOffset.UtcNow;
        var timeProvider = new FakeTimeProvider(now);

        var accountStore = new InMemoryParentAccountStore();
        var smsCodeStore = new InMemorySmsCodeStore();
        var services = new ParentLoginServices(accountStore, smsCodeStore, timeProvider);

        // Create parent with fresh phone (different from other tests)
        var testPhone = "+905551111111";
        var parent = Parent.Create(TenantId, testPhone);
        await accountStore.AddAsync(parent, CancellationToken.None);

        parent = await accountStore.FindByPhoneAsync(TenantId, testPhone, CancellationToken.None)
            ?? throw new InvalidOperationException();

        var code = SmsCode.Create(parent.Id, TenantId, "123456", Validity);
        await smsCodeStore.AddAsync(code, CancellationToken.None);

        // Verify wrong code
        var result = await services.VerifyCodeAsync(TenantId, testPhone, "654321", CancellationToken.None);

        result.Success.ShouldBeFalse();
        result.ErrorMessage.ShouldNotBeNull();
    }

    [Fact]
    public async Task VerifyCodeAsync_ExpiredCode_Fails()
    {
        var now = DateTimeOffset.UtcNow;
        var timeProvider = new FakeTimeProvider(now);

        var accountStore = new InMemoryParentAccountStore();
        var smsCodeStore = new InMemorySmsCodeStore();
        var services = new ParentLoginServices(accountStore, smsCodeStore, timeProvider);

        // Create parent and SMS code
        var testPhone = "+905552222222";
        var parent = Parent.Create(TenantId, testPhone);
        await accountStore.AddAsync(parent, CancellationToken.None);

        parent = await accountStore.FindByPhoneAsync(TenantId, testPhone, CancellationToken.None)
            ?? throw new InvalidOperationException();

        var code = SmsCode.Create(parent.Id, TenantId, "123456", TimeSpan.FromSeconds(1));
        await smsCodeStore.AddAsync(code, CancellationToken.None);

        // Move time forward
        timeProvider.Advance(TimeSpan.FromSeconds(2));

        // Verify expired code
        var result = await services.VerifyCodeAsync(TenantId, testPhone, "123456", CancellationToken.None);

        result.Success.ShouldBeFalse();
        result.ErrorMessage.ShouldNotBeNull();
        result.ErrorMessage.ShouldContain("süresi geçmiş");
    }

    [Fact]
    public async Task VerifyCodeAsync_MultipleWrongCodes_Lockout()
    {
        var now = DateTimeOffset.UtcNow;
        var timeProvider = new FakeTimeProvider(now);

        var accountStore = new InMemoryParentAccountStore();
        var smsCodeStore = new InMemorySmsCodeStore();
        var services = new ParentLoginServices(accountStore, smsCodeStore, timeProvider);

        // Create parent and SMS code
        var testPhone = "+905553333333";
        var parent = Parent.Create(TenantId, testPhone);
        await accountStore.AddAsync(parent, CancellationToken.None);

        parent = await accountStore.FindByPhoneAsync(TenantId, testPhone, CancellationToken.None)
            ?? throw new InvalidOperationException();

        var code = SmsCode.Create(parent.Id, TenantId, "123456", Validity);
        await smsCodeStore.AddAsync(code, CancellationToken.None);

        // 3 wrong attempts
        for (int i = 0; i < 3; i++)
        {
            var result = await services.VerifyCodeAsync(TenantId, testPhone, "000000", CancellationToken.None);
            result.Success.ShouldBeFalse();
        }

        // 4th attempt should be locked
        var lockedResult = await services.VerifyCodeAsync(TenantId, testPhone, "123456", CancellationToken.None);
        lockedResult.Success.ShouldBeFalse();
        lockedResult.ErrorMessage.ShouldNotBeNull();
        lockedResult.ErrorMessage.ShouldContain("Çok fazla başarısız deneme");
    }

    // In-memory implementations for testing
    private sealed class InMemoryParentAccountStore : IParentAccountStore
    {
        private readonly Dictionary<Guid, Parent> _parents = new();

        public Task<Parent?> FindByPhoneAsync(Guid tenantId, string phoneNumber, CancellationToken cancellationToken) =>
            Task.FromResult(_parents.Values.FirstOrDefault(p => p.TenantId == tenantId && p.PhoneNumber == phoneNumber));

        public Task<Parent?> FindByIdAsync(Guid id, CancellationToken cancellationToken) =>
            Task.FromResult(_parents.TryGetValue(id, out var parent) ? parent : null);

        public async Task AddAsync(Parent parent, CancellationToken cancellationToken)
        {
            _parents[parent.Id] = parent;
            await Task.CompletedTask;
        }

        public Task SaveChangesAsync(CancellationToken cancellationToken) =>
            Task.CompletedTask;
    }

    private sealed class InMemorySmsCodeStore : ISmsCodeStore
    {
        private readonly List<SmsCode> _codes = new();

        public Task<SmsCode?> FindLatestByParentAsync(Guid parentId, CancellationToken cancellationToken) =>
            Task.FromResult(_codes.Where(c => c.ParentId == parentId).OrderByDescending(c => c.ExpiresAt).FirstOrDefault());

        public async Task AddAsync(SmsCode smsCode, CancellationToken cancellationToken)
        {
            _codes.Add(smsCode);
            await Task.CompletedTask;
        }

        public Task DeleteExpiredAsync(DateTimeOffset before, CancellationToken cancellationToken) =>
            Task.CompletedTask;

        public Task SaveChangesAsync(CancellationToken cancellationToken) =>
            Task.CompletedTask;
    }
}
