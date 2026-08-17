# Future AIInfluence feature plan for Coop

## Current scope override — 2026-08-17

Earlier multi-client acceptance requirements in this roadmap are superseded. Current implementation and acceptance use one connected player only, with no player-to-player proposals, consent, inboxes, lifecycle, authorization or testing. Historical records remain factual; they are not future gates.

**Status:** planning and research only  
**Baseline:** AIPort `0.0.37-dev`, protocol `1`  
**Date:** 2026-08-14  
**Implementation effect of this document:** none. No gameplay code, protocol field, save data, DLL or deployment is changed by this milestone.

## 1. Executive summary

The original AIInfluence mod is not safe to load unchanged into a Coop campaign. It assumes one local player, mixes prompt generation with mutable campaign objects, contains client UI and server-worthy systems in the same module, persists state in many independent locations, and includes direct command/action paths that are too permissive for network or LLM input.

The target is therefore **feature parity by behavior, not binary reuse**:

- keep the BCOOP campaign server as the only authority;
- let the model propose typed intentions, never execute arbitrary commands;
- resolve the acting player, NPC and all game objects on the server;
- validate every mutation against current authoritative state;
- require consent for actions that spend, transfer, marry, harm or otherwise bind a player;
- serialize conflicting mutations and make retries idempotent;
- project committed state through existing Coop synchronization where it is proven, and add explicit AIPort snapshots/deltas where Coop has no representation;
- bind external state to a specific Coop save generation and commit it behind a save barrier;
- keep TTS, STT, images and most UI presentation client-local and non-authoritative;
- ship each subsystem behind a separate default-off feature flag.

The recommended order is not “port every original manager and switch it on.” The order is:

1. complete the single-connected-player narrative, persistence and reconnect test;
2. build the transaction, capability, audit and persistence foundations with mutations still disabled;
3. add persistent memory and read-only world/diplomacy projections;
4. add low-risk social state and tightly capped transfers;
5. run diplomacy in shadow/proposal mode before enabling any mutation;
6. add custom-state systems such as events and diseases only after save/load and join-in-progress are proven;
7. leave party control, combat, marriage, unique-character creation and death for late opt-in phases.

## 2. Scope and evidence

### 2.1 Sources reviewed

The plan is based on:

- the current AIPort source and documentation;
- the installed Coop and BCOOP assemblies/decompiled sources;
- the recovered AIInfluence source at `E:\AIInfluence_Extracted_20260813\decompiled_source_complete\AIInfluence`;
- the installed AIInfluence module as a read-only reference;
- 588 recovered C# files in the main AIInfluence namespace;
- the original response/action pipeline, diplomacy, persistence, dynamic event, disease, memory, initiative, quest, romance, party action, settlement combat, battle tactics and presentation systems.

Recovered source distribution confirms that this is much larger than a dialogue feature: `Behaviors` 98 files, `Diplomacy` 61, `Patches` 61, `ContentEditor` 39, `DynamicEvents` 37, `SettlementCombat` 37, `MemoryBook` 35, `Diseases` 26, `API` 22 and `ResponseActions` 11, plus 118 root-level files.

### 2.2 Confidence labels used below

- **Runtime-proven:** observed in the current AIPort/BCOOP runtime.
- **Coop source-confirmed:** a relevant Coop message, handler or AutoSync path exists, but the proposed AIPort use still needs runtime proof.
- **Original source-confirmed:** recovered AIInfluence contains the behavior/data model.
- **Design proposal:** the safe Coop implementation described here; it is not yet code.
- **Blocked:** no safe or complete synchronization path has been proven.

Decompiled code is a research aid. Some string literals and control flow are obfuscated. Where exact legacy parser aliases are uncertain, this plan deliberately defines a new strict schema rather than reproducing permissive legacy syntax.

## 3. Current `0.0.37` baseline

### 3.1 What already works

- AIPort client and BCOOP server load the same `0.0.37-dev` runtime with protocol `1`.
- Only explicit AIPort input options create AI requests:
  - `aiport_freeform_player_option` / “Сказать своими словами…”;
  - `aiport_continue_player_option` / “Сказать ещё…”.
- Vanilla player dialogue lines create no AI request.
- Hero entry is repeatable only at `hero_main_options` and reappears when vanilla branches return to that root.
- Supported regular `CharacterObject` roots include townsfolk/villagers, prison guards, castle guards and selected alley conversations.
- Both explicit AI input options request the existing Coop-wide authoritative pause before opening text input.
- Player identity is resolved from the connected peer; memory is isolated by peer, authoritative player hero, target and conversation.
- The server owns provider credentials, prompt construction, rate limits, cancellation and response delivery.
- Groq `llama-3.1-8b-instant` is enabled for development through server-only configuration and environment credentials.
- Prompt context is read-only and bounded: authoritative character facts, deterministic profile, objectives/politics, up to five relevant events from at most 96 campaign logs and bounded volatile history.
- Results are presentation-only and do not mutate the internal Bannerlord sentence graph.

### 3.2 What is intentionally absent

- no gameplay action execution;
- no diplomacy mutation;
- no synthetic dynamic-event generation;
- no writable AIPort campaign persistence;
- no persistent AI biography or cross-session memory;
- no original AIInfluence DLL or original action manager at runtime;
- no client-owned campaign consequence;
- no protocol support for action proposals, confirmation, snapshots or deltas.

### 3.3 Gates that remain before stateful work

1. Real single-connected-player identity, memory, cancellation and request test.
2. Reconnect and join-in-progress narrative test for the same authoritative player.
3. Confirmation that shared pause behavior cannot be abused by rapid repeated inquiries.
4. A frozen rollback point and repeatable disposable-server baseline.

## 4. Non-negotiable design invariants

1. **Server authority.** Only the campaign server may read authoritative state for a mutation, approve it, execute it and persist it.
2. **Explicit AI origin.** Vanilla dialogue never becomes an action request merely because a player clicked it.
3. **Model is an untrusted proposer.** Raw output is data, not a command stream.
4. **No identity impersonation.** Never swap or emulate `Hero.MainHero`; map peer/controller to the actual Coop hero.
5. **Stable IDs only.** Protocol and persistence use server-resolved string IDs and revisions, not serialized TaleWorlds object references.
6. **Default deny.** Unknown action type, field, target, enum, ID or capability is rejected.
7. **Consent before player harm or obligation.** Losing gold/items/troops/fiefs, marriage/intimacy, surrender, imprisonment and lethal actions require explicit policy and usually explicit player approval.
8. **Game-thread mutation.** Backend/network workers may only produce immutable proposals. TaleWorlds mutation runs on the authoritative game thread.
9. **Idempotency.** A retry, duplicate packet or reconnect must not repeat a consequence.
10. **Revision checks.** Approval is invalid if relevant state changed after the proposal snapshot.
11. **Bounded work.** Limit prompt size, action count, chain depth, affected entities, monetary values, duration, event count and background job concurrency.
12. **Auditable consequences.** Every accepted/rejected proposal receives a safe reason and an append-only audit record without secrets or hidden model thoughts.
13. **Generation-bound persistence.** External state never silently loads into a different Coop save generation.
14. **Projection is explicit.** Existing Coop synchronization is used only where verified; custom AIPort state uses versioned snapshots/deltas.
15. **Rollback first.** A feature cannot leave shadow mode until its backup, recovery and disable path are proven.
16. **No protobuf number reuse.** Removed fields remain reserved forever.

## 5. Original AIInfluence feature inventory

### 5.1 Typed response/action layer

`AIResponse` contains spoken response data plus action-oriented fields: `Actions`, legacy `ActionCommands`, kingdom and workshop fields, item transfers, social patches, generated character flavor, letter permission, TTS instructions, settlement, tribute and reparations data.

The recovered typed pipeline is:

```text
AIResponseActionParser
  -> AIResponseActionNormalizer
  -> AIResponseActionValidator
  -> AIResponseActionPipeline
  -> AIResponseActionExecutor / subsystem handlers
```

`AIResponseAction` supports `Type`, `ActorHeroId`, `TargetHeroIds`, `TargetId`, parameter values and nested `ThenActions`. The original legacy `AIActionManager.ParseAndExecuteCommand` also accepts raw action commands and tracks long-running hero actions.

**Port decision:** preserve the typed-pipeline idea, but do not expose the original parser, legacy raw commands or arbitrary `ThenActions`. The new schema accepts only canonical action names and fields emitted in a dedicated JSON object. Unknown/additional fields fail closed. Initially one proposal may contain at most one state-changing intent.

Exact typed action constants found in `AIResponseActionTypes.cs`:

| Original constant value | Semantic family | Coop-safe initial treatment |
|---|---|---|
| `attack` | combat | text/proposal only; late phase |
| `surrender` | combat | explicit affected-player confirmation |
| `accept_surrender` | combat | server encounter adapter; late phase |
| `release` | combat/captivity | server encounter adapter; high-risk |
| `leave` | social/UI | local dialogue-flow hint only |
| `relation` | social/native state | capped server delta after validation |
| `social` | custom social memory | server custom-state patch |
| `claim` | narrative identity claim | observation only, never authoritative fact |
| `lie` | narrative/social | observation and bounded trust penalty only |
| `deescalate` | social/combat | state-machine transition, not magic combat cancellation |
| `address` | group conversation | speaker/audience routing only |
| `romance` | relationship | custom bounded state, opt-in |
| `propose_marriage` | relationship | proposal requiring consent; no direct marriage |
| `accept_marriage` | relationship | strict eligibility and mutual consent |
| `reject_marriage` | relationship | safe state transition/notification |
| `intimate` | relationship | explicit consent and eligibility; default off |
| `transfer_gold` | economy | atomic capped transfer with owner authorization |
| `transfer_item` | inventory | atomic item-roster transfer with receipt |
| `navigate` | mission | client/mission-host navigation hint first |
| `talk` | mission | presentation/interaction request only |
| `follow` | mission/party | mission-local follow or server party task; separate schemas |
| `death` | story/lethal | disabled by default; last phase |
| `kingdom` | diplomacy | expanded into a strict diplomacy enum/state machine |
| `quest` | quest | expanded into create/update/complete/fail proposal schemas |

### 5.2 Legacy party actions and tasks

Original long-running campaign actions include:

- attack party;
- create party;
- create RP item;
- follow player;
- go to settlement;
- patrol settlement;
- raid village;
- return to player;
- siege settlement;
- transfer troops and prisoners;
- wait near settlement.

The recovered task system has step types `GoToSettlement`, `WaitInSettlement`, `ReturnToPlayer`, `FollowPlayer`, `AttackParty`, `SiegeSettlement`, `PatrolSettlement`, `WaitNearSettlement`, `RaidVillage` and `Custom`, with active/completed/cancelled task and step state.

**Port decision:** do not copy the raw command manager. Rebuild tasks as server-owned finite state machines with stable task IDs, an owner/issuer, one active movement controller per party, preconditions per step, cancellation reason, timeout and persisted checkpoint. `Custom` is not accepted from the model.

### 5.3 Diplomacy

Original diplomacy includes:

- war and peace;
- alliances;
- trade agreements;
- tribute demands, schedules and histories;
- reparations demands and one-time payments;
- territory/fief transfer;
- war fatigue and statistics;
- kingdom statements and player statement analysis;
- clan expulsion and pardon data;
- ruler kingdom tax policy;
- leadership history;
- queued diplomatic events and per-kingdom response pressure.

The exact recovered `DiplomaticAction` enum is:

```text
None
DeclareWar
ProposePeace, RejectPeace, AcceptPeace
ProposeAlliance, RejectAlliance, AcceptAlliance, BreakAlliance
ProposeTradeAgreement, AcceptTradeAgreement, RejectTradeAgreement, EndTradeAgreement
TransferTerritory, DemandTerritory, RejectTerritory
DemandTribute, AcceptTribute, RejectTribute, EndTribute
DemandReparations, AcceptReparations, RejectReparations
ExpelClan
GrantFief, ReceiveFief
QuarantineSettlement
SetKingdomTaxPolicy
```

Original data tracks participating kingdoms, statement queues, engagement/response pressure, statement schedules, alliances, agreement end times, pending tribute/reparation demands, payment/transfer history, war statistics and tax policy. `DiplomaticActionInfo` includes source/target kingdom, target clan, settlement, reason, daily tribute, duration, reparations, trade duration, quarantine duration, tax rate/scope and tax settlement.

### 5.4 Dynamic events and economic effects

A recovered `DynamicEvent` has a stable ID, type, title, description, image path, history, player involvement, kingdoms/characters, importance, applicable NPCs, settlement penalty, economic effects, creation/expiration time, participating kingdoms, statements, engagement, follow-up schedules and optional disease data.

Economic effects can target one or several entities and include:

- immediate/daily prosperity and food deltas;
- immediate/daily security and loyalty deltas;
- income multiplier;
- duration;
- market-price category modifiers;
- reason/source event.

The original manager generates events on an interval, materializes them, updates/ends them, exposes them to NPC knowledge and UI, and persists active effects.

**Port decision:** split this into two layers:

1. LLM-generated **narrative event proposal** with no consequence;
2. deterministic **effect template materialization** selected from a server allowlist with capped targets, magnitudes and duration.

The model never supplies executable code, arbitrary formulae or unbounded numeric modifiers.

### 5.5 Diseases and quarantine

Original systems cover disease definitions, outbreaks, seasonal diseases, hero/troop/prisoner infection, spread between parties and settlements, treatment, prevention, immunity, progression/recovery, combat/map/morale/skill modifiers, quarantine and economic impact.

This is a simulation subsystem, not a dialogue action. It touches hero health, rosters, settlement entry, battles, hourly/daily ticks and economy.

**Port decision:** if implemented, disease simulation is deterministic server code. The model may propose names, descriptions and narrative framing, but cannot choose individual infection rolls, deaths or arbitrary modifiers. Disease and quarantine need custom AIPort state projection and a server snapshot for join-in-progress.

### 5.6 NPC initiative, letters and group conversations

Original features include:

- NPC initiative chance/cooldowns;
- friendly, hostile, romance, familiarity, party and long-no-contact weighting;
- NPC map approach/return behavior;
- messenger cost and delivery time;
- pending player letters and NPC responses;
- mission group conversations;
- directed replies and participant limits;
- ambient NPC conversations and overheard speech;
- mission navigation/talk/follow services.

**Port decision:** scheduling is server-owned and deterministic. A model call may fill dialogue only after a server scheduler selects an eligible NPC and recipient. Initiatives are rate-limited per player/NPC, respect opt-out, never pause the campaign before the recipient accepts an interaction, and never perform a gameplay action merely because a letter was generated.

### 5.7 Persistent NPC context, Memory Book, world knowledge and RAG

Original `NPCContext` is extensive: conversation history, observations, memories, recent events, trust, emotional/escalation state, claims/lies, forces/location/time, romance partners, quests, diseases, action summaries, generated flavor, known secrets/info and pending consequences.

Memory Book entries carry ID, campaign day/time, title, summary, scene, memory text, image path and involved heroes. The original also has world information files, secret/knowledge access, event propagation and a RAG system.

**Port decision:** separate truth from prose:

- authoritative facts reference committed campaign/event IDs;
- observations record who could have perceived an event;
- social opinions are explicitly subjective custom state;
- summaries are regenerable model output with source references;
- generated images are optional presentation blobs, never evidence;
- RAG is read-only retrieval over approved server content and committed memories;
- private dyad memories are not sent to another player unless an explicit overhearing/sharing rule created a shared observation.

### 5.8 AI-generated quests

Original quest data supports create/update/complete/fail, giver/target/completer NPCs, multiple target IDs, reward gold, duration, verification notes, discrete progress, progress label, update logs and completion reason.

**Port decision:** create AIPort-owned quest definitions and progress state, but integrate rewards/consequences only through allowlisted adapters. The model proposes narrative and verification criteria; server code converts supported criteria into typed predicates. Free-form “AI verification notes” may inform a player-facing description but cannot prove completion.

### 5.9 Romance, marriage and intimacy

Original systems track per-counterparty romance level and last interaction/intimacy day, decay, proposals, acceptance/rejection, marriage and conception chance.

**Port decision:** all relationship state is keyed by actual player hero and NPC, not `MainHero`. Romance narration can be enabled separately from marriage. Marriage/intimacy require explicit mutual consent, native eligibility checks, cooldowns and a final server revalidation. No model output may bypass native relationship, family or campaign constraints. Intimacy and conception remain separately default-off.

### 5.10 Death and lethal consequences

Original code includes role-play death actions, kill data and death history. Death affects quests, clans, parties, leadership, succession, relationships and save integrity.

**Port decision:** `death` stays disabled until every dependent projection and recovery path is proven. If ever enabled, protect player-controlled heroes, quest-critical targets, current conversation actors and targets whose death would violate Coop ownership. Require an explicit server policy plus confirmation for any player-initiated lethal consequence. Prefer native battle/campaign outcomes over direct scripted death.

### 5.11 Unique characters, player party members and RP items

Original systems can generate unique role characters, spawn custom player party members and create role-play items.

**Port decision:** these require a replicated definition registry before any instance appears. Stable IDs, culture/body/equipment definitions and content hashes must reach clients before hero/item/party state that references them. Until Coop creation/lifetime synchronization is runtime-proven, keep them narrative-only or use predeclared vanilla assets.

### 5.12 Settlement combat and battle tactics

Original settlement combat can initiate mission combat from dialogue, spawn defenders, track sides, handle player knockout/escape, apply post-combat events and settlement penalties. Battle tactics can ask AI commanders for formation orders and execute them during a mission.

**Port decision:** these are mission-authority features, not ordinary campaign actions. First stage is advice text only. Later, orders may be issued only by the mission host/server to formations the actor is authorized to command. Settlement combat requires a dedicated Coop mission launch/closure adapter, deterministic participant roster and authoritative post-combat reconciliation. It is blocked until normal Coop battle and siege paths are stable under the same game version.

### 5.13 Client presentation and editor systems

Original client features include TTS/lip sync, STT, dialogue/event images, Memory Book and world-event windows, map notifications and a content editor/server.

**Port decision:**

- TTS/STT/images are client opt-in presentation features;
- server sends bounded text and optional presentation metadata, never credentials;
- STT output enters the same explicit text confirmation path as typed input;
- generated image/audio files have type/size/path validation and cannot contain executable content;
- content editor is an authenticated admin tool, not reachable by ordinary clients;
- editor changes create versioned content packs and require validation/reload, never direct arbitrary writes into live state;
- world-info/RAG ingestion treats all files as untrusted content, not instructions.

## 6. Coop capability and compatibility map

### 6.1 Existing paths that can be adapted after runtime proof

| State/action | Coop evidence | Proposed AIPort use | Remaining proof |
|---|---|---|---|
| War | `NetworkDeclareWar`, server/client stance handlers | server invokes native authoritative war adapter | two clients, reconnect, save/load |
| Peace | `NetworkMakePeace` includes tribute/duration/detail | native peace adapter; avoid duplicating custom tribute | state and payment semantics |
| Hero/player relation | relation change messages/handlers | capped relation commit | arbitrary hero-pair coverage and JIP |
| Hero gold | give-gold messages and Hero gold synchronization | atomic debit/credit adapter | sender/recipient ownership, negative values |
| Clan influence | change-influence messages | capped authorized delta | player clan vs AI clan semantics |
| Item roster | item-roster registry/update handlers | atomic transfer between registered rosters | modifiers, capacity, rollback and UI |
| Troop/prisoner roster | roster handlers and map-event roster messages | atomic roster transfer | owner authority and prisoner edge cases |
| Settlement ownership | ownership handler/message | territory transfer adapter | clan/kingdom consistency and decisions |
| Party behavior | server party behavior update paths | task step projection | AI ownership, conflict and pathing |
| Party lifetime | create/destroy messages exist | only after definition/lifetime proof | JIP, references and cleanup |
| Siege/raid/battle | extensive server launch/state messages | dedicated late-phase adapter | full mission lifecycle and host authority |
| Romance/marriage | romance request messages and marriage patches | final native commit after consent | multiplayer hero mapping and family sync |
| Save lifecycle | save-state, game-saved/loaded and object-registration handlers | external save barrier integration | exact hook ordering and failure injection |

The existence of a message does not grant AIPort permission to send it. Some messages are client requests, some are internal projection, and some assume a specific UI or decision flow. Each adapter needs a runtime test proving the correct authoritative entry point.

### 6.2 Custom AIPort projection required

Coop does not natively represent these AIInfluence concepts as complete replicated state:

- alliances distinct from native war/peace stances;
- trade agreement metadata and expiration;
- custom tribute/reparation demands and history;
- kingdom statement queues and response pressure;
- custom kingdom taxes if not represented by a native policy;
- dynamic events and economic-effect records;
- disease/infection/quarantine state;
- persistent NPC social state and Memory Book;
- AI-owned quests and their logs;
- pending letters/initiative invitations;
- task metadata and audit records;
- RP item metadata not backed by a predeclared game object;
- generated image/audio metadata.

These need AIPort capability negotiation, server snapshots, ordered deltas, a state revision and join-in-progress replay.

### 6.3 Blocked or late-phase areas

- arbitrary map movement commands from model text;
- unbounded party creation/destruction;
- direct raid/siege/map-event start;
- scripted death;
- mission combat launch from dialogue;
- battle order execution;
- generated heroes/items without a client definition registry;
- workshop ownership/sale until exact Coop ownership synchronization is confirmed;
- any action relying on swapping `Hero.MainHero`.

## 7. Target architecture

```text
Explicit player/NPC trigger
  -> authoritative context snapshot
  -> bounded backend request
  -> strict JSON response parser
  -> narrative response + zero/one typed intent proposal
  -> schema validation
  -> server ID resolution against prompt-time allowlist
  -> capability/feature-flag check
  -> actor and affected-owner authorization
  -> deterministic precondition evaluation
  -> optional player/admin confirmation
  -> revision recheck and ordered entity locks
  -> game-thread command adapter
  -> authoritative projection/broadcast
  -> audit and persistence commit
  -> safe user-facing result
```

### 7.1 Components

#### `AIResponseParser`

- extracts spoken response independently from proposed intent;
- rejects invalid JSON rather than repairing it into an action;
- enforces `additionalProperties: false`, depth/size/action-count limits and canonical enum names;
- never logs hidden thoughts or credentials;
- returns narrative even when an intent is rejected, if the narrative itself is safe.

#### `IntentRegistry`

For each canonical intent, stores:

- schema version;
- required/optional fields and numeric bounds;
- feature flag;
- risk class;
- authorization rule;
- required capabilities;
- precondition evaluator;
- command adapter;
- projection and persistence strategy;
- confirmation policy;
- idempotency scope.

#### `AuthorizationService`

Resolves permissions from the peer and current campaign state, never from IDs claimed by the client/model. Roles include ordinary player, party owner/leader, clan leader, kingdom ruler, mission commander and server administrator. AI NPC autonomy is a separate server policy, not a user role.

#### `IntentCoordinator`

Owns request/intent state, idempotency, expected revisions, expiration, ordered entity locks and cancellation. It serializes conflicts such as two proposals spending the same gold or controlling the same party.

#### `CommandAdapters`

Small audited server-only adapters around proven native/Coop operations. They return a typed receipt containing before/after values, touched entities and projection status. They do not call the backend and do not parse prose.

#### `AIPortStateStore`

Owns custom social, memory, diplomacy, event, disease, quest, task and presentation metadata. It exposes immutable snapshots to prompt construction and commits only through the transaction coordinator/save barrier.

#### `ProjectionService`

- uses proven native Coop synchronization for native state;
- sends AIPort deltas for custom state;
- provides filtered snapshots for reconnect/join-in-progress;
- repairs a client from a server snapshot if a delta is missed;
- never sends another player's private dialogue or memory scope.

#### `AuditService`

Writes structured records for proposal, rejection, approval, commit and recovery. Store IDs, action type, revisions, bounded reason codes and before/after hashes; do not store API keys, authorization headers, full prompts by default or private hidden thoughts.

### 7.2 State scopes

Use explicit scopes to prevent multiplayer leakage:

1. **Conversation volatile:** `(peerId, playerHeroId, targetId, conversationId)`; cleared on end/disconnect.
2. **Player-NPC dyad:** `(campaignId, playerHeroId, npcId)`; relationship, memories and private correspondence.
3. **Group/observation:** event visible to a defined participant/audience set.
4. **NPC shared identity:** stable server-authored profile and public biography facts.
5. **World shared:** diplomacy, events, diseases, quests with public visibility, tasks and committed campaign facts.
6. **Admin/private system:** audit, provider status, content-pack metadata and recovery information.

A private dyad summary is never promoted to shared/world scope merely because an LLM mentions it.

## 8. Intent transaction lifecycle

### 8.1 States

```text
Received
  -> Parsed | Rejected
  -> Proposed
  -> AwaitingConsent | Authorized | Rejected | Expired
  -> Prepared
  -> Committed | Failed
  -> Projected
  -> Persisted
```

A recovery record may end in `Compensated`, `ProjectionPending` or `PersistencePending`; those are operational states, not permission to repeat the gameplay consequence.

### 8.2 Identity and idempotency

- `IntentId` is server-issued.
- Idempotency key is `(campaignGeneration, requestId, intentOrdinal)`.
- A duplicate returns the previous receipt or pending status.
- Confirmation references the exact `IntentId`, expected state revision and digest of affected entities/values.
- Confirmation expires quickly and is invalidated on conversation end, disconnect, target loss, save generation change or relevant revision change.

### 8.3 Concurrency

- Narrative-only requests may be concurrent within provider limits.
- Stateful intents acquire locks by canonical sorted keys such as `hero:<id>`, `party:<id>`, `settlement:<id>`, `kingdom:<id>`.
- Never hold an entity lock while waiting for an LLM or player confirmation.
- Re-read and validate state after locks are acquired.
- One party may have one movement controller/task at a time.
- One diplomatic pair has one active negotiation state machine per topic.
- Daily schedulers use deterministic server ordering and do not race dialogue commits.

### 8.4 Commit and failure rules

1. Prepare a receipt with expected before values.
2. Execute on the campaign game thread.
3. Verify authoritative after values.
4. Publish native/custom projection.
5. Append audit and mark custom state dirty.
6. Flush at the next safe persistence point or immediately for critical custom state.

For irreversible native operations, “rollback” means preventing unsafe execution through last-moment validation and preserving an audit/recovery path; do not pretend every Bannerlord action can be atomically undone. Custom AIPort state should use copy-on-write transactions and be genuinely reversible until commit.

If projection fails after commit, do not repeat the consequence. Mark projection pending and repair clients from the authoritative snapshot.

### 8.5 Chained actions

- Phase 1: no state-changing `ThenActions`.
- Later: only a server-authored workflow may chain at most three typed steps.
- Each step has explicit dependencies and compensation policy.
- The model cannot create loops, arbitrary graph edges or a raw command fallback.

## 9. Proposed canonical intent schemas

These are new safe schemas, not compatibility promises for the original raw parser.

### 9.1 Common envelope

```json
{
  "schema": "aiport.intent.v1",
  "type": "transfer_gold",
  "actorId": "server-resolved-or-omitted",
  "targetIds": ["allowed-id"],
  "parameters": {},
  "reason": "bounded player-facing explanation"
}
```

Rules:

- `actorId` is ignored unless it matches the server-selected speaker; preferably omit it from the model schema;
- target IDs must come from a small prompt-time allowlist and be resolved again on commit;
- no negative or floating monetary quantities;
- reason has a short length cap and never changes authorization;
- unknown keys reject the intent;
- one response initially carries zero or one intent.

### 9.2 Action policy matrix

| Canonical intent | Required typed data | Authorization/consent | Reversibility | Initial phase |
|---|---|---|---|---|
| `leave` | conversation ID | current speaker/player | UI-only | early |
| `relation_delta` | NPC, player/hero, bounded delta, reason | server policy; per-day cap | compensatable | early |
| `social_patch` | dyad, enum state, bounded value | server policy; no native mutation | reversible | early |
| `record_claim` / `record_lie` | speaker, claim text/hash, audience | observation only | reversible | early |
| `transfer_gold` | source, destination, positive amount | source owner; confirm player loss | receipt/compensation possible | medium |
| `transfer_item` | rosters, item+modifier, count | source owner; confirm player loss | atomic inverse if unchanged | medium |
| `transfer_troops` | source/destination parties, roster deltas | party owners/leader | high-risk compensation | medium/late |
| `quest_create` | giver, targets, duration, supported predicate, reward | affected player accepts | custom state reversible | medium |
| `quest_update` | quest ID, progress/log | server predicate or admin | reversible | medium |
| `quest_complete/fail` | quest ID, reason, reward receipt | server predicate | reward may be irreversible | medium |
| `diplomacy_proposal` | topic, parties, terms, expiry | ruler/AI policy | reversible | medium |
| `diplomacy_accept/reject` | negotiation ID | authorized ruler; affected-player consent | accept may mutate native state | late |
| `party_task_create/cancel` | party, typed steps, limits | party owner/leader | cancelable, side effects vary | late |
| `mission_navigate/talk/follow` | mission actor/target | mission authority | transient | late |
| `combat_surrender/release` | encounter ID and side | affected controllers | often irreversible | late |
| `romance_change` | dyad and bounded delta | opt-in policy | reversible | late |
| `marriage_proposal` | two heroes | both affected players/NPC policy | proposal reversible | late |
| `marriage_commit` | proposal ID | mutual consent + native checks | difficult to reverse | very late |
| `intimacy` | dyad, consent token | explicit mutual consent | consequences may be irreversible | very late |
| `settlement_combat` | settlement, sides, trigger | mission/server authority | difficult | very late |
| `death` | target, native cause context | admin policy + protections | irreversible | last/default off |

Workshop buy/sell, unique-character creation and RP-item creation need separate schemas after their Coop registries are designed; they must not be overloaded into `transfer_item`.

## 10. Diplomacy design

### 10.1 Separate native and custom state

**Native authoritative state:** war/peace stance, kingdom/clan membership, settlement ownership, gold/influence and any native decisions.

**AIPort custom state:** alliance metadata, trade agreements, pending demands, tribute/reparation history not represented natively, statement queues, engagement/pressure, fatigue snapshots, tax policy metadata, negotiation/audit records.

Never duplicate a native fact as an independently mutable custom truth. Custom records reference native state and reconcile on load/daily tick.

### 10.2 Negotiation state machine

```text
Draft
  -> Proposed
  -> Delivered
  -> Accepted | Rejected | Expired | Invalidated
  -> NativeCommitPending
  -> Committed | Failed
```

Each proposal records:

- stable negotiation ID and topic;
- source/target kingdoms and authorized rulers at creation;
- normalized terms;
- campaign-day creation/expiry;
- expected native stance/ownership revision;
- proposer source (player, NPC scheduler, admin, dynamic event);
- statements and bounded reasons;
- approvals/consent;
- commit receipt.

Leadership change, kingdom destruction, war-state change, settlement ownership change or save-generation change may invalidate a proposal.

### 10.3 Per-action rules

- **DeclareWar:** native stance adapter; cooldown; no duplicate existing war; player-led affected kingdom requires ruler action/confirmation; AI-AI autonomy is configurable and rate-limited.
- **Peace:** native peace adapter; terms validated; use native tribute fields only if their semantics are confirmed, otherwise keep custom tribute separate to avoid double charging.
- **Alliance:** custom symmetric relation with canonical kingdom pair, start/end/reason and constraints against active war. Clients receive snapshots/deltas.
- **Trade agreement:** custom symmetric agreement with expiry and explicit deterministic effects. Narrative-only until an economic model is specified and tested.
- **Tribute:** pending demand then accepted schedule; positive bounded daily amount/duration; one transfer receipt per day; insufficient-funds policy is explicit (arrears, suspend or terminate), never invented at runtime.
- **Reparations:** pending demand then one-time capped payment; exact balance rechecked at commit.
- **Territory/fief:** native ownership adapter; verify current owner, destination clan/kingdom, no active ownership transaction and no protected settlement; affected player ruler confirms.
- **Expel clan:** prefer native kingdom decision flow where required; direct expulsion is blocked until Coop decision and membership synchronization are proven.
- **Tax policy:** custom policy only if a deterministic model applies it consistently on the server. Clamp rate and scope; clients display projected policy. Avoid stacking the same penalty on load/tick.
- **Quarantine:** delegated to disease subsystem; diplomacy may propose it but disease policy commits it.
- **War fatigue/statistics:** derived read-only inputs first. If custom pressure influences AI decisions, store and version it but never override native state directly.
- **Kingdom statements/player statements:** narrative records and proposals only. A statement cannot mutate diplomacy without a separate accepted intent.

### 10.4 Diplomacy rollout

1. Read-only dashboard/snapshot generated from native state.
2. LLM statements in shadow mode; log the proposal but do not create negotiations.
3. Custom negotiation records with manual admin acceptance and no native mutation.
4. Single-connected-player proposal/accept/reject UI and persistence for NPC-to-player workflows.
5. Enable one native adapter at a time: war, then peace, then settlement transfer.
6. Add custom alliances/trade agreements.
7. Add tribute/reparations/tax effects only after daily scheduler and crash recovery tests.
8. Consider AI-AI autonomous commits last, with strict global/day budgets.

## 11. Dynamic event and economy design

### 11.1 Event lifecycle

```text
GeneratedDraft -> Validated -> Scheduled -> Active -> Updated -> Resolved/Expired -> Archived
```

- Draft contains narrative, category, suggested participants and an allowlisted effect template ID.
- Validator resolves all entities and clamps importance/duration.
- Materializer computes exact effects deterministically from current state and template bounds.
- Daily tick applies idempotent deltas keyed by `(eventId, effectId, campaignDay)`.
- Resolution removes ongoing modifiers and records the final event.

### 11.2 Effect implementation

Prefer model/query adapters for ongoing modifiers over destructive repeated writes. If native values must change each day, record the exact applied delta receipt so a duplicate tick cannot apply twice.

Each effect contains:

- source event and effect IDs;
- target scope and resolved IDs;
- start/end campaign day;
- capped deltas/multipliers;
- stacking group and combination rule;
- last applied day;
- active/resolved state;
- human-readable reason.

### 11.3 Generation budgets

- global maximum active events;
- per-category and per-settlement cooldowns;
- maximum affected settlements/kingdoms;
- maximum duration and magnitude;
- no event generated solely to satisfy an LLM suggestion if the world preconditions fail;
- no direct lethal outcome in the event engine;
- generation disabled during save barrier, campaign load, battle finalization and state recovery.

## 12. Persistence and save-generation binding

### 12.1 Server-only layout

```text
E:\BCOOP\data\AIInfluence\campaigns\<Campaign.UniqueGameId>\
  manifest.json
  state\
    social.json
    diplomacy.json
    events.json
    diseases.json
    quests.json
    tasks.json
    npcs\<npc-id>.json
    players\<player-hero-id>.json
  snapshots\<coop-save-generation>\
  transactions\audit.ndjson
  blobs\images\
  tmp\
```

Use sanitized stable IDs or hashed filenames; never concatenate raw model/player text into a path.

### 12.2 Manifest

Required fields:

- state schema version;
- AIPort protocol/build compatibility range;
- campaign unique ID;
- Coop save slot/name and generation identifier;
- campaign day and authoritative state revision;
- commit ID and parent commit ID;
- file list, sizes and SHA-256 hashes;
- content-pack version/hash;
- last successful flush UTC;
- clean/dirty shutdown marker;
- migration history;
- optional matching snapshot ID.

### 12.3 Save barrier

1. Receive Coop save-start signal and reject new state-changing intents with a retryable `save_in_progress` result.
2. Cancel/await backend work that has not produced a proposal; do not hold the save for a provider call.
3. Let already committed game-thread commands finish; expire unconfirmed proposals or persist them explicitly as non-authoritative pending records.
4. Freeze custom-state revision.
5. Flush the in-memory transaction queue.
6. Serialize each file to `tmp` with deterministic ordering.
7. fsync/close, hash and validate the temporary files.
8. atomically replace data files where supported.
9. write the final manifest last with the new Coop save generation.
10. only then acknowledge/release the external-state barrier.

The exact integration point with Coop `GameSaveStateChanged`, `GameSaved`, `GameLoaded` and `AllGameObjectsRegistered` must be runtime-proven before writes are enabled.

### 12.4 Load and recovery

- Load no external mutation state before the campaign and registered objects are available.
- Match campaign ID and exact save generation.
- Validate hashes and schema before resolving object IDs.
- If the current generation has no valid commit, restore only a matching snapshot/parent; otherwise start AIPort in read-only recovery mode.
- Quarantine orphan IDs and report them; do not silently remap by display name.
- Never load “latest folder” from another save.
- Migrations are pure, versioned and backup the pre-migration state.

### 12.5 Join-in-progress

1. Client and server negotiate capability/schema versions.
2. Server captures snapshot revision `R`.
3. Send public world/custom state in bounded chunks.
4. Send only that player's private/dyad state.
5. Buffer deltas newer than `R`.
6. Client validates all chunks and atomically activates the snapshot.
7. Replay buffered deltas in order.
8. On a gap/hash mismatch, discard and request a fresh snapshot.

No client is required to possess provider credentials, full prompts, hidden thoughts, private memories of another player or server audit logs.

## 13. Protocol evolution

Protocol `1` stays unchanged for the current narrative runtime. Future implementation should add capability-negotiated messages rather than overloading dialogue result text.

Conceptual message families:

- capabilities request/response;
- intent proposal/status/confirmation/decision/result;
- custom-state snapshot request/header/chunk/complete;
- ordered state delta and snapshot-required error;
- notification/letter/quest/diplomacy presentation DTOs;
- admin-only feature status and recovery summaries.

Common fields should include protocol/message schema version, request/correlation ID, campaign generation, state revision and bounded reason code. Large state uses chunk limits and content hashes.

Rules:

- maintain a protobuf field-number ledger in `docs/PROTOCOL.md` before implementation;
- never reuse removed numbers;
- unknown message or intent types fail closed;
- capability response determines which UI/actions a client may expose;
- old clients remain narrative-only or are rejected clearly if a required state schema cannot be rendered safely;
- no TaleWorlds objects, file paths, API keys, prompts or arbitrary type names cross the protocol;
- action confirmation is a server-issued token bound to peer, player hero, intent, values, revision and expiry;
- client acknowledgements never substitute for authoritative commit receipts.

## 14. Multiplayer authorization and consent

### 14.1 Actor resolution

- Conversation speaker comes from the authoritative conversation target.
- Player actor comes from peer/controller mapping.
- Party/clan/kingdom authority comes from current campaign ownership/leadership.
- A model-provided actor ID cannot widen these scopes.
- AI-AI autonomous actions use a dedicated server policy principal and global budgets.

### 14.2 Affected-player rules

| Consequence | Minimum rule |
|---|---|
| Player loses gold/item/troops | explicit owner confirmation with exact amount/items |
| Player gains benign reward | server validation; notification; optional confirmation for capacity |
| Player party movement/task | owner/leader approval; visible cancel control |
| Player kingdom war/peace/fief | authorized ruler and affected-player policy |
| Surrender/release/imprisonment | affected controller approval or native encounter flow |
| Romance proposal | recipient can accept/reject without penalty outside explicit social rules |
| Marriage/intimacy | explicit mutual consent and current eligibility |
| Player hero death | prohibited by default |
| Admin content/effect change | authenticated admin channel and audit |

Consent tokens are single-use and cannot be inferred from ordinary dialogue text. “Yes” in a vanilla dialogue or generated response is not a protocol confirmation unless the dedicated confirmation UI/action submitted it.

### 14.3 Shared pause

The current explicit AI options request Coop-wide pause as required. For stateful future systems:

- do not hold an entity lock or save barrier during the pause or backend request;
- rate-limit who can repeatedly request pause;
- expose the current pause requester/reason if Coop UI permits;
- letters, background initiatives and event generation never pause automatically;
- do not auto-unpause; shared pause remains server-authoritative and bounded.

## 15. Feature-by-feature implementation plan

### 15.1 Persistent memory and Memory Book — early

1. Define truth/observation/opinion/summary record types and source references.
2. Persist only committed dialogue turns and world observations after cancellation has lost the commit race.
3. Summarize asynchronously; summary failure cannot delete source records.
4. Scope private dyad memory by actual player hero and NPC.
5. Add retention, compaction and deletion policies.
6. Add read-only Memory Book snapshot UI; images later.
7. Prove single-player privacy, reconnect, JIP, save/load and rollback.

### 15.2 Social state and relations — early to medium

- Keep custom trust/emotional/escalation state separate from native relation.
- `claim` and `lie` create subjective observations, not world facts.
- Relation changes use a per-interaction and per-day cap and cannot be emitted repeatedly from regenerated output.
- Every relation delta has a reason code and before/after receipt.
- Do not let private social state leak into another player's prompt.

### 15.3 Gold, items, troops and workshops — medium

- Implement transfers as balanced atomic transactions: exact debit and credit, no minting/loss unless the schema explicitly defines a reward/sink.
- Resolve roster IDs from authoritative ownership; reject missing modifiers/capacity/availability.
- Confirm any player-owned loss.
- Deduplicate by intent ID and include a receipt in audit/UI.
- Troops/prisoners need party ownership and roster invariant checks.
- Workshop sale/purchase remains blocked until ownership and economic UI synchronization are source- and runtime-proven.

### 15.4 Quests — medium

- Start with AIPort-owned narrative quests and manually verifiable completion.
- Then add allowlisted predicates such as talk to NPC, reach settlement, possess item, pay amount or defeat a tracked party if each event can be observed authoritatively.
- Reward through a separate capped transaction after completion commit.
- Giver/target death, kingdom destruction and save migration have explicit fail/retarget policies.
- JIP sends only quests visible to the joining player.

### 15.5 Diplomacy — medium to late

Follow section 10. Statements and shadow proposals first; native mutations one adapter at a time. Never let one LLM response both invent terms and commit war/peace without an independently authorized state transition.

### 15.6 Dynamic events/economy — late

- Begin with read-only world events generated from existing campaign logs.
- Next add narrative-only synthetic events.
- Add deterministic effect templates only after persistence/JIP.
- Daily effect application is idempotent and bounded.
- Event images are client presentation and may fail without affecting event state.

### 15.7 Diseases/quarantine — late

- Build deterministic epidemiology with a seeded server RNG stream or recorded rolls.
- Store disease instances, targets, stages and last processed day.
- Project status summaries to clients; server owns all effects.
- Integrate quarantine with settlement entry and economy through dedicated adapters.
- Death chance remains disabled until lethal action policy is complete.

### 15.8 NPC initiative, letters and messengers — medium/late

- Scheduler chooses eligible NPCs using authoritative cooldown/state.
- Background generation has a global queue and cannot starve player dialogue.
- Letters have sender/recipient, sent/arrival campaign day, delivery status and cost receipt.
- Recipient explicitly opens/accepts an initiative; no automatic pause or forced conversation.
- Map approach behavior is a separate party task, not implied by a letter.

### 15.9 Group and ambient conversations — medium/late

- Mission-local server/host coordinator owns participant list and speaking turn.
- Cap participants, turns and directed replies.
- Record only speech perceivable by each audience.
- Ambient speech is cosmetic by default; it cannot mutate relationship/world state without a separate observed-event rule.
- TTS queues are client-local and failure-tolerant.

### 15.10 Party tasks — late

- One task controller per party; task issuer/owner and cancellation rights are explicit.
- Persist current step, target, start/deadline, pathing result and last transition.
- Revalidate at each step and after load.
- Use only proven Coop server behavior APIs; never call raw `SetMove*` from an HTTP worker.
- Attack/raid/siege steps transition through native encounter/siege managers, not direct campaign mutation.
- On invalid target, stop safely and notify rather than selecting a new target from prose.

### 15.11 Romance, marriage and intimacy — very late

- Romance narration and custom level can ship separately behind opt-in.
- Decay is deterministic and generation-bound.
- Proposals create pending records; acceptance/rejection is dedicated UI/protocol.
- Native marriage is a final server adapter after mutual consent and eligibility checks.
- Intimacy/conception is separately gated, never inferred from model text and never required for feature parity.

### 15.12 Unique characters, party members and RP items — very late

- Prefer templates built from existing synchronized game assets.
- Introduce a content-definition manifest and stable IDs before instances.
- Server validates definitions; clients receive/cache the same content hash.
- Failed client definition load blocks the instance projection rather than creating a broken save.
- Purge/archive tools must preserve references in memories, quests and audit.

### 15.13 Battle tactics and settlement combat — very late

- Phase 1: non-authoritative tactical advice.
- Phase 2: authorized orders to the requesting player's formations only.
- Phase 3: AI ally/enemy commanders under mission-server policy.
- Settlement combat needs a dedicated launch token, side/roster snapshot, mission result and idempotent campaign reconciliation.
- Never allow a dialogue response to spawn agents or apply casualties directly.

### 15.14 Death — last/default off

- Prefer deaths produced by native battle/campaign systems.
- Direct role-play death requires a dedicated admin-level policy, protected-target checks and explicit cause/audit.
- All dependent quest, clan, party, leadership, marriage, memory and projection handlers must pass before enabling.

### 15.15 TTS, STT, images, UI, editor and RAG — parallel presentation track

- Can progress independently because they do not own campaign state.
- Keep backend keys server-only; clients may use local providers only by explicit personal configuration.
- Sanitize markup and paths; bound text/audio/image sizes and cache quotas.
- STT transcript is shown for player confirmation before it becomes explicit AIPort input.
- Content packs are signed/hashed or admin-approved, versioned and loaded read-only during a campaign session.
- RAG returns text excerpts and source IDs only; retrieved content cannot alter system policy or invoke actions.

## 16. Feature flags and rollout policy

Every flag defaults to `false` except already-proven narrative dialogue. Suggested independent flags:

```text
EnablePersistentMemory
EnableMemoryBookUi
EnableSocialState
EnableNativeRelationActions
EnableGoldTransfers
EnableItemTransfers
EnableTroopTransfers
EnableQuestSystem
EnableDiplomacyStatements
EnableDiplomacyProposals
EnableDiplomacyMutations
EnableAllianceState
EnableTradeAgreementState
EnableTributeAndReparations
EnableKingdomTaxes
EnableDynamicEventsNarrative
EnableDynamicEventEffects
EnableDiseaseSystem
EnableNpcInitiative
EnableGroupConversations
EnableAmbientConversations
EnablePartyTasks
EnableRomanceState
EnableMarriageActions
EnableIntimacy
EnableUniqueCharacters
EnableRpItems
EnableBattleAdvice
EnableBattleOrderExecution
EnableSettlementCombat
EnableDirectDeathAction
EnableTts
EnableStt
EnableGeneratedImages
EnableAdminContentEditor
```

Each mutation flag also has:

- `ShadowOnly` mode;
- per-player/AI-AI/admin policy;
- global and per-entity rate limits;
- numeric caps and cooldowns;
- required capability set;
- audit level;
- emergency kill switch that stops new proposals without deleting state.

## 17. Testing strategy

### 17.1 Test layers

1. **Static/source invariants:** no original DLL reference, no raw command parser, no client mutation, no secret path, unique protobuf numbers.
2. **Schema/property tests:** malformed JSON, unknown fields, fabricated IDs, overflows, negative values, excessive depth/chains and fuzzed Unicode.
3. **Deterministic fake backend:** narrative-only, valid intent, invalid intent, timeout, duplicate, late response and prompt injection.
4. **Command-adapter unit tests:** before/after receipts, exact caps, precondition changes and idempotent duplicates.
5. **Disposable headless server:** campaign game-thread execution, native projection and save hooks.
6. **Single-connected-player runtime tests:** authoritative identity, NPC-to-player consent routing, privacy, duplicate/conflict serialization, shared pause and reconnect resistance.
7. **Save/load/JIP:** every custom state and pending workflow.
8. **Failure injection:** crash between prepare/commit/projection/persistence, corrupt file, missing object, dropped delta and migration failure.
9. **Soak:** daily/hourly schedulers, many NPCs/events, provider outages and repeated reconnects.

### 17.2 Mandatory single-connected-player matrix for every stateful feature

| Scenario | Required result |
|---|---|
| One player talks to different NPCs | isolated narrative/private state by NPC and conversation |
| Same player repeats or overlaps a request | defined memory scope; stateful commits serialized |
| Duplicate request/confirmation | one consequence, same receipt |
| Stale or mismatched confirmation/cancel | rejected |
| Disconnect while proposed | expires or remains safely pending by policy |
| Disconnect during commit | server completes once; reconnect gets receipt/snapshot |
| Reconnect | no stale UI; authoritative state restored |
| Join in progress | valid filtered snapshot plus ordered deltas |
| State changes before confirmation | stale proposal rejected |
| Save during backend request | no mutation/save deadlock |
| Save during committed transaction | generation contains a consistent commit |
| Crash before native commit | no consequence; safe retry/recovery |
| Crash after native commit before projection | no repeat; snapshot repairs client |
| Corrupt/mismatched external state | read-only recovery, never wrong-generation load |
| Feature flag disabled mid-session | no new proposals; existing committed state remains readable |
| Malicious prompt/player text | cannot select unauthorized actor/ID/action |

### 17.3 Acceptance gates

- **G0 Research/design:** this document complete; no runtime change.
- **G1 Foundation:** transaction coordinator, intent registry, audit and capability handshake tested with a no-op intent.
- **G2 Persistence:** empty/read-only state store passes save/load, mismatch and crash recovery.
- **G3 Single connected player:** narrative isolation, authoritative identity, reconnect/JIP and foundation messages pass on one real client.
- **G4 Shadow mode:** proposals are parsed/validated/audited but cannot mutate.
- **G5 One adapter:** a single low-risk action passes all matrices and rollback.
- **G6 Stateful subsystem:** persistence, JIP and projection proven for that subsystem.
- **G7 Opt-in pilot:** bounded live test with kill switch and documented rollback.

No feature skips directly from source research to G7.

## 18. Phased roadmap and dependencies

### Phase 0 — now: research only

- Preserve `0.0.37` deployment.
- Complete this design and source inventory.
- Do not change protocol, save format or gameplay code.

### Phase 1 — single-connected-player narrative proof

- authoritative identity and private-memory isolation;
- bounded provider queueing;
- cancel ownership;
- reconnect/JIP for the same player;
- shared pause behavior.

### Phase 2 — safety foundation

- capability negotiation;
- no-op typed intent parser/registry;
- transaction states/idempotency/entity locks;
- audit records;
- feature-flag and authorization framework;
- no mutations.

### Phase 3 — generation-bound persistence

- manifest and empty state files;
- save barrier and matching-generation load;
- snapshots, corruption handling and JIP snapshot transport;
- still no LLM-driven mutations.

### Phase 4 — memory/social/read-only world

- persistent dyad memory and Memory Book;
- observations and read-only diplomacy/event projections;
- capped custom social state;
- optional client presentation track.

### Phase 5 — low-risk transactional actions

- native relation delta;
- capped gold transfer;
- item transfer;
- AIPort quest proposals and manual completion.

Enable one adapter per build/test bundle, not all together.

### Phase 6 — diplomacy shadow mode and proposals

- statements, fatigue/statistics inputs and negotiation records;
- connected-player authorization/consent for NPC-to-player proposals;
- save/load/JIP;
- zero native diplomacy mutations at first.

### Phase 7 — diplomacy commits

- war then peace;
- territory transfer only after ownership proof;
- custom alliance/trade state;
- tribute/reparations/taxes last.

### Phase 8 — dynamic events and diseases

- narrative events;
- deterministic economic templates;
- disease simulation/quarantine after event state is stable.

### Phase 9 — initiative, groups and tasks

- letters and opt-in initiative;
- group/ambient conversations;
- mission-local navigation;
- server party tasks, with raid/siege last.

### Phase 10 — high-risk role-play systems

- romance/marriage/intimacy;
- unique characters/party members/RP items;
- battle orders/settlement combat;
- direct death remains final and may stay permanently disabled.

Dependency summary:

```text
Single-connected-player proof
  -> capabilities + transaction/audit
  -> generation-bound persistence + JIP
  -> persistent memory/custom state
  -> low-risk adapters
  -> diplomacy proposals
  -> diplomacy commits
  -> events/economy
  -> diseases and long-running tasks
  -> combat/creation/marriage/death
```

## 19. Security and prompt-injection boundary

Treat all of the following as untrusted content: player text, model output, retrieved RAG chunks, world-info/content-editor files, memory summaries, event descriptions and NPC letters.

Required defenses:

- separate policy/schema from content in prompts;
- never follow instructions contained in retrieved content;
- server-generated target allowlists and canonical enum values;
- strict JSON parser with no type-name deserialization;
- numeric/string/list/depth bounds;
- no arbitrary file paths, URLs, reflection, shell, C# or template execution;
- no raw `ParseAndExecuteCommand` compatibility mode;
- no secrets in prompts returned to clients or logs;
- no action based on internal thoughts;
- narrative can claim an action occurred only after the server returns a committed receipt; otherwise the response must use proposal/future tense or a safe rejection follow-up;
- content editor access is separate from ordinary game networking and requires admin authentication;
- audit redaction and retention are explicit.

## 20. Rollback and operational recovery

For every feature milestone:

1. make a source/docs backup before edits;
2. preserve current deployed client/server binaries and hashes;
3. build and stage separately;
4. test on `artifacts\runtime-m0` and disposable save only;
5. record source and deployment rollback paths;
6. verify client/server hashes;
7. keep the live campaign untouched until acceptance gates pass;
8. expose a kill switch that blocks new proposals;
9. keep readers/migrations capable of showing existing state even when mutation flags are off;
10. never delete state as part of disabling a feature.

If custom state is corrupt or mismatched, disable mutations and load a matching verified snapshot. If native state committed but AIPort custom projection did not, reconcile from native truth and the commit receipt; do not blindly replay the command.

## 21. Explicit non-goals for the next implementation cycle

- no original `AIInfluence.dll` or analysis-clean DLL deployment;
- no port of the original `SubModule` lifecycle;
- no direct use of legacy raw action commands;
- no protocol field allocation during this planning milestone;
- no new writable campaign files yet;
- no diplomacy/action/event/disease implementation before single-connected-player narrative proof;
- no client-side authority;
- no unreviewed automatic AI-AI war, peace, fief transfer or taxation;
- no direct death, marriage, intimacy, party creation, raid, siege, settlement combat or battle-order execution;
- no model-quality tuning treated as a substitute for validation/authorization.

## 22. Recommended immediate next work

Use the disposable runtime with one connected player only:

1. run the bundled identity/memory/cancel/shared-pause and reconnect/JIP test;
2. document the result without adding player-to-player workflows;
3. keep typed intent/capability DTOs server-authoritative;
4. keep transaction validation/audit default-deny and idempotent;
5. validate save-generation binding and the save barrier;
6. do not enable any original feature family until those foundations pass.

## 23. Research file map

Primary original reference files include:

- `AIResponse.cs`, `AIDecisionHandler.cs`;
- `ResponseActions\AIResponseActionTypes.cs`, parser, normalizer, validator, executor and pipeline;
- `Behaviors\AIActions\*` and `TaskSystem\*`;
- `Diplomacy\DiplomacyManager.cs`, storage/persistence and each alliance/trade/tribute/reparation/territory/fatigue/tax subsystem;
- `DynamicEvents\*`, including economic effects and settlement penalties;
- `Diseases\*`;
- `NPCInitiativeSystem.cs`, letters/messenger and group-conversation systems;
- `MemoryBook\*`, `WorldInfoManager.cs`, `RagSystem\*`;
- quest, marriage, intimacy, death history, unique-role-character, player-party-member and RP-item systems;
- settlement combat, battle tactics, TTS/STT, dialogue images, world-event UI and content editor.

Project summaries remain in:

- `docs\ARCHITECTURE.md`;
- `docs\ACTION_MATRIX.md`;
- `docs\PERSISTENCE.md`;
- `docs\PROTOCOL.md`;
- `docs\COOP_COMPATIBILITY_ANALYSIS.md`.

This file is the master design for future feature parity; the smaller documents remain implementation-specific summaries and must be updated when a phase moves from design to code.

## Implementation update — 0.0.47-dev

Phases 2 and the initial private-memory portion of phases 3–4 now exist in a cumulative protocol-2 foundation: capability negotiation, no-op intent validation/audit, generation-bound atomic state files, private snapshots and bounded persistent dyad dialogue records. This does not authorize gameplay mutations. Runtime JIP and completed-save reload remain acceptance gates before social or native adapters.

## Implementation update — 0.0.97-dev

The first bounded slice of phase-9 NPC initiative is now implemented in source as a default-off shadow-only scheduler. It selects diplomatic war/peace proposals deterministically from server-owned campaign snapshots, applies authority/precondition checks, writes durable proposal provenance and queues offline recipients. It does not call an LLM or any native campaign mutation.

The diplomacy presentation layer now has a typed private multi-item inbox with bounded revision-pinned pagination and exact map-notification reconciliation. Decision submission remains server-authoritative and works outside conversation.

This does not advance native diplomacy rollout. The next acceptance gate is runtime persistence/JIP with one connected player; custom alliances, trade agreements, tribute/reparations and general NPC letters remain later milestones.
