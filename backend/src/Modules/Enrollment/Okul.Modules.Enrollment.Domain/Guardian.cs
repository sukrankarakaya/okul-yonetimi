namespace Okul.Modules.Enrollment.Domain;

/// <summary>
/// A veli registered by the kurum (spec 0007, Requirement 9): the kurum's veli telefon list. The login account comes into
/// being on the first successful login and is the same on every later one (AC-7). The same telefon in two kurumlar is two
/// separate veliler (Requirement 8).
/// </summary>
public sealed class Guardian
{
    private Guardian()
    {
    }

    public Guid Id { get; private set; }

    public Guid TenantId { get; private set; }

    /// <summary>E.164, unique per kurum.</summary>
    public string Phone { get; private set; } = string.Empty;

    public string? Name { get; private set; }

    public DateTimeOffset RegisteredAt { get; private set; }

    /// <summary>The first successful login — the login account exists from then on.</summary>
    public DateTimeOffset? AccountCreatedAt { get; private set; }

    public bool HasAccount => AccountCreatedAt is not null;

    /// <summary>"Veli" when the kurum recorded no name (spec 0006, Requirement 7).</summary>
    public string DisplayName => string.IsNullOrWhiteSpace(Name) ? "Veli" : Name;

    public static Guardian Register(Guid id, Guid tenantId, PhoneNumber phone, string? name, DateTimeOffset now)
    {
        if (id == Guid.Empty || tenantId == Guid.Empty)
        {
            throw new ArgumentException("Veli ve kurum kimliği boş olamaz.");
        }

        ArgumentNullException.ThrowIfNull(phone);
        return new Guardian
        {
            Id = id,
            TenantId = tenantId,
            Phone = phone.E164,
            Name = string.IsNullOrWhiteSpace(name) ? null : name.Trim(),
            RegisteredAt = now,
        };
    }

    /// <summary>A successful login: the account is created once, then reused.</summary>
    public void RecordLogin(DateTimeOffset now) => AccountCreatedAt ??= now;
}
