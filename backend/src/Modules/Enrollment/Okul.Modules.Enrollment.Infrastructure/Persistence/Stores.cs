using Microsoft.EntityFrameworkCore;
using Okul.Modules.Enrollment.Application;
using Okul.Modules.Enrollment.Domain;

namespace Okul.Modules.Enrollment.Infrastructure.Persistence;

internal sealed class GuardianStore : IGuardianStore
{
    private readonly EnrollmentDbContext _db;

    public GuardianStore(EnrollmentDbContext db)
    {
        _db = db;
    }

    public Task<Guardian?> FindByPhoneAsync(PhoneNumber phone, CancellationToken cancellationToken) =>
        _db.Guardians.SingleOrDefaultAsync(g => g.Phone == phone.E164, cancellationToken);

    public Task<Guardian?> FindByIdAsync(Guid guardianId, CancellationToken cancellationToken) =>
        _db.Guardians.SingleOrDefaultAsync(g => g.Id == guardianId, cancellationToken);

    public Task SaveChangesAsync(CancellationToken cancellationToken) => _db.SaveChangesAsync(cancellationToken);
}

internal sealed class KvkkConsentStore : IKvkkConsentStore
{
    private readonly EnrollmentDbContext _db;

    public KvkkConsentStore(EnrollmentDbContext db)
    {
        _db = db;
    }

    public Task<KvkkConsent?> FindLatestAsync(Guid guardianId, CancellationToken cancellationToken) =>
        _db.KvkkConsents.AsNoTracking()
            .Where(c => c.GuardianId == guardianId)
            .OrderByDescending(c => c.AcceptedAt)
            .FirstOrDefaultAsync(cancellationToken);

    public async Task AddAsync(KvkkConsent consent, CancellationToken cancellationToken)
    {
        _db.KvkkConsents.Add(consent);
        await _db.SaveChangesAsync(cancellationToken);
    }
}

internal sealed class GuardianLoginStore : IGuardianLoginStore
{
    private readonly GuardianLoginDbContext _db;

    public GuardianLoginStore(GuardianLoginDbContext db)
    {
        _db = db;
    }

    public Task<GuardianLoginThrottle?> FindThrottleAsync(string pairKey, CancellationToken cancellationToken) =>
        _db.Throttles.AsNoTracking().SingleOrDefaultAsync(t => t.Key == pairKey, cancellationToken);

    /// <summary>
    /// One transaction holding a PostgreSQL advisory lock on the pair key: concurrent requests and checks for one pair run
    /// one after the other — no failure is lost, and one code cannot open two sessions. The first use of a pair inserts
    /// under the same lock. The rules stay in the domain types.
    /// </summary>
    public async Task<T> UpdateAsync<T>(string pairKey, Func<GuardianLoginPair, T> change, CancellationToken cancellationToken)
    {
        await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);
        await _db.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT pg_advisory_xact_lock(hashtextextended({"kayit:" + pairKey}, 0))", cancellationToken);

        var throttle = await _db.Throttles.SingleOrDefaultAsync(t => t.Key == pairKey, cancellationToken);
        if (throttle is null)
        {
            throttle = GuardianLoginThrottle.Create(pairKey);
            _db.Throttles.Add(throttle);
        }

        var latest = await _db.Codes
            .Where(c => c.PairKey == pairKey)
            .OrderByDescending(c => c.IssuedAt)
            .FirstOrDefaultAsync(cancellationToken);

        var pair = new GuardianLoginPair(throttle, latest);
        var result = change(pair);
        if (pair.IssuedCode is { } issued)
        {
            _db.Codes.Add(issued);
        }

        await _db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return result;
    }
}
