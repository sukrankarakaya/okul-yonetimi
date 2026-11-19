namespace Okul.Modules.Institution.Domain;

/// <summary>Şube — a physical campus of a kurum (docs/domain.md; not a class section like "7-A").</summary>
public sealed class Campus
{
    public const int NameMaxLength = 200;

    private Campus()
    {
    }

    public Guid Id { get; private set; }

    public Guid TenantId { get; private set; }

    public string Name { get; private set; } = string.Empty;

    internal static Campus Create(Guid id, Guid tenantId, string name)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("Şube kimliği boş olamaz.", nameof(id));
        }

        return new Campus { Id = id, TenantId = tenantId, Name = name };
    }
}
