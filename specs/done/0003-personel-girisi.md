# Spec 0003 — Personel girişi: kurum kodu, e-posta ve şifreyle oturum

- Status: Shipped
- Mode: lite
- Plan: `specs/plans/0003-plan.md`
- Self-critique: done

## Intent
Okulun personeli (Kurucu/Yönetici, Öğretmen, Muhasebe/İdari) bugünkü test girişinin yerine kendi hesabıyla
web paneline ve mobil uygulamaya güvenli biçimde giriş yapsın. Başarı: her kullanıcı yalnızca kendi kurumunun
verisine ulaşır; kısa ömürlü oturum kullanıcı fark etmeden yenilenir; çıkış oturumu gerçekten kapatır; kaba
kuvvet denemesi kilitlemeyle durur; test girişi üretimde ve dağıtılan uygulama paketlerinde hiçbir biçimde yoktur.
Bu spec, gerçek girişin üç parçasının ilkidir (kullanıcı kararı 2026-11-19): 2FA → spec 0004, veli girişi
(telefon + SMS kodu, KVKK rızası) → spec 0005. Bilerek yapılmayanlar: şifre sıfırlama, hesapların arayüzden
oluşturulması (davet), birden fazla kuruma üyelik, SSO (Faz 2).

## Requirements
1. Personel, **kurum kodu + e-posta + şifre** ile web paneline ve mobil uygulamaya giriş yapar. Kurum kodu, kurumu
   tanımlayan, kurumlar arasında benzersiz bir koddur: 3–12 karakter, harf, rakam ve tire. Kurum kodu ve e-posta
   büyük/küçük harf duyarsız ve kültürden bağımsız karşılaştırılır (T-1: `I`/`ı` sorunu olmaz); baştaki ve sondaki
   boşluklar kırpılır.
2. Başarılı giriş, oturumu girişteki hesabın kurumuna ve rollerine bağlar. Hangi kurumun verisinin görüleceği
   yalnızca oturumdan belirlenir; istemci kurum seçemez veya değiştiremez (BR-1). Bir hesap birden fazla rol
   taşıyabilir (ör. Öğretmen + Muhasebe); oturum hepsini taşır.
3. Hatalı girişte hangi alanın yanlış olduğu, hesabın veya kurumun var olup olmadığı ele verilmez: yanlış şifre,
   bilinmeyen e-posta, bilinmeyen kurum kodu ve başka kurumun e-postası aynı yanıtı alır.
4. Aynı kurum kodu + e-posta için art arda 5 başarısız denemeden sonra o giriş 15 dakika kilitlenir. "Art arda":
   son başarısız denemeden 15 dakika geçince sayaç kendiliğinden sıfırlanır. Kilitliyken doğru şifre de oturum açmaz
   ve kilit süresince yapılan denemeler kilidi uzatmaz. Süre dolunca kilit kendiliğinden kalkar. Başarılı bir giriş
   sayacı sıfırlar. Kilitlenme var olmayan bir hesap için de aynı biçimde görünür (Requirement 3). Ayrıca aynı
   kaynaktan dakikada 20'den fazla giriş denemesi, hesaptan bağımsız olarak, aynı "çok fazla deneme" yanıtını alır.
5. Oturum kısa ömürlüdür (erişim 15 dakika) ve kullanıcı fark etmeden yenilenir. Yenilemenin girişten itibaren
   sabit bir üst sınırı vardır: web'de 12 saat, mobilde 30 gün; kullanıldıkça uzamaz. Sınır dolunca kullanıcı yeniden
   giriş yapar. Aynı anda birden fazla istek yenileme gerektirirse tek bir yenileme yapılır; web'de aynı tarayıcının
   sekmeleri aynı oturumu paylaşır. Bir kez kullanılmış bir yenileme bilgisi yeniden gelirse (çalınma işareti) o oturum
   tamamen kapanır ve kullanıcı yeniden giriş yapar.
6. Çıkış, oturumu sunucuda da **hemen** kapatır: çıkıştan sonraki ilk istekten itibaren o oturumla ne veri alınır ne
   oturum yenilenir (erişim süresinin dolması beklenmez). Aynı kullanıcının başka bir cihazdaki oturumu etkilenmez.
7. Oturum açmış kullanıcı kendi şifresini değiştirir: mevcut şifresini doğrular, yeni şifre en az 10 karakterdir ve
   iki kez aynı girilir. Değişiklikten sonra eski şifre çalışmaz ve kullanıcının diğer oturumları **hemen** kapanır
   (Requirement 6 gibi); bu cihazdaki oturum sürer.
8. Web'de oturum bilgisi sayfadaki betiklerin okuyamayacağı biçimde tutulur; tarayıcı deposunda (kalıcı veya oturum
   deposu) bulunmaz. Mobilde oturum bilgisi cihazın güvenli deposunda tutulur.
9. Test girişi yalnızca geliştirme ve test ortamlarında kalır (otomatik testler için). Üretim ortamında bulunmaz
   (0001/AC-6 korunur), dışarıya yayımlanan API dokümanında yer almaz ve web ile mobil üretim paketlerinde hiçbir
   kalıntısı olmaz (B-3). Web ve mobil uygulamalardaki otomatik test girişi kalkar; uygulamalar giriş ekranıyla açılır.
10. Oturumu olmayan ya da yenilenemeyen kullanıcı giriş ekranına döner; kurum verisi gösterilmez.

## User experience
- **Flow:** Kullanıcı web panelini veya mobil uygulamayı açar → oturum yoksa **Giriş** ekranı → kurum kodu, e-posta,
  şifre → "Giriş yap" → **Kurum bilgisi** ekranı (0001). Kurum bilgisi ekranında "Çıkış yap" → Giriş ekranı.
  Kurum bilgisi ekranından "Şifremi değiştir" → **Şifre değiştir** ekranı → başarıda Kurum bilgisi ekranına döner.
  Mobilde kurum kodu ve e-posta cihazda hatırlanır ve bir sonraki girişte dolu gelir; şifre asla hatırlanmaz.
- **States — Giriş (web ve mobil, aynı metinler):**
  - *Boş / eksik alan:* boş alanın altında "Bu alan gerekli."; istek gönderilmez.
  - *Gönderiliyor:* "Giriş yap" düğmesi devre dışı + yükleniyor göstergesi; çift gönderim olmaz.
  - *Hatalı bilgi:* "Kurum kodu, e-posta veya şifre hatalı." — şifre alanı temizlenir, diğerleri kalır.
  - *Kilitli:* "Çok fazla başarısız deneme. 15 dakika sonra tekrar deneyin."
  - *Bağlantı hatası:* "Bağlantı kurulamadı, tekrar deneyin."
  - *Oturum süresi doldu:* Giriş ekranı, üstünde "Oturumunuzun süresi doldu, tekrar giriş yapın."
  - *Çıkış yapıldı:* Giriş ekranı, ek mesaj yok.
- **States — Şifre değiştir:** alanlar *mevcut şifre*, *yeni şifre*, *yeni şifre (tekrar)*.
  - *Kural ihlali:* "Yeni şifre en az 10 karakter olmalı." / "Yeni şifreler eşleşmiyor."
  - *Mevcut şifre yanlış:* "Mevcut şifre hatalı."
  - *Gönderiliyor:* düğme devre dışı + yükleniyor göstergesi.
  - *Başarılı:* Kurum bilgisi ekranı, üstünde "Şifreniz değiştirildi."
  - *Bağlantı hatası:* "Bağlantı kurulamadı, tekrar deneyin."
- **Design reference:** none — kullanıcı kararı (2026-11-19): şimdilik modern ve sade bir arayüz, 0001 ekranlarıyla
  aynı görsel dil; tasarım sonra ele alınacak. Onaylı bir tasarım geldiğinde spec değişikliği olarak işlenir (R-08).
- **Accessibility:** her alanın görünür etiketi var; web'de Tab sırası kurum kodu → e-posta → şifre → düğme, Enter
  formu gönderir; hata ve durum metinleri ekran okuyucuya duyurulur ve ilgili alanla ilişkilidir; şifre alanı
  tarayıcının ve cihazın şifre yöneticisiyle uyumludur; dokunma hedefleri en az 44 px; metin kontrastı en az 4.5:1.

## Constraints & out of scope
- Sensitive area: Kimlik ve yetki
- Sensitive area: Tenant izolasyonu (oturumun kuruma bağlanması — BR-1)
- Değişen 0001 davranışı (0001 `specs/done/`'da, değiştirilmez): 0001'in User experience'taki "test girişiyle oturum
  açmış kullanıcı" akışı ve "Oturum açmanız gerekiyor." durumu bu spec'le giriş ekranına dönüşür (Requirement 9–10);
  0001'in AC-1…AC-7 kanıtları gerçek girişle de yeşil kalır.
- Hesaplar bu spec'te arayüzden oluşturulmaz: geliştirme ve testte örnek hesaplar hazır gelir; üretimde ilk
  yöneticiyi açan bir operasyon adımı dokümante edilir.
- Şifreler geri çevrilemez biçimde saklanır; hiçbir log, hata yanıtı veya çıktıda görünmez (`docs/security.md`).
- Veli rolü bu spec'te giriş yapamaz (spec 0005); yalnızca veli rolü taşıyan bir hesap "hatalı bilgi" yanıtı alır.
  Hem personel hem veli rolü olan hesap personel olarak girer.
- Bir hesap tek bir kuruma aittir; aynı e-posta iki kurumda iki ayrı hesap olabilir, kurum kodu onları ayırır.
- UI testleri spec'teki metinleri literal kullanır (B-7).
- Boyut: 10 kriter (sınırda). Şifre değiştirmenin ayrı bir spec olması önerilmedi: oturum kapatma mantığını giriş ve
  çıkışla paylaşır, ayrı spec aynı mantığı iki kez kanıtlatırdı — kullanıcı, 2026-11-19, self-critique G8.
- **Out of scope:** 2FA (spec 0004); veli girişi ve KVKK rızası (spec 0005); şifre sıfırlama ve personel daveti (e-posta
  sağlayıcısı gerekir, ADR); "beni hatırla"; aktif oturumların listesi ve uzaktan kapatma; SSO (Faz 2); birden fazla
  kuruma üyelik; giriş denemelerinin audit kaydı; yönetici paneline özel ekranlar; B-6.

## Acceptance criteria
- [x] AC-1 — Doğru kurum kodu, e-posta ve şifreyle Yönetici, Öğretmen ve Muhasebe rollerinin her biri web'de ve mobilde
  giriş yapar ve kendi kurumunun Kurum bilgisi ekranını görür; iki rollü bir hesabın oturumu iki rolü de taşır.
- [x] AC-2 — Yanlış şifre, bilinmeyen e-posta, bilinmeyen kurum kodu, başka kurumun e-postası ve yalnızca veli rolü
  taşıyan hesap aynı "hatalı bilgi" yanıtını alır; hiçbirinde oturum açılmaz.
- [x] AC-3 — Aynı kurum kodu + e-posta için 5 başarısız denemeden sonra giriş 15 dakika kilitlenir (var olmayan hesapta
  da aynı yanıt); kilitliyken doğru şifre reddedilir ve denemeler kilidi uzatmaz; 15 dakika sonra doğru şifre oturum
  açar; 4 hata + başarılı giriş + 4 hata kilitlemez; 4 hata + 15 dakika bekleme + 4 hata kilitlemez; aynı kaynaktan
  dakikada 21. deneme "çok fazla deneme" yanıtını alır. Kurum kodu ve e-posta büyük/küçük harf ve baş/son boşluk
  farkıyla aynı hesabı bulur (`I`/`ı` içeren örnek dahil).
- [x] AC-4 — A kurumunun personelinin oturumu B kurumunun hiçbir verisine erişemez ve istemci tarafında kurum
  değiştirilemez: 0001'in AC-2, AC-3, AC-4 kanıtları gerçek girişle açılan oturumlarla yeşildir.
- [x] AC-5 — Erişim süresi (15 dk) dolduğunda oturum kullanıcı fark etmeden yenilenir ve ekran kesintisiz çalışır;
  aynı anda gelen birden fazla istek tek bir yenileme yapar; yenilemenin üst sınırı (web 12 saat, mobil 30 gün) dolunca
  yenileme reddedilir ve kullanıcı "Oturumunuzun süresi doldu" mesajıyla Giriş ekranına döner; kullanılmış bir
  yenileme bilgisi tekrar gelirse oturum tamamen kapanır.
- [x] AC-6 — Çıkıştan sonraki ilk istekte, erişim süresi dolmamış olsa da, o oturumla ne veri alınır ne oturum
  yenilenir; aynı kullanıcının başka cihazdaki oturumu çalışmaya devam eder.
- [x] AC-7 — Şifre değiştirme: mevcut şifre yanlışsa, yeni şifre 10 karakterden kısaysa veya iki giriş eşleşmiyorsa
  reddedilir; başarıda eski şifreyle giriş reddedilir, kullanıcının diğer oturumlarının bir sonraki isteği reddedilir,
  bu cihazdaki oturum sürer.
- [x] AC-8 — Web'de oturum bilgisi sayfa betiklerinden okunamaz ve tarayıcı deposunda bulunmaz; mobilde oturum bilgisi
  cihazın güvenli deposundadır; şifre hiçbir log, hata yanıtı veya çıktıda görünmez.
- [x] AC-9 — Test girişi üretim ortamında bulunmaz, yayımlanan API dokümanında yer almaz ve web ile mobil üretim
  paketlerinde hiçbir kalıntısı yoktur; web ve mobil uygulamalar oturum yokken Giriş ekranıyla açılır.
- [x] AC-10 — Web ve mobilde Giriş ve Şifre değiştir ekranlarının User experience'taki her durumu kendi metniyle görünür;
  Kurum bilgisi ekranında "Çıkış yap" ve "Şifremi değiştir" çalışır; erişilebilirlik maddeleri (etiketler, Tab sırası,
  Enter, duyurular) sağlanır.

## Definition of Done
- [x] Every acceptance criterion mapped to proof (test or reproducible observation)
- [x] `scripts/check` green
- [x] Independent review done; real findings fixed, noise rejected with written rationale, deferred ones in `specs/backlog.md`
- [x] Docs / ADRs updated if behavior or architecture changed
- [x] Spec moved to `specs/done/` (it becomes immutable there)

## Scorecard (fill at ship — honest numbers make the process improvable)
| Metric | Value |
|---|---|
| Spec revisions | |
| Fix rounds | |
| Review findings: real / noise / deferred | |
| Regressions introduced | |
| Bugs escaped to production | |
