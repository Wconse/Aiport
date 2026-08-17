# Milestone 0.0.59 — diplomatic authority shadow gate

Дата: 2026-08-17

## Цель

Закрыть следующий safety-gate перед любыми будущими native diplomatic adapters: shadow-заявление о войне или мире может исходить только от полномочного представителя исходной фракции и адресоваться полномочному представителю целевой фракции.

## Реализовано

- Новый серверный `DiplomacyAuthorityService` вычисляет полномочия только из authoritative campaign objects.
- Для королевства полномочен только текущий `Kingdom.Leader`.
- Для независимого клана полномочен только `Clan.Leader`, если `Clan.Kingdom == null`.
- Обычный вассал, член клана, бандитская фракция, одинаковая faction pair и unsupported faction fail closed.
- Проверка выполняется дважды: при proposal и непосредственно перед записью confirmed shadow receipt.
- Если полномочия изменились после proposal, confirmation отклоняется с `stale_diplomatic_authority`.
- Новые точные причины отказа:
  - `player_faction_authority_required`;
  - `target_faction_authority_required`;
  - `stale_diplomatic_authority`.
- `/diplomacy-authority` добавлен как read-only alias `/diplomacy-snapshot`.
- Snapshot и runtime gate показывают source/target authority и итог для пары.
- Новый capability: `512`; ожидаемые cumulative flags: `1023`.

## Безопасность

- `Hero.MainHero` не используется.
- Authority не принимается от клиента и не выводится из текста модели.
- Native war/peace/relation/ownership/economy mutation APIs отсутствуют.
- Confirmed result по-прежнему создаёт только persistent shadow receipt.
- Authority повторно проверяется после proposal, поэтому смена лидера или faction pair не может использовать устаревшее подтверждение.

## Проверка

- Текущие cumulative regression suites 0.0.50–0.0.59: 8/8 PASS.
- Новый suite `tools\test_0_0_59_diplomatic_authority.py` проверяет ruler/independent-clan authority, proposal rejection, confirmation revalidation, diagnostics, capability negotiation и отсутствие native mutation APIs.
- Clean build прошёл без ошибок.

## Build / deploy

- Build: `0.0.59-dev`.
- Runtime DLL: 225280 bytes.
- SHA-256: `4c0a4976c9b9ecb59a09e6196d58b12b914b721611cd8834e654682c8990b230`.
- Bootstrap SHA-256: `8357d5c56766dabba2777d472b87b1919a1925242b6d1f8a00bf99fd653934cf`.
- Source rollback: `backups\source-20260817-003522-pre-0.0.59`.
- Deployment rollback: `backups\m0-20260817-003716`.
- Startup log: `artifacts\runtime-m0\logs\coop-server-20260817-003716.log`.
- PID: `12184`.
- Restored state: `loaded:3:social:5:diplomacy:0`, revision `8`, generation `4ea97daf7c4e8ae14149a02cff988e72`, `ReadOnly=False`.
- Server reached `SERVING`; client/server hashes match.
- Smoke process is keyless by design.

## Следующий логичный этап

Не включать native war/peace. Следующий средний milestone — recipient-side diplomatic consent / durable negotiation lifecycle с expiry и idempotent acceptance, после чего потребуется двухклиентный тест полномочий, приватности и reconnect/JIP.
