> **Обновление 0.0.58:** дипломатия теперь работает для независимого player clan через `MapFaction`; social cooldown равен 15 секундам. Результаты этого прогона: `docs/RUNTIME_GATE_0_0_57_RESULTS.md`.

# Полный runtime-gate 0.0.57

Этот тест выполняется только на disposable-сохранении `aiport-m0`. Не использовать `saveauto1`.

## 0. Запуск с Groq без сохранения ключа

1. Закрой Bannerlord и launcher.
2. Запусти `tools\start_0_0_57_gate_with_groq.cmd`.
3. В скрытом поле введи Groq API key. Скрипт передаёт его только окружению серверного процесса и не записывает на диск.
4. В новом окне сервера дождись `[DedicatedServer] SERVING`.
5. Запусти клиент и подключись к disposable-серверу `aiport-m0`.

## 1. Первичная диагностика

Поговори с героем другого королевства и последовательно введи:

```text
/aiport-status
/diplomacy-snapshot
/aiport-gate baseline
```

Ожидания:

- Build `0.0.57-dev`, protocol `2`, flags `511`.
- `loaded=True`, `readOnly=False`, `saving=False`.
- `Backend: configured=True; active=True; keyPresent=True`.
- У собеседника есть hero target и, для дипломатического теста, другое королевство.
- Baseline сообщает `STORED`.

Если у игрока нет королевства, сначала вступи в королевство. Если собеседник из того же королевства, выбери другого героя.

## 2. Реальный Groq-turn и память

В том же диалоге введи обычную фразу без `/`, например:

```text
Кратко оцени отношения между нашими королевствами и объясни свою позицию.
```

Ожидания:

- Приходит содержательный ответ NPC, не сообщение о недоступном backend.
- После ответа `/aiport-gate report` показывает увеличение `revision` и `memory`.

## 3. Social shadow, подтверждение и cooldown

В том же диалоге:

```text
/relation-propose +1
/relation-confirm
```

Ожидание: подтверждение записано в shadow-режиме.

Сразу, не ожидая пяти секунд, повтори:

```text
/relation-propose +1
/relation-confirm
```

Ожидание: второе подтверждение отклонено с `social_cooldown`.

Подожди 6 секунд и снова выполни:

```text
/relation-propose +1
/relation-confirm
/relation-shadow -1
/aiport-gate report
```

Ожидания отчёта:

- `nativeRelationUnchanged=PASS`;
- `nativeWarStateUnchanged=PASS`;
- social delta минимум `+2`;
- target custom score вырос на `2`;
- `/relation-shadow -1` не меняет persistent score или revision.

## 4. Diplomatic shadow statement

Посмотри `/diplomacy-snapshot`.

- Если с королевством собеседника сейчас **мир**, сначала проверь неправильный вариант `/diplomacy-propose peace`: ожидается `not_at_war`. Затем используй `war`.
- Если сейчас **война**, сначала проверь неправильный вариант `/diplomacy-propose war`: ожидается `already_at_war`. Затем используй `peace`.

Для корректного действия выполни:

```text
/diplomacy-propose war
/diplomacy-confirm
```

или:

```text
/diplomacy-propose peace
/diplomacy-confirm
```

Сразу повтори ту же пару команд. Ожидание второго подтверждения: `diplomacy_cooldown`.

Затем:

```text
/aiport-gate report
/diplomacy-snapshot
```

Ожидания:

- diplomacy delta минимум `+1`;
- в отчёте появилась последняя shadow-запись;
- `nativeWarStateUnchanged=PASS`;
- война или мир в повторной сводке не изменились.

## 5. Второй NPC

Заверши диалог, поговори с другим героем и выполни:

```text
/aiport-gate baseline
/relation-propose -1
/relation-confirm
/aiport-gate report
```

Ожидания: отдельный target custom score, social delta `+1`, native relation не изменилась.

## 6. Reconnect / JIP

1. Выйди в меню, не останавливая сервер.
2. Подключись снова.
3. Поговори с первым NPC.
4. Выполни:

```text
/aiport-status
/aiport-gate report
```

Ожидания:

- generation не изменилась;
- social и diplomacy counts не обнулились;
- private state snapshot был принят;
- для того же NPC baseline comparison остаётся доступным и native-проверки дают PASS.

## 7. Save → restart → load

1. Запусти `tools\save_0_0_57_gate.cmd` и дождись подтверждения команды `save`.
2. Подожди 5–10 секунд.
3. Закрой клиент.
4. Снова запусти `tools\start_0_0_57_gate_with_groq.cmd` и повторно введи ключ.
5. Дождись `SERVING`, затем подключись клиентом.
6. Поговори с первым NPC и выполни:

```text
/aiport-status
/diplomacy-snapshot
```

Ожидания:

- `loaded=True`, `readOnly=False`;
- та же campaign generation;
- social count не меньше `3`;
- diplomacy count не меньше `1`;
- memory count не меньше `1`;
- revision не откатился;
- Groq снова `active=True`.

После рестарта baseline-регистр намеренно пустой. Создай новый:

```text
/aiport-gate baseline
/aiport-gate report
```

Ожидание: обе native-проверки PASS.

Отправь ещё одну обычную реплику NPC, чтобы подтвердить Groq после рестарта.

## 8. Автоматическая проверка логов

После всех шагов запусти:

```text
tools\check_0_0_57_runtime_gate.cmd
```

Скрипт создаст:

```text
artifacts\runtime-m0\gate-0.0.57-report.json
```

Ожидается PASS для runtime, flags, baseline/report, social receipt/cooldown, diplomacy receipt/precondition/cooldown, Groq, save/load, state snapshot, SERVING и отсутствия native mutation/fatal errors.

После выполнения достаточно написать агенту `готово`; агент самостоятельно перечитает логи и manifest-файлы и даст итоговый verdict.
