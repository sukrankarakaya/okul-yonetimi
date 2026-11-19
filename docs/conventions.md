# Conventions

> Bootstrap 2026-11-17. Her kural ya araçla zorlanır (tercih edilen) ya da review'da kontrol edilir.

## Language
- Chat language (interviews, sessions): Turkish (`tr`)
- Document language (docs/, specs, plans, ADRs, review and verify reports): Turkish (`tr`)
- Always English (protocol, not prose): ANEW core files, template headings and field labels,
  `Status:` values (Draft / Approved / In progress / Shipped), `Approved by / on:`, `Source:`.
- Code identifiers, branch names, commit messages: kod tanımlayıcıları ve dal adları **İngilizce**
  (terim ↔ kod adı eşlemesi `docs/domain.md`); commit mesajı açıklaması **Türkçe**, Conventional
  Commits tipi İngilizce (`feat(tahsilat): taksit planı oluşturma`). Kullanıcıya görünen metinler Türkçe.
- Agent messages (handoff summaries, refusals, `Next:` lines): chat language; command names, file
  paths and protocol fields verbatim.

## Language & framework versions
Sabitlendi: spec 0001 (walking skeleton), 2026-11-17. Kaynak: `global.json`, `backend/Directory.Packages.props`,
`package-lock.json`. Yükseltme ayrı bir iştir (plan + check green).
- Backend: .NET SDK 10.0.404 (`global.json`, `rollForward: latestFeature`), ASP.NET Core / EF Core 10.0.15,
  Npgsql EF 10.0.3, nArchitecture 2.0.0 (`NArchitecture.Core.Mediator`, `…CrossCuttingConcerns.Exception`;
  MediatR kullanılmaz — A-1). Testler Microsoft.Testing.Platform ile (`global.json` → `test.runner`).
- Test: xunit.v3 4.0.3, Shouldly 4.3.0, NSubstitute 6.2.0, NetArchTest 1.3.2, Testcontainers 4.17.0 (`postgres:18`),
  Microsoft.Extensions.TimeProvider.Testing 10.0.0 (`FakeTimeProvider`)
- Kimlik: Microsoft.Extensions.Identity.Core 10.0.15 (yalnızca `PasswordHasher`, PBKDF2 — ASP.NET Core Identity'nin geri
  kalanı kullanılmaz); mobil oturum: expo-secure-store ~58.0.3
- Veritabanı: PostgreSQL 18
- Node 24+ (`.nvmrc`; bu makinede 26 çalışıyor), npm workspaces (`web`, `mobile`, `packages/*`)
- TypeScript **5.9.x** — 7.x değil: openapi-typescript ve Expo TS 5 JS API'sine bağlı. ESLint **9.x** — 10 değil:
  eslint-config-expo henüz uyumsuz. İkisi de kökte devDependency (workspace hoisting).
- Web: React 19.3.0 (Expo ile aynı sürüm — tek kopya), Vite 8, Vitest 5, Testing Library
- Mobil: Expo SDK 58 (`expo ~58.0.26`, React Native 0.88.1), jest-expo 58, React Native Testing Library 14
- API istemcisi: openapi-fetch 0.17 + openapi-typescript 7 (`packages/api-client`)
- Henüz eklenmedi (ilgili spec'lerle): FluentValidation, Hangfire, Polly, Claude API

## Naming
- C#: .NET standardı (PascalCase tipler/üyeler, `_camelCase` private alanlar, `I` önekli arayüzler).
  Modül projeleri `Okul.Modules.<Modül>.<Katman>`.
- Para alanları `...Kurus` ile biter (`AmountKurus`, `RemainingKurus`).
- DB: modül başına şema (`kayit`, `tahsilat` …), tablolar ve kolonlar snake_case.
- API: REST, `/api/v1/<kaynak>` kebab-case, JSON camelCase; OpenAPI şeması `packages/api-client` üretir.
- TS: bileşenler PascalCase dosya, hook'lar `useX`, testler `*.test.ts(x)` kaynak yanında.
- Backend testleri: `<Sınıf>Tests`, test adı `Yontem_Durum_Beklenen`; entegrasyon testleri `[Trait("Category","Integration")]`.

## Error handling
- İş kuralı ihlali → `BusinessException` (nArchitecture); girdi doğrulama → FluentValidation.
- Global middleware tüm hataları **RFC 9457 ProblemDetails** olarak döner; mesaj Türkçe ve anlaşılır.
  Stack trace, SQL, iç tip adı veya kişisel veri yanıtta asla yer almaz.
- Beklenen hatalar 4xx, loglanır ama alarm üretmez; beklenmeyenler 5xx + correlation ID ile loglanır.
- Dış servis çağrıları zaman aşımı + Polly yeniden deneme ile; **ödeme çağrısı idempotency key olmadan tekrarlanmaz.**

## Data rules
- Para: kuruş cinsinden `long`; `float`/`double`/`decimal` para için kullanılmaz (BR-2). Gösterim UI'da formatlanır.
- Zaman: DB'de UTC `timestamptz`; gösterim `Europe/Istanbul`; vade gibi takvim günleri `DateOnly`.
  Kodda `DateTime.Now`/`UtcNow` yok — `TimeProvider` enjekte edilir.
- ID: `Guid` v7.
- Tenant: tenant'a ait her tabloda zorunlu `TenantId` + EF global query filter. `IgnoreQueryFilters()`
  yalnızca sistem işlerinde ve gerekçe yorumuyla.
- Oturum öncesi (giriş, yenileme) kurum `ITenantScope` ile verilir; sorgular yine filtre altında çalışır. Tek kurumlar
  arası okuma: kurum kodu → kurum kimliği (`IInstitutionDirectory`, yalnızca kimlik döndürür). Tek kiracısız bağlam:
  `LoginThrottleDbContext` (mimari testinde adıyla izinli) — spec 0003.
- Silme: kişisel veride soft delete + saklama süresi sonunda anonimleştirme. Makbuz ve audit log silinmez.
- Telefon E.164 (`+905…`), e-posta küçük harfe normalize edilerek saklanır.
- Kişisel veri alanları `[Sensitive]` ile işaretlenir; log'da otomatik maskelenir.

## Known traps
- T-1 Türkçe I/ı/İ/i: kültüre bağlı `ToLower/ToUpper/Compare` — Invariant/Ordinal kullan; UI `tr-TR`; DB aramada ICU collation `tr-TR-x-icu` — from bootstrap; `ToUpperInvariant('İ')` `İ` kalır — bir değeri karşılaştırmak için normalize eden **tek** fonksiyon kullanılır (kurum kodu: `InstitutionCodeFormat`, e-posta: `EmailAddress`), aynı değerin bir yolda bir fonksiyonla, başka yolda başka biçimde normalize edilmesi kurum kodunun varlığını sızdırdı (spec 0003 review round 1 #3)
- T-2 `IgnoreQueryFilters()`, ham SQL, Dapper tenant filtresini atlar — ham sorgu yalnızca Raporlama'da, `TenantId` elle + izolasyon testi — from bootstrap
- T-3 Hangfire işinde tenant bağlamı yok — her iş `TenantId` parametresi alır, başta bağlam kurulur, yoksa fail — from bootstrap
- T-4 `DateTime.Now` / `DateTimeKind` — Npgsql `timestamptz`'ye UTC olmayanı reddeder; `TimeProvider` + `DateTimeOffset` UTC, vade `DateOnly` — from bootstrap
- T-5 Taksit bölmede kuruş kaybı — kalan kuruş son taksite; toplam = sözleşme net tutarı testli (BR-11) — from bootstrap
- T-6 Ödeme callback'i tekrar/geç/sahte gelebilir — imza doğrula, idempotent işle, durumu sağlayıcı sorgusuyla kesinleştir — from bootstrap
- T-7 N+1 sorgu (sınıf → veliler → taksitler) — liste sorgularında projection (`Select`) — from bootstrap
- T-8 Tek DbContext'te migration çakışması — modül başına DbContext + şema + migration klasörü — from bootstrap
- T-9 Expo native paket sürüm uyumsuzluğu — bağımlılığı `npx expo install` ile ekle, SDK ile birlikte yükselt — from bootstrap
- T-10 Log'a telefon, TC no, push token, sağlık notu düşmesi — `[Sensitive]` + otomatik maskeleme — from bootstrap
- T-11 AI prompt'una fazla veri gitmesi — yalnızca gerekli alanlar; TC no ve sağlık notu AI'a asla gitmez — from bootstrap
- T-12 `UseExceptionHandler` hata yolunda `Response.Clear()` ile önceden yazılmış başlıkları siler — her yanıtta olması gereken başlıklar `Response.OnStarting` ile yazılır, testte bir hata yanıtı (404) da ölçülür — from spec 0001 review round 2

## Enforced by tooling
Spec 0001'den beri `scripts/check.conf`'ta:
- `dotnet format --verify-no-changes` + `.editorconfig`; `TreatWarningsAsErrors=true`; nullable reference types açık.
- NetArchTest mimari testleri (`docs/architecture.md`, Forbidden dependencies 1–3 ve 6; 4–5 Tahsilat/AI
  modülleriyle gelir); modül listesi `Okul.slnx`'ten, her modül DbContext varlığında `Tenant` filtresi zorunlu.
- ESLint (import kısıtı: `web/`, `mobile/` yalnızca `packages/api-client`) + Prettier + `tsc --noEmit` strict.
- `dotnet list package --vulnerable` (High/Critical → kırmızı); `npm audit --audit-level=high` (istisnasız — plan 0001, A-2).
