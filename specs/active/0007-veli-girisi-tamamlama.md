# Spec 0007 — Veli girişini uçtan uca tamamlama: telefon + SMS kodu, KVKK rızası

- Status: In progress
- Mode: lite
- Plan: `specs/plans/0007-plan.md`
- Self-critique: done

## Intent
Spec 0005 veli girişini tanımladı ve `specs/done/`'a taşındı; ancak kodda uçtan uca çalışmıyor: veli uç noktası,
api-client çağrısı ve veli oturumu yok, ekranlar bağlı değil, REVIEW/VERIFY yapılmadı (B-17; tuval F0005-Analiz).
Veli ana ekranı (0006) çalışan bir veli oturumuna bağlı. Bu spec veli girişini gerçekten çalışır hâle getirir ve
0005'teki çelişkileri kararla giderir. Başarı: veli web ve mobilde SMS koduyla girer, ilk girişte KVKK rızası verir,
oturumu personel oturumuyla aynı kurallarla yaşar, çıkış oturumu hemen kapatır; her kriter bağımsız REVIEW + VERIFY
ile kanıtlanır. 0005 dosyası değiştirilmez (kural 7); bu spec onun giriş davranışını tutarlı biçimde yeniden tanımlar.
Bilinçli olarak yapılmayan: çocuk/öğrenci verisi (0006), veli yönetim arayüzü, rızanın geri çekilmesi.

## Requirements
<!-- "(K-n)" = aşağıdaki clarify kararına bağlı (kullanıcı onayı, 2026-11-20). -->
1. Veli **kurum kodu + cep telefonu** ile giriş başlatır (K1). Kurum kodu 0003 kurallarıyla karşılaştırılır; telefon
   Türkiye cep biçimine normalize edilir (`0532…`, `532…`, `+90 532…` aynı numaradır, boşluk/tire yok sayılır).
2. Yalnızca o kurumda kayıtlı bir velinin telefonuna kod gönderilir. Kayıtlı olmayan numara, bilinmeyen kurum kodu
   ve kayıtlı numara **aynı yanıtı** alır; numaranın kayıtlı olup olmadığı ele verilmez (K3).
3. Kod 6 haneli, rastgele, **tek kullanımlık** ve 5 dakika geçerlidir; yeni kod istenince önceki geçersiz olur;
   boşluklar yok sayılır (K2 — 0005'teki 30 sn pencere modeli kullanılmaz).
4. Kaba kuvvet: aynı kurum + telefon için art arda 5 yanlış kod → 15 dakika kilit (kilitliyken doğru kod de oturum
   açmaz; 0003 Requirement 4'ün sayaç kuralları); kilit kayıtlı olmayan numarada da aynı biçimde görünür (K3, G-3).
   Aynı kurum + telefon için dakikada en fazla 3 kod isteği, "tekrar gönder" dahil (G-2); aynı kaynaktan dakikada en
   fazla 20 giriş denemesi.
5. SMS üretimde Netgsm ile gönderilir; geliştirme ve testte gerçek SMS gitmez. Sağlayıcı geçici hatada 3 kez
   denenir, denemelerin toplamı en fazla 10 saniyedir; sonra kullanıcıya hata gösterilir (G-4).
6. KVKK: rızası olmayan veli (ilk giriş, ya da aydınlatma metninin sürümü değiştiyse — K6) önce rıza ekranını görür;
   rıza verilene kadar hiçbir kurum verisi dönmez ve sunucu da reddeder. Rıza kaydı: veli, kurum, metin sürümü, zaman.
7. Oturum 0003 kurallarıyla yaşar: erişim 15 dk, sessiz yenileme, üst sınır web 12 saat / mobil 30 gün, kullanılmış
   yenileme bilgisi tekrar gelirse oturum kapanır, çıkış sunucuda hemen kapatır (K4).
8. Veli oturumu yalnızca veli işlemlerine, personel oturumu yalnızca personel işlemlerine erişir. Aynı telefon iki
   kurumda iki ayrı velidir; bir kurumun oturumu diğerinin verisine erişemez.
9. Veli kaydı (kurumun veli telefon listesi) bu spec'te yalnızca geliştirme/demo örnek verisiyle oluşur; veli giriş
   hesabı kayıtlı velinin ilk başarılı girişinde oluşur, sonraki girişlerde aynı hesap kullanılır (K5).
10. Telefonunu kaybeden veli için sunucu komutu (kurum kodu + telefon) velinin oturumlarını kapatır ve kilidini
    kaldırır; gizli veri yazdırmaz (K8).
11. Kod veritabanında okunamaz biçimde saklanır; kod ve tam telefon numarası hiçbir log, hata yanıtı veya çıktıda
    görünmez (telefon maskeli).
12. Veli girişi web ve mobilde çalışır; personel Giriş ekranında "Veli girişi" bağlantısıyla açılır (K7).

## User experience
- **Flow:** Giriş ekranı → "Veli girişi" → **Telefon** (kurum kodu + telefon) → "SMS kodu gönder" → **SMS kodu** →
  (rızasız) **KVKK** → "Devam et" → veli ana ekranı (0006; o gelene kadar mevcut Kurum bilgisi ekranı). Mobilde kurum
  kodu cihazda hatırlanır ve çıkışta silinmez; telefon numarası ve kod hiç hatırlanmaz (G-5).
- **States — Telefon:** başlık "Veli Girişi", alanlar "Kurum kodu", "Telefon numarası", düğme "SMS kodu gönder".
  - *Boş alan:* "Bu alan gerekli." · *Geçersiz telefon:* "Lütfen geçerli bir telefon numarası girin."
  - *Gönderiliyor:* düğme devre dışı, "SMS kodu gönderiliyor..."
  - *Gönderildi (kayıtlı olsun olmasın — K3):* SMS kodu ekranına geçer, üstte "Numara kayıtlıysa SMS kodu gönderildi. Kod 5 dakika geçerli."
  - *Sağlayıcı hatası:* "SMS gönderilemedi. Tekrar deneyin." · *Hız sınırı:* "Çok sık kod istediniz. 1 dakika sonra tekrar deneyin."
  - *Bağlantı hatası:* "Bağlantı kurulamadı, tekrar deneyin."
- **States — SMS kodu:** başlık "SMS Kodunu Girin", alan "6 haneli kod", düğme "Doğrula", bağlantılar "SMS kodunu tekrar gönder", "Vazgeç".
  - *Boş:* "Bu alan gerekli." · *Doğrulanıyor:* düğme devre dışı, "Doğrulanıyor..."
  - *Hatalı / kullanılmış kod:* "Kod hatalı veya süresi geçmiş." (alan temizlenir)
  - *Süresi doldu:* "Kodun süresi doldu. Yeni kod isteyin." · *Kilitli:* "Çok fazla başarısız deneme. 15 dakika sonra tekrar deneyin."
  - *Tekrar gönder hız sınırında:* bağlantı devre dışı + "Çok sık kod istediniz. 1 dakika sonra tekrar deneyin." (G-2)
  - *Vazgeç:* bekleyen kod iptal edilir, Telefon ekranına dönülür; kurum kodu ve telefon alanlarda kalır (G-1).
- **States — KVKK:** başlık "KVKK Aydınlatması", metin (yapılandırmadan, sürümlü), onay kutusu "Verilerimin işlenmesine
  rıza veriyorum", düğme "Devam et". *İşaretsiz:* düğme devre dışı + "Lütfen rıza onayını işaretleyin." *Başarılı:* rıza kaydedilir, ana ekran.
- **States — oturum:** *süresi doldu:* Veli Girişi ekranı, üstte "Oturumunuzun süresi doldu, tekrar giriş yapın." *Çıkış:* Veli Girişi ekranı.
- **Design reference:** tuval "Okul Yönetimi — Feature Analizleri", pano `F0005-Ekranlar`, sürüm `1795197149-3b51`
  (2026-11-20) — metinler bu spec'tekilerle değişir ve Telefon ekranına "Kurum kodu" alanı eklenir; 0003 görsel dili.
- **Accessibility:** görünür etiketler; telefon için `tel`, kod için sayısal klavye + OTP otomatik doldurma; web'de Tab
  sırası kurum kodu → telefon → düğme, Enter gönderir; durum metinleri duyurulur ve alanla ilişkili; KVKK metni
  kaydırılabilir ve okunaklı; kontrast en az 4.5:1; dokunma hedefleri en az 44 px.

## Constraints & out of scope
- Sensitive area: Kimlik ve yetki
- Sensitive area: Tenant izolasyonu
- Sensitive area: Çocuk ve sağlık verisi (KVKK) — açık rıza kaydı
- Yeniden tanımlanan 0005 davranışı (0005 değiştirilmez): 0005/AC-1…AC-5 ve AC-8…AC-13 bu spec'in kriterleriyle
  karşılanır; 0005/AC-6–AC-7 (çocuk verisi) → 0006 (B-17).
- Mimari: veli kaydı `docs/architecture.md`'ye göre Kayıt modülüne ait; 0005'in ayrı `Parents` modülü PLAN'da ADR
  önerisiyle ele alınır (spec davranışı değiştirmez).
- KVKK metninin hukuk onayı bu spec'i bloklamaz: onaylanana kadar yer tutucu metin ve sürüm `taslak` (B-18 ile birlikte).
- UI testleri spec'teki metinleri literal kullanır (B-7).
- Out of scope: veli/öğrenci yönetim arayüzü ve içe aktarma; çocuk verisi (0006); rızanın geri çekilmesi; velide 2FA;
  e-posta/biyometrik giriş; giriş audit kaydı; SMS sağlayıcı yedeği; SMS süre ölçümü; birden çok numaralı veli.

## Acceptance criteria
- [ ] AC-1 — Kurum kodu + kayıtlı telefonla kod istenince SMS (testte sahte sağlayıcıda) 6 haneli kodla gider ve SMS kodu ekranı açılır; kayıtlı olmayan numara ve bilinmeyen kurum kodu aynı yanıtı alır ve SMS gitmez; farklı yazılmış aynı numara (`0532…`, `+90 532…`) aynı veliyi bulur; geçersiz numara "Lütfen geçerli bir telefon numarası girin."
- [ ] AC-2 — Doğru kod 5 dakika içinde oturum açar; yanlış kod "Kod hatalı veya süresi geçmiş."; 5 dakikadan sonra "Kodun süresi doldu…"; kullanılmış kod, yeni kod istendikten sonra eski kod ve "Vazgeç" sonrası bekleyen kod reddedilir; "Vazgeç" Telefon ekranına alanları koruyarak döner; boşluklu kod kabul edilir.
- [ ] AC-3 — 5 yanlış kod 15 dakika kilitler, kilitliyken doğru kod reddedilir, 15 dakika sonra açılır; kayıtlı olmayan numarada 5 yanlış kod aynı kilit yanıtını alır; aynı kurum + telefon için dakikadaki 4. kod isteği ("tekrar gönder" dahil) hız sınırı mesajını alır; aynı kaynaktan dakikadaki 21. deneme reddedilir.
- [ ] AC-4 — Rızası olmayan veli doğrulamadan sonra KVKK ekranını görür; işaretsiz "Devam et" kapalıdır; rıza sonrası veli, kurum, metin sürümü ve zaman kaydedilir; rıza olmadan kurum verisi istekleri sunucuda reddedilir; rızalı veli ekranı görmez; metin sürümü değişince yeniden sorulur.
- [ ] AC-5 — Veli oturumu 0003 kurallarıyla yenilenir (sessiz yenileme, tek yenileme, web 12 sa / mobil 30 gün üst sınır, tekrar kullanılan yenileme bilgisi oturumu kapatır); çıkıştan sonraki ilk istek reddedilir.
- [ ] AC-6 — Veli oturumu personel işlemlerine, personel oturumu veli işlemlerine erişemez; iki kurumda kayıtlı aynı telefon iki ayrı veli hesabıdır ve bir kurumun oturumu diğerinin verisine erişemez.
- [ ] AC-7 — Kayıtlı velinin ilk başarılı girişi hesap oluşturur, sonraki girişler aynı hesabı kullanır; sıfırlama komutu velinin oturumlarını kapatır ve kilidini kaldırır, olmayan veli için açık hata verir, gizli veri yazdırmaz.
- [ ] AC-8 — Kod veritabanında okunamaz; kod ve tam telefon hiçbir log, hata yanıtı ve çıktıda yoktur; geliştirme/testte gerçek SMS gitmez; sağlayıcı 3 denemede de başarısızsa en geç 10 saniyede "SMS gönderilemedi. Tekrar deneyin."
- [ ] AC-9 — Web ve mobilde Giriş ekranındaki "Veli girişi" bağlantısıyla Telefon, SMS kodu ve KVKK ekranlarına ulaşılır; User experience'taki her durum kendi metniyle görünür; mobilde kurum kodu çıkıştan sonra da dolu gelir, telefon ve kod hatırlanmaz; erişilebilirlik maddeleri sağlanır.

## Definition of Done
- [ ] Every acceptance criterion mapped to proof (test or reproducible observation)
- [ ] `scripts/check` green
- [ ] Independent review done; real findings fixed, noise rejected with written rationale, deferred ones in `specs/backlog.md`
- [ ] Docs / ADRs updated if behavior or architecture changed
- [ ] Spec moved to `specs/done/` (it becomes immutable there)

## Clarify kararları (kullanıcı onayı, 2026-11-20 — hepsi önerildiği gibi)
- **K1** Kurum kodu + telefon; mobilde kurum kodu hatırlanır (0005'teki "kurum kodu + e-posta" kilit anahtarı yerine).
- **K2** Rastgele, tek kullanımlık, 5 dk geçerli kod; yeni kod eskisini iptal eder (0005'teki 30 sn pencere modeli yerine).
- **K3** Kayıtlı olan / olmayan numara aynı yanıtı alır; SMS yalnız kayıtlıya. Ödünleşim kabul: yanlış numara yazan veli SMS bekler.
- **K4** Oturum kuralları 0003'ün aynısı.
- **K5** Veli kaydı bu spec'te örnek veriden; yönetim arayüzü ve içe aktarma ayrı spec.
- **K6** Aydınlatma metni sürümü değişince rıza yeniden alınır.
- **K7** Web + mobil.
- **K8** Sıfırlama komutu bu spec'te.
- **SIZE:** 9 kriter, tek yetenek (veli girişi); bölme önerilmedi.

## Self-critique
Onaylandı (kullanıcı, 2026-11-20) ve yukarıya işlendi:
- G-1 "Vazgeç" bekleyen kodu iptal eder, alanları korur (UX, AC-2).
- G-2 "Tekrar gönder" dakikada 3 isteğe sayılır (Requirement 4, UX, AC-3).
- G-3 Kilit kayıtlı olmayan numarada da aynı görünür (Requirement 4, AC-3).
- G-4 Sağlayıcı denemeleri toplam en fazla 10 sn (Requirement 5, AC-8).
- G-5 Mobilde kurum kodu çıkışta silinmez; telefon ve kod hatırlanmaz (UX, AC-9).

## Scorecard (fill at ship — honest numbers make the process improvable)
| Metric | Value |
|---|---|
| Spec revisions | |
| Fix rounds | |
| Review findings: real / noise / deferred | |
| Regressions introduced | |
| Bugs escaped to production | |
