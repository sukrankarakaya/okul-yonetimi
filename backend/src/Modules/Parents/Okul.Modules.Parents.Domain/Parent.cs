namespace Okul.Modules.Parents.Domain;

public sealed class Parent
{
    private Parent() { }

    public Guid Id { get; init; }

    public Guid TenantId { get; init; }

    public string PhoneNumber { get; init; } = null!;

    public string? Name { get; init; }

    public DateTimeOffset CreatedAt { get; init; }

    public static Parent Create(Guid tenantId, string phoneNumber, string? name = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(phoneNumber);
        return new Parent
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            PhoneNumber = phoneNumber,
            Name = name,
            CreatedAt = DateTimeOffset.UtcNow
        };
    }

    public bool IsLockedOut(DateTimeOffset now, TimeSpan lockDuration)
    {
        return LastFailureAt.HasValue && now - LastFailureAt < lockDuration;
    }

    public void RecordFailure(DateTimeOffset now)
    {
        FailureCount++;
        LastFailureAt = now;
    }

    public void ResetFailures()
    {
        FailureCount = 0;
        LastFailureAt = null;
    }

    public int FailureCount { get; private set; }

    public DateTimeOffset? LastFailureAt { get; private set; }
}
