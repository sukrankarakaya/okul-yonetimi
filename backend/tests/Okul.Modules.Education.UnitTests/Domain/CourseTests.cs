using Okul.Modules.Education.Domain;
using Shouldly;

namespace Okul.Modules.Education.UnitTests.Domain;

public sealed class CourseTests
{
    [Fact]
    public void Create_Valid_Success()
    {
        var c = Course.Create(Guid.NewGuid(), "Web Dev", "Learn HTML", CourseLevel.Beginner, 180, Guid.NewGuid());
        c.Title.ShouldBe("Web Dev");
        c.Level.ShouldBe(CourseLevel.Beginner);
    }

    [Fact]
    public void Create_EmptyTitle_Throws()
    {
        Should.Throw<ArgumentException>(() =>
            Course.Create(Guid.NewGuid(), "", "Desc", CourseLevel.Beginner, 100, Guid.NewGuid()));
    }

    [Fact]
    public void Create_ZeroDuration_Throws()
    {
        Should.Throw<ArgumentOutOfRangeException>(() =>
            Course.Create(Guid.NewGuid(), "Title", "Desc", CourseLevel.Beginner, 0, Guid.NewGuid()));
    }

    [Theory]
    [InlineData(CourseLevel.Beginner)]
    [InlineData(CourseLevel.Intermediate)]
    [InlineData(CourseLevel.Advanced)]
    public void Create_AllLevels_Success(CourseLevel level)
    {
        var c = Course.Create(Guid.NewGuid(), "Title", "Desc", level, 100, Guid.NewGuid());
        c.Level.ShouldBe(level);
    }
}
