using NArchitecture.Core.Mediator;

namespace Okul.Modules.Education.Application.Commands;

public sealed record EnrollCourseCommand(Guid TenantId, Guid StudentId, Guid CourseId, int TotalLessons)
    : IRequest<EnrollCourseResult>;

public sealed record EnrollCourseResult(bool Success, string? ErrorMessage = null);

public sealed class EnrollCourseCommandHandler : IRequestHandler<EnrollCourseCommand, EnrollCourseResult>
{
    private readonly IEnrollmentStore _store;

    public EnrollCourseCommandHandler(IEnrollmentStore store) => _store = store;

    public async Task<EnrollCourseResult> Handle(EnrollCourseCommand request, CancellationToken cancellationToken)
    {
        var existing = await _store.FindByStudentAndCourseAsync(request.StudentId, request.CourseId, cancellationToken);
        if (existing is not null)
            return new EnrollCourseResult(false, "Zaten bu kursa kayıtlı");

        var enrollment = Domain.Enrollment.Create(request.TenantId, request.StudentId, request.CourseId, request.TotalLessons);
        await _store.AddAsync(enrollment, cancellationToken);
        return new EnrollCourseResult(true);
    }
}
