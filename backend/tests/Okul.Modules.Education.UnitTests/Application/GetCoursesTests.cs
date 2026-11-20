using Okul.Modules.Education.Application.Queries;
using Okul.Modules.Education.Domain;
using Shouldly;

namespace Okul.Modules.Education.UnitTests.Application;

public sealed class GetCoursesTests
{
    [Fact]
    public async Task Handle_ReturnsAllCourses()
    {
        var tenantId = Guid.NewGuid();
        var store = new InMemoryCourseStore();

        var c1 = Course.Create(tenantId, "Web", "HTML", CourseLevel.Beginner, 100, Guid.NewGuid());
        var c2 = Course.Create(tenantId, "Python", "Data", CourseLevel.Intermediate, 150, Guid.NewGuid());
        await store.AddAsync(c1, CancellationToken.None);
        await store.AddAsync(c2, CancellationToken.None);

        var handler = new GetCoursesQueryHandler(store);
        var result = await handler.Handle(new GetCoursesQuery(tenantId), CancellationToken.None);

        result.Courses.ShouldHaveCount(2);
    }

    [Fact]
    public async Task Handle_FiltersByLevel()
    {
        var tenantId = Guid.NewGuid();
        var store = new InMemoryCourseStore();

        await store.AddAsync(Course.Create(tenantId, "Web", "HTML", CourseLevel.Beginner, 100, Guid.NewGuid()), CancellationToken.None);
        await store.AddAsync(Course.Create(tenantId, "React", "JS", CourseLevel.Advanced, 200, Guid.NewGuid()), CancellationToken.None);

        var handler = new GetCoursesQueryHandler(store);
        var result = await handler.Handle(new GetCoursesQuery(tenantId, CourseLevel.Beginner), CancellationToken.None);

        result.Courses.ShouldHaveCount(1);
        result.Courses[0].Level.ShouldBe(CourseLevel.Beginner);
    }

    private sealed class InMemoryCourseStore : ICourseStore
    {
        private readonly List<Course> _courses = new();

        public Task<IReadOnlyList<Course>> GetByTenantAsync(Guid tenantId, CancellationToken cancellationToken) =>
            Task.FromResult(_courses.Where(c => c.TenantId == tenantId).ToList() as IReadOnlyList<Course>);

        public async Task AddAsync(Course course, CancellationToken cancellationToken)
        {
            _courses.Add(course);
            await Task.CompletedTask;
        }
    }
}
