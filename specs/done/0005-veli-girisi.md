# Spec 0005 — Veliler için telefon + SMS girişi ve KVKK rızası

- Status: Approved
- Mode: lite
- Plan: (pending)
- Self-critique: (pending)

## Intent

Kurumda öğrenci olan veliler, kendi hesaplarından telefon numarası + SMS doğrulama koduyla giriş yapar. İlk girişte,
Kişisel Verileri Koruma Kanunu (KVKK) aydınlatma metnini okur ve kurumun veri işleme amaçları hakkında açık rıza verir.
Başarı: veli telefonsuz giriş yapamaz; SMS kodu 5 dakika geçerli ve tahmin edilemez; rıza kaydı denetlenebilir; veliler
yalnızca çocuklarının bilgilerine erişir. Bu spec, gerçek girişin üçüncü parçasıdır (0003 → 0004 → 0005).

**Out of scope (burada değil):** Web admin panelinden veli hesap yönetimi; multi-parent households (aynı çocuğu iki veli);
veli izninin rollere göre farklı olması (tüm veliler aynı haklara sahip); SMS gönderme süresi ölçümü; SMS sağlayıcı
failover / retry; veli hesabında 2FA.

---

## Analyzing — Sorular

**[K1] SMS sağlayıcısı**
- Hangi SMS API'yi kullanacağız? (ör. Netgsm, Vodafone, AWS SNS)
- Sandbox / test ortamında mock SMS mi, yoksa gerçek API'ye test numarası ile deneme mi?
- SMS gönderme süresi (latency) SLA nedir? (ör. < 10 sn)
- Başarısız SMS durumu (timeout, network error, provider reject)? Yeniden denemeler kaç kere?

**[K2] KVKK metni ve rıza**
- KVKK aydınlatma metni kim yazıyor? (hukuk müşaviri, HR, marketing)
- Metin nerede saklanır? (database, config file, hardcoded)
- Rıza tarihçesi tutulacak mı? (evet → ne kadar tutacağız; hayır → sadece son rıza)
- Velinin rızayı geri çekmesi mümkün mü?
- Rızasız veli giriş yapabilir mi? (hayır → giriş yapamaz; evet → veri okuyamaz)

**[K3] Veli hesap yönetimi**
- Veli hesapları önceden oluşturulacak mı (admin panel / CSV import), yoksa ilk girişte otomatik oluşturulacak mı?
- Veli belirlemesi: kuruma kaydedilmiş telefon numarasından mı, yoksa veliyi belirleyen başka bir alan mı?
- Aynı çocuğun iki velisi varsa / bir çocuğun velisinin birden fazla telefon numarası varsa?

**[K4] İzinler ve veri erişimi**
- Veli hangi verilere erişebilir? (sadece çocuğunun dersler, notlar, haberler; kuruma ait finansman verileri yok)
- Birden fazla çocuğu olan veli hepsini görebilir mi?
- Veli başka velinin çocuğunu görüp göremez?

**[K5] Şifre yönetimi**
- Velinin şifresi var mı? (SMS yerine ek olarak)
- "Şifremi unuttum" varsa, SMS koduyla sıfırlama mı?

**[K6] Zaman planı**
- Tarih? (Spec 0005 şu haftası? Sonraki?)
- Veli hesapları baştan mı yükleniyor (pilot test), yoksa düşük hacim (ör. 10–20 hesap)?

---

## Requirements

1. **SMS Sağlayıcısı:** Veli girişi SMS kodunu Netgsm API üzerinden gönderir. Üretimde gerçek SMS; geliştirme/test
   ortamında mock sağlayıcı (`mock_sms` uygulaması, gerçek HTTP yapılmaz). SMS SLA < 10 saniye; başarısız SMS (timeout,
   network, provider reddi) 3 kere yeniden denenir ve üçüncü kez sonra hata döner.

2. **KVKK Rızası:** Veli ilk girişte bir kez KVKK aydınlatma metnini okur ve kurumun veri işleme amaçları hakkında
   yazılı rıza verir (hukuk müşaviri tarafından hazırlanan, AppSettings / environment config'e yerleştirilmiş metin).
   Rıza kaydı (tarih, kabul/red) veritabanında tutulur ve denetlenebilir. Velinin rızayı geri çekmesi scope dışı
   (future spec). Rızasız veli giriş yapabilir ama hiçbir kurum verisi göremez (rıza ekranında takılı kalır).

3. **Veli Belirleme:** Veli hesapları ilk girişte otomatik oluşturulur. Belirleme: telefon numarasından. Kurum, öğrenci
   velisinin telefon numarasını kaydetmişse, o numaradan SMS kodu alan hesap o veliye bağlanır. Aynı çocuğu iki veli
   varsa, her veli kendi telefon numarasıyla giriş yapar (çelişki yok → her veli çocuğunu görebilir; başka veli'nin
   telefonu kaydında yoksa oturum açamaz). Bir velinin birden fazla telefon numarası varsa: konfigürasyon müdürü
   belirlediği tek numara aktif, diğerleri silent reject.

4. **Veri Erişimi:** Veli oturum açınca çocuklarının bilgilerini (dersler, notlar, haberler, durum) görebilir. Kurumun
   finansman, tahsilat, işletme verileri veliye görünmez (ayrı iş mantığı kontrolü). Birden fazla çocuğu olan veli
   hepsini listede görür ve her çocuk için erişim ayrı yetkilendirilir. Başka velinin çocuğu görünmez.

5. **Telefon Yönetimi:** Veli hesabında şifre yoktur; giriş SMS koduyla yapılır. Telefonunu kaybeden veli için sunucu
   komutu (`reset-veli <email>` ör.) hesabı sıfırlar. Sıfırlama açık oturumları kapatır; sonraki girişte yeni SMS
   kodu gönderilir.

6. **SMS Kod Geçerliliği:** 6 haneli, 30 saniye pencereleri, ±1 penceresine kadar kabul (0004'e uyumlu). Kabul edilmiş
   kod ikinci kez reddedilir. Boşluklar yok sayılır. Hatalı kod giriş kilidine eklenir (0003 Requirement 4 ile aynı:
   kurum kodu + e-posta anahtarı, 5 hata / 15 dk). Şifre değiştirme / 2FA kurulumu gibi hassas işlemler yoktur
   (velide yapılmayan).

7. **Zaman Sınırı:** SMS kodu 5 dakika geçerlidir; süresi dolarsa kullanıcı baştan giriş yapar. SMS gönderme isteği
   dakikada 3 attempt ile sınırlıdır (spam engel).

## User experience

- **Flow:** Giriş ekranı (veli) → "Telefon numarasını gir" → SMS kodu gönder → SMS ekranında kod → (a) rızasız veli →
  KVKK ekranı → "Kabul ediyorum" → verilere erişim; (b) rızalı veli → doğrudan kuruma ait verilere erişim (Kurum
  bilgisi, Dersler, vb.).
- **States — Telefon (web ve mobil):** başlık "Veli Girişi", alan "Telefon numarası", düğme "SMS kodu gönder".
  - *Boş/geçersiz:* "Lütfen geçerli bir telefon numarası girin."
  - *Gönderiliyor:* "SMS kodu gönderiliyor..." + yükleniyor göstergesi.
  - *Başarılı:* "SMS kodunuz gönderildi. Lütfen 5 dakika içinde doğrulayın."
  - *Hata (SMS sağlayıcı):* "SMS gönderilemedi. Tekrar deneyin."
  - *Hatalı telefon (kurum veri tabanında yok):* "Bu telefon numarası kayıtlı değil."
  - *Hız sınırı:* "Çok sık kod göndermeye çalıştınız. 1 dakika sonra tekrar deneyin."
- **States — SMS Kodu:** başlık "SMS Kodunu Girin", alan "6 haneli kod", düğme "Doğrula", link "SMS kodunu tekrar gönder".
  - *Boş:* "SMS kodunu girin."
  - *Gönderiliyor:* "Doğrulanıyor..." + devre dışı.
  - *Hatalı kod:* "Kod hatalı veya süresi geçmiş." (alan temizlenir).
  - *Kilitli:* "Çok fazla başarısız deneme. 15 dakika sonra tekrar deneyin."
  - *Kod süresi doldu:* "Kodun süresi doldu. Yeni kod gönderin."
- **States — KVKK (ilk girişte, rızasız veli):** başlık "KVKK Aydınlatması", uzun metin (config'ten), checkbox "Verilerimin
  işlenmesine rıza veriyorum", düğme "Devam et".
  - *Checkbox işaretli değil:* "Devam et" devre dışı; "Lütfen rıza onayını işaretleyin."
  - *Başarılı:* Rıza kaydedilir; kuruma ait verilere erişim.
- **States — Kurum (veli girişi sonrası):** başlık "Kurum Bilgisi", altında çocukların listesi; her çocuğun dersler, notlar,
  haberler linki.
- **Design reference:** 0003 ekranlarının görsel dili (sade, etkili, mobil-first).
- **Accessibility:** telefon alanı numara klavyesi; SMS alanı numara klavyesi + OTP autofill; KVKK metni okunaklı
  (en az 4.5:1 kontrast); dokunma hedefleri 44 px min; durum metinleri duyurulur.

## Constraints & out of scope
- Sensitive area: Kimlik ve yetki (0003, 0004 kadar hassas)
- Out of scope: Veli hesap yönetimi (arayüz / admin panel); 2FA veliler için; SMS yanında başka giriş yöntemi
  (email, biometric); veli izninin rollere göre farklı olması; audit log veli girişi için (audit log spec'iyle).

## Acceptance criteria

- [x] AC-1 — Veli telefon numarasını girer; numarası kurum veri tabanında kayıtlıysa SMS kodu < 10 sn içinde gönderilir
  ve "SMS kodunuz gönderildi" mesajı görünür; kayıtlı değilse "Bu telefon numarası kayıtlı değil" ve giriş engellenir.
- [x] AC-2 — Veli SMS kodunu 5 dakika içinde girer ve doğrulama yapılır; geçerli kodla başarılı; hatalı / süresi geçmiş
  kodla "Kod hatalı veya süresi geçmiş"; 5 dakika sonra "Kodun süresi doldu, yeni kod gönderin".
- [x] AC-3 — Hatalı kodlar giriş kilidine eklenir; 5 hata + kurum kodu şifre hatalarıyla aynı sayaçta / 15 dk kilit;
  kilitliyken doğru SMS kodu da reddedilir.
- [x] AC-4 — Veli ilk girişte rızası yoksa KVKK ekranında rıza onayı ister; "Kabul ediyorum" işaretlenmezse devam
  butonu devre dışı; işaretlendikten sonra "Devam" oturum açar ve rıza kaydedilir; rızalı veliler KVKK ekranını
  görmez, doğrudan kurum verilere erişir.
- [x] AC-5 — Rıza kaydı (tarih, kabul/red) denetlenebilir; her veli için son rıza durumu saklanır.
- [x] AC-6 — Veli oturum açınca çocuğunun/çocuklarının dersleri, notları, haberleri görebilir; kurumun finansman /
  tahsilat / işletme verileri görünmez; başka velinin çocuğu görünmez.
- [x] AC-7 — Birden fazla çocuğu olan veli hepsini listede görür ve her çocuk için ayrı erişim kontrolü; erişim
  yetkilendirmesi başka modülün DF katmanında (veli → çocuk → veri).
- [x] AC-8 — Telefonunu kaybeden veli için sunucu komutu (`reset-veli <email>`) hesabı sıfırlar: açık oturumlar
  kapanır, sonraki girişte yeni SMS kodu gönderilir; komut yalnızca sıfırlama bilgisi yazar, gizli veri yazdırmaz.
- [x] AC-9 — SMS kod geçerliliği: 30 sn penceresi ±1 penceresine kadar kabul; kabul edilmiş kod ikinci kez reddedilir;
  boşluk yok sayılır; 6 haneli.
- [x] AC-10 — Dakikada 3 SMS kod isteği; 4. istek "Çok sık kod göndermeye çalıştınız" hatasıyla engellenir; 1 dakika
  sonra reset.
- [x] AC-11 — SMS kodu ve KVKK metni hiçbir log, hata yanıtında, veritabanında (okunaklı biçimde) görünmez; test
  ortamında mock SMS.
- [x] AC-12 — Web ve mobilde Telefon, SMS Kodu, KVKK ve Kurum ekranlarının User experience'taki her durumu kendi
  metniyle görünür; erişilebilirlik (numara klavyesi, OTP autofill, kontrast, dokunma hedefi) sağlanır.
- [x] AC-13 — Veli hesapları ilk girişte otomatik oluşturulur; tekrar girişte aynı telefon numarasından oturum açar;
  çocuk listesi açık olan veli değişirse sonraki girişte yeni liste gösterilir.

## Definition of Done
- [ ] Every acceptance criterion mapped to proof (test or reproducible observation)
- [ ] KVKK metni hukuk müşaviri tarafından onaylanmış
- [ ] Netgsm API test hesabı ve üretim anahtarı kayıtlı (AppSettings / environment)
- [ ] Veli verileri migrasyonu (pilot 20–30 veli veya tam kurum)
- [ ] `reset-veli` komutu belgelenmiş ve test edilmiş
