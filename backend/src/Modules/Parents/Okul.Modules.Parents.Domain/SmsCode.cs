namespace Okul.Modules.Parents.Domain;

public sealed class SmsCode
{
    private SmsCode() { }

    public Guid Id { get; init; }

    public Guid ParentId { get; init; }

    public Guid TenantId { get; init; }

    public string Code { get; init; } = null!;

    public DateTimeOffset ExpiresAt { get; init; }

    public int AttemptCount { get; private set; }

    public static SmsCode Create(Guid parentId, Guid tenantId, string code, TimeSpan validity)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        if (code.Length != 6 || !code.All(char.IsDigit))
        {
            throw new ArgumentException("SMS code must be 6 digits.");
        }

        return new SmsCode
        {
            Id = Guid.NewGuid(),
            ParentId = parentId,
            TenantId = tenantId,
            Code = code,
            ExpiresAt = DateTimeOffset.UtcNow.Add(validity),
            AttemptCount = 0
        };
    }

    public bool IsExpired(DateTimeOffset now) => now >= ExpiresAt;

    public void RecordAttempt() => AttemptCount++;

    public bool IsValid(string attempt, DateTimeOffset now)
    {
        return !IsExpired(now) && Code == attempt;
    }
}
