namespace Okul.Modules.School.Application;

// Demo modülü (AGENTS.md, Demo modu). Hiçbir uçta kurum parametresi yok: kurum oturumdan gelir (BR-1).

public sealed record ClassGroupDto(Guid Id, string Name, int GradeLevel, string TeacherName, int StudentCount);

public sealed record StudentListItemDto(
    Guid Id, int Number, string FullName, Guid ClassGroupId, string ClassName, string GuardianName,
    int AbsenceCount, long OverdueKurus, bool HasHealthNote);

public sealed record InstallmentDto(
    Guid Id, Guid StudentId, string StudentName, string ClassName, int Number, DateOnly DueDate,
    long AmountKurus, long PaidKurus, long RemainingKurus, string Status, string? ReceiptNo, DateTimeOffset? PaidAt);

public sealed record AttendanceDayDto(DateOnly Date, string Status);

public sealed record GuardianMessageDto(
    Guid Id, Guid StudentId, string StudentName, string Body, bool AiAssisted, string SentBy, DateTimeOffset SentAt);

public sealed record StudentDetailDto(
    Guid Id, int Number, string FirstName, string LastName, Guid ClassGroupId, string ClassName, string TeacherName,
    DateOnly BirthDate, string? HealthNote, string? Allergies, bool PhotoConsent,
    string GuardianName, string GuardianRelation, string GuardianPhone, string? AuthorizedPickup,
    IReadOnlyList<AttendanceDayDto> RecentAttendance, int AbsenceCount, int LateCount,
    IReadOnlyList<InstallmentDto> Installments, IReadOnlyList<GuardianMessageDto> Messages);

public sealed record AttendanceRowDto(Guid StudentId, int Number, string FullName, string? Status, bool GuardianNotified);

public sealed record AttendanceSheetDto(Guid ClassGroupId, string ClassName, DateOnly Date, bool Taken,
    IReadOnlyList<AttendanceRowDto> Rows);

public sealed record AttendanceEntry(Guid StudentId, string Status);

public sealed record SaveAttendanceRequest(Guid ClassGroupId, DateOnly Date, IReadOnlyList<AttendanceEntry> Entries);

public sealed record SaveAttendanceResult(int Saved, int Absent, int GuardiansNotified);

public sealed record AnnouncementDto(
    Guid Id, string Title, string Body, Guid? ClassGroupId, string Target, DateTimeOffset CreatedAt,
    int RecipientCount, int ReadCount);

public sealed record CreateAnnouncementRequest(string Title, string Body, Guid? ClassGroupId);

public sealed record PayInstallmentResult(InstallmentDto Installment);

public sealed record DashboardDto(
    DateOnly Today, int StudentCount, int ClassCount, int AbsentToday, int LateToday, int ClassesWithoutAttendance,
    long OverdueKurus, int OverdueInstallmentCount, long CollectedThisMonthKurus, long ExpectedThisMonthKurus,
    int AnnouncementsLast7Days, double AnnouncementReadRate,
    IReadOnlyList<StudentListItemDto> AbsentStudents, IReadOnlyList<InstallmentDto> TopOverdue,
    IReadOnlyList<AnnouncementDto> RecentAnnouncements);

public sealed record DraftMessageRequest(Guid StudentId, string TeacherNote);

/// <summary>AI taslağı: insan onaylamadan veliye gitmez (BR-9); kaynak veri görünür (AI ilkeleri).</summary>
public sealed record DraftMessageResult(string Draft, string Engine, IReadOnlyList<string> Sources);

public sealed record SendMessageRequest(Guid StudentId, string Body, bool AiAssisted);

public interface ISchoolService
{
    Task<DashboardDto> GetDashboardAsync(CancellationToken cancellationToken);

    Task<IReadOnlyList<ClassGroupDto>> GetClassesAsync(CancellationToken cancellationToken);

    Task<IReadOnlyList<StudentListItemDto>> GetStudentsAsync(Guid? classGroupId, string? search, CancellationToken cancellationToken);

    Task<StudentDetailDto?> GetStudentAsync(Guid studentId, CancellationToken cancellationToken);

    Task<AttendanceSheetDto?> GetAttendanceAsync(Guid classGroupId, DateOnly date, CancellationToken cancellationToken);

    Task<SaveAttendanceResult> SaveAttendanceAsync(SaveAttendanceRequest request, CancellationToken cancellationToken);

    Task<IReadOnlyList<AnnouncementDto>> GetAnnouncementsAsync(CancellationToken cancellationToken);

    Task<AnnouncementDto> CreateAnnouncementAsync(CreateAnnouncementRequest request, CancellationToken cancellationToken);

    Task<IReadOnlyList<InstallmentDto>> GetInstallmentsAsync(string? filter, CancellationToken cancellationToken);

    Task<InstallmentDto?> PayInstallmentAsync(Guid installmentId, CancellationToken cancellationToken);

    Task<DraftMessageResult?> DraftMessageAsync(DraftMessageRequest request, CancellationToken cancellationToken);

    Task<GuardianMessageDto?> SendMessageAsync(SendMessageRequest request, string sentBy, CancellationToken cancellationToken);

    Task<IReadOnlyList<GuardianMessageDto>> GetMessagesAsync(CancellationToken cancellationToken);
}

/// <summary>Öğretmen notundan kibar veli mesajı taslağı üretir (AI veya şablon).</summary>
public interface IMessageDrafter
{
    Task<DraftMessageResult> DraftAsync(MessageDraftInput input, CancellationToken cancellationToken);
}

public sealed record MessageDraftInput(
    string StudentFirstName, string StudentFullName, string ClassName, string GuardianName, string GuardianRelation,
    string TeacherName, string TeacherNote, int AbsenceCount, int LateCount);

/// <summary>Kullanıcı hatası (400) — WebApi bunu ProblemDetails'e çevirir.</summary>
public sealed class SchoolValidationException(string message) : Exception(message);
