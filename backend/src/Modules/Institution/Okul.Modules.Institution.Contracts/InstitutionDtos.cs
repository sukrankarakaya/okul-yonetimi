namespace Okul.Modules.Institution.Contracts;

/// <summary>Kurum bilgisi: name + campuses in Turkish alphabetical order (spec 0001).</summary>
public sealed record InstitutionDto(Guid Id, string Name, IReadOnlyList<CampusDto> Campuses);

public sealed record CampusDto(Guid Id, string Name);
