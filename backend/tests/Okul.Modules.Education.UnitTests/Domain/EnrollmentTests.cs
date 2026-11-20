using Okul.Modules.Education.Domain;
using Shouldly;

namespace Okul.Modules.Education.UnitTests.Domain;

public sealed class EnrollmentTests
{
    [Fact]
    public void Create_Valid_Success()
    {
        var enrollment = Enrollment.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 10);
        enrollment.CompletedLessons.ShouldBe(0);
        enrollment.TotalLessons.ShouldBe(10);
    }

    [Fact]
    public void GetProgressPercent_HalfComplete_50()
    {
        var e = Enrollment.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 10);
        for (int i = 0; i < 5; i++) e.CompleteLesson();
        e.GetProgressPercent().ShouldBe(50);
    }

    [Fact]
    public void CompleteLesson_Full_Capped()
    {
        var e = Enrollment.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 2);
        e.CompleteLesson();
        e.CompleteLesson();
        e.CompleteLesson(); // over cap
        e.CompletedLessons.ShouldBe(2);
    }
}
