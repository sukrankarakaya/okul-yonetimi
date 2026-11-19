# Spec 0001 — Walking skeleton: kurum bilgisi uçtan uca
<!-- Small behavior changes use TEMPLATE-mini.md — brevity is sanctioned, discipline is not. -->

- Status: Shipped
- Mode: lite
- Plan: `specs/plans/0001-plan.md`
- Self-critique: done

## Intent
Geliştirme ekibi, Faz 1 modüllerinin (`docs/kapsam.md`) üzerine inşa edileceği çalışan bir iskelet
istiyor: bir kurumun kullanıcısı, kendi kurumunun adını ve şubelerini web panelinde ve mobil
uygulamada görür; başka bir kurumun bilgisini hiçbir yoldan göremez. Başarı: bu dar senaryo uçtan uca
otomatik testlerle kanıtlanır, tenant izolasyonu ilk günden test altındadır ve `scripts/check` gerçek
adımlarla yeşil döner. Bilerek yapılmayanlar: gerçek giriş akışı (şifre, OTP, 2FA), kurum/şube oluşturma
ve düzenleme ekranları, logo ve markalama, diğer modüller.

## Requirements
1. Kimliği doğrulanmış bir kullanıcı, ait olduğu kurumun adını ve o kurumun şubelerinin adlarını görür.
2. Hangi kurumun bilgisinin gösterileceği yalnızca oturumdaki kullanıcıdan belirlenir; istemcinin
   kurum seçmesine veya başka bir kurum belirtmesine izin verilmez (BR-1).
3. Başka bir kuruma ait bir kayda (ör. şube) doğrudan erişim denemesi "bulunamadı" sonucu verir;
   kaydın var olup olmadığı ele verilmez.
4. Faz 1'in dört rolünden (Kurucu/Yönetici, Öğretmen, Veli, Muhasebe/İdari) herhangi birine sahip,
   o kuruma ait her kullanıcı bu bilgiyi görebilir.
5. Oturumu olmayan istek hiçbir kurum verisi almaz.
6. Bu iskelette oturum, **yalnızca geliştirme ve test ortamlarında** var olan bir test girişiyle açılır;
   üretim ortamında bu test girişi mevcut değildir. Gerçek giriş akışı sonraki bir spec'in konusudur.
7. Şubeler ada göre Türkçe alfabetik sırayla listelenir (ç, ğ, ı, İ, ö, ş, ü doğru yerde — T-1).
8. Aynı bilgi ve aynı ekran durumları hem web panelinde hem mobil uygulamada sunulur.
9. Mimari sınırlar (`docs/architecture.md`, Forbidden dependencies) otomatik denetlenir; bir ihlal
   doğrulamayı (`scripts/check`) kırmızıya düşürür.

## User experience
- **Flow:** Kullanıcı (test girişiyle oturum açmış) web panelini veya mobil uygulamayı açar →
  "Kurum bilgisi" ekranı açılır → kurum adı başlıkta, altında şube listesi görünür.
- **States (web ve mobil, aynı):**
  - *Yükleniyor:* bilgi gelene kadar yükleniyor göstergesi.
  - *Başarılı:* kurum adı + Türkçe alfabetik şube listesi.
  - *Boş:* kurumun şubesi yok → kurum adı + "Henüz şube tanımlanmamış."
  - *Hata:* "Bilgiler alınamadı, tekrar deneyin." + "Tekrar dene" düğmesi; düğme isteği yeniler.
  - *Oturum yok:* "Oturum açmanız gerekiyor." — hiçbir kurum verisi gösterilmez. Süresi dolmuş oturum da bu durumdur.
  - Bağlantı yoksa (mobil) *Hata* durumu gösterilir; ayrı bir çevrimdışı ekranı yoktur.
- **Design reference:** none — the flow above is the design.
- **Accessibility:** kurum adı ekranın başlığıdır (web: sayfa başlığı/heading; mobil: erişilebilirlik
  başlığı); şube listesi liste olarak duyurulur; "Tekrar dene" düğmesi etiketlidir ve web'de klavyeyle
  (Tab + Enter) çalışır; durum metinleri ekran okuyucuya duyurulur.

## Constraints & out of scope
- Sensitive area: Tenant izolasyonu
- Sensitive area: Kimlik ve yetki (yalnızca test girişinin üretimde bulunmaması bakımından)
- Bu spec'te her kullanıcı tek bir kuruma aittir; birden fazla kurum üyeliği kapsamda değildir (G1).
- Şubeler sayfalamasız, tamamı listelenir; en fazla 50 şubeyle sorunsuz çalışır (G2).
- Kurum ve şube adları her zaman düz metin olarak gösterilir, işaretleme olarak yorumlanmaz (G6).
- Testler iki örnek kurumla çalışır (biri şubeli, biri şubesiz); örnek kurumlar yalnızca test/geliştirme
  ortamında oluşur.
- Mobil ekran durumları bileşen testleriyle kanıtlanır; SHIP ekran görüntüleri ajanca Expo web
  çıktısından alınır. Gerçek cihaz kontrolü isteğe bağlıdır (insan).
- **Out of scope:** gerçek giriş (şifre, SMS OTP, 2FA, oturum yenileme) → sonraki spec; kurum ve şube
  oluşturma/düzenleme/silme ekranları; logo, markalı açılış; push bildirim; diğer 11 modül; sınıf,
  dönem, ders programı; audit log (kurum bilgisi kişisel veri değildir — G5); çevrimdışı mod; çok şubeli konsolide görünüm (Faz 3).

## Acceptance criteria
- [x] AC-1 — Şubeli örnek kurumun kullanıcısı, dört rolün her biriyle, kendi kurumunun adını ve tüm şube adlarını alır.
- [x] AC-2 — A kurumunun kullanıcısı yalnızca A'nın verisini alır; B kurumunun adı veya şubelerinden hiçbiri sonuçta yer almaz.
- [x] AC-3 — İstemci istekte başka bir kurum belirtse bile (ör. B'nin kimliği) sonuç oturumdaki kullanıcının kurumudur; B'nin verisi dönmez.
- [x] AC-4 — A kurumunun kullanıcısı B kurumunun bir şubesine doğrudan eriştiğinde "bulunamadı" alır; aynı yanıt var olmayan bir şube için de aynıdır.
- [x] AC-5 — Oturumu olmayan veya süresi dolmuş oturumlu istek reddedilir ve hiçbir kurum verisi içermez.
- [x] AC-6 — Üretim ortamında test girişi mevcut değildir: test girişi denemesi oturum açmaz.
- [x] AC-7 — Şubeler Türkçe alfabetik sırayla döner (ör. "Çankaya", "Ihlamur", "İzmir", "Şişli", "Üsküdar" doğru sırada).
- [x] AC-8 — Web: yükleniyor, başarılı, boş, hata (+ "Tekrar dene" isteği yeniler) ve oturum yok (süresi dolmuş oturum dahil) durumları User experience'taki metinlerle görünür; özel karakterli ad (`&`, `<`) düz metin olarak görünür; erişilebilirlik maddeleri sağlanır.
- [x] AC-9 — Mobil: AC-8'deki beş durum, özel karakter davranışı ve erişilebilirlik maddeleri mobil uygulamada aynı şekilde sağlanır; bağlantı yokken hata durumu gösterilir ve "Tekrar dene" çalışır.
- [x] AC-10 — Bir mimari kuralın kasıtlı ihlali (ör. bir modülün başka modülün Domain katmanına referansı) `scripts/check`'i kırmızıya düşürür.

## Definition of Done
- [x] Every acceptance criterion mapped to proof (test or reproducible observation)
- [x] `scripts/check` green
- [x] Independent review done; real findings fixed, noise rejected with written rationale, deferred ones in `specs/backlog.md`
- [x] Docs / ADRs updated if behavior or architecture changed
- [x] Spec moved to `specs/done/` (it becomes immutable there)

## Scorecard (fill at ship — honest numbers make the process improvable)
| Metric | Value |
|---|---|
| Spec revisions | 0 kayıtlı (onaydan sonra revizyon kaydı yok; plan 4 amendment: A-1…A-4) |
| Fix rounds | 3 (review 4 tur; tur 4 fix turu açmadı) |
| Review findings: real / noise / deferred | 19 / 0 / 3 (review tur 1–4; N-1 VERIFY'da gözlendi, testi B-6) · VERIFY: 4 bulgu → B-3 genişletildi, B-7, B-8 |
| Regressions introduced | 1 (fix-1: L-3 düzeltmesi güvenlik başlıklarını exception yolunda kaybediyordu → tur 2 F-1, T-12) |
| Bugs escaped to production | 0 (henüz üretim yok) |
