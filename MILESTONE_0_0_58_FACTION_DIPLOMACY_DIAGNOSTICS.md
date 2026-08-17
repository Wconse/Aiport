# Milestone 0.0.58 — faction-aware diplomacy and diagnostics

Дата: 2026-08-16

## Изменения

- Diplomacy snapshot, proposal, confirmation и validation gate используют authoritative `Hero.MapFaction`, а не только `Hero.Clan.Kingdom`.
- Независимый player clan поддерживается как политическая сторона shadow statement.
- War/peace precondition вычисляется через `FactionManager.IsAtWarAgainstFaction`.
- Bandit factions, same-faction пары и изменившаяся между proposal/confirm faction pair отклоняются.
- Существующий persistence format сохранён; поля с историческими именами `SourceKingdomId/TargetKingdomId` теперь содержат authoritative faction IDs.
- Social cooldown увеличен с 5 до 15 секунд и получил явное клиентское сообщение.
- Diplomacy cooldown получил явное 30-секундное сообщение.
- Runtime gate возвращает structured comparison metadata; серверный лог теперь содержит `HasBaseline`, `SameTarget`, `NativeRelationUnchanged`, `NativeWarStateUnchanged` и все deltas.
- Исправлен post-gate checker 0.0.57.

## Проверки

Все cumulative suites 0.0.50–0.0.58 прошли. Новый suite проверяет faction authority, независимый клан, bandit/same-faction rejection, confirmation rebinding, native comparison audit, cooldown duration/UX и отсутствие native mutation APIs.

## Build/deploy

- Build: `0.0.58-dev`.
- Size: 219648 bytes.
- SHA-256: `8c4e24e6a57147ce7f2d39f39ddb84522e3580c7d918d1b318bbc1c09ae2c5c8`.
- Source rollback: `backups\source-20260816-234547-pre-0.0.58`.
- Deployment rollback: `backups\m0-20260816-235142`.
- Startup log: `artifacts\runtime-m0\logs\coop-server-20260816-235143.log`.
- PID: 22372.
- Startup: `loaded:3:social:5:diplomacy:0`, revision 8, read-only false, SERVING.
- Client/server hashes match.

Текущий smoke-server запущен без API key. Для AI-turn используется version-independent `tools\start_aiport_with_groq.cmd`.
