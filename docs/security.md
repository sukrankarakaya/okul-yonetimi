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
- Kısa ömürlü JWT access token (15 dk) + dönen refresh token; mobilde SecureStore, web'de httpOnly cookie
  (spec 0003): web'de erişim ve yenileme **ikisi de** httpOnly + Secure + SameSite=Strict çerez, gövdede jeton yok;
  çerezli değiştiren isteklerde `X-Okul-Csrf` başlığı şart. Oturum mutlak sınırı web 12 saat, mobil 30 gün; yenileme
  bilgisi her kullanımda döner, kullanılmış bilgi tekrar gelirse oturum kapanır (10 sn paralel tolerans).
- **Her istekte oturum denetimi:** çıkış ve şifre değişikliği bir sonraki istekte geçerlidir (15 dk beklenmez).
- Personel girişi: kurum kodu + e-posta + şifre; yanlış girdinin her türü tek cevap alır; şifre PBKDF2 özetiyle saklanır,
  bilinmeyen hesapta da sahte bir doğrulama çalışır. Test girişi yalnızca Development/Testing'de, OpenAPI dışında.
- **İlk yönetici (üretim):** `dotnet Okul.WebApi.dll create-admin --kurum-kodu <kod> --e-posta <adres>`; şifre standart
  girdiden okunur (terminalde ekrana yansımadan; pipe ile secret manager'dan da verilebilir), komut satırına yazılmaz ve hiçbir yerde yazdırılmaz (veritabanı önceden migrate edilmiş olmalı).
- 2FA (TOTP) **Kurucu/Yönetici ve Muhasebe için zorunlu**, Öğretmen için isteğe bağlı (spec 0004): rollerden biri zorunluysa
  zorunlu; RFC 6238, 6 hane / 30 sn, ±1 pencere, kabul edilen adım bir daha kabul edilmez. Doğru şifreden sonra oturum
  değil 5 dakikalık bir ara adım (opak belirteç, veri açmaz); zorunlu ve kurulmamış hesap kurulumu tamamlamadan veri
  göremez. Kurtarma kodları: 10 tek kullanımlık `XXXX-XXXX`, bir kez gösterilir. Yanlış kodlar ve şifre değiştirmedeki
  yanlış mevcut şifre giriş kilidiyle aynı sayaca eklenir (B-11); kaynak sınırı kod adımlarını da kapsar.
- **`Auth:TwoFactorKey`** (base64, 32 bayt; `Auth:SigningKey` gibi gizli yapılandırma): 2FA anahtarı AES-GCM ile bu
  anahtarla şifreli, kurtarma kodları bundan türetilen anahtarla HMAC özeti olarak saklanır — veritabanı kopyası tek
  başına ne anahtarı açar ne kodu dener. Üretimde yoksa uygulama açılmaz; **kaybolursa bütün 2FA kurulumları okunamaz**
  (her hesap `reset-2fa` ile sıfırlanır). Geliştirme/Test'te tanımsızsa her açılışta geçici anahtar — geliştirmede kurulan 2FA yeniden başlatınca açılamaz (doğrulama 500); kalıcı olsun diye `dotnet user-secrets set Auth:TwoFactorKey "$(openssl rand -base64 32)"` (WebApi projesinde).
- **2FA sıfırlama (operasyon):** `dotnet Okul.WebApi.dll reset-2fa --kurum-kodu <kod> --e-posta <adres>` — 2FA ve
  kurtarma kodlarını siler, hesabın bütün oturumlarını ve ara adımlarını kapatır; yalnızca sonucu ve kapanan oturum
  sayısını yazar. Sunucuya erişim yetkidir; kim, ne zaman çalıştırdı kaydı audit log spec'iyle gelir.
- **Veli girişi (spec 0007, ADR 0002):** kurum kodu + cep telefonu → 6 haneli, tek kullanımlık, 5 dk geçerli SMS kodu
  (yeni kod ve "Vazgeç" bekleyeni iptal eder). Kayıtlı olan/olmayan numara ve bilinmeyen kurum kodu **aynı yanıtı** alır;
  kayıtlı olmayana da gönderilmeyen bir kod düşer, süre/iptal/kilit davranışı aynıdır; kod isteği en az 1,5 sn yanıt verir
  (`Auth:GuardianCodeMinimumResponseMs`) ki SMS süresi kayıtlıyı ele vermesin — yalnız sağlayıcı yavaş/çökükken fark
  görünür. Aynı kurum + telefon için 5 yanlış kod / 15 dk kilit, dakikada 3 kod isteği; kaynak başına dakikada 20.
  Oturum personelinkiyle aynı kurallar (Kimlik, `AccountKind = Guardian`); veli oturumu personel uçlarına erişemez
  (varsayılan politika yalnız personel rolleri; çıkış, oturum ve kurum bilgisi uçları ikisine açık); veli verisi güncel
  KVKK rızası ister (rıza: veli, kurum, metin sürümü, zaman; sürüm değişince yeniden).
- **`Auth:GuardianCodeKey`** (base64, 32 bayt, gizli yapılandırma): kurum kodu + telefon anahtarı ve kod HMAC özetle
  saklanır — veritabanı kopyası telefon ya da kod denemeye yetmez. Üretimde yoksa uygulama açılmaz; Geliştirme/Test'te geçici.
- **SMS:** üretimde yalnız Netgsm (`Sms:Provider = Netgsm`, `Sms:Netgsm:UserCode|Password|SenderId` secret manager'da);
  sahte gönderici yalnız Development/Testing, başka ortamda uygulama açılmaz. Kod ve tam telefon hiçbir log/yanıtta yok.
  Gerçek gönderim yalnız sahte HTTP ile kanıtlı — hesap gelince manuel duman testi (plan 0007, R-5).
- **Veli sıfırlama (operasyon):** `dotnet Okul.WebApi.dll reset-veli --kurum-kodu <kod> --telefon <numara>` — velinin
  bütün oturumlarını kapatır, kilidi kaldırır, bekleyen kodu bitirir; yalnızca sonucu ve kapanan oturum sayısını yazar.
- Giriş ve OTP'de rate limit + hesap kilitleme. Personel girişi (spec 0003): aynı kurum kodu + e-posta için 5 hata /
  15 dk → 15 dk kilit (var olmayan hesapta da aynı); kaynak başına dakikada 20 deneme. TLS sonlandıran proxy arkasında
  kaynak adresi için B-4.
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
