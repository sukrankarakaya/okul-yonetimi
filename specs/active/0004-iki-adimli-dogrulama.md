# Spec 0004 — Personel için iki adımlı doğrulama (2FA)

- Status: Draft
- Mode: lite
- Plan: `specs/plans/0004-plan.md`
- Self-critique: pending

## Intent
Hesabı ele geçirilen bir Kurucu/Yönetici ya da Muhasebe kullanıcısı kurumun bütün verisine ve tahsilat bilgisine
ulaşabilir. Bu yüzden bu roller girişte şifrenin yanında telefonlarındaki doğrulama uygulamasının (ör. Google
Authenticator, Microsoft Authenticator) ürettiği 6 haneli kodu da girer; Öğretmen isterse açar (`docs/security.md`).
Başarı: zorunlu rollerden biri kod olmadan hiçbir kurum verisine ulaşamaz; telefonunu kaybeden kullanıcı tek
kullanımlık bir kurtarma koduyla girer; kodlar kaba kuvvetle tahmin edilemez. Bu spec, gerçek girişin ikinci
parçasıdır (0003 → 0004 → 0005). Bilerek yapılmayanlar: SMS ile kod (veli girişi 0005'te), donanım anahtarı, "bu
cihazı hatırla", SSO (Faz 2), arayüzden kullanıcı yönetimi.

## Requirements
1. **Kimde zorunlu:** Kurucu/Yönetici veya Muhasebe rolü taşıyan her hesapta iki adımlı doğrulama zorunludur; bu
   rollerden biri bile varsa (ör. Öğretmen + Muhasebe) zorunludur. Yalnızca Öğretmen rolü taşıyan hesapta isteğe
   bağlıdır.
2. **İlk kurulum:** Zorunlu bir hesap, iki adımlı doğrulaması kurulu değilken şifresiyle giriş yaptığında oturum
   açılmaz; kullanıcı **Kurulum** adımına geçer ve bu adımı tamamlamadan hiçbir kurum verisine ulaşamaz. Kurulumda
   doğrulama uygulamasına eklenecek bilgi hem QR kodu hem elle girilebilen bir anahtar olarak gösterilir; kullanıcı
   uygulamanın ürettiği ilk kodu girerek kurulumu onaylar. Erteleme yoktur. Onaylanmamış kurulum kalıcı değildir:
   kullanıcı yarıda bırakırsa sonraki girişte kurulum baştan başlar.
3. **Kurtarma kodları:** Kurulum onaylanınca kullanıcıya **10 tek kullanımlık kurtarma kodu** bir kez gösterilir;
   bir daha gösterilmez. Kullanıcı kodları kaydettiğini onaylayınca devam eder. Her kurtarma kodu girişte doğrulama
   kodu yerine bir kez kullanılabilir.
4. **Girişte kod:** İki adımlı doğrulaması açık her hesap, doğru şifreden sonra **Doğrulama** adımına geçer; geçerli
   bir doğrulama kodu ya da kullanılmamış bir kurtarma kodu girmeden oturum açılmaz. Bu her girişte, web'de ve mobilde
   sorulur; oturum yenilemesi (0003, Requirement 5) kod sormaz. Doğrulama adımı, şifre adımından itibaren 5 dakika
   geçerlidir; süre dolarsa kullanıcı Giriş ekranına döner.
5. **Kodun geçerliliği:** Doğrulama kodu 30 saniyelik pencerelerle değişir; cihaz saati farkı için bir önceki ve bir
   sonraki pencerenin kodu da kabul edilir. Kabul edilmiş bir kod, geçerlilik süresi içinde ikinci kez kabul edilmez.
   Kodlardaki boşluklar yok sayılır; kurtarma kodlarında büyük/küçük harf farkı yoktur.
6. **Kaba kuvvete karşı:** Doğrulama adımındaki yanlış kodlar, şifre adımındaki yanlış denemelerle **aynı sayaca**
   eklenir (0003, Requirement 4: aynı kurum kodu + e-posta için 5 hata / 15 dk → 15 dk kilit). Kilitliyken doğru kod da
   oturum açmaz. Şifre değiştirmede yanlış "mevcut şifre" denemeleri de aynı sayaca eklenir (B-11); kilitliyken şifre
   değiştirilemez.
7. **Öğretmenin seçimi:** Yalnızca Öğretmen rolü taşıyan kullanıcı, oturum açıkken iki adımlı doğrulamayı açar (aynı
   kurulum akışı) ya da kapatır. Kapatmak geçerli bir doğrulama kodu ister. Zorunlu bir hesapta kapatma seçeneği
   görünmez ve kapatma istekleri reddedilir.
8. **Kurtarma kodlarını yenileme:** İki adımlı doğrulaması açık kullanıcı, geçerli bir doğrulama kodu girerek yeni 10
   kurtarma kodu alır; eski kodların hepsi o anda geçersiz olur.
9. **Sıfırlama (operasyon):** Telefonunu ve kurtarma kodlarını kaybeden kullanıcı için, sunucuda çalışan bir yönetim
   komutu o hesabın iki adımlı doğrulamasını sıfırlar (0003'teki ilk yönetici komutu gibi; arayüz yok). Sıfırlama
   hesabın bütün açık oturumlarını hemen kapatır; zorunlu hesap bir sonraki girişte kurulumu baştan yapar.
10. **Gizlilik:** Doğrulama anahtarı ve kurtarma kodları geri okunamaz biçimde saklanır (anahtar şifreli, kurtarma
    kodları geri çevrilemez özetle); hiçbir log, hata yanıtı veya çıktıda görünmez. Kurulumda gösterilen anahtar ve
    kurtarma kodları yalnızca o ekranda bir kez görünür.
11. **Sürekli koruma:** Zorunlu rol taşıyan ama iki adımlı doğrulaması kurulu olmayan bir hesap (ör. rolü sonradan
    değişen) bir sonraki girişinde Requirement 2'deki kurulum adımına alınır.

## User experience
- **Flow:** Giriş ekranı (0003) → "Giriş yap" → (a) 2FA açık → **Doğrulama** ekranı → kod → Kurum bilgisi;
  (b) zorunlu ve kurulu değil → **Kurulum** ekranı → QR / anahtar + ilk kod → **Kurtarma kodları** ekranı →
  "Kaydettim, devam et" → Kurum bilgisi; (c) 2FA kapalı Öğretmen → doğrudan Kurum bilgisi. Doğrulama ekranında
  "Kurtarma kodu kullan" bağlantısı kurtarma kodu girişine geçer. Kurum bilgisi ekranında **"Güvenlik"** →
  Güvenlik ekranı: 2FA durumu, "Kurtarma kodlarını yenile", (yalnızca Öğretmen) "İki adımlı doğrulamayı aç / kapat".
- **States — Doğrulama (web ve mobil, aynı metinler):** başlık "İki adımlı doğrulama", alan "Doğrulama kodu".
  - *Boş:* "Bu alan gerekli."; istek gönderilmez.
  - *Gönderiliyor:* "Doğrula" düğmesi devre dışı + yükleniyor göstergesi; çift gönderim olmaz.
  - *Hatalı kod:* "Kod hatalı veya süresi geçmiş." — alan temizlenir.
  - *Kilitli:* "Çok fazla başarısız deneme. 15 dakika sonra tekrar deneyin."
  - *Adım süresi doldu:* Giriş ekranı, üstünde "Doğrulama süresi doldu, tekrar giriş yapın."
  - *Kurtarma kodu modu:* alan "Kurtarma kodu"; aynı durumlar; "Doğrulama kodu kullan" ile geri dönülür.
  - *Bağlantı hatası:* "Bağlantı kurulamadı, tekrar deneyin."
- **States — Kurulum:** açıklama metni "Doğrulama uygulamanızla QR kodunu okutun veya anahtarı elle girin.", QR kodu,
  anahtar (kopyalanabilir), alan "Doğrulama kodu", düğme "Kurulumu tamamla". Hatalı kod / gönderiliyor / bağlantı
  hatası / adım süresi doldu durumları Doğrulama ile aynı metinlerle.
- **States — Kurtarma kodları:** 10 kod listesi, uyarı "Bu kodlar bir daha gösterilmeyecek. Güvenli bir yere
  kaydedin.", "Kopyala" (web'de ayrıca "İndir"), düğme "Kaydettim, devam et".
- **States — Güvenlik:** durum "İki adımlı doğrulama: Açık" / "Kapalı"; zorunlu hesapta "Rolünüz gereği zorunlu."
  notu ve kapatma seçeneği yok. Kurtarma kodlarını yenileme ve kapatma bir doğrulama kodu ister (hatalı kod / kilitli /
  bağlantı hatası metinleri yukarıdaki gibi). Başarıda: "Yeni kurtarma kodlarınız oluşturuldu." (ardından Kurtarma
  kodları ekranı) / "İki adımlı doğrulama kapatıldı."
- **Design reference:** none — 0003 ekranlarının görsel dili; analiz tuvalindeki `F0004-Ekranlar` maketleri durumların
  görünümünü gösterir, onaylanırsa tarihli hâli referanstır.
- **Accessibility:** kod alanları sayısal klavye açar ve tek kullanımlık kod otomatik doldurmayla uyumludur
  (web `autocomplete="one-time-code"`, mobil aynı karşılık); QR kodunun yanında anahtar metin olarak da vardır (ekran
  okuyucu); her alanın görünür etiketi, Enter formu gönderir, hata ve durum metinleri duyurulur; dokunma hedefleri en
  az 44 px; kontrast en az 4.5:1.

## Constraints & out of scope
- Sensitive area: Kimlik ve yetki
- Değişen 0003 davranışı (0003 `specs/done/`'da, değiştirilmez): 0003'ün AC-1'indeki "doğru şifreyle giriş yapar ve
  Kurum bilgisi ekranını görür" akışı, iki adımlı doğrulaması açık ya da zorunlu hesaplarda Doğrulama / Kurulum
  adımından geçer (Requirement 2, 4); 0003 AC-7'deki şifre değiştirme, yanlış mevcut şifre denemelerinde kilide bağlanır
  (Requirement 6, B-11). 0003'ün diğer kanıtları (kilit, oturum yenileme, çıkış, depolama, test girişi) yeşil kalır.
- Test ve geliştirme ortamlarındaki test girişi (0003 Requirement 9) iki adımlı doğrulamayı atlar; üretimde yoktur.
- Doğrulama kodu standart zaman tabanlı tek kullanımlık şifre biçimindedir (6 hane, 30 sn), yaygın doğrulama
  uygulamalarıyla uyumludur.
- Kurulum ve doğrulama adımları, oturum değildir: bu adımlardayken hiçbir kurum verisi okunamaz.
- UI testleri spec'teki metinleri literal kullanır (B-7).
- Boyut: 12 kriter (sınırın üstünde). Kurulum, doğrulama, kurtarma ve sıfırlama ayrı ayrı yayınlanamaz (zorunlu rol
  kurtarma yolu olmadan kilitlenir); ayrı spec önerilmedi — kullanıcı onayı bekliyor.
- **Out of scope:** SMS / e-posta ile kod; donanım anahtarı (WebAuthn/passkey); "bu cihazı hatırla"; yönetici
  panelinden başka kullanıcının 2FA'sını sıfırlama (kullanıcı yönetimi gelince); 2FA olaylarının audit kaydı
  (audit log spec'iyle); veli hesapları (0005); B-12, B-13.

## Acceptance criteria
- [ ] AC-1 — Yönetici, Muhasebe ve Öğretmen + Muhasebe hesapları 2FA kurulu değilken doğru şifreyle girdiğinde oturum
  açılmaz, Kurulum ekranı gelir; bu adımdayken hiçbir kurum verisi okunamaz; yalnızca Öğretmen rolü taşıyan ve 2FA'sı
  kapalı hesap doğrudan Kurum bilgisi ekranına geçer.
- [ ] AC-2 — Kurulumda QR kodu ve elle girilebilen anahtar görünür; doğrulama uygulamasının ürettiği geçerli kodla kurulum
  tamamlanır; hatalı kodla tamamlanmaz; yarıda bırakılan kurulum sonraki girişte yeni bir anahtarla baştan başlar.
- [ ] AC-3 — Kurulum tamamlanınca 10 kurtarma kodu bir kez gösterilir ve "Kaydettim, devam et" ile Kurum bilgisi
  ekranına geçilir; kodlar başka hiçbir yerden yeniden görüntülenemez.
- [ ] AC-4 — 2FA'sı açık hesap doğru şifreden sonra Doğrulama ekranına geçer; geçerli kodla oturum açılır ve Kurum
  bilgisi görünür; kod girilmeden, hatalı kodla ya da şifre adımından 5 dakika sonra oturum açılmaz (süre dolunca
  "Doğrulama süresi doldu" ile Giriş ekranı); oturum yenilemesi kod sormaz.
- [ ] AC-5 — Bir önceki ve bir sonraki 30 sn penceresinin kodu kabul edilir, iki pencere öteki reddedilir; kabul edilmiş
  bir kod aynı hesapta ikinci kez reddedilir; boşluklu kod kabul edilir.
- [ ] AC-6 — Kullanılmamış bir kurtarma kodu doğrulama kodu yerine oturum açar (büyük/küçük harf farkı yok); aynı kurtarma
  kodu ikinci kez reddedilir.
- [ ] AC-7 — Doğrulama adımındaki yanlış kodlar şifre hatalarıyla aynı sayaca eklenir: örneğin 3 yanlış şifre + 2 yanlış
  kod kilitler; kilitliyken doğru kod reddedilir; 15 dakika sonra açılır. Şifre değiştirmede 5 yanlış mevcut şifre aynı
  kilidi tetikler ve kilitliyken doğru mevcut şifreyle de şifre değiştirilemez (B-11).
- [ ] AC-8 — Yalnızca Öğretmen rolü taşıyan kullanıcı Güvenlik ekranından 2FA'yı açar (kurulum + kurtarma kodları) ve
  geçerli kodla kapatır; hatalı kodla kapatamaz; zorunlu hesapta kapatma seçeneği görünmez ve kapatma isteği reddedilir.
- [ ] AC-9 — Geçerli kodla kurtarma kodları yenilenir: yeni 10 kod bir kez gösterilir ve eski kodların hiçbiri artık
  oturum açmaz; hatalı kodla yenilenmez.
- [ ] AC-10 — Yönetim komutu bir hesabın 2FA'sını sıfırlar: hesabın açık oturumları bir sonraki istekte reddedilir; zorunlu
  hesap sonraki girişte kurulumu baştan yapar; komut olmayan hesap için açık bir hata verir ve hiçbir şeyi değiştirmez.
- [ ] AC-11 — Doğrulama anahtarı ve kurtarma kodları veritabanında okunabilir biçimde bulunmaz; hiçbir log, hata yanıtı
  veya çıktıda görünmez; test girişi 2FA'yı yalnızca geliştirme/test ortamında atlar ve üretimde bulunmaz (0003 AC-9
  yeşil kalır).
- [ ] AC-12 — Web ve mobilde Doğrulama, Kurulum, Kurtarma kodları ve Güvenlik ekranlarının User experience'taki her
  durumu kendi metniyle görünür; erişilebilirlik maddeleri (sayısal klavye, tek kullanımlık kod otomatik doldurma,
  anahtarın metin olarak varlığı, etiketler, Enter, duyurular) sağlanır.

## Definition of Done
- [ ] Every acceptance criterion mapped to proof (test or reproducible observation)
- [ ] `scripts/check` green
- [ ] Independent review done; real findings fixed, noise rejected with written rationale, deferred ones in `specs/backlog.md`
- [ ] Docs / ADRs updated if behavior or architecture changed
- [ ] Spec moved to `specs/done/` (it becomes immutable there)

## Scorecard (fill at ship — honest numbers make the process improvable)
| Metric | Value |
|---|---|
| Spec revisions | |
| Fix rounds | |
| Review findings: real / noise / deferred | |
| Regressions introduced | |
| Bugs escaped to production | |
