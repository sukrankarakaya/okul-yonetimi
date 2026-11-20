namespace Okul.Modules.Parents.Domain;

public sealed class KvkkConsent
{
    private KvkkConsent() { }

    public Guid Id { get; init; }

    public Guid ParentId { get; init; }

    public Guid TenantId { get; init; }

    public bool Accepted { get; init; }

    public DateTimeOffset RecordedAt { get; init; }

    public static KvkkConsent Create(Guid parentId, Guid tenantId, bool accepted)
    {
        return new KvkkConsent
        {
            Id = Guid.NewGuid(),
            ParentId = parentId,
            TenantId = tenantId,
            Accepted = accepted,
            RecordedAt = DateTimeOffset.UtcNow
        };
    }
}
