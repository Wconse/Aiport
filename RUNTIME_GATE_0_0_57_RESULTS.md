# Runtime gate 0.0.57 — результаты

Дата: 2026-08-16

## Итог

`PASS_CORE_WITH_GAPS`: основная инфраструктура, Groq, social shadow, второй NPC, reconnect/JIP, automatic save и native-mutation suppression сработали. Дипломатический receipt не был создан из-за ограничения 0.0.57 на наличие королевства игрока. Отдельный manual restart был пропущен, но последующее развёртывание 0.0.58 перезапустило сервер и успешно загрузило сохранённые memory/social данные из той же generation.

## Подтверждено логами

- Клиент и сервер согласовали build `0.0.57-dev`, protocol 2, flags 511.
- Groq: `enabled=True`, `keyPresent=True`, `BackendEnabled=True`; ответ `Stub=False`, 209 символов.
- Первый AI-turn поднял revision 2 -> 3 и создал одну memory turn.
- Создано четыре social receipt `+1` для `hero:lord_5_1`: score 0 -> 4.
- Создан один social receipt `-1` для `hero:lord_5_13`: score 0 -> -1.
- Итог перед reconnect: revision 8, memory records 3, social records 5.
- Второй NPC получил отдельный target ledger.
- Reconnect: peer 0 отключился, peer 1 подключился; flags 511, revision 8.
- Private snapshot после JIP: `Ready=True`, 3429 символов.
- Automatic save при reconnect: `saved:3:social:5:diplomacy:0`, stable generation `4ea97daf7c4e8ae14149a02cff988e72`.
- После фактического restart при deployment 0.0.58: `loaded:3:social:5:diplomacy:0`, revision 8, `ReadOnly=False`, та же generation.
- Нет AIPort fatal/unhandled exception.
- Нет `MutationApplied=true` или `NativeMutationApplied=true`.

## Разобранные отклонения

### 1. Social cooldown не сработал

Это не потеря receipt и не race. В 0.0.57 cooldown был всего 5 секунд, а интервалы между фактическими confirmations составили 10.221, 15.389 и 7.073 секунды. Все они законно прошли. В 0.0.58 окно увеличено до 15 секунд, а клиент показывает понятное сообщение.

### 2. Diplomacy была заблокирована

Обе попытки завершились `player_kingdom_required`. Игрок находился вне королевства, поэтому proposal не дошёл до confirmation/ledger. В 0.0.58 контекст обобщён с `Kingdom` на authoritative `Hero.MapFaction`: независимый клан теперь может формировать war/peace shadow statement против фракции NPC. Bandit и same-faction пары по-прежнему отклоняются.

### 3. Первоначальный checker показал ложные FAIL

Причины: Serilog заключал `Mode` в кавычки, а Groq evidence находился в двух отдельных строках (`BackendEnabled=True` и `Stub=False`). Checker исправлен: он выводит `PASS`, `NOT_RUN`, `BLOCKED` и `FAIL`, а пропущенный step 8 больше не считается core failure. Исправленный verdict — `PASS_CORE_WITH_GAPS`.

### 4. Coop ObjectManager warning flood

В тестовом логе 1653 сообщения `ObjectManager Failed to get id/get object`, в основном для динамических `MobileParty`, `ItemRoster`, внутренних `A.F+A` и null `PartyTemplateObject`. Они появляются при world sync/reconnect, не исходят из AIPort и не сопровождались crash/fatal exception. AIPort не меняет Coop из-за этих предупреждений; они остаются отдельным upstream-наблюдением.

### 5. Step 8 был пропущен

Полный user-driven save/restart с дипломатическим receipt не выполнен. Однако automatic save и последующий restart 0.0.58 доказали сохранение memory/social и стабильной generation. Дипломатическую persistence по-прежнему покрывает executable harness; runtime receipt станет возможен с faction-aware 0.0.58.
