using Okul.Modules.Institution.Domain;
using Shouldly;
using InstitutionEntity = Okul.Modules.Institution.Domain.Institution;

namespace Okul.Modules.Institution.UnitTests.Domain;

public sealed class InstitutionTests
{
    [Fact]
    public void Create_TrimsName()
    {
        var institution = InstitutionEntity.Create(Guid.CreateVersion7(), "  Atatürk Koleji  ", "OKUL-1");

        institution.Name.ShouldBe("Atatürk Koleji");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_EmptyName_Throws(string name)
    {
        Should.Throw<ArgumentException>(() => InstitutionEntity.Create(Guid.CreateVersion7(), name, "OKUL-1"));
    }

    [Fact]
    public void Create_NameLongerThanMax_Throws()
    {
        var name = new string('a', InstitutionEntity.NameMaxLength + 1);

        Should.Throw<ArgumentException>(() => InstitutionEntity.Create(Guid.CreateVersion7(), name, "OKUL-1"));
    }

    [Fact]
    public void Create_EmptyId_Throws()
    {
        Should.Throw<ArgumentException>(() => InstitutionEntity.Create(Guid.Empty, "Atatürk Koleji", "OKUL-1"));
    }

    [Fact]
    public void AddCampus_CampusBelongsToTheInstitutionsTenant()
    {
        var institution = InstitutionEntity.Create(Guid.CreateVersion7(), "Atatürk Koleji", "OKUL-1");

        var campus = institution.AddCampus(Guid.CreateVersion7(), " Üsküdar ");

        campus.TenantId.ShouldBe(institution.Id);
        campus.Name.ShouldBe("Üsküdar");
        institution.Campuses.ShouldContain(campus);
    }

    [Fact]
    public void AddCampus_EmptyName_Throws()
    {
        var institution = InstitutionEntity.Create(Guid.CreateVersion7(), "Atatürk Koleji", "OKUL-1");

        Should.Throw<ArgumentException>(() => institution.AddCampus(Guid.CreateVersion7(), " "));
    }
}
