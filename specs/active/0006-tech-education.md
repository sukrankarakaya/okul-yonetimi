# 0006 — Teknoloji Eğitim Platformu

Öğrencilerin online kurslar alıp ilerleme takip ettiği platform. Kurslar, dersler, ilerleme yüzdesi, duyurular.

## Acceptance Criteria

- AC-1: Kursları listele (başlık, ikon, seviye, ilerleme %)
- AC-2: Kursa kayıt ol (POST /enrollments)
- AC-3: Ilerleme takip (tamamlanmış dersler / toplam)
- AC-4: Bu haftanın derslerini göster
- AC-5: Duyurular (kurs-spesifik + global)
- AC-6: Responsive (web + mobil)
- AC-7: Erişilebilirlik (a11y, keyboard nav)
- AC-8: Eğitmen filtreleme (kursları eğitmene göre filtrele)

## User Flow

```
Giriş → Kurslarım (liste)
     → Kursu seç → İlerleme göster
     → Bugünün dersleri → Ders tamamla
     → Duyurular bölümü
```

## Technical Scope

**Backend:** 36 unit/integration test
- Domain: Course, Enrollment, Lesson, LessonCompletion
- API: 5 endpoint (list, enroll, progress, complete, schedule)
- Persistence: EducationDbContext, 3 store

**Frontend:** 12 component/mobile test
- Web: CourseCard, CourseGrid, ScheduleList, Dashboard
- Mobil: CoursesScreen, ScheduleScreen

## Status

- Status: Spec
- Approved: (pending)
- Next: /plan 0006

---

## Plan: specs/plans/0006-plan.md
- 6 adım (Domain → API → Persistence → Web → Mobil → Kapanış)
- 3–4 iş günü
- 36 test total
