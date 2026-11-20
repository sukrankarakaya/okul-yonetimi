namespace Okul.Modules.Education.Domain;

public sealed class Lesson
{
    public Guid Id { get; private set; }
    public Guid CourseId { get; private set; }
    public string Title { get; private set; } = null!;
    public string VideoUrl { get; private set; } = null!;
    public int DurationMinutes { get; private set; }
    public int Order { get; private set; }

    private Lesson() { }

    public static Lesson Create(Guid courseId, string title, string videoUrl, int durationMinutes, int order)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        ArgumentException.ThrowIfNullOrWhiteSpace(videoUrl);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(durationMinutes, 0);

        return new Lesson
        {
            Id = Guid.NewGuid(),
            CourseId = courseId,
            Title = title,
            VideoUrl = videoUrl,
            DurationMinutes = durationMinutes,
            Order = order
        };
    }
}
