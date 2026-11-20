using NArchitecture.Core.Mediator;

namespace Okul.Modules.Education.Application.Commands;

public sealed record CompleteLessonCommand(Guid EnrollmentId, Guid LessonId)
    : IRequest<CompleteLessonResult>;

public sealed record CompleteLessonResult(bool Success, int ProgressPercent);

public sealed class CompleteLessonCommandHandler : IRequestHandler<CompleteLessonCommand, CompleteLessonResult>
{
    private readonly IEnrollmentStore _enrollmentStore;
    private readonly ILessonCompletionStore _completionStore;

    public CompleteLessonCommandHandler(IEnrollmentStore enrollmentStore, ILessonCompletionStore completionStore)
    {
        _enrollmentStore = enrollmentStore;
        _completionStore = completionStore;
    }

    public async Task<CompleteLessonResult> Handle(CompleteLessonCommand request, CancellationToken cancellationToken)
    {
        var enrollment = await _enrollmentStore.FindByIdAsync(request.EnrollmentId, cancellationToken);
        if (enrollment is null)
            return new CompleteLessonResult(false, 0);

        enrollment.CompleteLesson();
        await _enrollmentStore.SaveChangesAsync(cancellationToken);

        var completion = Domain.LessonCompletion.Create(request.EnrollmentId, request.LessonId);
        await _completionStore.AddAsync(completion, cancellationToken);

        return new CompleteLessonResult(true, enrollment.GetProgressPercent());
    }
}
