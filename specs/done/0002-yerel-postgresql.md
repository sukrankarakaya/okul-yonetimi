# Spec 0002 — Entegrasyon testleri yerel PostgreSQL 18 ile (mini)

- Status: Shipped
- Mode: lite
- Plan: `specs/plans/0002-plan.md`
- Self-critique: done
- Source: ADR 0001 (`docs/decisions/0001-entegrasyon-testleri-yerel-postgresql.md`), kullanıcı kararı 2026-11-18 / 2026-11-19
- Design: —
- Supersedes: —

## Intent
Tek geliştiricinin 8 GB RAM'li makinesinde backend entegrasyon testleri Docker olmadan, yerel PostgreSQL 18 ile
koşsun. Hedef, Docker VM'inin tuttuğu yaklaşık 4 GB belleği geri kazanmak (ADR 0001). Tenant izolasyonu ve Türkçe
sıralama kanıtları aynı güvenle gerçek PostgreSQL üzerinde üretilmeye devam etmeli ve hiçbir koşu arkasında
veritabanı bırakmamalı. Konteyner yolu ileride CI için kalır.

## Changed behavior
- [x] CB-1 — Test veritabanı sunucusu ortam değişkeniyle (`OKUL_TEST_PG`) verildiğinde entegrasyon testleri o sunucuda
  koşar. Değişken bir bakım veritabanının (ör. `postgres`) bağlantısıdır; test veritabanları oradan oluşturulup
  silinir, testler test veritabanına bağlanır. Docker kapalıyken full check yeşildir. Boş ya da yalnızca boşluk
  içeren değer tanımsız sayılır (PB-1).
- [x] CB-2 — Her koşu (entegrasyon test projesinin tek bir çalıştırılması) kendine ait yeni bir test veritabanıyla
  başlar. Adı `okul_test_` önekini, oluşturma zamanını
  (UTC, saniye) ve kısa bir benzersiz eki taşır. Koşu sonunda, testler geçse de kalsa da silinir.
- [x] CB-3 — Koşu açılışında, adı `okul_test_` ile başlayan ve adındaki UTC zamanına göre 60 dakikadan eski olan
  veritabanları, açık bağlantıları kapatılarak silinir (sınır: 61 dakika silinir, 59 dakika kalır). Daha yeni olanlara
  (paralel bir koşu), adındaki zamanı okunamayanlara ve `okul_test_` ile başlamayan veritabanlarına dokunulmaz.
- [x] CB-4 — Sunucuya ulaşılamazsa, sunucu PostgreSQL 18 değilse ya da `tr-TR-x-icu` collation'ı yoksa entegrasyon
  testleri hiçbir test koşmadan başarısız olur. Mesaj nedeni adlandırır, parolayı içermez ve konteyner yoluna
  geçilmez.
- [x] CB-5 — Test rolü süper kullanıcı değilken, yalnızca veritabanı oluşturma yetkisiyle, full check yeşildir.
- [x] CB-6 — check'in entegrasyon ön koşulu yola göre değişir. Değişken tanımlıysa sunucuya erişim aranır: sunucu
  açıkken doctor full'ün koşabileceğini söyler (Docker kapalı olsa da), sunucu kapalıyken koşamayacağını söyler.
  Sunucu denetim aracı (`pg_isready`) bulunamazsa doctor full'ün koşamayacağını nedeniyle söyler ve Docker'a
  düşmez. Değişken tanımlı değilse bugünkü gibi Docker aranır.

## Preserved behavior
- [x] PB-1 — Değişken tanımlı değilken (ya da boşken) entegrasyon testleri bugünkü gibi geçici bir PostgreSQL 18 konteynerinde
  koşar, yeşildir ve konteyner koşu sonunda kapanır.
- [x] PB-2 — Spec 0001'in AC-1…AC-7 entegrasyon testleri iki yolda da yeşildir. Yerel yolda tenant filtresi
  kaldırıldığında tenant izolasyonu testleri kırmızıya döner, yani yeni yolda da kanıtlar gerçekten koruyor.
- [x] PB-3 — Fast profil değişmez: Docker ve yerel sunucu olmadan yeşildir ve hiçbir veritabanına bağlanmaz.
- [x] PB-4 — Yerel yolda check arkasında süreç bırakmaz (RESIDUE CLEAN, fast + full).
- [x] PB-5 — Bağlantı bilgisi (parola dahil) repoya, test çıktısına veya check çıktısına yazılmaz.

## Out of scope
- Sensitive area: Tenant izolasyonu (kanıt altyapısı — 0001'in AC-2…AC-4 kanıtları bu fixture'da koşar)
- CI kurulumu ve remote (B-1). Konteyner yolu yalnızca bu spec'in VERIFY'ında kanıtlanır.
- Dağıtımın konteynerleştirilmesi (altyapı spec'i, B-4 ile).
- Docker Desktop'ın kaldırılması: kullanıcının kararı, VERIFY'dan sonra.
- PostgreSQL kurulumunun otomasyonu: kurulum adımları dokümante edilir, script yazılmaz.
- Residue aracının veritabanı ölçmesi: kalan veritabanı VERIFY'da gözlemle kanıtlanır.
- Ürün davranışı, web ve mobil testler: değişmez.
- Boyut: 11 kriter (sınırda). Bölme önerilmedi — tek yetenek (fixture + ön koşulu); doctor tarafı fixture'sız kanıtlanamaz.

## Definition of Done
- [x] `scripts/check` green
- [x] Independent review done; real findings fixed, noise rejected with written rationale, deferred ones in `specs/backlog.md`
- [x] Criterion ↔ evidence table complete for CB-* **and** PB-* (UI: before/after screenshots, agent-captured, judged at SHIP)
- [x] Spec moved to `specs/done/` (immutable there)
