using NArchitecture.Core.Mediator;

namespace Okul.Modules.Education.Application.Queries;

public sealed record GetCoursesQuery(Guid TenantId, CourseLevel? Level = null)
    : IRequest<GetCoursesResult>;

public sealed record CourseDto(Guid Id, string Title, string Description, CourseLevel Level, int DurationMinutes);
public sealed record GetCoursesResult(IReadOnlyList<CourseDto> Courses);

public sealed class GetCoursesQueryHandler : IRequestHandler<GetCoursesQuery, GetCoursesResult>
{
    private readonly ICourseStore _store;

    public GetCoursesQueryHandler(ICourseStore store) => _store = store;

    public async Task<GetCoursesResult> Handle(GetCoursesQuery request, CancellationToken cancellationToken)
    {
        var courses = await _store.GetByTenantAsync(request.TenantId, cancellationToken);
        if (request.Level.HasValue)
            courses = courses.Where(c => c.Level == request.Level).ToList();

        var dtos = courses.Select(c => new CourseDto(c.Id, c.Title, c.Description, c.Level, c.DurationMinutes)).ToList();
        return new GetCoursesResult(dtos);
    }
}
