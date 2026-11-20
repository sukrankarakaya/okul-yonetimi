namespace Okul.Modules.Enrollment.Domain;

/// <summary>
/// A veli's açık rıza to ONE version of the aydınlatma metni (spec 0007, Requirement 6; K6): veli, kurum, metin sürümü,
/// zaman. A new text version needs a new rıza. Records are only added, never changed (auditable). Faz 1: kept in Kayıt,
/// later in Uyum (ADR 0002).
/// </summary>
public sealed class KvkkConsent
{
    private KvkkConsent()
    {
    }

    public Guid Id { get; private set; }

    public Guid TenantId { get; private set; }

    public Guid GuardianId { get; private set; }

    public string TextVersion { get; private set; } = string.Empty;

    public DateTimeOffset AcceptedAt { get; private set; }

    public static KvkkConsent Accept(Guid id, Guid tenantId, Guid guardianId, string textVersion, DateTimeOffset now)
    {
        if (id == Guid.Empty || tenantId == Guid.Empty || guardianId == Guid.Empty)
        {
            throw new ArgumentException("Rıza, kurum ve veli kimliği boş olamaz.");
        }

        if (string.IsNullOrWhiteSpace(textVersion))
        {
            throw new ArgumentException("Metin sürümü boş olamaz.", nameof(textVersion));
        }

        return new KvkkConsent
        {
            Id = id,
            TenantId = tenantId,
            GuardianId = guardianId,
            TextVersion = textVersion,
            AcceptedAt = now,
        };
    }

    /// <summary>Does this rıza cover the version shown today?</summary>
    public bool Covers(string currentTextVersion) =>
        string.Equals(TextVersion, currentTextVersion, StringComparison.Ordinal);
}
