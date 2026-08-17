# AIInfluence 6.0.2 → Bannerlord Coop 0.1.1

## Исходный статический анализ совместимости

**Дата анализа:** 2026-08-13  
**Bannerlord:** 1.4.7  
**AIInfluence:** 6.0.2  
**Bannerlord Coop:** 0.1.1, commit `324d59795711727d40bc1aa3eb56a652ae656306`  
**Исходные данные:** декомпилированные AIInfluence, Coop.Core, Coop, Common, DedicatedServer.Windows и серверный AutoSyncExport.

> Это анализ восстановленного кода. В AIInfluence остались артефакты обфускации/декомпиляции, поэтому до сборочного прототипа некоторые детали являются инженерными выводами, а не подтвержденным runtime-тестом.

---

## 1. Краткий вывод

Совместимость **реализуема**, но это не маленький Harmony-патч к текущей DLL. Нужен сервер-авторитетный порт с разделением AIInfluence как минимум на три части:

1. **общий сетевой протокол**;
2. **серверное ядро AIInfluence** — промпты, HTTP-запросы, память, дипломатия, разрешенные действия, сохранение;
3. **клиентская оболочка** — диалоговый UI, ожидание ответа, ошибки и необязательная визуализация событий.

### Реалистичная оценка

| Цель | Сложность | Оценка для одного опытного C# / Bannerlord-разработчика |
|---|---:|---:|
| Только серверные AI-диалоги, без исполнения AI-команд | Средняя | 2–4 недели |
| MVP: диалоги + память + война/мир + несколько безопасных действий | Высокая | 5–8 недель |
| Большая часть дипломатии и стратегических действий | Очень высокая | 2–4 месяца |
| Почти полная функциональность AIInfluence 6.0.2, включая миссии/UI/динамические системы | Экстремальная | 3–6+ месяцев |

Главный риск — не сам вызов LLM. Самые дорогие части: устранение однопользовательских глобальных состояний, безопасная маршрутизация запросов разных игроков, исполнение игровых действий и согласование внешних JSON-сохранений с Coop.

---

## 2. Что удалось восстановить и проверить

В пакете исходников находятся:

- 610 декомпилированных C#-файлов AIInfluence;
- около 468 810 строк;
- 2 890 типов и 17 091 метод по метаданным;
- читаемые версии ключевых классов;
- исходная защищенная DLL и нормализованная DLL только для анализа;
- индексы типов и методов.

Статическая классификация файлов AIInfluence по прямым зависимостям дала:

| Категория | Файлов | Значение |
|---|---:|---|
| Явные client/UI-зависимости | 101 | Нельзя безусловно загружать на dedicated server |
| Mission/agent-зависимости | 25 | Требуют отдельной оценки или клиентского исполнения |
| Потенциально headless, но связаны с MCM | 79 | Логика пригодна, но настройки нужно отвязать от `GlobalSettings<ModSettings>` |
| Потенциально headless | 405 | Не является гарантией компилируемости, но хорошая база серверного ядра |

Полная машинная классификация сохранена в `HEADLESS_PARTITION.tsv`.

---

## 3. Почему оригинальную AIInfluence.dll нельзя просто положить на сервер

### 3.1. `SubModule` не headless-safe

`AIInfluence.SubModule.OnSubModuleLoad()` без условий создает и включает UIExtender. В `OnGameStart()` он:

- сканирует все типы сборки и пытается применить Harmony-патчи;
- добавляет UI-, mission- и campaign-зависимые behavior/model;
- инициализирует Memory Book и map notification;
- запускает системы, которые предполагают локальный экран, карту и `InformationManager`.

`OnApplicationTick()` вызывает UI popup, hotkey/input handlers, захват изображений, browser hosts, map layer и FPS overlay.

На dedicated server это дает высокий риск `TypeLoadException`, ошибок UI-типов, невалидных Harmony-целей или обращения к отсутствующему экрану/миссии.

### 3.2. Нельзя запускать полный `InitializationManager`

`InitializeAllSystems()` смешивает:

- серверно-пригодную campaign-логику;
- Native ConversationManager;
- MCM;
- динамические события;
- дипломатию;
- UI/изображения/TTS;
- Hermit, arena, болезни, RP items, settlement combat;
- чтение и запись внешнего состояния.

Для Coop его надо заменить явными профилями запуска, а не пытаться окружить десятками `if (isServer)`.

---

## 4. Рекомендуемая архитектура

```text
AIInfluence.Coop.Protocol
  ├─ protobuf-сообщения
  ├─ DTO без TaleWorlds Hero/MobileParty/Settlement
  └─ версия протокола и capability flags

AIInfluence.Coop.Server
  ├─ ServerBootstrap
  ├─ PlayerContextResolver
  ├─ ConversationCoordinator
  ├─ PromptService
  ├─ AIBackendService
  ├─ ResponseParser
  ├─ ActionPolicy + ActionExecutor
  ├─ Diplomacy/DynamicEvents projections
  └─ ServerPersistence

AIInfluence.Coop.Client
  ├─ ClientBootstrap
  ├─ Conversation UI bridge
  ├─ pending/error/cancel state
  ├─ optional notification/event UI
  └─ никаких API-ключей и авторитетных решений
```

### 4.1. Сетевой слой Coop

В Coop сообщения — типы `IMessage` с protobuf-контрактами. `SerializableTypeMapper` сканирует все загруженные сборки AppDomain и рассчитывает стабильный ID из полного имени типа. Это позволяет вынести новые сообщения в отдельную сборку, если **одинаковая сборка загружена на клиенте и сервере до первого сообщения**.

При этом автоматическая регистрация handlers идет через `RegisterAllTypesWithInterface` и `InterfaceCollector`, который сканирует AppDomain по namespace `Coop.Core.Client` или `Coop.Core.Server`. Отсюда два важных вывода:

1. sidecar-handler технически может быть обнаружен, если заранее загружен и находится под ожидаемым namespace;
2. в изученном Coop нет очевидного универсального загрузчика произвольных plugin DLL, поэтому сначала нужен маленький startup-spike: подтвердить, как дополнительный Bannerlord-мод загружается dedicated server и успевает ли он зарегистрироваться до построения Autofac-контейнера.

**Предпочтение:** не переписывать hash-pinned `Coop.Core.dll`, а подключить согласованный addon-мод клиенту и серверу. Если порядок загрузки не позволит автосканирование, минимальный поддерживаемый hook в Coop лучше, чем Harmony-инъекция в приватный Autofac-контейнер.

### 4.2. Серверная авторитетность

На сервере должны находиться:

- API-ключи и адреса локальных backend;
- генерация промпта;
- NPC context, RAG и долговременная память;
- дипломатическая логика;
- динамические события;
- проверка и исполнение AI-команд;
- внешние JSON-сохранения.

Клиент передает только намерение игрока и показывает результат.

---

## 5. Сетевой протокол диалога

Минимальный протокол:

```text
Client → Server: AIConversationRequest
  ProtocolVersion
  RequestId (GUID)
  ConversationId (GUID)
  PlayerControllerId
  ClaimedPlayerHeroId
  NpcHeroId
  PlayerText
  ClientSequence

Server → Client: AIConversationAccepted
  RequestId
  QueuePosition / EstimatedState

Server → Client: AIConversationResult
  RequestId
  ConversationId
  ServerSequence
  DisplayText
  SpeakerHeroId
  AllowedActionSummaries[]
  Completed

Server → Client: AIConversationError
  RequestId
  ErrorCode
  SafeMessage
  Retryable

Client → Server: AIConversationCancel
  RequestId
```

Для MVP потоковая передача токенов не нужна. Она усложняет порядок сообщений и восстановление после reconnect. Ее можно добавить позднее отдельным `AIConversationChunk`.

### Обязательная серверная валидация

Сервер не должен доверять присланному `HeroId`. По `NetPeer` / controller id он обязан найти назначенного Coop-героя и проверить:

- игрок подключен и полностью вошел в campaign state;
- NPC существует и доступен этому игроку;
- длину и частоту сообщений;
- отсутствие повторного `RequestId`;
- что игрок не подменяет другого героя;
- что для пары player/NPC разрешен выбранный режим взаимодействия.

### Параллельность

Нужны:

- очередь или mutex на `ConversationId`;
- отдельный лимит запросов на игрока;
- общий semaphore для backend;
- timeout/cancellation;
- idempotency-cache по `RequestId`;
- выполнение любых TaleWorlds-мутаций только на game thread.

---

## 6. Устранение single-player глобальных состояний

Критические глобалы:

- `Hero.MainHero`;
- `MobileParty.MainParty`;
- `PlayerEncounter.Current`;
- `Campaign.Current.ConversationManager`;
- `Mission.Current`;
- `GlobalSettings<ModSettings>.Instance`.

Их нельзя просто временно переназначать под текущего игрока: два запроса могут пересечься, а часть событий выполняется асинхронно.

Нужен неизменяемый серверный контекст:

```csharp
public sealed record AIRequestContext(
    string RequestId,
    string PlayerControllerId,
    Hero PlayerHero,
    MobileParty PlayerParty,
    Hero NpcHero,
    string ConversationId,
    CampaignTime StartedAt,
    CancellationToken CancellationToken);
```

Этот контекст передается в:

- `PromptGenerator`;
- генераторы world/player/NPC context;
- relation/memory logic;
- response validator;
- action policy;
- persistence attribution.

В первую очередь надо параметризовать `PromptGenerator`, `AIInfluenceBehavior.HandlePlayerInput`, `SendAIRequest`, `DialogManager`-последствия и `AIActionManager`.

---

## 7. AI backend и настройки

HTTP-часть в целом пригодна для headless:

- OpenRouter;
- DeepSeek;
- Player2;
- Ollama;
- KoboldCpp.

Проблема — не HTTP, а 55 прямых обращений к `GlobalSettings<ModSettings>.Instance` только в API-папке и еще десятки обращений в campaign-системах.

Нужен интерфейс:

```csharp
public interface IAIInfluenceSettings
{
    string Backend { get; }
    string Model { get; }
    string ApiKey { get; }
    Uri Endpoint { get; }
    TimeSpan Timeout { get; }
    int MaxConcurrentRequests { get; }
    bool EnableDiplomacy { get; }
    bool EnableDynamicEvents { get; }
}
```

### Разделение настроек

**Только сервер:** API-ключи, backend URL, модель, timeout, лимиты, системные промпты, разрешенные действия.  
**Авторитетные gameplay-настройки:** дипломатия, dynamic events, frequency, economic effects. Сервер может отправлять клиенту только безопасные read-only capabilities.  
**Только клиент:** UI, кнопки, окно, звук/TTS, изображения, hotkeys.

Рекомендуемый серверный путь:

```text
E:\BCOOP\data\AIInfluence\server.json
E:\BCOOP\data\AIInfluence\secrets.json
```

`secrets.json` не должен передаваться клиенту или попадать в save archive.

---

## 8. Матрица игровых действий

### 8.1. Хорошие кандидаты для раннего MVP

| Действие | Поддержка Coop | Рекомендация |
|---|---|---|
| Объявление войны | Есть `NetworkDeclareWar` и server/client stance handlers | Выполнять на сервере, проверить фактический broadcast |
| Мир | Есть `NetworkMakePeace` | Выполнять на сервере |
| Золото героя | AutoSyncExport содержит `Hero_Gold` | Разрешить после runtime-теста |
| Influence клана | AutoSyncExport синхронизирует поле | Разрешить после runtime-теста |
| Состав войск/пленных | Есть roster handlers и AutoSync | Разрешить небольшими атомарными изменениями |
| Инвентарь party | Есть item roster sync | Разрешить после теста полного/дельта обновления |
| Состояние героя | Есть hero state messages/AutoSync | Только ограниченный набор переходов |
| Супруг/супруга | AutoSync поля `Hero.Spouse` | Не означает полную корректность marriage pipeline; отложить |
| Вход/выход party в settlement | Есть handlers/messages | Серверный вызов и runtime-тест |
| Siege entry | Есть отдельные server/client handlers | Не разрешать до отдельного теста |

### 8.2. Нужна новая проекция/синхронизация

| Система | Почему недостаточно текущего Coop |
|---|---|
| AIInfluence alliances | Это собственное состояние, в Coop не найдено |
| Trade agreements | Собственный JSON и экономические эффекты |
| Reparations | Собственная модель/записи |
| Custom tribute schedules | Поле vanilla wallet не передает смысл и историю соглашения |
| Dynamic events | Список, стадии и знания NPC существуют только в AIInfluence JSON |
| Diseases/quarantine | В Coop не найдено протокола |
| RP items | Пользовательская система AIInfluence |
| Memory Book / NPC memories | Серверное состояние, клиенту нужна только проекция |
| Pending player/kingdom statements | Нужна адресная доставка и идентификация игрока |

### 8.3. Высокий риск — исключить из первого MVP

| Действие | Риск |
|---|---|
| `SetMove*`, follow, patrol, go-to-settlement | В Coop найден sync состояния party, но не подтвержден безопасный sync AI-команд/целей |
| Create/destroy party | Частичная lifetime-синхронизация есть, но создание AIInfluence-party надо тестировать от рождения до reconnect/save |
| Raid village | Coop имеет hostile-action handlers, но прямой вызов AIInfluence может обходить ожидаемый request/approval путь |
| Начало атаки/map event | Сильно зависит от server mission/instance lifecycle |
| Siege command | Много связанного состояния и отдельные approval-потоки Coop |
| Kill/death | Необратимо; требует проверки hero state, clan/party/quest последствий |
| Marriage/romance/family spawn | Несколько зависимых объектов и событий |
| Quest creation/completion | Не подтверждена полная синхронизация quest state |
| Mission combat/tactics/dialogue movement | Client/mission-instance код, не campaign headless core |
| Settlement combat, scene images, TTS/STT | Клиентские presentation/mission-системы |

### 8.4. Главный принцип исполнения

LLM не должен напрямую вызывать `AIActionManager.ParseAndExecuteCommand`.

Нужен pipeline:

```text
raw LLM response
  → strict JSON parser
  → schema validation
  → resolve IDs on server
  → ActionPolicy whitelist
  → authorization for requesting player
  → precondition check
  → execute on game thread
  → observe/broadcast
  → persist audit record
  → return safe result to client
```

Для MVP whitelist рекомендуется ограничить:

- no-op/narrative response;
- relation/memory updates, если они серверные;
- gold/influence в малых лимитах;
- war/peace через явные адаптеры;
- roster transfer только после runtime-теста.

Все неизвестные команды должны быть отклонены, а не выполнены «по возможности».

---

## 9. Диалоги и клиентский UI

`DialogManager` сейчас регистрирует native Bannerlord conversation lines, читает локальный `Campaign.Current.ConversationManager` и последствиями вызывает `AIInfluenceBehavior.HandlePlayerInput()`.

В Coop нужен client bridge:

1. клиент определяет NPC и локального Coop-игрока;
2. отображает строку/поле ввода;
3. создает `RequestId` и отправляет запрос;
4. блокирует только собственную AI-ветку, а не всю campaign simulation;
5. отображает pending/error/cancel;
6. принимает ответ только для совпадающего `ConversationId`;
7. игнорирует поздний ответ после закрытия диалога либо кладет его в журнал.

Для первого прототипа лучше сохранить native dialogue shell, но убрать из него авторитетные последствия. Полноценные Memory Book, web editor, image capture и world events UI стоит оставить за пределами MVP.

---

## 10. Дипломатия и динамические события

### Дипломатия

`DiplomacyManager` хорошо подходит для сервера: он слушает campaign events и для войны/мира использует vanilla actions, которые Coop уже знает как синхронизировать.

Однако следующие подсистемы AIInfluence не vanilla:

- alliances;
- war fatigue;
- trade agreements;
- reparations;
- territory transfer history;
- pending statements;
- custom tribute/economic effects.

Их следует оставить серверными и отправлять клиентам read-only snapshot/delta только для UI. Клиент не должен самостоятельно пересчитывать ежедневную дипломатию.

### Dynamic Events

Генерация и суточный tick должны выполняться только один раз на сервере. Клиент получает:

- ID события;
- безопасный текст;
- дату/стадию;
- затронутые entity IDs;
- optional presentation metadata.

NPC knowledge и последствия остаются на сервере.

---

## 11. Сохранение

AIInfluence сочетает Bannerlord `SyncData` и большой объем внешнего JSON. Обнаружено не менее 32 прямых `File.WriteAllText`, а `CampaignDiplomacyPersistence.SaveFull()` вызывается из многих систем.

Текущая привязка к `Campaign.UniqueGameId` полезна, но недостаточна для Coop autosave/rollback/reconnect.

### Требуемая модель

```text
BCOOP/data/AIInfluence/campaigns/<UniqueGameId>/
  manifest.json
  aiinfluence_campaign_diplomacy.json
  npcs/
  snapshots/
  logs/
```

`manifest.json` должен содержать:

- protocol/schema version;
- Coop save slot/id;
- `Campaign.UniqueGameId`;
- campaign day;
- generation number;
- hashes основных файлов;
- время последнего успешного flush.

### Save barrier

Перед Coop save/server shutdown:

1. запретить новые мутации;
2. дождаться или отменить активные AI-запросы;
3. завершить `SaveQueueManager`;
4. записать временные файлы;
5. atomically rename;
6. создать manifest/snapshot;
7. только затем подтверждать завершение save.

После загрузки нужно проверять, что внешний snapshot соответствует загруженному Coop save. При несовпадении — восстановить подходящий snapshot или отключить AI mutation, а не молча смешивать состояния.

---

## 12. План реализации

### Фаза 0 — технический spike загрузки (2–4 дня)

- собрать минимальный Protocol addon;
- загрузить его на dedicated server и client;
- передать ping/pong protobuf message;
- подтвердить discovery handlers и module validation;
- ничего не менять в авторитетном мире.

**Критерий:** стабильный reconnect и совпадающие type IDs.

### Фаза 1 — dialogue-only вертикальный срез (1–2 недели после spike)

- server config без MCM;
- `PlayerContextResolver`;
- один backend;
- request/accepted/result/error/cancel;
- server-side prompt для конкретного player hero/NPC;
- никакого исполнения AI actions;
- минимальный native client UI.

### Фаза 2 — память и сохранение (1 неделя)

- NPC context только на сервере;
- save barrier;
- snapshot/manifest;
- audit и rate limits.

### Фаза 3 — безопасные действия (1–2 недели)

- strict response schema;
- whitelist;
- war/peace;
- gold/influence;
- один тип roster transfer;
- integration tests/reconnect/save-load.

### Фаза 4 — дипломатические проекции (2–4 недели)

- alliances/trade/reparations/tributes;
- snapshot + delta client messages;
- UI read models.

### Фаза 5 — рискованные campaign actions

- party movement;
- raid/siege;
- create/destroy party;
- death/marriage/quests;
- по одной функции с отдельной проверкой join-in-progress и save/load.

### Фаза 6 — client/mission features

- TTS/STT;
- images;
- Memory Book UI;
- group conversations;
- settlement combat;
- battle tactics.

---

## 13. Что не стоит делать

1. Не устанавливать `AIInfluence.analysis-clean.dll` в игру — она предназначена только для анализа.
2. Не загружать оригинальный `SubModule` на dedicated server без разделения lifecycle.
3. Не хранить API-ключ на клиентах.
4. Не подменять `Hero.MainHero` на время запроса.
5. Не вызывать `ParseAndExecuteCommand` напрямую из результата LLM.
6. Не рассчитывать, что синхронизация поля автоматически синхронизирует всю семантику action pipeline.
7. Не включать сразу raid/siege/death/marriage/quest.
8. Не модифицировать hash-pinned Coop assemblies до проверки addon-механизма.
9. Не считать внешние JSON второстепенными: для AIInfluence это часть авторитетного save.

---

## 14. Ближайший практический следующий шаг

Сделать **минимальный Coop addon spike**, который:

- загружается в клиент и dedicated server;
- объявляет два protobuf message;
- регистрирует client/server handler;
- отправляет запрос с `RequestId`;
- возвращает echo-ответ;
- логирует controller id и разрешенный server-side hero id;
- проходит reconnect и module validation.

Этот тест за 2–4 дня снимет главный инфраструктурный риск. После него можно переносить AI request pipeline, не трогая пока 90% AIInfluence.

---

## 15. Итоговая оценка

**Сделать серверные AI-диалоги реально.** Восстановленного кода достаточно, чтобы не переписывать всю идею с нуля. Основные алгоритмы промпта, backend-клиенты, NPC context, дипломатия и persistence видны.

Но совместимость нужно строить как **server-authoritative адаптацию**, а не как попытку запустить single-player мод на всех участниках. Самый разумный MVP — серверные диалоги и память, затем небольшой whitelist действий. Полная функциональность должна добавляться по action-by-action матрице с обязательными save/load, reconnect и join-in-progress тестами.
