# Security

> Bootstrap 2026-11-17. Çocuk verisi işlendiği için KVKK ve güvenlik MVP'den itibaren tamdır (`docs/kapsam.md`).
> Her plan ve review bu kurallara uyar.

## Secrets
- Secrets never enter the repo, specs, prompts, or chat. `.env` is gitignored; provide `.env.example`.
- Agents never print secret values, even when debugging.
- Lokal: `dotnet user-secrets`; ortamlar: secret manager. Ödeme, SMS ve AI anahtarları ortam başına ayrı.

## Input & output
- Tüm girdiler sınırda doğrulanır (FluentValidation; web/mobilde şema doğrulama). Yalnızca parametreli sorgu.
- Hata yanıtları ProblemDetails; stack trace, SQL, iç tip adı, kişisel veri sızmaz (`docs/conventions.md`).
- Dosya yükleme: tip (içerik imzası) ve boyut kontrolü, virüs tarama kancası; dosyalar public olmayan
  depolamada, erişim kısa ömürlü imzalı URL ile.
- Kişisel veri (`[Sensitive]`) log'da maskelenir (T-10). AI'a veri minimizasyonuyla gider; TC no ve sağlık notu asla (T-11).
- CORS yalnızca bilinen origin'ler; CSP, HSTS ve diğer güvenlik başlıkları açık. Hedef: OWASP ASVS Level 2.
- Taşıma TLS 1.2+; disk/DB şifreli; sağlık notu, alerji ve TC kimlik no **alan bazında ayrıca şifreli**,
  anahtarlar uygulama dışında.
- Kart verisi sisteme hiç girmez: sağlayıcı (iyzico/Paycell) ödeme formu + tokenizasyon. Ödeme callback'i
  imza doğrulamalı ve idempotent (T-6).

## AuthN / AuthZ
- Personel: e-posta + şifre. Veli: telefon + SMS OTP.
- Kısa ömürlü JWT access token (15 dk) + dönen refresh token; mobilde SecureStore, web'de httpOnly cookie.
  *Geçici (spec 0001 → gerçek giriş spec'i):* iskelette web token yalnızca bellekte tutulur (localStorage yok); httpOnly cookie akışı gerçek giriş spec'inde.
- 2FA (TOTP) **Kurucu/Yönetici ve Muhasebe için zorunlu**, Öğretmen için isteğe bağlı.
- Giriş ve OTP'de rate limit + hesap kilitleme.
- **Default deny:** her endpoint açık yetki politikası ister; yetkisiz endpoint mimari/entegrasyon testinde yakalanır.
- Yetki iki katmanlı: rol + kaynak (şube, sınıf, "kendi çocuğu") — BR-6. Tenant izolasyonu BR-1, her sorguda.
- KVKK: veli ilk girişte aydınlatma metni + açık rıza; rıza kayıtları sürümlü. Fotoğraf paylaşımında
  öğrenci bazlı izin (BR-8). Mezun/ayrılan öğrenci verisi saklama süresi sonunda anonimleştirilir;
  finans kayıtları yasal süre boyunca tutulur.
- Audit log: öğrenci kaydının her görüntülenmesi/değiştirilmesi (kim, ne, ne zaman, IP); append-only.
- AI sağlayıcısı: verinin model eğitiminde kullanılmadığı sözleşme şartı (ADR). Barındırma ve Türkiye'de
  barındırma seçeneği altyapı ADR'sinde.

## Dependencies
- Yeni paket: aktif bakımlı, uyumlu lisans (MIT, Apache-2.0, BSD); plan onayında gerekçesiyle listelenir.
- GPL/AGPL ve ticari/ücretli lisanslı paketler (ör. FluentAssertions 8+) ADR olmadan eklenmez.
- `dotnet list package --vulnerable` ve `npm audit --audit-level=high` check'te; yüksek seviye açık check'i kırar.
- Sürümler kilitli: `packages.lock.json`, `package-lock.json`. Expo paketleri `npx expo install` ile (T-9).

## Review lens
Security is a mandatory dimension of every independent review (see `prompts/review.md`), not a
separate afterthought phase.
