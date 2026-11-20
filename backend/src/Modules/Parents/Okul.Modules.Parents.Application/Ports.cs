using Okul.Modules.Parents.Domain;

namespace Okul.Modules.Parents.Application;

public interface IParentAccountStore
{
    Task<Parent?> FindByPhoneAsync(Guid tenantId, string phoneNumber, CancellationToken cancellationToken);
    Task<Parent?> FindByIdAsync(Guid id, CancellationToken cancellationToken);
    Task AddAsync(Parent parent, CancellationToken cancellationToken);
    Task SaveChangesAsync(CancellationToken cancellationToken);
}

public interface ISmsCodeStore
{
    Task<SmsCode?> FindLatestByParentAsync(Guid parentId, CancellationToken cancellationToken);
    Task AddAsync(SmsCode smsCode, CancellationToken cancellationToken);
    Task DeleteExpiredAsync(DateTimeOffset before, CancellationToken cancellationToken);
    Task SaveChangesAsync(CancellationToken cancellationToken);
}

public interface IKvkkConsentStore
{
    Task<KvkkConsent?> FindLatestByParentAsync(Guid parentId, CancellationToken cancellationToken);
    Task AddAsync(KvkkConsent consent, CancellationToken cancellationToken);
    Task SaveChangesAsync(CancellationToken cancellationToken);
}
