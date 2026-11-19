# Devir notu — Windows → Mac (2026-11-19)

Projeyi Mac'e taşımak ve kaldığın yerden devam etmek için gerekenler. Bu dosya bir **anlık durum notudur**;
kurallar yine `AGENTS.md`'de, ürün kapsamı `docs/kapsam.md`'de. İş bittikçe eskir — Mac'te ilk iş tamamlanınca
kalıcı kısımlar (`docs/testing.md` macOS kurulumu) bir `/change` ile yerine taşınmalı, bu dosya silinmeli.

## 1. Kaldığın yer

| Spec | Durum | Sıradaki adım |
|---|---|---|
| 0001 walking skeleton | **Shipped** (`main`) | — |
| 0002 yerel PostgreSQL | **Shipped** (`main`, `287c287`) — VERIFY 11/11 PASS | — |
| 0003 personel girişi | dal `feature/0003-personel-girisi`; BUILD + fix-1…fix-3 bitti, `main` merge'lendi (A-4 onaylı), **review turu 3 temiz** (0 bulgu) | **Triage kararı senin** → VERIFY → SHIP |

0003 için Mac'te Claude'a şunu söylemen yeterli:

> "0003 review turu 3 temiz, triage'ı onaylıyorum (`scripts/gate triage 0003 --review`); sonra VERIFY'ı bağımsız bir QA subagent'ıyla yap."

Son kanıt: `CHECK GREEN (full, 12 steps, 210 s) @7a354dc` (fast 132, entegrasyon 68) — plan `specs/plans/0003-plan.md`, Build evidence.

Sonraki işler (sıra önerisi): 0003 SHIP → spec 0004 (2FA) → 0005 (veli SMS girişi + KVKK). Açık backlog: `specs/backlog.md`
(B-9…B-13 dahil); B-1'in "remote yok" kısmı bu devirle değişti — remote var, push başladı; trivial politikası (1)'e ve
CI'ya geçiş ayrı bir karar (öneri: Mac'te ilk iş, bir trivial/`/change`).

## 2. Mac'te kurulum (bir kez)

Projeyi **iCloud ile senkronlanan klasörlere koyma** (Masaüstü/Belgeler) — Windows'taki OneDrive notuyla aynı sebep
(`bin/`, `obj/`, `node_modules/` senkronu). Öneri: `~/dev/okul-yonetimi`.

1. **Araçlar** (Homebrew ile):
   ```sh
   brew install git gh node@24 postgresql@18
   brew install --cask dotnet-sdk        # global.json: SDK 10.0.404, rollForward latestFeature
   brew install --cask docker            # isteğe bağlı — yalnızca OKUL_TEST_PG tanımsızken (konteyner yolu)
   gh auth login                         # GitHub hesabın
   ```
   Node sürümü `.nvmrc`'de (24+). `node@24` keg-only ise brew'un söylediği PATH satırını `~/.zshrc`'ye ekle.
2. **Repo:**
   ```sh
   mkdir -p ~/dev && cd ~/dev
   gh repo clone sukrankarakaya/okul-yonetimi
   cd okul-yonetimi
   git fetch --all && git checkout feature/0003-personel-girisi
   ```
3. **Script izinleri:** `scripts/` dosyaları repoda çalıştırılabilir bit'i olmadan kayıtlı (Windows'ta commit'lendi).
   Kalıcı düzeltme ayrı bir küçük değişiklik (öneri: `git update-index --chmod=+x scripts/*` + commit, trivial dal).
   O gelene kadar:
   ```sh
   chmod +x scripts/check scripts/doctor scripts/gate scripts/init scripts/residue scripts/selftest scripts/status
   git config core.fileMode false   # yerel izin farkı commit'e girmesin — kalıcı düzeltmeden sonra geri al
   ```
4. **PostgreSQL 18 + test rolü** (parola hiçbir komut satırına yazılmaz):
   ```sh
   brew services start postgresql@18
   echo 'export PATH="$(brew --prefix postgresql@18)/bin:$PATH"' >> ~/.zshrc && exec zsh
   createuser --login --createdb --pwprompt okul_test    # Homebrew'da süper kullanıcı senin macOS kullanıcın
   ```
   pgpass (yalnızca sana açık olmalı, yoksa libpq dosyayı yok sayar):
   ```sh
   touch ~/.pgpass && chmod 600 ~/.pgpass && open -e ~/.pgpass
   # tek satır:  localhost:5432:*:okul_test:<parola>
   ```
   Değişken:
   ```sh
   echo 'export OKUL_TEST_PG="postgresql://okul_test@localhost:5432/postgres"' >> ~/.zshrc && exec zsh
   ```
5. **Doğrula:**
   ```sh
   psql "$OKUL_TEST_PG" -w -Atc "select rolsuper, rolcreatedb from pg_roles where rolname = current_user"   # f|t
   psql "$OKUL_TEST_PG" -w -Atc "select collname from pg_collation where collname = 'tr-TR-x-icu'"          # bir satır
   psql "$OKUL_TEST_PG" -w -Atc "show server_encoding"                                                      # UTF8
   ```
   Collation satırı gelmezse sunucu ICU'suz derlenmiş demektir — testler bilerek açık bir mesajla düşer (CB-4).
   O durumda dur ve Claude'a söyle (R-13); EDB'nin macOS yükleyicisi ICU içerir.
6. **Bağımlılıklar ve ilk koşu:**
   ```sh
   npm ci
   ./scripts/doctor          # profile fast + full: runnable, 0 fail
   ./scripts/check --fast    # hızlı tur
   ./scripts/check           # full — tek kanıt; Mac'teki ilk full green'i 0003 planına yazdır
   ```
   Mac farklı bir makine: BUILD/VERIFY girişinde doctor'ın sonucu plandaki Risks'ten farklıysa R-13 (beklenen;
   Claude bunu not eder).

## 3. Claude Code'un bu makinede kalan notları

Claude'un hafızası (`~/.claude/projects/…/memory/`) makineye özeldir ve Git'le taşınmaz. Mac'te ilk oturumda
Claude'a **"docs/devir.md'yi oku, 3. bölümdeki notları hafızana kaydet"** de. Notlar:

- **Durum notları:** her adım değişiminde ve uzun işlerde kısa bir "Durum notu" ver (ANEW aşaması, ne yapıldı, ne koşuyor,
  sırada ne var). Test/check koşularını kısa tut: hedefli test ya da `--fast` ile ilerle, full'ü yalnızca segment
  kapanışında koş; uzun tekrar döngülerinden kaçın, gerekiyorsa süresini söyle.
- **Feature analiz tuvali:** her spec'in analizi Claude Design tuvaline ("Okul Yönetimi — Feature Analizleri",
  https://claude.ai/artifact/QHYBYCt3Rf3gTDjxC12ydp) eklenir ve link kullanıcıya iletilir: `FNNNN-Analiz.dc.html`
  (niyet, gereksinimler, hassas alanlar, AC ↔ test, kararlar, kapsam dışı) ve `FNNNN-Ekranlar.dc.html` (web + mobil
  durum maketleri); `Main.dc.html`'deki feature haritası kartının aşama şeridi (ANALYZE…SHIP) gate'ler geçtikçe güncellenir.
  Tuval bir görünümdür, spec değildir.
- **Bağımsızlık:** REVIEW ve VERIFY'ı builder'ın oturumu değil, read-only bir subagent (ya da yeni oturum) dosyalardan yapar
  (AGENTS.md kural 3); kullanıcı aynı oturumda devam etmek istediğinde bu yol kullanıldı.
- **Docker:** bu projede entegrasyon testleri artık yerel PG 18'de (spec 0002); Docker yalnızca `OKUL_TEST_PG`
  tanımsızken. Windows'ta Docker Desktop penceresini kapatmak motoru durdurmuyordu (`docker desktop stop` gerekiyordu).
  Konteynerleştirilmiş dağıtım ayrı bir infra kararı (B-4 ile).
- **Kullanıcı tercihi:** sistem komutlarında (servis durdurma vb.) adım adım, hangi pencerede yazılacağıyla birlikte tarif et.
- **Süreç dersleri (bu oturumdan):** doküman commit'leri de check'e bağlanır — check'i commit'ten **önce** koş;
  plan triage başlıkları `scripts/gate triage` biçiminde olmalı (`### VERIFY round <n> — …`), yoksa `scripts/status`
  yanlış aşama gösterir; fix notu bir `fix-k` ya da `[plan N/fix-…]` etiketli commit'in SHA'sını adlandırmalı.

## 4. Windows'ta geride kalanlar

- Geçici VERIFY klonu (scratchpad, `verify-0002`) silinemedi — zararsız, istersen sil.
- Windows'taki PG 18 servisi ve `OKUL_TEST_PG` / pgpass orada kalır; Mac'e bir şey taşınmaz (veritabanları her koşuda
  sıfırdan açılır, kalıcı veri yok).
