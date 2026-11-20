using Okul.Modules.Education.Domain;

namespace Okul.Modules.Education.Application;

public interface ICourseStore
{
    Task<IReadOnlyList<Course>> GetByTenantAsync(Guid tenantId, CancellationToken cancellationToken);
    Task AddAsync(Course course, CancellationToken cancellationToken);
}

public interface IEnrollmentStore
{
    Task<Enrollment?> FindByStudentAndCourseAsync(Guid studentId, Guid courseId, CancellationToken cancellationToken);
    Task<Enrollment?> FindByIdAsync(Guid id, CancellationToken cancellationToken);
    Task AddAsync(Enrollment enrollment, CancellationToken cancellationToken);
}

public interface ILessonStore
{
    Task<IReadOnlyList<Lesson>> GetByCourseAsync(Guid courseId, CancellationToken cancellationToken);
    Task AddAsync(Lesson lesson, CancellationToken cancellationToken);
}

public interface ILessonCompletionStore
{
    Task AddAsync(Domain.LessonCompletion completion, CancellationToken cancellationToken);
}
