# ADR 0002 — Veli girişi Kayıt (Enrollment) modülünde; veli oturumunu Kimlik açar

- Status: Accepted
- Date: 2026-11-20

## Context
`docs/architecture.md` veli kaydını Kayıt (`Enrollment`) modülüne, oturumları Kimlik (`Identity`) modülüne, açık rıza
kayıtlarını Uyum (`Compliance`) modülüne verir. Spec 0005 ise ayrı bir `Parents` modülü açtı (şema `parent`). Bu modül
mimari tabloda yok, uçtan uca bağlı değil ve kuralları spec'le çelişiyor (tek hatada kilit, kayıtsız numaraya hesap,
30 sn pencere modeli; tuval F0005-Analiz, B-17). Yalnızca geliştirme verisi var. Spec 0007 veli girişini tamamlıyor;
spec 0006 öğrenciyi ve veli–öğrenci bağını getirecek. İkisi de "veli" kavramına dayanıyor. Kimlik modülünde de
oturum yenileme her oturumu personel hesabı sayıyor, yani bir veli oturumu ilk yenilemede kapanırdı.

## Decision
`Parents` modülü kaldırılır ve yerine Kayıt modülü (`Okul.Modules.Enrollment.*`, şema `kayit`) kurulur. Kayıtlı veli,
veli giriş kodu, giriş kilidi ve (Faz 1'de) KVKK rıza kaydı bu modüldedir. Kayıt modülü kodu doğrulayınca oturumu
Kimlik modülü açar (`Identity.Contracts.IGuardianSessions`). Oturum kaydı hesap türünü taşır (personel / veli); Kimlik
bir veli oturumunu yenilerken veliyi Kayıt'a sorar (`Enrollment.Contracts.IGuardianDirectory`). Modüller birbirini
yalnızca contracts üzerinden kullanır.

## Consequences
Kazançlar:
- Mimari tabloyla uyum: 0006'daki öğrenci ve veli–öğrenci bağı aynı modülde kalır, sınır aşan bir bağ gerekmez.
- Oturum kuralları (yenileme, üst sınır, çıkış, çalınma işareti) tek yerde kalır; veli için kopyası yazılmaz.
- 0005'in zayıf kodu ve testleri spec 0007'nin kriterleriyle yeniden yazılır; şimdi taşımak ucuz (yalnız dev verisi).
Bedeller:
- `parent` şeması ve `Parent_Initial` migration'ı kalkar; geliştirme veritabanları yeniden oluşturulur.
- 0005'in 30 birim testi silinip yerine 0007'nin testleri gelir (plan 0007, R-4 — kullanıcı onayı 2026-11-20).
- Kimlik ↔ Kayıt contracts düzeyinde iki yönlü bilir (Kayıt → `IGuardianSessions`, Kimlik → `IGuardianDirectory`);
  assembly döngüsü yoktur, mimari testleri bunu doğrular.
- Rıza kaydı Faz 1'de Uyum yerine Kayıt'ta tutulur.

## Alternatives considered
- **`Parents` modülünü tutmak:** en az değişiklik; ama mimari tabloda olmayan bir modül kalır ve 0006'daki veli–öğrenci
  bağı Parents ile Kayıt arasında modül sınırını aşar. Kaybetti.
- **Veli girişini Kimlik modülüne koymak:** oturumla aynı yerde olurdu; ama kayıtlı veli listesi (telefon) Kayıt verisidir,
  Kimlik'in başka modülün tablosunu okuması yasak (mimari kural 3). Kaybetti.
- **Rıza kaydını şimdi Uyum modülünde açmak:** mimariye tam uyum; ama Faz 1'de tek kullanıcısı veli girişi olan bir modül
  için ek proje, şema ve contract. Ertelendi (aşağıda tetikleyici).

## Revisit triggers
- Uyum modülü (audit log, saklama/anonimleştirme) ilk spec'ini aldığında: rıza kaydı oraya taşınır.
- Veli ve personel olan aynı kişi için tek hesap istendiğinde: hesap modeli yeniden ele alınır.
