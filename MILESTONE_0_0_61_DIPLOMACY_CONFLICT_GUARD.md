# Milestone 0.0.61 — diplomacy conflict guard

Дата: 2026-08-17

## Цель

Исключить конфликтующие bilateral negotiations и гарантировать, что acceptance использует актуальный authoritative контекст обеих фракций, а не только сохранённый recipient hero ID.

## Pair lock

Для неупорядоченной пары фракций разрешено не более одного активного `pending_recipient` предложения. Пока оно активно, блокируются:

- повтор того же действия;
- противоположное действие;
- предложение в обратном направлении.

Причина отказа: `diplomacy_pair_pending`. После accept, reject или expiry pair lock освобождается.

Дополнительный предел: максимум 16 pending negotiations на одного инициатора или получателя.

## Acceptance-time revalidation

Непосредственно перед `accept` сервер повторно проверяет:

- authoritative source hero и recipient hero;
- текущие `MapFaction` обеих сторон;
- совпадение faction IDs с persisted negotiation;
- полномочия обоих лидеров;
- distinct/eligible faction pair;
- текущую war/peace precondition.

Изменившийся контекст отклоняется. Никакая native mutation после acceptance не выполняется.

## Expiry и история

- Due-записи переходят в `expired` с revision bump.
- Structured logs содержат число expiry transitions.
- `/diplomacy-history` показывает последние доступные игроку переговоры и их статусы.
- Snapshot включает read-only authority report, inbox и lifecycle history.

## Capability

- Новый bit: `2048`.
- Ожидаемые cumulative flags: `4095`.

## Проверка

- Cumulative suites 0.0.50–0.0.61: 10/10 PASS.
- Executable harness подтвердил same-pair, reverse-direction и opposite-action blocking, освобождение lock после решения и expiry, историю и lookup.
- Clean build и server startup smoke прошли.
- Native mutation APIs отсутствуют.

## Build / deploy

- Build: `0.0.61-dev`.
- Runtime DLL: 239616 bytes.
- SHA-256: `dc42dcb854b785f139f4bcb2bbf385ce2efd744d16e141c14b6c3c67fbf17089`.
- Source rollback: `backups\source-20260817-005650-pre-0.0.61`.
- Deployment rollback: `backups\m0-20260817-005744`.
- Startup log: `artifacts\runtime-m0\logs\coop-server-20260817-005745.log`.
- PID: `25208`.
- Restored state: `loaded:3:social:5:diplomacy:0`, revision `8`, generation `4ea97daf7c4e8ae14149a02cff988e72`, `ReadOnly=False`.
- Server reached `SERVING`; client/server hashes match.

## Участие пользователя

Не требуется. Двухклиентная ручная проверка остаётся частью будущего bundled gate и сейчас не блокирует разработку.
