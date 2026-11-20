# Spec 0006 — Veli ana ekranı: çocuk listesi

- Status: Draft
- Mode: lite
- Plan: `specs/plans/0006-plan.md`
- Self-critique: done

## Intent
Giriş yapan veli (0005: SMS + KVKK rızası) bugün yalnızca "Kurum Bilgisi" ekranını görüyor; kodda öğrenci ve veli–öğrenci
bağı henüz yok. Bu spec, veli ana ekranının ilk dilimidir: veli girişten sonra ana ekrana gelir ve kendisine bağlı
çocuklarını (ad, sınıf/şube, fotoğraf) görür, birden çok çocuğu varsa birini seçer, profil menüsünden çıkış yapar.
Bunun için asgari öğrenci kaydı ve veli–öğrenci bağı oluşturulur. Kurum için bu ekran satış kancasının ilk yüzüdür
(`docs/kapsam.md`). Başarı: veli yalnızca kendi çocuklarını görür, her durum (yükleniyor, boş, hata) anlaşılırdır.
Bilinçli olarak yapılmayan: bugünkü dersler (0007), duyurular (0008), öğrenci/bağ yönetim ekranları ve içe aktarma.

## Requirements
1. KVKK rızası olan veli girişten sonra ana ekrana yönlendirilir; rızası olmayan veli önce rıza ekranını görür (0005 akışı korunur).
2. Kurumda asgari bir öğrenci kaydı bulunur: ad, soyad, sınıf/şube, isteğe bağlı fotoğraf; öğrenci bir kuruma aittir.
3. Bir veli bir veya birden çok öğrenciye bağlanabilir (kardeşler); bağ veli ile öğrencinin aynı kurumda olmasını gerektirir.
4. Bu spec'te öğrenci ve bağlar yalnızca geliştirme/demo örnek verisiyle oluşur; yönetici arayüzü ve Excel/e-Okul içe aktarma yoktur.
5. Ana ekran velinin bağlı çocuklarını ad soyad, sınıf/şube ve fotoğrafla (fotoğraf yoksa veya yüklenemezse baş harflerle) listeler; sıralama ada göre alfabetik (tr-TR).
6. Birden çok çocukta veli bir çocuğu seçebilir; seçili çocuk görsel olarak ve ekran okuyucuda belirtilir. Tek çocukta seçici gösterilmez, çocuk otomatik seçilidir. Seçili çocuk 0007/0008 bölümlerinin girdisidir.
7. Profil menüsü: veli adı (yoksa "Veli") ve maskeli telefon (yalnız son 2 hane açık, ör. `+90 5** *** ** 67`), "Ayarlar" bağlantısı (içerik bu spec'te yok), "Çıkış". Çıkış oturumu sonlandırır ve giriş ekranına döner.
8. Veli yalnızca kendi kurumundaki ve kendisine bağlı öğrencilerin verisini görür; başka velinin çocuğu veya başka kurumun verisi hiçbir yoldan görünmez, istenirse reddedilir.
9. Ekran web ve mobilde çalışır; mobil birincil kanaldır.

## User experience
- **Flow:** Veli girişi (0005) → (ilk girişte KVKK) → Ana ekran "Çocuklarım" → (çoklu çocukta) çocuk seç → profil menüsü → Çıkış → giriş ekranı.
- **States:**
  - *Yükleniyor:* çocuk listesi için iskelet; ekran donmaz.
  - *Başarılı:* çocuk kartları (ad soyad, sınıf/şube, fotoğraf/baş harfler).
  - *Tek çocuk:* seçici yok, çocuk seçili.
  - *Çoklu çocuk:* kartlardan biri seçili (varsayılan: listedeki ilk).
  - *Bağlı çocuk yok:* "Hesabınıza bağlı öğrenci bulunamadı. Lütfen okulunuzla iletişime geçin."
  - *Yükleme hatası:* "Çocuk listesi yüklenemedi." + "Tekrar dene".
  - *Oturum geçersiz / süresi doldu:* giriş ekranına döner.
- **Design reference:** https://claude.ai/artifact/Utft1dikrmxkKShhsBvRWH — sürüm `1795176735-03ab` (2026-11-20 itibarıyla
  sabitlendi; sonraki değişiklik spec değişikliğidir, R-08). Bu spec için geçerli kısımlar: başlık "Veli Paneli",
  Ayarlar/Çıkış, "Çocuklarım" kart ızgarası. "Bugünün Dersleri" 0007, "Son Duyurular" 0008 kapsamıdır.
- **Accessibility:** ekran okuyucu bölüm başlığını ve kartları okur (fotoğraf alt metni = çocuğun adı; seçili kart
  "seçili" olarak duyurulur); metin 1.5× büyütmede taşma/kesilme yok; web'de tüm öğelere klavyeyle sırayla erişilir ve
  çocuk seçimi klavyeyle yapılır; kontrast en az 4.5:1; dokunma hedefleri en az 44 px; durum metinleri duyurulur.

## Constraints & out of scope
- Sensitive area: Çocuk ve sağlık verisi (KVKK) — çocuk adı, sınıfı, fotoğrafı veliye gösterilir; fotoğraf gösterimi
  için 0005 KVKK rızası yeterli sayılır (hukuki teyit: B-18). Sağlık/alerji verisi bu spec'te yok.
- Sensitive area: Kimlik ve yetki — veli → öğrenci görünürlüğü.
- Sensitive area: Tenant izolasyonu — öğrenci ve bağ kurum verisidir.
- Split: proposed 0006 (çocuk listesi) → 0007 (ders programı + "bugünkü dersler") → 0008 (duyurular + ana ekran duyuru
  bölümü); kullanıcı onayladı, 2026-11-20 — gerekçe: üç bölümün de verisi yoktu, her biri kapsam.md Faz 1 modül 2/1/4'e oturuyor.
- 0007'ye devredilen karar: "bugün" kurumun saat dilimine göre hesaplanır; varsayılan `Europe/Istanbul` (`docs/conventions.md`).
- Out of scope: öğrenci/veli–öğrenci bağı yönetim arayüzü, Excel/e-Okul içe aktarma, sağlık notu/alerji, teslim
  alabilecek kişiler, birden çok velinin aynı çocuğa bağlanmasının yönetimi, ders/duyuru bölümleri, not/ödev,
  devamsızlık, ödeme, mesajlaşma, AI özet, Ayarlar sayfası içeriği, öğrenci rolü (Faz 2).
- Out of scope: online kurs / LMS (kapsam.md "Kapsam dışı"; eski taslak `parking/0006-education` dalında).

## Acceptance criteria
- [ ] AC-1 — KVKK rızası olan veli girişten sonra ana ekrana yönlendirilir; rızası olmayan veli önce rıza ekranını görür ve ana ekran verisine erişemez.
- [ ] AC-2 — Ana ekran velinin bağlı çocuklarını ad soyad ve sınıf/şube ile, ada göre alfabetik (tr-TR) listeler.
- [ ] AC-3 — Fotoğrafı olan çocukta fotoğraf, fotoğrafı olmayan veya fotoğrafı yüklenemeyen çocukta baş harfler gösterilir.
- [ ] AC-4 — Başka velinin çocuğu ya da başka kurumun öğrencisi listede görünmez; böyle bir öğrencinin verisi istenirse reddedilir; farklı kurumdaki veli ile öğrenci bağlanamaz.
- [ ] AC-5 — Birden çok çocukta ilk çocuk varsayılan seçilidir, veli başka bir çocuğu seçebilir ve seçili kart ekran okuyucuda "seçili" duyurulur; tek çocukta seçici yoktur.
- [ ] AC-6 — Bağlı çocuk yoksa "Hesabınıza bağlı öğrenci bulunamadı. Lütfen okulunuzla iletişime geçin." gösterilir.
- [ ] AC-7 — Liste yüklenirken iskelet, yükleme hatasında "Çocuk listesi yüklenemedi." + çalışan "Tekrar dene" gösterilir; oturum geçersizse giriş ekranına dönülür.
- [ ] AC-8 — Profil menüsünde veli adı (yoksa "Veli") ve yalnız son 2 hanesi açık telefon görünür; "Çıkış" oturumu sonlandırır (eski oturumla istek reddedilir) ve giriş ekranına döner.
- [ ] AC-9 — Web ve mobilde aynı akış; ekran okuyucu etiketleri, 1.5× metin büyütmede taşmasızlık, 44 px dokunma hedefi ve web'de klavyeyle çocuk seçimi sağlanır.

## Definition of Done
- [ ] Every acceptance criterion mapped to proof (test or reproducible observation)
- [ ] `scripts/check` green
- [ ] Independent review done; real findings fixed, noise rejected with written rationale, deferred ones in `specs/backlog.md`
- [ ] Docs / ADRs updated if behavior or architecture changed
- [ ] Spec moved to `specs/done/` (it becomes immutable there)

## Self-critique
Onaylandı (kullanıcı, 2026-11-20) ve yukarıya işlendi:
- G-1 SIZE → 0006/0007/0008 bölündü (Constraints, Split).
- G-2 bağı kim kurar → bu spec'te yalnız örnek veri; yönetim arayüzü ve içe aktarma ayrı spec (Requirement 4).
- G-3 fotoğraf KVKK → 0005 rızası yeterli, baş harf yedeği (AC-3); hukuki teyit B-18.
- G-4 "bugün" saat dilimi → kurum saat dilimi, varsayılan Europe/Istanbul; 0007'ye devredildi.
- G-5 tasarım → artifact sürüm `1795176735-03ab` ile sabitlendi.

## Scorecard (fill at ship — honest numbers make the process improvable)
| Metric | Value |
|---|---|
| Spec revisions | |
| Fix rounds | |
| Review findings: real / noise / deferred | |
| Regressions introduced | |
| Bugs escaped to production | |
