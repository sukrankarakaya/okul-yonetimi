# Backlog — deferred findings

Findings a human triaged as **defer**: real, but fixed later by decision (`workflows/feature-development.md`,
step 7). Nothing lands here without that decision; noise is rejected in the triage record, not parked here.

- A criterion moved out at the ship gate (R-08, `→ moved to B-<n>`) lands here too, by the
  human's ship decision; the entry names the spec and criterion.
- One entry per finding, newest last. Delete the entry in the commit of the work that closes it.
- An entry that grows into new behavior becomes a spec (`specs/active/`), not a fix.

<!-- Format (labels stay English):
- **B-<n>** — <finding, one line> — Source: spec <NNNN>, review round <n> (<file:line>) — Severity: <…>
  — Recommendation: <the reviewer's action> — Deferred by: <name>, <YYYY-MM-DD>, <why>
  A decision deferred at bootstrap (its REPORT) uses Source: bootstrap <YYYY-MM-DD>.
-->

- **B-1** — Remote/CI yok, push yok: çalışma yalnızca lokal; full green CI'da değil lokal koşudan gelir, trivial
  politikası (2)'de — **Commit kısmı çözüldü:** 2026-11-19 spec 0001 ship'inde lokal commit'ler başladı
  (`chore/bootstrap` 7ba1a23, `feature/0001-walking-skeleton`, `main` 008324b); `doctor` immutability kontrolü
  etkin — Source: bootstrap 2026-11-17 — Severity: low (commit gelince medium'dan düştü) — Recommendation: remote
  eklenince push, trivial politikası (1)'e (PR + bir reviewer) ve CI'ya geç — Deferred by: kullanıcı, 2026-11-17,
  "commitlemek için vakit kaybetmek istemiyorum"; commit kısmı kapandı: kullanıcı, 2026-11-19, ship onayı
- **B-2** — api-client drift kontrolü: üretilmiş TS tipleri backend OpenAPI'den kopabilir; check'te
  "yeniden üret + fark yok" adımı yok — Source: spec 0001, plan risk R5 — Severity: low —
  Recommendation: commit'ler başlayınca (B-1) fast adım olarak ekle — Deferred by: kullanıcı, 2026-11-17, plan 0001 R5 kararı
- **B-3** — **Kapandı (spec 0003, 2026-11-19):** test girişi OpenAPI dışında (`ExcludeFromDescription`), `createDevAuthClient`
  ve web/mobil `devLogin` silindi, login tek seferlik yenilemeyle; `expo export` / `vite build` paketlerinde `test-login`
  sayımı spec 0003 VERIFY'ında. Eski kayıt: Geliştirme route'u (`/api/v1/dev/test-login`) OpenAPI dokümanında ve üretilmiş tiplerde yer alıyor.
  Web üretim paketine girmiyor (VERIFY 2026-11-19 doğruladı); **mobil üretim paketlerine giriyor**: `expo export`
  (web/ios/android) üçünde de `createDevAuthClient` + `/api/v1/dev/test-login` var — ölü kod (`devLogin` modülü ve örnek
  tenant yok), Metro tree-shaking yapmıyor; `packages/api-client/src/client.ts`'deki "tree-shaken" yorumu Metro için
  yanlış. Ayrıca Expo web dev'de bir sayfa yüklemesi 4–7 kez `POST /dev/test-login` gönderiyor (eşzamanlı
  `accessToken === null` kontrolü) — Source: spec 0001, review round 1 (`packages/api-client/openapi.json:8`) +
  VERIFY F-1/F-2 — Severity: low — Recommendation: gerçek giriş spec'inde OpenAPI dokümanı DevAuth olmadan
  üretilsin; `createDevAuthClient` ayrı giriş noktasına (`@okul/api-client/dev`, yalnızca `devLogin.ts` import eder)
  taşınsın ve VERIFY'da `expo export` paketinde `test-login` 0 olsun; `client.ts` yorumu düzeltilsin; login tek promise
  ile sınırlansın — Deferred by: kullanıcı, 2026-11-18, triage; genişletildi: kullanıcı, 2026-11-19, ship kararı
- **B-4** — TLS sonlandıran proxy arkasında `UseHttpsRedirection`/HSTS `Request.IsHttps` false görür (döngü / HSTS yok);
  `UseForwardedHeaders` yok — Source: spec 0001, review round 2 (`Hosting/SecurityHeaders.cs`) — Severity: info —
  Recommendation: dağıtım/altyapı spec'inde bilinen proxy'lerle ForwardedHeaders bu adımlardan önce; giriş hız
  sınırında IPv6 adresleri /64 ile gruplansın (spec 0003 review tur 1 #13) —
  Deferred by: kullanıcı, 2026-11-18, triage
- **B-5** — Android TalkBack durum metnini iki kez okuyabilir (`announceForAccessibility` + `accessibilityLiveRegion`),
  doğrulanmadı — Source: spec 0001, review round 2 (`mobile/src/features/institution/InstitutionScreen.tsx`) —
  Severity: info — Recommendation: gerçek cihaz kontrolünde dinle; çift okuma varsa duyuruyu `Platform.OS === 'ios'`
  ile sınırla, testi platforma ayır — Deferred by: kullanıcı, 2026-11-18, triage
- **B-6** — HTTPS yönlendirme yanıtının (307) güvenlik başlıklarını taşıdığını gösteren kalıcı regresyon testi yok;
  middleware sırası (`UseOkulSecurityHeaders` → `UseHttpsRedirection`) yalnızca kodda doğru — Source: spec 0001,
  review round 4, N-1 (`backend/tests/Okul.WebApi.IntegrationTests/SecurityHeadersTests.cs:104`) — Severity: info —
  Recommendation: `UseSetting("https_port", "443")` ile `http://` isteği → `StatusCode == TemporaryRedirect` +
  `AssertSecurityHeaders`; bir sonraki WebApi/güvenlik değişikliğinde (ör. gerçek giriş spec'i veya B-4) ekle —
  Deferred by: kullanıcı, 2026-11-19, triage
- **B-7** — UI ve rol testleri beklenen değerleri üretim kodundan alıyor: web/mobil testleri metinleri bileşenin
  `texts.*` sabitlerinden, `GetMine_ReturnsOwnInstitutionAndCampuses_ForEachRole` rolleri `Roles.All`'dan; mobil test
  `item.props.children` okuyor — metin değişirse veya rol silinirse ilgili test yeşil kalır — Source: spec 0001,
  VERIFY 2026-11-19 (`web/src/features/institution/InstitutionPage.test.tsx`,
  `mobile/src/features/institution/InstitutionScreen.test.tsx`) — Severity: info — Recommendation: spec metinleri ve
  dört rol testte literal; mobilde `getByText`/erişilebilir ad — bir sonraki kurum UI/rol değişikliğinde —
  Deferred by: kullanıcı, 2026-11-19, ship kararı
- **B-8** — AC-10 yalnızca mimari kural 6 ile kasıtlı kırıldı; spec örneği olan kural 1 (başka modülün Domain
  katmanına referans) tek modülle denenemez — Source: spec 0001, VERIFY F-3 — Severity: info — Recommendation:
  ikinci modülü getiren spec'in VERIFY'ında kural 1 kasıtlı kırılsın — Deferred by: kullanıcı, 2026-11-19, ship kararı
- **B-9** — Entegrasyon testlerinin `finally` temizliği (`CleanUpAsync`) hata verirse fırlattığı `AggregateException` testin
  kendi hatasının yerine geçer; asıl neden yalnızca inner exception'da görünür — Source: spec 0002, review round 3, I-1
  (`backend/tests/Okul.WebApi.IntegrationTests/Infrastructure/TestDatabaseLifecycleTests.cs:115-125`) — Severity: info —
  Recommendation: test zaten bir hatayla çıkıyorsa temizlik hatası yalnızca stderr'e yazılsın, değilse fırlatılsın
  (`ApiFixture`'daki L-2 deseni) — Deferred by: kullanıcı, 2026-11-19, triage (fix-3 hassas alanda bir review turu
  daha gerektirirdi; kriter etkilenmiyor)
- **B-10** — Sunucu sürüm/collation denetimi (`ServerRequirements.Problem`) yalnızca saf birim testiyle korunuyor;
  `VerifyServerAsync` içindeki çağrı devre dışı bırakılınca 86 testin hepsi yeşil kaldı (VERIFY kasıtlı kırma B7) —
  Source: spec 0002, VERIFY bulgu 1 — Severity: low — Recommendation: `VerifyServerAsync`'in sonucu işlediğini sınayan
  bir ek nokta + test; davranış bugün gözlemle kanıtlı (collation adı değişince testler koşmadan açık mesajla düşer) —
  Deferred by: kullanıcı, 2026-11-19, VERIFY triage
- **B-11** — **Kapandı (spec 0004, 2026-11-20):** yanlış mevcut şifre giriş kilidine bağlandı, kilitliyken 429 ve "Çok fazla başarısız deneme…" — Şifre değiştirmede "mevcut şifre" denemeleri sınırsız: ele geçirilmiş bir oturumla şifre kilitlenmeden tahmin
  edilip değiştirilebilir — Source: spec 0003, review round 1 #4 (`Okul.Modules.Identity.Application/ChangePassword.cs`,
  `/auth/password`) — Severity: medium — Recommendation: mevcut şifre doğrulaması giriş kilidine (kurum + e-posta anahtarı)
  bağlansın, kilitliyken 429 + "Çok fazla başarısız deneme…" (yeni ekran durumu → spec değişikliği); spec 0004 (2FA) ile —
  Deferred by: kullanıcı, 2026-11-19, triage
- **B-12** — Girişte zaman farkı: bilinmeyen kurum kodunda hesap sorgusu çalışmıyor, bilinen kodda bir sorgu daha var
  (PBKDF2'nin yanında küçük, ölçülmedi) — Source: spec 0003, review round 1 #8 (`Login.cs`) — Severity: low —
  Recommendation: çok örnekli p50/p95 ölçümü; fark anlamlıysa bilinmeyen kodda da eşdeğer bir sorgu — Deferred by:
  kullanıcı, 2026-11-19, triage (R-12)
- **B-13** — `kimlik.login_throttles` ve `kimlik.sessions` için temizlik yok; rastgele e-postalı denemeler ve biten oturumlar
  kalıcı satır bırakır; fix-1'den beri her hesabın ilk başarılı girişi de bir (sıfırlanmış) satır yazar — Source: spec 0003,
  review round 1 #12 + round 2 I-2 — Severity: info — Recommendation: süresi geçmiş satırları
  silen bir arka plan işi (Hangfire geldiğinde) — Deferred by: kullanıcı, 2026-11-19, triage
- **B-14** — **Kapandı (PR #2, `f3c1181`, 2026-11-20):** macOS `/bin/sh` (bash 3.2) `scripts/check` ve `scripts/doctor`'u
  ayrıştıramıyordu (`$( … )` içindeki `case` kalıpları); `scripts/*` ve hook'lar 100644 kayıtlıydı — Source: Mac'e devir
  (devir notu, `d93c0d9`) — Severity: high (Mac'te check yok)
- **B-15** — bash 3.2 regresyonu Linux CI'da yakalanmaz (ubuntu'da `/bin/sh` dash, bash 5) — Source: B-14 review
  (`.github/workflows/check.yml`) — Severity: low — Recommendation: CI'a geçiş kararıyla (B-1) birlikte `macos-latest`
  üzerinde `/bin/sh -n scripts/*` adımı; Mac'te `./scripts/selftest` bugün koruyor — Deferred by: kullanıcı, 2026-11-20, triage
- **B-16** — Mobil Kurulum ekranında 2FA anahtarı satır ortasında kırılıyor (okunurluk; "Kopyala" var) — Source: spec 0004,
  VERIFY round 1 #3 (`mobile/src/features/login/SetupStep.tsx`) — Severity: info — Recommendation: anahtarı 4'lü gruplarla
  ve satır kırılmadan göster — Deferred by: kullanıcı, 2026-11-20, triage
- **B-17** — Spec 0005 AC-6/AC-7 ("veli çocuklarının derslerini/notlarını/haberlerini görür", "çoklu çocuk + ayrı erişim
  kontrolü") `[x]` işaretli shipped, ancak kodda öğrenci ve veli–öğrenci bağı yok (`Parent` çocuk taşımıyor); kanıt
  bu davranışı kapsıyor olamaz — Source: spec 0005 (shipped, immutable), 0006 ANALYZE bulgusu
  (`backend/src/Modules/Parents/Okul.Modules.Parents.Domain/Parent.cs`) — Severity: medium (süreç: kanıtsız işaretli
  kriter) — Recommendation: davranış 0006 (çocuk listesi), 0007 (dersler), 0008 (duyurular) ile gelir; notlar ayrı spec;
  0005 VERIFY tablosunu yeniden okuyup kanıtsız işaretin nasıl geçtiğini süreç dersine yaz — Deferred by: kullanıcı,
  2026-11-20, 0006 split onayı
- **B-18** — Çocuk fotoğrafının veliye gösterimi için 0005 KVKK rızasının yeterliliği hukuken teyit edilmedi — Source:
  spec 0006 self-critique G-3 — Severity: low — Recommendation: KVKK danışmanına sor; ayrı açık rıza gerekirse fotoğrafı
  rızaya bağla, yedek baş harfler zaten var (0006 AC-3) — Deferred by: kullanıcı, 2026-11-20, G-3 onayı
