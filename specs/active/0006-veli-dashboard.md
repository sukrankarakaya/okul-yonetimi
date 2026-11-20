# Spec 0006 — Veli ana ekranı (dashboard)

- Status: Draft
- Mode: lite
- Plan: `specs/plans/0006-plan.md`
- Self-critique: pending (SIZE/bağımlılık kararı ve açık sorular — aşağıda "Self-critique")

## Intent
Giriş yapan veli (0005: SMS + KVKK rızası) bugün yalnızca "Kurum Bilgisi" ekranını görüyor. Veli, uygulamayı açtığında
çocuklarını ve günün önemli bilgilerini tek bakışta görmek istiyor: çocukları, bugünkü dersleri ve okuldan son
duyurular. Kurum için bu ekran satış kancasıdır ("velinin bayılacağı mobil deneyim", `docs/kapsam.md`). Başarı: veli
girişten sonra doğrudan ana ekrana gelir, yalnızca kendi çocuklarını ve onlara ait bilgileri görür, her bölümün boş /
hata durumu anlaşılırdır. Bilinçli olarak yapılmayan: not/ödev özeti, ödeme, mesajlaşma, AI haftalık özet, duyuru
okundu bilgisi — bunlar kendi modülleriyle gelir.

## Requirements
1. Veli girişi (0005) tamamlanınca veli ana ekrana yönlendirilir; KVKK rızası olmayan veli ana ekranı göremez (önce rıza).
2. Ana ekran velinin kurumdaki çocuklarını listeler: ad soyad, sınıf/şube, fotoğraf (yoksa baş harfler).
3. Kardeşi olan veli tüm çocuklarını tek hesapta görür; bir çocuk seçilince günlük bölümler o çocuğa göre gösterilir.
4. Seçili çocuğun bugünkü dersleri ders programındaki sırasıyla gösterilir (saat + ders adı), en fazla 5 ders;
   fazlası varsa "tümünü gör" bilgisi.
5. Duyurular bölümü veliye yönelik son 3 duyuruyu gösterir (tarih + başlık), en yeni üstte. Hangi duyuruların veliye
   yönelik sayıldığı (okul / sınıf / öğrenci bazında) kapsam.md modül 4'e göre belirlenir.
6. Profil menüsü: veli adı/telefonu (maskeli), "Ayarlar" bağlantısı, "Çıkış". Çıkış oturumu sonlandırır ve girişe döner.
7. Veli yalnızca kendi kurumundaki ve kendisine bağlı çocukların verisini görür; başka velinin çocuğu, başka kurumun
   verisi hiçbir yoldan görünmez.
8. Ekran web ve mobilde çalışır; mobil birincil kanaldır.

## User experience
- **Flow:** Veli girişi (0005) → (ilk girişte KVKK) → Ana ekran → çocuk seç → bugünkü dersler / duyurular → profil menüsü → çıkış.
- **States:**
  - *Yükleniyor:* her bölüm için iskelet; ekran donmaz.
  - *Başarılı:* çocuk kartları + seçili çocuğun bugünkü dersleri + son duyurular.
  - *Tek çocuk:* seçici gösterilmez, çocuk otomatik seçilidir.
  - *Bağlı çocuk yok:* "Hesabınıza bağlı öğrenci bulunamadı. Lütfen okulunuzla iletişime geçin."
  - *Bugün ders yok:* "Bugün ders yok." (hafta sonu / tatil dahil)
  - *Duyuru yok:* "Henüz duyuru yok."
  - *Bölüm hatası:* yalnızca o bölüm "Yüklenemedi. Tekrar dene." gösterir; diğer bölümler çalışır.
  - *Oturum süresi doldu / yetkisiz:* giriş ekranına döner.
- **Design reference:** aday — 0006 ilk taslak commit'inde (`fbc44b7`) geçen
  https://claude.ai/artifact/Utft1dikrmxkKShhsBvRWH ("sarı tonlar, dark mode"). Onaylanacak sabit sürüm/tarihli export
  bekleniyor; yoksa "none — the flow above is the design" + 0003/0005 görsel dili.
- **Accessibility:** ekran okuyucu bölüm başlıklarını ve çocuk kartlarını okur (fotoğrafa alt metin = çocuğun adı);
  metin 1.5× büyütmede taşma/kesilme yok; klavye ile (web) tüm öğelere sırayla erişilir; kontrast en az 4.5:1;
  dokunma hedefleri en az 44 px; durum metinleri duyurulur.

## Constraints & out of scope
- Sensitive area: Çocuk ve sağlık verisi (KVKK) — çocuk adı, sınıfı, fotoğrafı veliye gösterilir.
- Sensitive area: Kimlik ve yetki — veli → çocuk görünürlüğü.
- Sensitive area: Tenant izolasyonu — kurum verisi ayrımı.
- **Bağımlılık (açık):** kodda öğrenci kaydı, veli–öğrenci bağı, sınıf/ders programı ve duyuru verisi henüz yok
  (yalnızca Identity, Institution, Parents modülleri var; `Parent` çocuk bilgisi taşımıyor). Bu verinin hangi spec'te
  oluşacağı Self-critique G-1'de karara bağlanacak.
- Out of scope: not/ödev özeti, devamsızlık, ödeme/borç, mesajlaşma, randevu, AI haftalık özet, duyuru detay sayfası ve
  okundu bilgisi, push bildirim, veli ayarlar sayfasının içeriği (yalnız bağlantı), öğrenci rolü (Faz 2).
- Out of scope: online kurs / LMS (kapsam.md "Kapsam dışı"; eski 0006 taslağı `parking/0006-education` dalında).

## Acceptance criteria
- [ ] AC-1 — KVKK rızası olan veli girişten sonra ana ekrana yönlendirilir; rızası olmayan veli önce rıza ekranını görür, ana ekranı göremez.
- [ ] AC-2 — Ana ekran velinin kendi kurumundaki bağlı çocuklarını ad, sınıf/şube ve fotoğraf (yoksa baş harfler) ile listeler.
- [ ] AC-3 — Başka velinin çocuğu ya da başka kurumun verisi ana ekranda ve ana ekranın veri isteklerinde görünmez (yetkisiz istek reddedilir).
- [ ] AC-4 — Birden çok çocuğu olan veli çocuk seçebilir; bölümler seçili çocuğa göre değişir. Tek çocukta seçici yoktur.
- [ ] AC-5 — Seçili çocuğun bugünkü dersleri sırayla (saat + ders) en fazla 5 adet gösterilir; 5'ten fazlaysa fazlalık belirtilir; ders yoksa "Bugün ders yok."
- [ ] AC-6 — Veliye yönelik son 3 duyuru (tarih + başlık) en yeni üstte gösterilir; duyuru yoksa "Henüz duyuru yok."
- [ ] AC-7 — Bağlı çocuk yoksa "Hesabınıza bağlı öğrenci bulunamadı…" mesajı gösterilir.
- [ ] AC-8 — Bir bölümün verisi yüklenemezse yalnız o bölüm hata + "Tekrar dene" gösterir; oturum geçersizse giriş ekranına dönülür.
- [ ] AC-9 — Profil menüsünden çıkış oturumu sonlandırır ve giriş ekranına döner; telefon maskeli gösterilir.
- [ ] AC-10 — Web ve mobilde aynı akış; ekran okuyucu etiketleri, 1.5× metin büyütme, 44 px dokunma hedefi ve (web) klavye erişimi sağlanır.

## Definition of Done
- [ ] Every acceptance criterion mapped to proof (test or reproducible observation)
- [ ] `scripts/check` green
- [ ] Independent review done; real findings fixed, noise rejected with written rationale, deferred ones in `specs/backlog.md`
- [ ] Docs / ADRs updated if behavior or architecture changed
- [ ] Spec moved to `specs/done/` (it becomes immutable there)

## Self-critique
<!-- Onay bekliyor; onaylananlar yukarıya işlenip "Self-critique: done" yapılır. -->
- **G-1 (SIZE + bağımlılık):** Spec 3 yetenek (çocuk listesi, bugünkü dersler, duyurular) ve 10 AC içeriyor; üçünün de
  veri kaynağı yok. *Öneri:* böl —
  **0006** öğrenci kartı (asgari) + veli–öğrenci bağı + ana ekran iskeleti ve çocuk listesi (AC-1–4, 7–10);
  **0007** sınıf/ders programı + "bugünkü dersler" bölümü (AC-5);
  **0008** duyuru (okul/sınıf/öğrenci) + ana ekran duyuru bölümü (AC-6).
  Gerekçe: her biri kendi başına test edilebilir ve kapsam.md Faz 1 modüllerine (2, 1, 4) birebir oturuyor; 0006
  tek başına bile veliye değer verir.
- **G-2:** Veli–öğrenci bağını kim kurar? *Öneri:* 0006'da örnek veri (seed) + yönetici tarafı sonraki spec'e; Excel/e-Okul içe aktarma ayrı spec.
- **G-3:** Fotoğraf KVKK: fotoğraf gösterimi ayrı rıza gerektiriyor mu? *Öneri:* 0005 rızasını yeterli say, fotoğraf yoksa baş harfler; hukuki teyit backlog'a.
- **G-4:** "Bugün" hangi saat dilimine göre? *Öneri:* kurumun saat dilimi (varsayılan Europe/Istanbul).
- **G-5:** Tasarım referansı: aday artifact sabit sürüm olarak onaylanacak mı? *Öneri:* evet, tarihli export ile.

## Scorecard (fill at ship — honest numbers make the process improvable)
| Metric | Value |
|---|---|
| Spec revisions | |
| Fix rounds | |
| Review findings: real / noise / deferred | |
| Regressions introduced | |
| Bugs escaped to production | |
