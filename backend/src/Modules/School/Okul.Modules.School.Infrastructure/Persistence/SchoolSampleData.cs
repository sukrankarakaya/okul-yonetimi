using Microsoft.EntityFrameworkCore;
using Okul.Modules.School.Domain;

namespace Okul.Modules.School.Infrastructure.Persistence;

/// <summary>
/// Demo örnek verisi (yalnızca Development). Deterministik (sabit tohum): her kurulumda aynı okul çıkar.
/// Tarihler kurulum gününe göre göreli: son 10 iş günü yoklama, Eylül'den Haziran'a 10 taksit.
/// </summary>
public static class SchoolSampleData
{
    // Kurum kimlikleri InstitutionSampleData ile aynı (modüller arası referans yok; sabit değer).
    public static readonly Guid AtaturkKolejiId = Guid.Parse("019a8f00-0000-7000-8000-00000000000a");
    public static readonly Guid BilgeEtutId = Guid.Parse("019a8f00-0000-7000-8000-00000000000b");

    private static readonly string[] GirlNames =
    [
        "Zeynep", "Elif", "Defne", "Ecrin", "Azra", "Nehir", "Asel", "Eylül", "Mira", "Duru", "Ela", "İpek",
        "Su", "Ada", "Lina", "Öykü", "Nisa", "Beren", "Ceren", "Şevval", "Yağmur", "Ilgın", "Çağla", "Melis",
    ];

    private static readonly string[] BoyNames =
    [
        "Yusuf", "Eymen", "Ömer", "Mustafa", "Aras", "Kerem", "Alparslan", "Göktuğ", "Çınar", "Emir", "Mert",
        "Deniz", "Efe", "Arda", "Batuhan", "Ilgaz", "İlker", "Kaan", "Onur", "Selim", "Tuna", "Ural", "Barış", "Doruk",
    ];

    private static readonly string[] LastNames =
    [
        "Yılmaz", "Kaya", "Demir", "Şahin", "Çelik", "Yıldız", "Yıldırım", "Öztürk", "Aydın", "Özdemir", "Arslan",
        "Doğan", "Kılıç", "Aslan", "Çetin", "Kara", "Koç", "Kurt", "Özkan", "Şimşek", "Polat", "Erdoğan", "Güneş",
        "Aksoy", "Turan", "Ünal", "İnce", "Bulut", "Tekin", "Karaca",
    ];

    private static readonly string[] ParentFirstNames =
    [
        "Ayşe", "Fatma", "Emine", "Hatice", "Zehra", "Esra", "Gül", "Selin", "Burcu", "Derya", "Mehmet", "Ahmet",
        "Ali", "Hasan", "Murat", "Serkan", "Emre", "Burak", "Cem", "Tolga",
    ];

    private static readonly string[] HealthNotes =
        ["Astım — inhaler çantasında.", "Gözlük kullanıyor, ön sırada oturmalı.", "Tip 1 diyabet — öğle öncesi ölçüm.", "Epilepsi öyküsü, ilacı revirde."];

    private static readonly string[] AllergyNotes = ["Fıstık", "Laktoz", "Polen", "Penisilin", "Yumurta", "Gluten"];

    private static readonly (string Name, int Grade, string Teacher)[] AtaturkClasses =
    [
        ("5-A", 5, "Selin Aydın"), ("5-B", 5, "Murat Koç"), ("6-A", 6, "Derya Çetin"),
        ("7-A", 7, "Emre Kılıç"), ("7-B", 7, "Gül Özkan"), ("8-A", 8, "Serkan Turan"),
    ];

    public const long AnnualFeeKurus = 180_000_00; // 180.000 TL
    public const int InstallmentCount = 10;

    public static async Task SeedAsync(SchoolDbContext db, DateOnly today, CancellationToken cancellationToken = default)
    {
        if (await db.ClassGroups.IgnoreQueryFilters().AnyAsync(cancellationToken)) // sistem işi: kurumlar arası (T-2)
        {
            return;
        }

        var random = new Random(20261119);
        SeedSchool(db, random, AtaturkKolejiId, AtaturkClasses, studentsPerClass: 16, today, idPrefix: 0xA);
        SeedSchool(db, random, BilgeEtutId, [("LGS-1", 8, "Hasan Polat")], studentsPerClass: 8, today, idPrefix: 0xB);
        await db.SaveChangesAsync(cancellationToken);
    }

    private static void SeedSchool(
        SchoolDbContext db, Random random, Guid tenantId, (string Name, int Grade, string Teacher)[] classes,
        int studentsPerClass, DateOnly today, int idPrefix)
    {
        var schoolDays = LastSchoolDays(today, 10);
        var counter = 0;
        Guid NextId() => Guid.Parse($"019a9{idPrefix:x1}00-0000-7000-8000-{++counter:x12}");

        for (var c = 0; c < classes.Length; c++)
        {
            var (name, grade, teacher) = classes[c];
            var classGroup = new ClassGroup { Id = NextId(), TenantId = tenantId, Name = name, GradeLevel = grade, TeacherName = teacher };
            db.ClassGroups.Add(classGroup);

            // Bugünün yoklaması: son sınıf hariç hepsi alınmış (panelde "yoklama alınmamış" görünsün).
            var todayTaken = c < classes.Length - 1;

            for (var n = 1; n <= studentsPerClass; n++)
            {
                var girl = random.Next(2) == 0;
                var first = girl ? GirlNames[random.Next(GirlNames.Length)] : BoyNames[random.Next(BoyNames.Length)];
                var last = LastNames[random.Next(LastNames.Length)];
                var motherFirst = ParentFirstNames[random.Next(10)];
                var fatherFirst = ParentFirstNames[10 + random.Next(10)];
                var mother = random.Next(3) > 0;

                var student = new Student
                {
                    Id = NextId(),
                    TenantId = tenantId,
                    ClassGroupId = classGroup.Id,
                    Number = 100 * grade + 10 * c + n,
                    FirstName = first,
                    LastName = last,
                    BirthDate = new DateOnly(2026 - grade - 6, 1 + random.Next(12), 1 + random.Next(28)),
                    HealthNote = random.Next(8) == 0 ? HealthNotes[random.Next(HealthNotes.Length)] : null,
                    Allergies = random.Next(6) == 0 ? AllergyNotes[random.Next(AllergyNotes.Length)] : null,
                    PhotoConsent = random.Next(10) > 0,
                    GuardianName = $"{(mother ? motherFirst : fatherFirst)} {last}",
                    GuardianRelation = mother ? "Anne" : "Baba",
                    GuardianPhone = $"05{30 + random.Next(30)} {random.Next(100, 999)} {random.Next(10, 99)} {random.Next(10, 99)}",
                    AuthorizedPickup = random.Next(3) == 0 ? $"{ParentFirstNames[random.Next(ParentFirstNames.Length)]} {LastNames[random.Next(LastNames.Length)]} (servis/akraba)" : null,
                };
                db.Students.Add(student);

                // Yoklama: öğrencilerin çoğu düzenli; birkaçı sık devamsız (panelde risk görünsün).
                var frequentAbsent = random.Next(9) == 0;
                foreach (var day in schoolDays)
                {
                    if (day == today && !todayTaken)
                    {
                        continue;
                    }

                    var roll = random.Next(100);
                    var status = roll < (frequentAbsent ? 25 : 4) ? AttendanceStatus.Absent
                        : roll < (frequentAbsent ? 35 : 8) ? AttendanceStatus.Late
                        : roll < (frequentAbsent ? 38 : 9) ? AttendanceStatus.Excused
                        : AttendanceStatus.Present;
                    db.Attendance.Add(new AttendanceRecord
                    {
                        Id = NextId(),
                        TenantId = tenantId,
                        StudentId = student.Id,
                        Date = day,
                        Status = status,
                        GuardianNotifiedAt = status == AttendanceStatus.Absent
                            ? new DateTimeOffset(day.ToDateTime(new TimeOnly(9, 5)), TimeSpan.FromHours(3))
                            : null,
                    });
                }

                // Taksitler: indirim (kardeş/burs) + 10 eşit taksit, kalan kuruş son taksitte (BR-11).
                var discountPercent = random.Next(10) switch { 0 => 25, 1 or 2 => 10, _ => 0 };
                var net = AnnualFeeKurus * (100 - discountPercent) / 100;
                var each = net / InstallmentCount;
                var payer = random.Next(100);
                for (var i = 1; i <= InstallmentCount; i++)
                {
                    var due = new DateOnly(2026, 9, 5).AddMonths(i - 1);
                    var amount = i == InstallmentCount ? net - each * (InstallmentCount - 1) : each;
                    var paid = 0L;
                    if (due < today)
                    {
                        // %80 düzenli öder; %12 son taksiti geciktirir; %8 iki taksit geride.
                        var monthsBehind = payer < 80 ? 0 : payer < 92 ? 1 : 2;
                        var dueIndexFromLatest = CountDueBefore(today) - i;
                        paid = dueIndexFromLatest >= monthsBehind ? amount : 0;
                        if (paid == 0 && payer % 3 == 0)
                        {
                            paid = amount / 2; // kısmi ödeme
                        }
                    }

                    db.Installments.Add(new Installment
                    {
                        Id = NextId(),
                        TenantId = tenantId,
                        StudentId = student.Id,
                        Number = i,
                        DueDate = due,
                        AmountKurus = amount,
                        PaidKurus = paid,
                        PaidAt = paid > 0 ? new DateTimeOffset(due.AddDays(random.Next(-3, 4)).ToDateTime(new TimeOnly(14, 0)), TimeSpan.FromHours(3)) : null,
                        ReceiptNo = paid == amount ? $"MKB-{due:yyyyMM}-{counter:D5}" : null,
                    });
                }
            }
        }

        var now = new DateTimeOffset(today.ToDateTime(new TimeOnly(8, 30)), TimeSpan.FromHours(3));
        var total = classes.Length * studentsPerClass;
        db.Announcements.AddRange(
            new Announcement
            {
                Id = NextId(),
                TenantId = tenantId,
                Title = "Veli toplantısı — 27 Kasım Perşembe",
                Body = "Değerli velilerimiz, 1. dönem veli toplantımız 27 Kasım Perşembe 17.30'da sınıflarda yapılacaktır.",
                CreatedAt = now.AddDays(-2),
                RecipientCount = total,
                ReadCount = (int)(total * 0.82),
            },
            new Announcement
            {
                Id = NextId(),
                TenantId = tenantId,
                Title = "Kış dönemi servis saatleri",
                Body = "1 Aralık'tan itibaren sabah servisleri 15 dakika erken hareket edecektir.",
                CreatedAt = now.AddDays(-5),
                RecipientCount = total,
                ReadCount = (int)(total * 0.91),
            },
            new Announcement
            {
                Id = NextId(),
                TenantId = tenantId,
                ClassGroupId = null,
                Title = "Bilim şenliği gönüllüleri",
                Body = "Aralık sonundaki bilim şenliğimiz için gönüllü velilerimizi rehberlik servisine bekliyoruz.",
                CreatedAt = now.AddDays(-9),
                RecipientCount = total,
                ReadCount = (int)(total * 0.64),
            });
    }

    private static int CountDueBefore(DateOnly today)
    {
        var count = 0;
        for (var i = 0; i < InstallmentCount; i++)
        {
            if (new DateOnly(2026, 9, 5).AddMonths(i) < today)
            {
                count++;
            }
        }

        return count;
    }

    /// <summary>Bugün dahil son <paramref name="count"/> iş günü (bugün hafta sonuysa önceki iş günleri).</summary>
    public static IReadOnlyList<DateOnly> LastSchoolDays(DateOnly today, int count)
    {
        var days = new List<DateOnly>();
        for (var d = today; days.Count < count; d = d.AddDays(-1))
        {
            if (d.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday))
            {
                days.Add(d);
            }
        }

        days.Reverse();
        return days;
    }
}
