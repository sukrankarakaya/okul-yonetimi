using Microsoft.EntityFrameworkCore;
using Okul.Core.Tenancy;
using Okul.Modules.School.Application;
using Okul.Modules.School.Domain;
using Okul.Modules.School.Infrastructure.Persistence;

namespace Okul.Modules.School.Infrastructure;

/// <summary>Demo okul işlemleri. Kurum filtresi DbContext'te; yazımlar TenantId'yi oturumdan alır (BR-1).</summary>
internal sealed class SchoolService : ISchoolService
{
    /// <summary>Türkiye saati (UTC+3, yaz saati yok).</summary>
    public static readonly TimeSpan TurkeyOffset = TimeSpan.FromHours(3);

    private readonly SchoolDbContext _db;
    private readonly ITenantContext _tenant;
    private readonly TimeProvider _time;
    private readonly IMessageDrafter _drafter;

    public SchoolService(SchoolDbContext db, ITenantContext tenant, TimeProvider time, IMessageDrafter drafter)
    {
        _db = db;
        _tenant = tenant;
        _time = time;
        _drafter = drafter;
    }

    private DateTimeOffset Now => _time.GetUtcNow().ToOffset(TurkeyOffset);

    private DateOnly Today => DateOnly.FromDateTime(Now.DateTime);

    private Guid TenantId => _tenant.TenantId is { } id && id != Guid.Empty
        ? id
        : throw new InvalidOperationException("Tenant context is missing for a tenant-scoped operation.");

    public async Task<DashboardDto> GetDashboardAsync(CancellationToken cancellationToken)
    {
        _ = TenantId;
        var today = Today;
        var students = await _db.Students.AsNoTracking().ToListAsync(cancellationToken);
        var classes = await _db.ClassGroups.AsNoTracking().ToDictionaryAsync(c => c.Id, cancellationToken);
        var todays = await _db.Attendance.AsNoTracking().Where(a => a.Date == today).ToListAsync(cancellationToken);
        var installments = await _db.Installments.AsNoTracking().ToListAsync(cancellationToken);
        var announcements = await _db.Announcements.AsNoTracking().OrderByDescending(a => a.CreatedAt).ToListAsync(cancellationToken);
        var absenceCounts = await AbsenceCountsAsync(cancellationToken);
        var byStudent = students.ToDictionary(s => s.Id);

        var takenClassIds = todays.Select(a => byStudent.GetValueOrDefault(a.StudentId)?.ClassGroupId).Where(id => id is not null).ToHashSet();
        var absentToday = todays.Where(a => a.Status == AttendanceStatus.Absent).Select(a => a.StudentId).ToHashSet();
        var overdue = installments.Where(i => i.IsOverdue(today)).ToList();
        var monthStart = new DateOnly(today.Year, today.Month, 1);
        var thisMonth = installments.Where(i => i.DueDate >= monthStart && i.DueDate < monthStart.AddMonths(1)).ToList();
        var recent = announcements.Where(a => a.CreatedAt >= Now.AddDays(-7)).ToList();
        var overdueByStudent = overdue.GroupBy(i => i.StudentId).ToDictionary(g => g.Key, g => g.Sum(i => i.RemainingKurus));

        return new DashboardDto(
            today,
            students.Count,
            classes.Count,
            absentToday.Count,
            todays.Count(a => a.Status == AttendanceStatus.Late),
            classes.Count - takenClassIds.Count,
            overdue.Sum(i => i.RemainingKurus),
            overdue.Count,
            thisMonth.Sum(i => i.PaidKurus),
            thisMonth.Sum(i => i.AmountKurus),
            recent.Count,
            announcements.Count == 0 ? 0 : announcements.Sum(a => (double)a.ReadCount) / Math.Max(1, announcements.Sum(a => a.RecipientCount)),
            students.Where(s => absentToday.Contains(s.Id))
                .Select(s => ToListItem(s, classes, absenceCounts, overdueByStudent))
                .OrderBy(s => s.ClassName).ThenBy(s => s.Number).ToList(),
            overdue.OrderByDescending(i => i.RemainingKurus).ThenBy(i => i.DueDate).Take(6)
                .Select(i => ToInstallment(i, byStudent, classes, today)).ToList(),
            announcements.Take(3).Select(a => ToAnnouncement(a, classes)).ToList());
    }

    public async Task<IReadOnlyList<ClassGroupDto>> GetClassesAsync(CancellationToken cancellationToken)
    {
        _ = TenantId;
        var counts = await _db.Students.GroupBy(s => s.ClassGroupId)
            .Select(g => new { g.Key, Count = g.Count() }).ToDictionaryAsync(x => x.Key, x => x.Count, cancellationToken);
        var classes = await _db.ClassGroups.AsNoTracking().OrderBy(c => c.GradeLevel).ThenBy(c => c.Name).ToListAsync(cancellationToken);
        return classes.Select(c => new ClassGroupDto(c.Id, c.Name, c.GradeLevel, c.TeacherName, counts.GetValueOrDefault(c.Id))).ToList();
    }

    public async Task<IReadOnlyList<StudentListItemDto>> GetStudentsAsync(Guid? classGroupId, string? search, CancellationToken cancellationToken)
    {
        _ = TenantId;
        var query = _db.Students.AsNoTracking();
        if (classGroupId is { } cid)
        {
            query = query.Where(s => s.ClassGroupId == cid);
        }

        var students = await query.ToListAsync(cancellationToken);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            var tr = System.Globalization.CultureInfo.GetCultureInfo("tr-TR").CompareInfo;
            students = students.Where(s =>
                    tr.IndexOf($"{s.FirstName} {s.LastName}", term, System.Globalization.CompareOptions.IgnoreCase) >= 0
                    || s.Number.ToString(System.Globalization.CultureInfo.InvariantCulture).Contains(term, StringComparison.Ordinal)
                    || tr.IndexOf(s.GuardianName, term, System.Globalization.CompareOptions.IgnoreCase) >= 0)
                .ToList();
        }

        var classes = await _db.ClassGroups.AsNoTracking().ToDictionaryAsync(c => c.Id, cancellationToken);
        var absences = await AbsenceCountsAsync(cancellationToken);
        var today = Today;
        var overdue = (await _db.Installments.AsNoTracking().Where(i => i.DueDate < today && i.PaidKurus < i.AmountKurus).ToListAsync(cancellationToken))
            .GroupBy(i => i.StudentId).ToDictionary(g => g.Key, g => g.Sum(i => i.RemainingKurus));

        return students.Select(s => ToListItem(s, classes, absences, overdue))
            .OrderBy(s => s.ClassName, StringComparer.Ordinal).ThenBy(s => s.Number).ToList();
    }

    public async Task<StudentDetailDto?> GetStudentAsync(Guid studentId, CancellationToken cancellationToken)
    {
        _ = TenantId;
        var s = await _db.Students.AsNoTracking().FirstOrDefaultAsync(x => x.Id == studentId, cancellationToken);
        if (s is null)
        {
            return null;
        }

        var classGroup = await _db.ClassGroups.AsNoTracking().FirstAsync(c => c.Id == s.ClassGroupId, cancellationToken);
        var attendance = await _db.Attendance.AsNoTracking().Where(a => a.StudentId == s.Id).OrderBy(a => a.Date).ToListAsync(cancellationToken);
        var installments = await _db.Installments.AsNoTracking().Where(i => i.StudentId == s.Id).OrderBy(i => i.Number).ToListAsync(cancellationToken);
        var messages = await _db.Messages.AsNoTracking().Where(m => m.StudentId == s.Id).OrderByDescending(m => m.SentAt).ToListAsync(cancellationToken);
        var byStudent = new Dictionary<Guid, Student> { [s.Id] = s };
        var classes = new Dictionary<Guid, ClassGroup> { [classGroup.Id] = classGroup };
        var today = Today;

        return new StudentDetailDto(
            s.Id, s.Number, s.FirstName, s.LastName, classGroup.Id, classGroup.Name, classGroup.TeacherName, s.BirthDate,
            s.HealthNote, s.Allergies, s.PhotoConsent, s.GuardianName, s.GuardianRelation, s.GuardianPhone, s.AuthorizedPickup,
            attendance.TakeLast(10).Select(a => new AttendanceDayDto(a.Date, a.Status)).ToList(),
            attendance.Count(a => a.Status == AttendanceStatus.Absent),
            attendance.Count(a => a.Status == AttendanceStatus.Late),
            installments.Select(i => ToInstallment(i, byStudent, classes, today)).ToList(),
            messages.Select(m => ToMessage(m, byStudent)).ToList());
    }

    public async Task<AttendanceSheetDto?> GetAttendanceAsync(Guid classGroupId, DateOnly date, CancellationToken cancellationToken)
    {
        _ = TenantId;
        var classGroup = await _db.ClassGroups.AsNoTracking().FirstOrDefaultAsync(c => c.Id == classGroupId, cancellationToken);
        if (classGroup is null)
        {
            return null;
        }

        var students = await _db.Students.AsNoTracking().Where(s => s.ClassGroupId == classGroupId).OrderBy(s => s.Number).ToListAsync(cancellationToken);
        var ids = students.Select(s => s.Id).ToList();
        var records = await _db.Attendance.AsNoTracking().Where(a => a.Date == date && ids.Contains(a.StudentId))
            .ToDictionaryAsync(a => a.StudentId, cancellationToken);

        return new AttendanceSheetDto(classGroup.Id, classGroup.Name, date, records.Count > 0,
            students.Select(s => new AttendanceRowDto(s.Id, s.Number, $"{s.FirstName} {s.LastName}",
                records.GetValueOrDefault(s.Id)?.Status, records.GetValueOrDefault(s.Id)?.GuardianNotifiedAt is not null)).ToList());
    }

    public async Task<SaveAttendanceResult> SaveAttendanceAsync(SaveAttendanceRequest request, CancellationToken cancellationToken)
    {
        var tenantId = TenantId;
        if (request.Date > Today)
        {
            throw new SchoolValidationException("Gelecek bir gün için yoklama alınamaz.");
        }

        if (request.Entries.Any(e => !AttendanceStatus.All.Contains(e.Status)))
        {
            throw new SchoolValidationException("Geçersiz yoklama durumu.");
        }

        var studentIds = await _db.Students.Where(s => s.ClassGroupId == request.ClassGroupId).Select(s => s.Id).ToListAsync(cancellationToken);
        if (studentIds.Count == 0 || request.Entries.Any(e => !studentIds.Contains(e.StudentId)))
        {
            throw new SchoolValidationException("Öğrenci bu sınıfta değil.");
        }

        var existing = await _db.Attendance.Where(a => a.Date == request.Date && studentIds.Contains(a.StudentId))
            .ToDictionaryAsync(a => a.StudentId, cancellationToken);
        var notified = 0;
        foreach (var entry in request.Entries)
        {
            if (!existing.TryGetValue(entry.StudentId, out var record))
            {
                record = new AttendanceRecord { Id = Guid.CreateVersion7(), TenantId = tenantId, StudentId = entry.StudentId, Date = request.Date };
                _db.Attendance.Add(record);
            }

            record.Status = entry.Status;

            // BR-5: "yok" olan öğrencinin velisine bir kez bildirim (demo: simüle, gönderim kaydı tutulur).
            if (entry.Status == AttendanceStatus.Absent && record.GuardianNotifiedAt is null)
            {
                record.GuardianNotifiedAt = Now;
                notified++;
            }
        }

        await _db.SaveChangesAsync(cancellationToken);
        return new SaveAttendanceResult(request.Entries.Count, request.Entries.Count(e => e.Status == AttendanceStatus.Absent), notified);
    }

    public async Task<IReadOnlyList<AnnouncementDto>> GetAnnouncementsAsync(CancellationToken cancellationToken)
    {
        _ = TenantId;
        var classes = await _db.ClassGroups.AsNoTracking().ToDictionaryAsync(c => c.Id, cancellationToken);
        var list = await _db.Announcements.AsNoTracking().OrderByDescending(a => a.CreatedAt).ToListAsync(cancellationToken);
        return list.Select(a => ToAnnouncement(a, classes)).ToList();
    }

    public async Task<AnnouncementDto> CreateAnnouncementAsync(CreateAnnouncementRequest request, CancellationToken cancellationToken)
    {
        var tenantId = TenantId;
        var title = request.Title?.Trim() ?? string.Empty;
        var body = request.Body?.Trim() ?? string.Empty;
        if (title.Length is 0 or > 160 || body.Length is 0 or > 4000)
        {
            throw new SchoolValidationException("Başlık (en çok 160) ve metin (en çok 4000 karakter) gerekli.");
        }

        var classes = await _db.ClassGroups.AsNoTracking().ToDictionaryAsync(c => c.Id, cancellationToken);
        if (request.ClassGroupId is { } cid && !classes.ContainsKey(cid))
        {
            throw new SchoolValidationException("Sınıf bulunamadı.");
        }

        var recipients = request.ClassGroupId is { } target
            ? await _db.Students.CountAsync(s => s.ClassGroupId == target, cancellationToken)
            : await _db.Students.CountAsync(cancellationToken);

        var announcement = new Announcement
        {
            Id = Guid.CreateVersion7(),
            TenantId = tenantId,
            ClassGroupId = request.ClassGroupId,
            Title = title,
            Body = body,
            CreatedAt = Now,
            RecipientCount = recipients,
            ReadCount = 0,
        };
        _db.Announcements.Add(announcement);
        await _db.SaveChangesAsync(cancellationToken);
        return ToAnnouncement(announcement, classes);
    }

    public async Task<IReadOnlyList<InstallmentDto>> GetInstallmentsAsync(string? filter, CancellationToken cancellationToken)
    {
        _ = TenantId;
        var today = Today;
        var query = _db.Installments.AsNoTracking();
        query = filter switch
        {
            "overdue" => query.Where(i => i.DueDate < today && i.PaidKurus < i.AmountKurus),
            "upcoming" => query.Where(i => i.DueDate >= today && i.DueDate < today.AddDays(31) && i.PaidKurus < i.AmountKurus),
            "paid" => query.Where(i => i.PaidKurus >= i.AmountKurus),
            _ => query.Where(i => i.DueDate < today.AddDays(31)),
        };

        var list = await query.ToListAsync(cancellationToken);
        var students = await _db.Students.AsNoTracking().ToDictionaryAsync(s => s.Id, cancellationToken);
        var classes = await _db.ClassGroups.AsNoTracking().ToDictionaryAsync(c => c.Id, cancellationToken);
        return list.OrderBy(i => i.DueDate).ThenBy(i => students[i.StudentId].LastName, StringComparer.Create(System.Globalization.CultureInfo.GetCultureInfo("tr-TR"), false))
            .Select(i => ToInstallment(i, students, classes, today)).ToList();
    }

    public async Task<InstallmentDto?> PayInstallmentAsync(Guid installmentId, CancellationToken cancellationToken)
    {
        _ = TenantId;
        var installment = await _db.Installments.FirstOrDefaultAsync(i => i.Id == installmentId, cancellationToken);
        if (installment is null)
        {
            return null;
        }

        if (installment.RemainingKurus <= 0)
        {
            throw new SchoolValidationException("Bu taksit zaten ödenmiş.");
        }

        // Demo: kalan tutarın tamamı tahsil edilir, makbuz numarası verilir (kesilen makbuz değişmez, BR-4).
        installment.PaidKurus = installment.AmountKurus;
        installment.PaidAt = Now;
        installment.ReceiptNo = $"MKB-{Now:yyyyMMdd}-{Random.Shared.Next(10000, 99999)}";
        await _db.SaveChangesAsync(cancellationToken);

        var students = await _db.Students.AsNoTracking().Where(s => s.Id == installment.StudentId).ToDictionaryAsync(s => s.Id, cancellationToken);
        var classes = await _db.ClassGroups.AsNoTracking().ToDictionaryAsync(c => c.Id, cancellationToken);
        return ToInstallment(installment, students, classes, Today);
    }

    public async Task<DraftMessageResult?> DraftMessageAsync(DraftMessageRequest request, CancellationToken cancellationToken)
    {
        _ = TenantId;
        var note = request.TeacherNote?.Trim() ?? string.Empty;
        if (note.Length is 0 or > 2000)
        {
            throw new SchoolValidationException("Öğretmen notu gerekli (en çok 2000 karakter).");
        }

        var s = await _db.Students.AsNoTracking().FirstOrDefaultAsync(x => x.Id == request.StudentId, cancellationToken);
        if (s is null)
        {
            return null;
        }

        var classGroup = await _db.ClassGroups.AsNoTracking().FirstAsync(c => c.Id == s.ClassGroupId, cancellationToken);
        var attendance = await _db.Attendance.AsNoTracking().Where(a => a.StudentId == s.Id).Select(a => a.Status).ToListAsync(cancellationToken);

        return await _drafter.DraftAsync(new MessageDraftInput(
            s.FirstName, $"{s.FirstName} {s.LastName}", classGroup.Name, s.GuardianName, s.GuardianRelation, classGroup.TeacherName,
            note, attendance.Count(a => a == AttendanceStatus.Absent), attendance.Count(a => a == AttendanceStatus.Late)), cancellationToken);
    }

    public async Task<GuardianMessageDto?> SendMessageAsync(SendMessageRequest request, string sentBy, CancellationToken cancellationToken)
    {
        var tenantId = TenantId;
        var body = request.Body?.Trim() ?? string.Empty;
        if (body.Length is 0 or > 4000)
        {
            throw new SchoolValidationException("Mesaj metni gerekli (en çok 4000 karakter).");
        }

        var s = await _db.Students.AsNoTracking().FirstOrDefaultAsync(x => x.Id == request.StudentId, cancellationToken);
        if (s is null)
        {
            return null;
        }

        // BR-9: bu uç yalnızca öğretmenin onayladığı (gerekirse düzenlediği) metni alır; AI kendisi göndermez.
        var message = new GuardianMessage
        {
            Id = Guid.CreateVersion7(),
            TenantId = tenantId,
            StudentId = s.Id,
            Body = body,
            AiAssisted = request.AiAssisted,
            SentBy = sentBy,
            SentAt = Now,
        };
        _db.Messages.Add(message);
        await _db.SaveChangesAsync(cancellationToken);
        return ToMessage(message, new Dictionary<Guid, Student> { [s.Id] = s });
    }

    public async Task<IReadOnlyList<GuardianMessageDto>> GetMessagesAsync(CancellationToken cancellationToken)
    {
        _ = TenantId;
        var messages = await _db.Messages.AsNoTracking().OrderByDescending(m => m.SentAt).Take(50).ToListAsync(cancellationToken);
        var ids = messages.Select(m => m.StudentId).Distinct().ToList();
        var students = await _db.Students.AsNoTracking().Where(s => ids.Contains(s.Id)).ToDictionaryAsync(s => s.Id, cancellationToken);
        return messages.Select(m => ToMessage(m, students)).ToList();
    }

    private async Task<Dictionary<Guid, int>> AbsenceCountsAsync(CancellationToken cancellationToken) =>
        await _db.Attendance.Where(a => a.Status == AttendanceStatus.Absent).GroupBy(a => a.StudentId)
            .Select(g => new { g.Key, Count = g.Count() }).ToDictionaryAsync(x => x.Key, x => x.Count, cancellationToken);

    private static StudentListItemDto ToListItem(
        Student s, IReadOnlyDictionary<Guid, ClassGroup> classes, IReadOnlyDictionary<Guid, int> absences, IReadOnlyDictionary<Guid, long> overdue) =>
        new(s.Id, s.Number, $"{s.FirstName} {s.LastName}", s.ClassGroupId, classes.GetValueOrDefault(s.ClassGroupId)?.Name ?? "?",
            s.GuardianName, absences.GetValueOrDefault(s.Id), overdue.GetValueOrDefault(s.Id),
            s.HealthNote is not null || s.Allergies is not null);

    private static InstallmentDto ToInstallment(
        Installment i, IReadOnlyDictionary<Guid, Student> students, IReadOnlyDictionary<Guid, ClassGroup> classes, DateOnly today)
    {
        var student = students.GetValueOrDefault(i.StudentId);
        var status = i.RemainingKurus <= 0 ? "paid" : i.IsOverdue(today) ? "overdue" : i.PaidKurus > 0 ? "partial" : "pending";
        return new InstallmentDto(i.Id, i.StudentId, student is null ? "?" : $"{student.FirstName} {student.LastName}",
            student is null ? "?" : classes.GetValueOrDefault(student.ClassGroupId)?.Name ?? "?",
            i.Number, i.DueDate, i.AmountKurus, i.PaidKurus, i.RemainingKurus, status, i.ReceiptNo, i.PaidAt);
    }

    private static AnnouncementDto ToAnnouncement(Announcement a, IReadOnlyDictionary<Guid, ClassGroup> classes) =>
        new(a.Id, a.Title, a.Body, a.ClassGroupId,
            a.ClassGroupId is { } cid ? $"{classes.GetValueOrDefault(cid)?.Name ?? "?"} velileri" : "Tüm okul",
            a.CreatedAt, a.RecipientCount, a.ReadCount);

    private static GuardianMessageDto ToMessage(GuardianMessage m, IReadOnlyDictionary<Guid, Student> students) =>
        new(m.Id, m.StudentId, students.GetValueOrDefault(m.StudentId) is { } s ? $"{s.FirstName} {s.LastName}" : "?",
            m.Body, m.AiAssisted, m.SentBy, m.SentAt);
}
