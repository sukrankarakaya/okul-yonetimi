using Microsoft.EntityFrameworkCore;
using Okul.Modules.Parents.Application;
using Okul.Modules.Parents.Domain;

namespace Okul.Modules.Parents.Infrastructure.Persistence;

public sealed class ParentAccountStore : IParentAccountStore
{
    private readonly ParentDbContext _context;

    public ParentAccountStore(ParentDbContext context) => _context = context;

    public Task<Parent?> FindByPhoneAsync(Guid tenantId, string phoneNumber, CancellationToken cancellationToken) =>
        _context.Parents.FirstOrDefaultAsync(p => p.TenantId == tenantId && p.PhoneNumber == phoneNumber, cancellationToken);

    public Task<Parent?> FindByIdAsync(Guid id, CancellationToken cancellationToken) =>
        _context.Parents.FirstOrDefaultAsync(p => p.Id == id, cancellationToken);

    public async Task AddAsync(Parent parent, CancellationToken cancellationToken)
    {
        _context.Parents.Add(parent);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken) =>
        _context.SaveChangesAsync(cancellationToken);
}

public sealed class SmsCodeStore : ISmsCodeStore
{
    private readonly ParentDbContext _context;

    public SmsCodeStore(ParentDbContext context) => _context = context;

    public Task<SmsCode?> FindLatestByParentAsync(Guid parentId, CancellationToken cancellationToken) =>
        _context.SmsCodes
            .Where(s => s.ParentId == parentId)
            .OrderByDescending(s => s.ExpiresAt)
            .FirstOrDefaultAsync(cancellationToken);

    public async Task AddAsync(SmsCode smsCode, CancellationToken cancellationToken)
    {
        _context.SmsCodes.Add(smsCode);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteExpiredAsync(DateTimeOffset before, CancellationToken cancellationToken)
    {
        await _context.SmsCodes
            .Where(s => s.ExpiresAt < before)
            .ExecuteDeleteAsync(cancellationToken);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken) =>
        _context.SaveChangesAsync(cancellationToken);
}

public sealed class KvkkConsentStore : IKvkkConsentStore
{
    private readonly ParentDbContext _context;

    public KvkkConsentStore(ParentDbContext context) => _context = context;

    public Task<KvkkConsent?> FindLatestByParentAsync(Guid parentId, CancellationToken cancellationToken) =>
        _context.KvkkConsents
            .Where(k => k.ParentId == parentId)
            .OrderByDescending(k => k.RecordedAt)
            .FirstOrDefaultAsync(cancellationToken);

    public async Task AddAsync(KvkkConsent consent, CancellationToken cancellationToken)
    {
        _context.KvkkConsents.Add(consent);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken) =>
        _context.SaveChangesAsync(cancellationToken);
}
