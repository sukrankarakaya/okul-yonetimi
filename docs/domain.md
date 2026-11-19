# Domain

> Bootstrap 2026-11-17. Ürün kapsamı ve roller: `docs/kapsam.md`. Bir terim burada yoksa, ajan kendi
> anlamını uydurur — eksik terimi ekleyin.

## Ubiquitous language

| Terim | Kod adı | Anlam | Karıştırılmasın |
|---|---|---|---|
| Kurum | `Institution` | Sistemi satın alan okul/kurs. **Tenant budur.** | Şube |
| Şube | `Campus` | Kurumun fiziksel yerleşkesi (kampüs) | Sınıf ("7-A şubesi" anlamında kullanılmaz) |
| Sınıf seviyesi | `GradeLevel` | 1–12, anaokulu yaş grubu, kurs seviyesi | Sınıf |
| Sınıf | `ClassGroup` | Bir dönemde bir şubedeki öğrenci grubu (7-A) | |
| Dönem | `AcademicTerm` | Eğitim-öğretim yılı (2026-2027) ve yarıyılları | Taksit dönemi |
| Ders saati | `Lesson` | Ders programındaki tek bir ders oturumu (sınıf + ders + öğretmen + zaman) | |
| Öğrenci kartı | `Student` | Öğrencinin kimlik, sağlık, veli ve teslim bilgileri | |
| Veli | `Guardian` | Öğrenciden sorumlu yetişkin; öğrenci n veliye, veli n öğrenciye bağlanır | Kullanıcı |
| Kardeş bağı | `Sibling` | Aynı veliye bağlı öğrencilerin ilişkisi; veli tek hesapla tümünü görür | |
| Teslim alabilecek kişi | `AuthorizedPickup` | Öğrenciyi okuldan alma yetkisi olan kişi; veli olmak zorunda değil | Veli |
| Kullanıcı | `User` | Giriş yapan kişi; birden fazla rol taşıyabilir (öğretmen + veli) | Rol |
| Rol | `Role` | Faz 1: Kurucu/Yönetici, Öğretmen, Veli, Muhasebe/İdari | |
| Yoklama | `AttendanceRecord` | Bir ders saati için öğrenci başına var / yok / geç / izinli kaydı | Devamsızlık |
| Devamsızlık | `Absence` (hesaplanan) | Yoklama kayıtlarından türetilen toplam | |
| Duyuru | `Announcement` | Kurum, sınıf veya öğrenci hedefli tek yönlü bildirim; okundu bilgisi tutulur | Mesaj |
| Mesaj | `Message` | Öğretmen ↔ veli birebir yazışma | Duyuru |
| Onay formu | `ConsentForm` | Veliden evet/hayır yanıtı isteyen duyuru (gezi izni) | KVKK açık rızası |
| Ödev | `Assignment` | Öğretmenin verdiği iş + öğrenci bazlı teslim durumu | |
| Not | `Grade` | Yazılı/sözlü değerlendirme puanı | Öğretmen notu (gözlem) |
| Ücret sözleşmesi | `FeeAgreement` | Öğrencinin bir dönemlik yıllık ücreti + indirim/burs + taksit planı | |
| Taksit | `Installment` | Vadesi, tutarı, kalan tutarı ve durumu olan tek ödeme kalemi | |
| Cari | `Account` | Velinin borç-alacak hesabı | |
| Makbuz | `Receipt` | Tahsilatın belgesi; resmi fatura değil (e-Fatura Faz 2) | Fatura |
| Müsaitlik | `Availability` | Öğretmenin randevuya açık zaman dilimleri | |
| Randevu | `Appointment` | Veli talebiyle oluşan, öğretmenin onayladığı görüşme | |
| AI taslağı | `AiDraft` | AI'ın ürettiği, insan onayı bekleyen içerik; kaynak veri referansı taşır | Gönderilmiş mesaj |
| Sessiz mod | `QuietHours` | Öğretmenin mesai dışı; bildirimler ertelenir, mesaj kaybolmaz | |
| Açık rıza | `ConsentRecord` | Velinin aydınlatma metnini/rızayı onayladığı sürümlü kayıt (KVKK) | Onay formu |
| Audit log | `AuditEntry` | Kim, hangi kaydı, ne zaman görüntüledi/değiştirdi | Uygulama logu |

## Business rules
- **BR-1:** Bir kurumun verisini başka bir kurumun kullanıcısı hiçbir koşulda göremez veya değiştiremez.
- **BR-2:** Para tutarları TRY cinsinden, **kuruş olarak tam sayı** (`long`) saklanır ve hesaplanır.
- **BR-3:** Vadesi geçmiş ve kalan tutarı > 0 olan taksit "gecikmiş"tir; gecikme hatırlatması bu duruma göre tetiklenir.
- **BR-4:** Kesilmiş makbuz değiştirilmez ve silinmez; düzeltme iptal kaydı + yeni makbuzla yapılır.
- **BR-5:** Yoklamada "yok" işaretlenen öğrencinin velilerine otomatik bildirim gider; aynı ders saati için ikinci kez gönderilmez.
- **BR-6:** Öğretmen yalnızca ders verdiği sınıfları, veli yalnızca kendi çocuklarını görür; yönetici/muhasebe yetkili olduğu şubeleri görür.
- **BR-7:** Mesai dışında öğretmene gelen mesaj bildirimi sessizdir; mesaj kaybolmaz, ertesi mesai başında bildirilir.
- **BR-8:** Öğrencinin fotoğraf izni yoksa o öğrencinin göründüğü fotoğraf paylaşılamaz.
- **BR-9:** AI taslağı, yetkili bir insan onaylamadan veliye ulaşmaz.
- **BR-10:** Yoklama ders saati bazında alınır; kurum isterse günün ilk ders saatini günlük yoklama olarak kullanır.
- **BR-11:** Bir ücret sözleşmesinin taksit tutarlarının toplamı sözleşmenin net tutarına her zaman eşittir; bölmeden kalan kuruş son taksite eklenir.

## Key domain invariants
- Tenant izolasyonu (BR-1) hiçbir özellik baskısıyla gevşetilmez.
- **AI ilkeleri** (`docs/kapsam.md`): AI hiçbir mesajı onaysız veliye göndermez · çocuklara dair veriler
  model eğitiminde kullanılmaz · her AI çıktısında kaynak veri görünür.
- Makbuz ve audit log değiştirilemez (append-only).
- Kart verisi sistemde hiç saklanmaz ve işlenmez (sağlayıcı formu + token).
- e-Okul resmi kayıt sistemidir; ürün onu tamamlar, ikame etmez.

## Sensitive areas
Bu alanlara dokunan spec Constraints bölümünde `Sensitive area: <ad>` yazar:
- **Tahsilat** — ücret, taksit, ödeme, makbuz, cari
- **Tenant izolasyonu** — kurum verisinin ayrımı
- **Kimlik ve yetki** — giriş, 2FA, roller, görünürlük
- **Çocuk ve sağlık verisi (KVKK)** — öğrenci kartı, sağlık/alerji, fotoğraf, rıza, saklama/anonimleştirme
- **AI gönderimi** — AI taslağının veliye ulaşma yolu
