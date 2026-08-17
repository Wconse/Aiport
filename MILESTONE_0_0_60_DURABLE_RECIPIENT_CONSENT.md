# Milestone 0.0.60 — durable recipient consent

Дата: 2026-08-17

## Итог

Дипломатическое shadow-предложение после подтверждения инициатором больше не считается односторонне завершённым. Оно сохраняется как `pending_recipient` и требует отдельного решения authoritative целевого лидера.

## Lifecycle

```text
proposal -> source confirmation -> pending_recipient
pending_recipient -> accepted_shadow
pending_recipient -> rejected_shadow
pending_recipient -> expired
```

- Срок жизни: 24 часа UTC.
- Получатель привязан к `TargetHeroId` полномочного лидера, определённого сервером.
- Принять или отклонить запись может только Coop peer, authoritative hero которого совпадает с получателем.
- Повтор того же решения идемпотентен, включая повтор после process restart.
- Попытка изменить уже принятое решение отклоняется.
- Запись содержит status, expiry, resolution UTC и resolving hero.

## Команды

```text
/diplomacy-inbox
/diplomacy-accept <statement-id>
/diplomacy-reject <statement-id>
```

`/diplomacy-inbox` показывает только активные входящие предложения текущего authoritative героя. Исходные и входящие записи входят только в соответствующие private snapshots.

## Совместимость persistence

Существующие записи старого формата загружаются как `legacy_shadow_recorded`. Новые lifecycle-поля являются backward-compatible optional fields внутри `diplomacy.ndjson`; SHA-256 manifest и fail-closed integrity остаются обязательными.

## Безопасность

- Нет доверия к client-supplied player/recipient identity.
- Нет `Hero.MainHero`.
- Generation и revision проверяются сервером.
- Opposite replay, wrong recipient и expired proposal fail closed.
- Никакие native war/peace/relation/ownership/economy mutations не выполняются.

## Проверка

- Cumulative suites 0.0.50–0.0.60: 9/9 PASS.
- Новый executable harness проверил pending record, recipient binding, accept, replay, opposite decision rejection, expiry, import/restart idempotency и recipient-private projection.
- Clean build и disposable-server startup smoke прошли.

## Build / deploy

- Build: `0.0.60-dev`.
- Runtime DLL: 236032 bytes.
- SHA-256: `f5d1ecaf18c7391c0a5f38e2062e726d478bca6ab5c140d8e33daffa371ce11c`.
- Source rollback: `backups\source-20260817-004530-pre-0.0.60`.
- Deployment rollback: `backups\m0-20260817-004804`.
- Startup log: `artifacts\runtime-m0\logs\coop-server-20260817-004805.log`.
- PID: `2436`.
- Restored: `loaded:3:social:5:diplomacy:0`, revision `8`, generation `4ea97daf7c4e8ae14149a02cff988e72`, `ReadOnly=False`.
- Server reached `SERVING`; client/server hashes match.

## Участие пользователя

Сейчас не требуется. Реальный двухклиентный acceptance test будет нужен только в составе следующего крупного bundled gate, когда появится второй player-controlled faction leader.
