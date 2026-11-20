namespace Okul.Modules.Education.Domain;

public sealed class Enrollment
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid StudentId { get; private set; }
    public Guid CourseId { get; private set; }
    public int CompletedLessons { get; private set; }
    public int TotalLessons { get; private set; }
    public DateTimeOffset EnrolledAt { get; private set; }

    private Enrollment() { }

    public static Enrollment Create(Guid tenantId, Guid studentId, Guid courseId, int totalLessons)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(totalLessons, 0);
        return new Enrollment
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            StudentId = studentId,
            CourseId = courseId,
            CompletedLessons = 0,
            TotalLessons = totalLessons,
            EnrolledAt = DateTimeOffset.UtcNow
        };
    }

    public int GetProgressPercent() => (CompletedLessons * 100) / TotalLessons;

    public void CompleteLesson()
    {
        if (CompletedLessons < TotalLessons)
            CompletedLessons++;
    }
}
