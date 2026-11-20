# 0006 — Veli Dashboard

Girişten sonra veli ana ekranı: çocuk listesi, haber, ders takvimi özeti.

## Acceptance Criteria

- AC-1: Veli giriş sonrası dashboard'a yönlendir
- AC-2: Çocukları (Öğrenci) listele (fotoğraf, ad, sınıf)
- AC-3: Her çocuğun ders takviminin bugünün derslerini göster (5 ders limit)
- AC-4: "Duyurular" bölümü (son 3 duyuru, tarih + başlık)
- AC-5: Profil menüsü (çıkış, ayarlar link)
- AC-6: Responsive (web + mobil)
- AC-7: Erişilebilirlik (screen reader, 1.5× text zoom)

## User Flow

```
Login (0005) → SMS verify → KVKK → [0006] Dashboard
                                      ├─ Çocuk seç
                                      ├─ Bugünün dersleri
                                      ├─ Duyurular
                                      └─ Profil (çıkış)
```

## Technical Scope

**Backend:**
- Parent-Student relationship queries (tenant-scoped)
- Student schedule query (today's classes)
- Announcement feed (recent 3, public)

**Frontend (Web + Mobil):**
- Dashboard layout (header, 2 column)
- Student card (photo, name, grade)
- Class timetable widget
- Announcement list
- Logout + settings

**Testing:**
- E2E: Login → Dashboard render
- Unit: Schedule query, announcement order
- Component: Card, list, accessibility

## Status

- Status: Spec
- Approved: (pending)
- Next: /plan 0006
