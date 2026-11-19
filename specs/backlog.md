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
  VERIFY F-1/F-2 — Severity: low — Recommendation: gerçek giriş spec'inde (0002) OpenAPI dokümanı DevAuth olmadan
  üretilsin; `createDevAuthClient` ayrı giriş noktasına (`@okul/api-client/dev`, yalnızca `devLogin.ts` import eder)
  taşınsın ve VERIFY'da `expo export` paketinde `test-login` 0 olsun; `client.ts` yorumu düzeltilsin; login tek promise
  ile sınırlansın — Deferred by: kullanıcı, 2026-11-18, triage; genişletildi: kullanıcı, 2026-11-19, ship kararı
- **B-4** — TLS sonlandıran proxy arkasında `UseHttpsRedirection`/HSTS `Request.IsHttps` false görür (döngü / HSTS yok);
  `UseForwardedHeaders` yok — Source: spec 0001, review round 2 (`Hosting/SecurityHeaders.cs`) — Severity: info —
  Recommendation: dağıtım/altyapı spec'inde bilinen proxy'lerle ForwardedHeaders bu adımlardan önce —
  Deferred by: kullanıcı, 2026-11-18, triage
- **B-5** — Android TalkBack durum metnini iki kez okuyabilir (`announceForAccessibility` + `accessibilityLiveRegion`),
  doğrulanmadı — Source: spec 0001, review round 2 (`mobile/src/features/institution/InstitutionScreen.tsx`) —
  Severity: info — Recommendation: gerçek cihaz kontrolünde dinle; çift okuma varsa duyuruyu `Platform.OS === 'ios'`
  ile sınırla, testi platforma ayır — Deferred by: kullanıcı, 2026-11-18, triage
- **B-6** — HTTPS yönlendirme yanıtının (307) güvenlik başlıklarını taşıdığını gösteren kalıcı regresyon testi yok;
  middleware sırası (`UseOkulSecurityHeaders` → `UseHttpsRedirection`) yalnızca kodda doğru — Source: spec 0001,
  review round 4, N-1 (`backend/tests/Okul.WebApi.IntegrationTests/SecurityHeadersTests.cs:104`) — Severity: info —
  Recommendation: `UseSetting("https_port", "443")` ile `http://` isteği → `StatusCode == TemporaryRedirect` +
  `AssertSecurityHeaders`; bir sonraki WebApi/güvenlik değişikliğinde (ör. 0002 veya B-4) ekle —
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
