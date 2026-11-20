# Architecture

> Bootstrap 2026-11-17. Kapsam: `docs/kapsam.md` (Faz 1). Bu dosya *karar verilmiş* mimariyi anlatır.

## System overview
Okul Yönetim Platformu, kurum (tenant) bazlı veri izolasyonlu, multi-tenant bir SaaS'tır. Backend tek
deploy birimi olan bir **modüler monolittir**: .NET 10, ASP.NET Core Web API, nArchitecture (Clean
Architecture + CQRS/MediatR), EF Core 10, PostgreSQL 18; arka plan işleri Hangfire (PostgreSQL storage).
İstemciler: React 19 + TypeScript web paneli (yönetici, muhasebe, öğretmen) ve React Native/Expo tek
mobil uygulama (veli + öğretmen, role göre arayüz). Gerekçe: MVP ve pilot aşamasında mikroservisin
operasyon maliyeti faydasından büyük; net modül sınırları ileride bir modülün ayrılmasını mümkün kılar.

Depo yapısı (monorepo):
```
backend/src/Core/                          nArchitecture çekirdeği (Application, Persistence, Security, CrossCutting)
backend/src/Modules/<Modül>/{Domain,Application,Infrastructure}
backend/src/WebApi/                        tek host; modülleri birleştirir
backend/tests/                             birim, mimari, entegrasyon testleri
web/                                       React yönetim paneli
mobile/                                    Expo veli + öğretmen uygulaması
packages/api-client/                       OpenAPI'den üretilen TS istemcisi (web + mobile)
```

## Modules / components and ownership

| Module | Single responsibility | Owns |
|---|---|---|
| Kurum (`Institution`) | Kurumun akademik iskeleti | Kurum (tenant), şube, sınıf seviyesi, sınıf, dönem, akademik takvim, ders programı |
| Kimlik (`Identity`) | Kim girer, ne yapabilir | Kullanıcı, rol ataması, yetki, 2FA, oturum, şube bazlı görünürlük |
| Kayıt (`Enrollment`) | Öğrenci ve veli kayıtları | Öğrenci kartı, veli, kardeş bağı, sağlık notu/alerji, teslim alabilecek kişi, içe aktarma işleri; veli giriş kodu ve giriş kilidi; Faz 1'de KVKK rıza kaydı (ADR 0002) |
| Yoklama (`Attendance`) | Derse katılım | Yoklama kayıtları, devamsızlık toplamları |
| İletişim (`Communication`) | Okul ↔ veli içerikleri | Duyuru + okundu bilgisi, mesaj, onay formu + yanıtlar, dosya/fotoğraf paylaşımı |
| Akademik (`Academics`) | Ödev ve not | Ödev, teslim durumu, yazılı/sözlü not |
| Tahsilat (`Billing`) | Para akışı | Ücret sözleşmesi, indirim/burs, taksit, ödeme, makbuz, cari |
| Randevu (`Appointments`) | Öğretmen-veli görüşmesi | Müsaitlik takvimi, randevu talebi |
| Raporlama (`Reporting`) | Okuma amaçlı görünümler | Yönetici günlük özeti, raporlar, Excel dışa aktarma (yalnızca read model) |
| Bildirim (`Notifications`) | Kanala dağıtım | Push token, SMS gönderimi, kanal seçimi, sessiz mod, gönderim kayıtları |
| AI (`Ai`) | Taslak üretimi | Sesli nottan mesaj taslağı, haftalık gelişim özeti, taslak/onay durumu, kaynak veri referansı |
| Uyum (`Compliance`) | KVKK ve iz | Audit log, aydınlatma metni sürümleri, açık rıza kayıtları (Faz 1'de veli rızası Kayıt'ta — ADR 0002), saklama/anonimleştirme işleri |

## Communication rules
- Modüller arası senkron çağrı: yalnızca hedef modülün **public Application sözleşmesi** (contracts
  assembly: arayüzler + DTO'lar) üzerinden.
- Bir modüldeki olayın başka modülleri tetiklemesi (ör. yoklamada "yok" → velilere bildirim):
  **domain/integration event** ile; tetiklenen iş Hangfire ile asenkron.
- Veliye giden her bildirim Bildirim modülünden geçer. AI yalnızca taslak üretir; onaylanan taslak
  İletişim/Bildirim üzerinden gönderilir (BR-9).
- Her modülün kendi DbContext'i ve PostgreSQL şeması vardır (`kurum.*`, `kimlik.*`, `tahsilat.*` …). Kimlik modülü
  (spec 0003) Kurum modülünü yalnızca `IInstitutionDirectory` sözleşmesiyle kullanır (kurum kodu → kurum). Oturum
  öncesi tek kiracısız depolama giriş kilidi kaydıdır (özetli anahtar, kurum verisi yok); her kimliği doğrulanmış
  istekte oturumun açık olduğu Kimlik modülüne sorulur (`ISessionValidator`).
- Veli girişi (spec 0007, ADR 0002): Kayıt modülü kurum kodu + telefon + SMS kodunu doğrular, oturumu Kimlik açar
  (`IGuardianSessions`). Oturum kaydı hesap türünü taşır (personel / veli); Kimlik bir veli oturumunu yenilerken veliyi
  Kayıt'a sorar (`IGuardianDirectory`). Personel uçları yalnızca personel rollerini, veli uçları `Guardian` rolünü ve
  güncel KVKK rızasını ister.
  Kayıt'ın `GuardianLoginDbContext`'i, Kimlik'in `LoginThrottleDbContext`'i gibi kuruma bağlı olmayan ikinci bağlamdır
  (plan 0007, A-1): kayıtlı olmayan numara ve bilinmeyen kurum kodu kayıtlı olanla aynı davransın diye (K3); yalnız
  özetli kurum kodu + telefon anahtarını, kod özetini, sayaçları ve zamanları tutar.

## Forbidden dependencies (make them testable)
Mimari testleri (NetArchTest) her kuralı `scripts/check` içinde doğrular:
1. Bir modül başka bir modülün **Domain veya Infrastructure** katmanına referans veremez; yalnızca
   contracts assembly'sine.
2. **Domain** katmanları EF Core, ASP.NET Core, HTTP veya Infrastructure tiplerine bağımlı olamaz.
3. Bir modül başka modülün **DbContext'ine / tablolarına** erişemez. İstisna: Raporlama yalnızca read
   model üzerinden okur, hiç yazmaz.
4. **Tahsilat**, **AI** modülüne bağımlı olamaz.
5. **AI** modülü Bildirim'in gönderim arayüzüne ve dış kanal istemcilerine (SMS, push) referans veremez.
6. Dış servis istemcileri (ödeme, SMS, Claude, speech-to-text, depolama) yalnızca Infrastructure
   katmanında, bir Application arayüzünün arkasında bulunur.
7. `web/` ve `mobile/` backend'e yalnızca `packages/api-client/` üzerinden erişir (ESLint import kuralı).

## Deliberately out of scope
- Faz 2/3 modülleri ve "Kapsam dışı" listesi: `docs/kapsam.md`. Özellikle: öğrenci ve rehber öğretmen
  rolleri, sınav analizi, servis, etüt, e-Fatura, WhatsApp, SSO, ön kayıt CRM'i, çok şubeli konsolide
  yönetim, kurum markalı mağaza uygulaması, tam LMS, tam muhasebe/bordro, devlet okulları, veliler arası
  grup sohbeti.
- Mikroservis dağıtımı, çoklu veritabanı (database-per-tenant).
