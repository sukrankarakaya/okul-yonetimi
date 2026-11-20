namespace Okul.Modules.Education.Domain;

public enum CourseLevel { Beginner, Intermediate, Advanced }

public sealed class Course
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public string Title { get; private set; } = null!;
    public string Description { get; private set; } = null!;
    public CourseLevel Level { get; private set; }
    public int DurationMinutes { get; private set; }
    public Guid InstructorId { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    private Course() { }

    public static Course Create(Guid tenantId, string title, string description, CourseLevel level, int durationMinutes, Guid instructorId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        ArgumentException.ThrowIfNullOrWhiteSpace(description);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(durationMinutes, 0);

        return new Course
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Title = title,
            Description = description,
            Level = level,
            DurationMinutes = durationMinutes,
            InstructorId = instructorId,
            CreatedAt = DateTimeOffset.UtcNow
        };
    }
}
