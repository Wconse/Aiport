# Milestone 0.0.70 — bundled diplomacy lifecycle and default-off native war

Дата: 2026-08-17

## Результат

В одной крупной итерации завершён bilateral shadow lifecycle и добавлен изолированный native war adapter, который установлен, но выключен.

## Полный lifecycle

```text
proposal
→ source confirmation
→ pending_recipient
→ accepted_shadow / rejected_shadow / withdrawn_shadow / expired
→ optional explicit native-war preflight
→ optional explicit committed_native_war
```

Добавлено:

- `/diplomacy-withdraw <id>` для exact source hero;
- durable и идемпотентный withdrawal;
- освобождение pair lock после withdrawal;
- lifecycle notifications обеим онлайн-сторонам;
- reconnect/JIP recovery через private snapshot и history;
- durable final reason, native commit UTC, committing hero и mutation audit bit;
- lifecycle/native outcome в history и runtime gate.

## Native war adapter

В кодовой базе существует ровно один native mutation call:

```text
DeclareWarAction.ApplyByDefault(sourceFaction, targetFaction)
```

Он изолирован в `NativeWarAdapter.cs` и недостижим в текущем deployment, потому что необходимы сразу два независимых условия:

1. `enableNativeWarAdapter=true` в server config;
2. process environment `AIPORT_ENABLE_NATIVE_WAR=I_UNDERSTAND_NATIVE_WAR`.

Текущее состояние обоих условий: `False`.

### Commit protocol

1. Обе стороны должны принять durable shadow-предложение `war`.
2. Инициатор выполняет `/diplomacy-ready <statement-id>`.
3. Сервер повторно проверяет source/target heroes, factions, authority и отсутствие войны.
4. Только в armed-режиме сервер выдаёт single-use token на 60 секунд.
5. Явный `/diplomacy-native-war <statement-id> <token>` запускает повторную полную проверку.
6. Token привязан к peer, source hero, statement, campaign generation, revision и faction IDs.
7. После native call сервер проверяет фактический war postcondition.
8. Только подтверждённый результат записывается как `committed_native_war` с `NativeMutationApplied=true`.

Нативный peace намеренно не реализован.

## Проверки

- cumulative suites 0.0.50–0.0.70: 12/12 PASS;
- clean deterministic build: PASS;
- executable lifecycle/lease/persistence harness: PASS;
- ровно один разрешённый native-war call: PASS;
- unrelated mutation APIs отсутствуют: PASS;
- live config default-off: PASS;
- process environment unarmed: PASS;
- disposable startup smoke: PASS;
- client/server hash parity: PASS.

Нативная война во время проверки не запускалась.

## Build / deploy

- Build: `0.0.70-dev`.
- Runtime: 269824 bytes.
- SHA-256: `fa1b4b8f2139e242ef23cd8b9273f874c09824f9d5a8155715c56c13f922e1a9`.
- Source rollback: `backups\source-20260817-011337-pre-0.0.70`.
- Deployment rollback: `backups\m0-20260817-012100`.
- PID: `27428`.
- Log: `artifacts\runtime-m0\logs\coop-server-20260817-012101.log`.
- State: `loaded:3:social:5:diplomacy:0`, revision `8`, generation `4ea97daf7c4e8ae14149a02cff988e72`, `ReadOnly=False`, `SERVING`.
- Adapter audit: configured `False`, environment armed `False`, enabled `False`.

## Участие пользователя

Для установленного default-off пакета участие не требуется. Отдельное ручное участие потребуется только перед первым намеренным armed runtime test с двумя player-controlled faction leaders.
