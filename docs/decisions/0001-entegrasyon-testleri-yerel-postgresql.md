# ADR 0001 — Entegrasyon testlerinde yerel PostgreSQL 18 (Testcontainers yedek yol)

- Status: Accepted
- Date: 2026-11-19

## Context
Backend entegrasyon testleri (`backend/tests/Okul.WebApi.IntegrationTests`, check'te `backend-integration`, full
profil) her koşuda Testcontainers ile `postgres:18` konteyneri açar (`Infrastructure/ApiFixture.cs`). Tenant izolasyonu
(AC-2…AC-4, spec 0001) ve Türkçe sıralama (AC-7) bu testlerle kanıtlanır; ikisi de **gerçek PostgreSQL** ister:
`tr-TR-x-icu` ICU collation'ı, EF Core migration'ları ve tenant global query filter'ı başka bir veritabanında
(SQLite, in-memory) aynı davranmaz.

Tek geliştirme makinesi 8 GB RAM'li bir Windows 10 dizüstü. Docker Desktop'ın WSL 2 VM'i yaklaşık 4 GB ayırıyor;
spec 0001 boyunca bellek baskısı koşuları yavaşlattı ve bir kez soğuk önbellekte mobil Jest zaman aşımına yol açtı
(plan 0001, Fix turu 2). Remote ve CI henüz yok (`specs/backlog.md`, B-1). Kullanıcı 2026-11-18'de Docker'dan
çıkmayı seçti ama değişikliği spec 0001 ship edilene kadar erteledi; 0001 2026-11-19'da ship edildi.

## Decision
Entegrasyon testleri, `OKUL_TEST_PG` ortam değişkeni tanımlıysa bu makinedeki yerel PostgreSQL 18 sunucusunu kullanır:
fixture her koşuda benzersiz bir `okul_test_<guid>` veritabanı oluşturur, migration ve seed uygular, koşu sonunda
siler. Değişken yoksa bugünkü Testcontainers yolu aynen çalışır (ileride CI'da kullanılacak).

## Consequences
Kazançlar:
- Docker Desktop ve VM'i geliştirme için gereksiz hâle gelir: boşta yaklaşık 4 GB yerine yaklaşık 50–100 MB.
- Konteyner başlatma süresi entegrasyon adımından çıkar.
- Gerçek PostgreSQL 18 kalır; ICU collation ve tenant filtresi kanıtları aynı motor üzerinde.
- Testcontainers yolu kodda kalır: CI eklendiğinde aynı testler ek kod yolu olmadan konteynerle koşar.

Maliyetler:
- Yerel PostgreSQL sürümü elle takip edilir. Görüntü etiketi gibi sabitlenemez; yerel ile CI/üretim arasında
  sapma riski var.
- Yeni her geliştirme makinesinde bir kez kurulum gerekir (EDB PostgreSQL 18, ICU destekli; test kullanıcısı).
- Yerel PostgreSQL servisi check'in dışında sürekli çalışır. Bu ANEW-0007'yi bozmaz (check onu başlatmaz,
  durdurmaz), ama doctor yerel yolda `docker info` yerine sunucuya erişimi ön koşul olarak denetlemelidir.
- Bağlantı dizesi parola içerir: yalnızca kullanıcı ortam değişkeninde durur, repoya girmez. Test kullanıcısı
  süper kullanıcı değildir, yalnızca `CREATEDB` yetkisi alır.
- Çöken bir koşu veritabanı bırakabilir (konteyner yolunda konteyner yok olunca veri de yok oluyordu). Fixture
  açılışta 1 saatten eski `okul_test_*` veritabanlarını siler; residue raporu bunu not eder.
- İki kod yolu (yerel / konteyner) var. Bugün yalnızca yerel yol koşar; konteyner yolu CI gelene kadar
  düzenli olarak denenmez.

## Alternatives considered
- **Docker kalır, WSL VM belleği `.wslconfig` ile sınırlanır (ör. 2 GB).** Kod değişikliği yok ve görüntü sabit
  kalıyor. Kaybetti, çünkü bellek baskısını bitirmiyor, yalnızca küçültüyor: Docker Desktop ve VM yine açılmak
  zorunda ve 8 GB makinede yaklaşık 2 GB + Desktop yükü hâlâ büyük.
- **Gömülü PostgreSQL (fixture'ın indirip başlattığı ikili dosyalar, NuGet paketi).** Kurulum ve servis yok,
  sunucu yalnızca test sırasında çalışıyor. Kaybetti, çünkü pakette PG 18 ve ICU desteği doğrulanmadı; ICU yoksa AC-7
  kanıtlanamaz. Ayrıca küçük ve ticari olmayan bir pakete bağımlılık ekliyor.
- **SQLite / EF Core in-memory.** Ciddi bir seçenek değil: ICU collation ve PostgreSQL'e özgü davranış olmadan
  tenant ve sıralama kanıtları anlamsızlaşır.

## Revisit triggers
- PostGIS, pgvector gibi bir eklenti gerekirse (ör. AI modülü): yerel kurulum maliyeti artar, konteyner yolu
  yeniden değerlendirilir.
- Ekipte ikinci geliştirici olur ve kurulum sürtünmesi ölçülürse.
- Makine 16 GB RAM veya üstüne çıkarsa: Docker'ın bellek maliyeti önemsizleşir.
- CI eklendiğinde yerel ve CI (konteyner) sonuçları ayrışırsa.
- Dağıtım konteynerleştirmesi kararı (altyapı spec'i, B-4 ile) Docker'ı geliştirme ortamında zaten zorunlu kılarsa.

Kapsam: yalnızca entegrasyon testlerinin veritabanı. Dağıtımın konteynerle yapılıp yapılmayacağı ayrı bir karardır.
