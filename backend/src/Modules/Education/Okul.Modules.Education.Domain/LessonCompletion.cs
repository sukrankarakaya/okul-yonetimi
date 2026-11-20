namespace Okul.Modules.Education.Domain;

public sealed class LessonCompletion
{
    public Guid Id { get; private set; }
    public Guid EnrollmentId { get; private set; }
    public Guid LessonId { get; private set; }
    public DateTimeOffset CompletedAt { get; private set; }

    private LessonCompletion() { }

    public static LessonCompletion Create(Guid enrollmentId, Guid lessonId) => new()
    {
        Id = Guid.NewGuid(),
        EnrollmentId = enrollmentId,
        LessonId = lessonId,
        CompletedAt = DateTimeOffset.UtcNow
    };
}
