# Demo — Okul paneli (tek günlük, tıklanabilir)

- Status: In progress
- Mode: demo (AGENTS.md, Demo modu — kullanıcı kararı 2026-11-19)

## Intent
Gerçek ürün değil: Faz 1'in satış hikâyesini bir günde gösteren, gerçek backend + veritabanı üzerinde çalışan web paneli.

## Ekranlar
1. **Panel** — bugün devamsız / yoklaması alınmamış sınıf / geciken tahsilat / bu ay tahsilat / duyuru okunma.
2. **Öğrenciler** — arama + sınıf filtresi; öğrenci kartı (veli, sağlık/alerji, teslim, son 10 gün yoklama, ödeme planı, mesajlar).
3. **Yoklama** — sınıf + tarih; herkes "Var" başlar; "Yok" → veliye bir kez bildirim (BR-5, simüle).
4. **Duyurular** — okul/sınıf hedefli gönderim, okundu sayısı.
5. **Tahsilat** — gecikmiş / yaklaşan / ödenmiş; "Tahsil et" → makbuz no (BR-2 kuruş, BR-3 gecikme).
6. **Veli mesajı (AI)** — öğretmen notu → taslak (Claude Opus 5.5, anahtar yoksa şablon) → düzenle → onayla ve gönder (BR-9).

## Sınırlar
- Yalnızca web; mobil donduruldu. Ödeme, SMS, push yok — simüle.
- Örnek veri yalnızca Development'ta (`SchoolSampleData`): Atatürk Koleji 6 sınıf / 96 öğrenci, Bilge Etüt 1 sınıf / 8 öğrenci.
- Korunan: tenant izolasyonu (BR-1, mimari testler), mevcut testler zayıflatılmadı.

## Çalıştırma
1. PostgreSQL açık; `dotnet user-secrets` içinde `ConnectionStrings:Okul`, `Auth:SigningKey`, `Dev:SamplePassword`.
2. `dotnet run --project backend/src/WebApi/Okul.WebApi` (http://localhost:5080).
3. `npm run -w @okul/web dev` (http://localhost:5173) — port doluysa `npx vite --port 5174` (CORS'ta izinli).
4. Giriş: kurum kodu `ATATURK-IST`, e-posta `yonetici@ataturk.test`, şifre = `Dev:SamplePassword`.
5. Gerçek AI taslağı için: `ANTHROPIC_API_KEY` ortam değişkeni veya `dotnet user-secrets set Anthropic:ApiKey …`.
