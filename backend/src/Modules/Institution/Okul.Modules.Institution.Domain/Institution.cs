namespace Okul.Modules.Institution.Domain;

/// <summary>Kurum — the tenant itself (docs/domain.md). Its <see cref="Id"/> is the TenantId.</summary>
public sealed class Institution
{
    public const int NameMaxLength = 200;

    private readonly List<Campus> _campuses = [];

    private Institution()
    {
    }

    public Guid Id { get; private set; }

    public string Name { get; private set; } = string.Empty;

    /// <summary>Kurum kodu — how staff name their kurum at login (spec 0003); see <see cref="InstitutionCode"/>.</summary>
    public string Code { get; private set; } = string.Empty;

    public IReadOnlyCollection<Campus> Campuses => _campuses;

    public static Institution Create(Guid id, string name, string code)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("Kurum kimliği boş olamaz.", nameof(id));
        }

        return new Institution
        {
            Id = id,
            Name = NormalizeName(name, NameMaxLength, nameof(name)),
            Code = InstitutionCode.Require(code),
        };
    }

    /// <summary>Adds a şube (campus); the campus belongs to this kurum's tenant.</summary>
    public Campus AddCampus(Guid campusId, string name)
    {
        var campus = Campus.Create(campusId, Id, NormalizeName(name, Campus.NameMaxLength, nameof(name)));
        _campuses.Add(campus);
        return campus;
    }

    internal static string NormalizeName(string name, int maxLength, string parameterName)
    {
        var trimmed = name?.Trim() ?? string.Empty;
        if (trimmed.Length == 0)
        {
            throw new ArgumentException("Ad boş olamaz.", parameterName);
        }

        if (trimmed.Length > maxLength)
        {
            throw new ArgumentException($"Ad en fazla {maxLength} karakter olabilir.", parameterName);
        }

        return trimmed;
    }
}
