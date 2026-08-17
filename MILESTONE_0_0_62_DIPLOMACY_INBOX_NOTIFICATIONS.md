# Milestone 0.0.62 — diplomacy inbox notifications

Дата: 2026-08-17

## Цель

Сделать recipient consent заметным для второго игрока без ручного опроса inbox и восстановить уведомление после reconnect/JIP, не раскрывая чужое приватное состояние.

## Immediate delivery

После создания `pending_recipient` сервер ищет только текущий authoritative peer целевого героя. Привязка строится из:

- актуального connection token;
- server-observed Coop player mapping;
- authoritative `Player.HeroId`.

Если найдено несколько peer для одного hero ID, уведомление подавляется как неоднозначное.

## Reconnect / JIP

После успешного private snapshot сервер повторно отправляет уведомление, если у героя есть активные входящие предложения. Поэтому pending inbox снова становится видимым после reconnect или join-in-progress.

## Приватность

Notification содержит только:

- campaign generation;
- state revision;
- pending count;
- ID последнего доступного предложения;
- безопасный reason code.

Данные других игроков в event не включаются. Ledger считает записи только для exact recipient hero.

## Клиент

Клиент проверяет protocol, capability, generation, revision, count и canonical IDs. Повторы дедуплицируются. Сообщение выводится через application tick и не изменяет Bannerlord conversation graph или активный текст диалога.

## Capability

- Новый bit: `4096`.
- Ожидаемые cumulative flags: `8191`.

## Проверка

- Cumulative suites 0.0.50–0.0.62: 11/11 PASS.
- Executable harness подтвердил recipient-private count, latest ID, изоляцию другого героя и исчезновение resolved записи из pending count.
- Protobuf fields 1–7 уникальны.
- Clean build и disposable-server startup smoke прошли.
- Native mutation APIs отсутствуют.

## Build / deploy

- Build: `0.0.62-dev`.
- Runtime DLL: 245248 bytes.
- SHA-256: `9f1ea0aa32618bb3c709efa6d88e69ddc719451ec0745d5a196129cd3702f29f`.
- Source rollback: `backups\source-20260817-010305-pre-0.0.62`.
- Deployment rollback: `backups\m0-20260817-010345`.
- Startup log: `artifacts\runtime-m0\logs\coop-server-20260817-010346.log`.
- PID: `24432`.
- Restored state: `loaded:3:social:5:diplomacy:0`, revision `8`, generation `4ea97daf7c4e8ae14149a02cff988e72`, `ReadOnly=False`.
- Server reached `SERVING`; client/server hashes match.

## Участие пользователя

Не требуется. Реальная видимость уведомления у второго player-controlled faction leader будет проверена позже в общем двухклиентном bundled gate.
