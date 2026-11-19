namespace Okul.Modules.School.Domain;

// Demo modülü (AGENTS.md, Demo modu): sade varlıklar. Her varlık TenantId taşır → kurum filtresi (BR-1).

/// <summary>Bir dönemde bir şubedeki öğrenci grubu (7-A).</summary>
public sealed class ClassGroup
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public string Name { get; set; } = string.Empty;
    public int GradeLevel { get; set; }
    public string TeacherName { get; set; } = string.Empty;
}

/// <summary>Öğrenci kartı: kimlik, sağlık, veli bilgisi.</summary>
public sealed class Student
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid ClassGroupId { get; set; }
    public int Number { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public DateOnly BirthDate { get; set; }
    public string? HealthNote { get; set; }
    public string? Allergies { get; set; }
    public bool PhotoConsent { get; set; }
    public string GuardianName { get; set; } = string.Empty;
    public string GuardianRelation { get; set; } = string.Empty;
    public string GuardianPhone { get; set; } = string.Empty;
    public string? AuthorizedPickup { get; set; }
}

public static class AttendanceStatus
{
    public const string Present = "present";
    public const string Absent = "absent";
    public const string Late = "late";
    public const string Excused = "excused";

    public static readonly IReadOnlyList<string> All = [Present, Absent, Late, Excused];
}

/// <summary>Bir gün için öğrenci başına yoklama kaydı (demo: günlük yoklama, BR-10).</summary>
public sealed class AttendanceRecord
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid StudentId { get; set; }
    public DateOnly Date { get; set; }
    public string Status { get; set; } = AttendanceStatus.Present;

    /// <summary>Veliye devamsızlık bildirimi gittiği an; ikinci kez gönderilmez (BR-5).</summary>
    public DateTimeOffset? GuardianNotifiedAt { get; set; }
}

/// <summary>Kurum veya sınıf hedefli tek yönlü bildirim; okundu bilgisi tutulur.</summary>
public sealed class Announcement
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid? ClassGroupId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
    public int RecipientCount { get; set; }
    public int ReadCount { get; set; }
}

/// <summary>Taksit: tutarlar kuruş cinsinden (BR-2).</summary>
public sealed class Installment
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid StudentId { get; set; }
    public int Number { get; set; }
    public DateOnly DueDate { get; set; }
    public long AmountKurus { get; set; }
    public long PaidKurus { get; set; }
    public DateTimeOffset? PaidAt { get; set; }
    public string? ReceiptNo { get; set; }

    public long RemainingKurus => AmountKurus - PaidKurus;

    /// <summary>BR-3: vadesi geçmiş ve kalan tutarı &gt; 0.</summary>
    public bool IsOverdue(DateOnly today) => DueDate < today && RemainingKurus > 0;
}

/// <summary>Öğretmenden veliye gönderilmiş mesaj (demo: gönderim simüle).</summary>
public sealed class GuardianMessage
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid StudentId { get; set; }
    public string Body { get; set; } = string.Empty;
    public bool AiAssisted { get; set; }
    public string SentBy { get; set; } = string.Empty;
    public DateTimeOffset SentAt { get; set; }
}
