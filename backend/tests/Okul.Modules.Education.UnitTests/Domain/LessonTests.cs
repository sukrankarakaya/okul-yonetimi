using Okul.Modules.Education.Domain;
using Shouldly;

namespace Okul.Modules.Education.UnitTests.Domain;

public sealed class LessonTests
{
    [Fact]
    public void Create_Valid_Success()
    {
        var l = Lesson.Create(Guid.NewGuid(), "Intro", "https://youtube.com/x", 45, 1);
        l.Title.ShouldBe("Intro");
        l.DurationMinutes.ShouldBe(45);
        l.Order.ShouldBe(1);
    }

    [Fact]
    public void Create_EmptyTitle_Throws()
    {
        Should.Throw<ArgumentException>(() =>
            Lesson.Create(Guid.NewGuid(), "", "url", 10, 1));
    }

    [Fact]
    public void Create_ZeroDuration_Throws()
    {
        Should.Throw<ArgumentOutOfRangeException>(() =>
            Lesson.Create(Guid.NewGuid(), "Title", "url", 0, 1));
    }
}
