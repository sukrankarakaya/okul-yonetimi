# AGENTS.md — Project Rules

**Okul Yönetim Platformu:** küçük/orta ölçekli özel okullar ve kurs/etüt merkezleri için AI-native, multi-tenant SaaS
(.NET 10 modüler monolit + PostgreSQL · React web · React Native/Expo mobil). **Kapsamın tek kaynağı `docs/kapsam.md`**;
hedef Faz 1 (MVP). Faz 2/3 veya "Kapsam dışı" bir iş spec'ten önce işaretlenir; onaylanırsa önce `kapsam.md` güncellenir.

## Operating mode

**Mode: lite** (demo dallarında: **demo**, yukarıdaki bölüm) — bootstrap sets `lite` or `strict` (`workflows/README.md`); every workflow honors its gates.
**Start work** with `/new-feature` in lite (it chains the segments and asks at each gate) or with
`/analyze` in strict (one segment per role). Segment commands always stop (`workflows/segments.md`).
**Language: chat=tr · docs=tr** — bootstrap sets `chat=<xx> · docs=<xx>`; protocol fields stay English (`docs/conventions.md`).

## Demo modu (kullanıcı kararı 2026-11-19) — aşağıdaki kurallardan önce gelir

Bu proje gerçek bir ürün değil; amaç **1 günde tıklanabilir bir demo**. `demo/*` dallarında:
- **Tek demo spec'i** (`specs/active/demo-okul-paneli.md`) tüm demo ekranlarını kapsar; özellik başına spec/plan yok (kural 1–2 gevşer).
- **Review/VERIFY yok** (kural 3 gevşer); "bitti" = `scripts/check` fast adımları yeşil + ekran elle görüldü (kural 4 gevşer).
- **Yalnızca web.** Mobil donduruldu; mobil kodu bozulmamalı ama yeni ekran almaz.
- **Onay kapısı yok** (kural 6 gevşer): ajan önerisini yazıp uygular; kullanıcı sonucu görür.
- Hassas alan kuralları demo verisinde sadeleşir (örnek veri, gerçek ödeme/SMS yok, bildirimler simüle).
- **Kalanlar geçerli:** kural 5 (testler zayıflatılmaz), kural 7 (`specs/done/` değişmez), tenant izolasyonu (BR-1).

## Invariant rules (these survive bootstrap — never delete or weaken them)

1. **No spec, no code.** Every piece of work starts as a spec in `specs/active/` (from `specs/TEMPLATE.md`).
2. **Plan before build.** A human approves the plan before any code is written.
3. **The producer never verifies its own work.** Review and QA run in a separate session or a read-only subagent, working from files (diff + spec), never from the builder's chat.
4. **Evidence over claims.** "Done" requires `scripts/check` green and every acceptance criterion mapped to proof. Never claim completion without showing evidence.
5. **Tests are protected.** Weakening asserts, deleting or skipping tests to get to green is forbidden — always.
6. **Proposal rule.** Every question, option, or finding comes with your own recommendation and rationale. The human decides; nothing is applied without approval.
7. **Shipped specs are immutable.** Files under `specs/done/` are never edited.
8. **Uncertainty is surfaced, not assumed.** On ambiguity or a docs/code conflict: stop and use the matching recovery ramp (`prompts/recovery/`).

## Where things live

| What | Where |
|---|---|
| **Ürün kapsamı ve fazlar (her kararın referansı)** | `docs/kapsam.md` |
| Architecture & boundaries | `docs/architecture.md` |
| Domain language & business rules | `docs/domain.md` |
| Coding conventions | `docs/conventions.md` |
| Testing rules | `docs/testing.md` |
| Security rules | `docs/security.md` |
| Git & branching rules | `docs/git.md` |
| Decisions (ADRs) · roles (who may do what) | `docs/decisions/` · `docs/roles/` |
| Specs & plans | `specs/active/` · `specs/plans/` · shipped → `specs/done/` · deferred findings → `specs/backlog.md` |
| Processes & gates · prompts & recovery ramps | `workflows/` · `prompts/` |
| Every command: what it does, where it stops, lite vs strict | `docs/commands.md` |
| The single verification command | `scripts/check` |
