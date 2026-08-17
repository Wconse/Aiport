# Port status

## Current scope override — 2026-08-17

Current acceptance uses one connected player only; player-to-player interaction and second-client gates are removed. `0.0.99-dev` now bundles the authoritative `Hero_Player` resolver with selectable OpenAI-compatible or direct Player2 server backends. Native war/peace remain OFF. Historical multi-client records below are retained only as past evidence.

## Current runtime — 0.0.98-dev (deployed)

### Hotfix scope

- Fixed runtime blocker 0.0.97: the NPC diplomacy initiative recorded offers for stale player duplicates (`Player`, `main_hero`) while the authoritative connected hero was `Hero_Player`, so `RecipientOnline=False` and no client notification was delivered.
- Added `AIPort.Server.AuthoritativeDiplomacyRecipientFilter`: a pure, game-type-free filter that derives authoritative recipients from live `peer -> hero` mappings.
- `CollectPlayerDiplomacyTargets` now discovers candidates as before and then keeps only authoritative connected hero ids; every excluded alias is logged for audit.
- Online targets must additionally own exactly one connected peer, so the recipient can actually receive the private notice.
- Added a belt-and-braces guard before the durable record: a non-authoritative recipient is rejected with `recipient_not_authoritative_online` and consumes no daily budget.
- Ambiguous mappings (one hero id claimed by two peers) fail closed. With nobody connected the 0.0.95 offline queue behaviour is unchanged.
- Protocol remains `2`; cumulative capability flags remain `2097151`; no new capability bits and no new native mutation.

### Verification

- Build: PASS. `artifacts\bin\AIPort.dll`, 345088 bytes, SHA-256 `c589cd0e3bb6ce610c543006bccaf46d97fde07d98ecd9bb6858fce68210a5f4`.
- Bootstrap: 10240 bytes, SHA-256 `8357d5c56766dabba2777d472b87b1919a1925242b6d1f8a00bf99fd653934cf` (unchanged).
- Cumulative regression `0.0.50..0.0.98`: 21/21 scripts PASS.
- `test_0_0_98_authoritative_recipient.py`: 15 static checks plus an executable harness covering the `Hero_Player` / `Player` / `main_hero` duplicate case, disconnected peers, ambiguity, multi-player sessions and offline permissiveness.
- Deployed to client and server with identical hash `c589cd0e3bb6ce610c543006bccaf46d97fde07d98ecd9bb6858fce68210a5f4`.
- Source rollback: `backups\source-20260817-063750-pre-0.0.98`; deployment rollback: `backups\m0-20260817-064443`.
- Disposable server PID `11220`; log `artifacts\runtime-m0\logs\coop-server-20260817-064457.log`.
- Startup smoke: `build=0.0.98-dev, protocol=2`, restored `loaded:3:social:5:diplomacy:2`, generation `4ea97daf7c4e8ae14149a02cff988e72`, revision `10`, `ReadOnly=False`, `CampaignReady` and `SERVING`.
- The defect revisions 11..15 from the 0.0.97 run were never persisted; the durable ledger still holds revision `10` and 2 diplomacy rows, so pair cooldowns are clean for the retest.
- Gate remains stub (`enabled=False`, `keyPresent=False`); native war and peace remain OFF; no native mutation executed during build, tests, deployment or startup.

### Open runtime gate

- Still unproven end to end: automatic NPC initiative delivered to `Hero_Player` with `RecipientOnline=True`, full typed inbox, map notification, Accept/Reject with double-click protection, post-decision reconciliation, save/restart/reconnect JIP, and absence of `NativeMutationApplied=true`.

## Previous candidate — 0.0.97-dev (superseded by 0.0.98-dev)

### Implemented

- Protocol remains `2`; cumulative capability flags are `2097151`.
- Added `CapabilityDiplomacyInboxList = 524288` and `CapabilityNpcDiplomacyInitiativeScheduler = 1048576`.
- Added append-only `AIDiplomacyInboxPageRequest`, `AIDiplomacyInboxPageResponse` and `AIDiplomacyInboxEntry` contracts.
- Inbox pages are private to the authoritative peer -> controlled Hero mapping, newest-first, limited to 8 rows per page and 16 pending rows total.
- First-page refresh may advance to the server revision; continuation pages require the exact same revision and restart pagination on staleness.
- Client performs statement-ID accumulation/deduplication and reconciles the complete desired/published map-notification set.
- Decision notifications use an in-flight guard and remain present until authoritative resolution. Rejections release the guard instead of losing the offer.
- Fixed the `map_notification_decision` / `manual_diplomacy_recipient_decision` incompatibility with a strict two-value parser.
- Added deterministic campaign-hour NPC initiative selection, authority/precondition revalidation, daily and per-recipient budgets, minimum interval and pair cooldown.
- NPC initiative uses ruler/independent-clan leader sources and server-classified player Hero targets, including offline canonical Heroes.
- Scheduler output is always a durable `pending_recipient` shadow record; online delivery uses the private notice path and offline delivery waits for snapshot/JIP.
- Added durable origin/reason/score/campaign-day/campaign-hour metadata. Old diplomacy NDJSON rows load as legacy records.
- Scheduler state continuity is derived from the durable diplomacy ledger, including the last campaign hour.

### Verification

- Build: PASS.
- Artifact: `artifacts\bin\AIPort.dll`, 341504 bytes.
- SHA-256: `c52f5f0e67a35da1e826f0eb58311831fb028fa4bfa602d22408338a8357f17f`.
- Bootstrap: 10240 bytes, SHA-256 `8357d5c56766dabba2777d472b87b1919a1925242b6d1f8a00bf99fd653934cf`.
- Current cumulative regression set `0.0.50..0.0.97`: 20/20 scripts PASS.
- Executable 0.0.97 harness covers both accepted audit reasons, unknown-reason rejection, deterministic selection, pagination/privacy, budgets, cooldown, restart continuity, metadata roundtrip and legacy decoding.
- Actual protobuf-net request/response array roundtrip: PASS.
- Isolated source counts remain `DeclareWarAction.ApplyByDefault(` = 1 and `MakePeaceAction.Apply(` = 1.
- Source rollback: `backups\source-20260817-052827-pre-0.0.97`.
- Prepared runtime-binary rollback: `backups\runtime-20260817-055314-pre-0.0.97`.
- Candidate staging: `artifacts\stage-0.0.97`; manifests confirm identical client/server candidate hashes.

### Deployment boundary

- `0.0.97` is not deployed and not runtime-proven.
- The disposable runtime remains `0.0.95-dev`; last known server PID was `23872`.
- Saved manifest revision remains `10`; a later live client turn observed revision `11`. Decide whether to save it before stopping the current process.
- Scheduler config is default-off and the live config was not modified.
- No native war/peace setting or adapter was enabled.

## Current deployed baseline — 0.0.95-dev (2026-08-17)

- Runtime proof statement: `2c3dee6807d14af8a6962fa244b2460d`, `lord_5_1` / Battania -> `Hero_Player` / Player, action `war`, `pending_recipient`.
- Private snapshot/reconnect notification already passed on the deployed build.
- Durable revision: `10`; last observed live client revision: `11`.
- Runtime DLL: 303104 bytes; SHA-256 `297fd3b155f64b508dabda1769cf610e490d112009c98dada32fd8fb513569f7`.
- Deployment rollback: `backups\m0-20260817-034824`; source rollback: `backups\source-20260817-034733-pre-0.0.95`.
- Server log: `artifacts\runtime-m0\logs\coop-server-20260817-035023.log`; native war/peace adapters remain off.

## Current authoritative deployment

## Two-client disconnect isolation proof — 2026-08-14 09:17 +05:00

- Peer 1 disconnected normally at `09:17:32.269`; AIPort cleared only peer 1 with `ActiveRequestsAborted=0`.
- Peer 0 continued the same conversation with `CharacterObject_1649`: memory advanced from `MemoryTurns=5` before the disconnect to `6` and `7` afterward.
- This proves that disconnect cleanup for one peer does not clear another peer's active conversation memory.
- A later request was rate-limited for 1000 ms, retried successfully, and started at `MemoryTurns=1`; the subsequent request reached `2`. This is consistent with a new conversation ID after leaving/re-entering the full NPC dialogue.
- No AIPort exception, ownership conflict, or stale response was observed. Disconnect during an active backend request remains an automated delayed-backend test rather than a manual requirement.

## Two-client runtime proof — 2026-08-14 09:04–09:11 +05:00

- Two distinct peers connected and completed compatible `0.0.37-dev` / protocol 1 handshakes: remote peer 0 at `26.241.127.112` and local peer 1 at `127.0.0.1`.
- Server resolved separate authoritative identities: `Hero_Player2863` / `MobileParty_Player1860` and `Hero_Player` / `MobileParty_Player`.
- Both players successfully received real Groq replies. Request IDs were distinct.
- Same-target isolation was runtime-proven for `CharacterObject_1649`: both players started fresh conversations and each result committed as `MemoryTurns=1`; neither inherited the other player's history.
- The two same-target backend requests were accepted at `09:10:51.999` and `09:10:54.704`. They occurred in the same gameplay overlap, but did not overlap at the HTTP worker level because the first completed at `09:10:52.489`.
- No AIPort rejection, exception, suppressed result, or ownership warning occurred in the two-client window.
- Still unproven: deliberately overlapping in-flight requests, cross-peer cancellation resistance in live play, disconnect during an active request, and reconnect stale-result suppression.

- Build `0.0.37-dev`, protocol `1`.
- Runtime `AIPort.dll`: 97,792 bytes; SHA-256 `78bd9bb6c58602168a8391a758a4c18e9c6393f5de38d9317e57356d383fa148`.
- Bootstrap: 10,240 bytes; SHA-256 `8357d5c56766dabba2777d472b87b1919a1925242b6d1f8a00bf99fd653934cf`.
- Deployment rollback: `backups\m0-20260814-073639`; source rollback: `backups\source-20260814-072255-pre-0.0.37`.
- Client/server runtime and bootstrap hashes match.
- Groq is enabled through the server process environment with explicit config `E:\BCOOP\aiport-server.json`; model `llama-3.1-8b-instant`.
- Regular `CharacterObject` targets are supported in the townsfolk/villager, prison-guard, castle-guard and selected alley root menus. Hero dialogue remains root-only at `hero_main_options`.
- Every target receives a deterministic immutable narrative profile derived from target id, culture and occupation. Hero traits remain authoritative; the profile does not invent a biography and requires no persistence file.
- Up to five relevant authoritative campaign-log facts are added from a bounded scan of 96 recent entries: meetings, player battles, captivity/release, war/peace and settlement ownership changes.
- New 0.0.37 suite passed 21/21; 0.0.36 prompt suite passed 17/17; explicit-routing, provider and config suites remain green.
- Disposable server: wrapper PID `27484`, engine PID `29144`, log `artifacts\runtime-m0\logs\coop-server-20260814-073640.log`; `CampaignReady` and engine `SERVING` confirmed at `07:37:26`, with no fatal/unhandled error.
- Gameplay actions, diplomacy mutations, synthetic dynamic events and writable persistence remain disabled.
- Comprehensive planning-only future feature design is documented in `docs\FUTURE_AI_FEATURES_PLAN.md`; no gameplay code, protocol, persistence or deployment changed.
## Historical deployment snapshot

- Build `0.0.28-dev`, protocol `1`.
- Runtime 75,264 bytes, SHA-256 `b5ff59c1f1769367ee7b71e1699c57b3ed4eaf25368ea4bba7fab91baee37d02`; bootstrap SHA-256 `8357d5c56766dabba2777d472b87b1919a1925242b6d1f8a00bf99fd653934cf`.
- Deployment rollback `backups\m0-20260814-051256`; source rollback `backups\source-20260814-051222-pre-0.0.28`.
- Fixes 0.0.27 player-to-player return-token auto-chaining with a deterministic NPC bridge and keeps the main AI entry visible during pending/retry state.
- Server wrapper `27128`, engine `33984`, log `artifacts\runtime-m0\logs\coop-server-20260814-051306.log`; CampaignReady/SERVING, backend disabled/no key.


## Historical deployment snapshot

- Build `0.0.27-dev`, protocol `1`.
- Runtime 74,752 bytes, SHA-256 `19c29e93349ee278b533feee891f1f4779ccddbd2dd41fdf90345db3c9c94662`; bootstrap SHA-256 `8357d5c56766dabba2777d472b87b1919a1925242b6d1f8a00bf99fd653934cf`.
- Deployment rollback `backups\m0-20260814-050315`; source rollback `backups\source-20260814-050239-pre-0.0.27`.
- 0.0.26 bundled suite passed all 15 runtime checks. 0.0.27 adds `Сказать ещё...` and `Вернуться к обычному разговору.` post-response choices.
- Disposable server wrapper `25012`, engine `4248`, log `artifacts\runtime-m0\logs\coop-server-20260814-050322.log`; CampaignReady/SERVING. Backend disabled, no key.


## Historical deployment snapshot

- Build `0.0.24-dev`, protocol `1`.
- Runtime: 73,216 bytes; SHA-256 `222a3bcdff72fcd22726c68877715b3b46ea2920af426ede68c2ccccd5790464`.
- Bootstrap: 10,240 bytes; SHA-256 `8357d5c56766dabba2777d472b87b1919a1925242b6d1f8a00bf99fd653934cf`.
- Deployment rollback: `backups\m0-20260814-035153`; source rollback: `backups\source-20260814-034949-pre-0.0.24`.
- Disposable server: wrapper `35328`, engine `19640`, log `artifacts\runtime-m0\logs\coop-server-20260814-035201.log`; CampaignReady/SERVING at `03:52:44`.
- Client/server runtime hashes match. Backend explicitly disabled and no key present. Actions, diplomacy, dynamic events and persistence remain disabled.



## Current candidate

- Updated: 2026-08-14 03:53 +05:00.
- 0.0.23 runtime regression passed: compatible handshake, one native + one free-form + one native exit turn, memory 1 -> 2 -> 3, display-only results and clean conversation end.
- Built 0.0.24-dev (protocol 1): runtime 73,216 bytes, SHA-256 `222a3bcdff72fcd22726c68877715b3b46ea2920af426ede68c2ccccd5790464`; bootstrap unchanged at SHA-256 `8357d5c56766dabba2777d472b87b1919a1925242b6d1f8a00bf99fd653934cf`.
- Security delta: fixed `AIPORT_API_KEY` credential source; only HTTPS or loopback HTTP endpoints; no endpoint user-info/fragments.
- Source rollback: `backups\source-20260814-034949-pre-0.0.24`. Deployment pending.

Updated: 2026-08-14 00:46 +05:00

Read `docs/AGENT_HANDOFF.md` first.

## Overall

| Area | Status | Evidence / notes |
|---|---|---|
| Source recovery | Done | 610 C# files recovered and indexed |
| Load strategy | Runtime-confirmed | `AIPort.Bootstrap.dll` + `AssemblyResolve`; no extra active module ID |
| Handshake | Runtime-confirmed on 0.0.13 | RequestId `8fb02151c7ec40a0a38cd2eb8ae26467`, protocol 1, client/server `0.0.13-dev`, compatible |
| CampaignReady | Runtime-confirmed | Both pre-save-load and post-save-load phases observed |
| Campaign listener rebinding | Runtime-confirmed on 0.0.12 | All three event object hashes changed and listeners were reattached after save transfer |
| PlayerContextResolver | Runtime-confirmed | peer 0 -> `Hero_Player` / `MobileParty_Player` |
| Startup retry | Runtime-confirmed | retry recovered from `player_unresolved` |
| NPC conversation start | Runtime-confirmed on 0.0.12 | `Source=agent_joined`, `NpcHeroId=lord_3_1` |
| Stable conversation identity | Runtime-confirmed | both NPC requests used `afda57694a0c40a18a115fe93de0d451` |
| Conversation memory | Two-turn + reconnect runtime proof | server logged `MemoryTurns=1 -> 2`; disconnect clear and reconnect reset were observed |
| Conversation end | Runtime-confirmed | client received `ConversationEnded` and cleared current conversation state |
| Disconnect/reconnect cleanup | Runtime-confirmed | peer 0 clear, peer 1 reset, next NPC conversation restarted at `MemoryTurns=1` |
| Rate limiting | Retry success runtime-confirmed on 0.0.13 | server returned `RetryAfterMilliseconds=33450`; client scheduled `33700 ms`, retried with a new RequestId and reached `MemoryTurns=2` |
| Prompt context | Compiled and exercised | authoritative player/NPC identifiers plus bounded prior-turn history |
| Backend | Compiled, disabled in test | OpenRouter-compatible HTTP; environment variable `AIPORT_API_KEY` only |
| AI actions | Disabled | narrative-only; no campaign mutations, diplomacy or dynamic events |
| Current deploy | `0.0.23-dev` | runtime SHA-256 `d13198f0ee4c0e3840076e44b70ec6cbd800053478c6fb63672690fae6109316`; backup `m0-20260814-004339` |
| Cancellation and ownership | Hardened in `0.0.23-dev` | atomic cancel-vs-commit gate; peer/conversation ownership; disconnect abort; global RequestId uniqueness |
| Live campaign | Untouched | `saveauto1` not used; backup `live-campaign-20260813-174141` |

## Current runtime

- User confirmed both normal and paused free-form dialogue regressions on 0.0.22: immediate display, native continuation and clean exit.
- Current build/deploy: `0.0.23-dev`, protocol `1`.
- Disposable data directory: `artifacts/runtime-m0`; save: `aiport-m0`.
- Server log: `artifacts/runtime-m0/logs/coop-server-20260814-004353.log`.
- Wrapper PID `24340`, engine PID `30292`; UDP 4200; CampaignReady/SERVING at `00:44:38`.
- Runtime client/server DLLs match: 72,704 bytes, SHA-256 `d13198f0ee4c0e3840076e44b70ec6cbd800053478c6fb63672690fae6109316`.
- Bootstrap client/server DLLs match: 10,240 bytes, SHA-256 `8357d5c56766dabba2777d472b87b1919a1925242b6d1f8a00bf99fd653934cf`.
- Backend remains explicitly disabled with no key. Gameplay actions, diplomacy, dynamic events and persistence remain disabled.

## 0.0.13 implementation and deployment

- Added protobuf field `AIConversationError.RetryAfterMilliseconds` as field number 5; existing field numbers were preserved.
- Server rate limiter now returns a precise delay:
  - `1000 ms` when the single inflight slot is occupied;
  - otherwise the remaining time until the oldest per-player sliding-window entry expires.
- Client tracks the exact pending RequestId, ConversationId, NPC, text, turn and retry attempt.
- Only a retryable `rate_limited` error matching the current pending request can schedule a retry.
- Retry is bounded to 5 attempts and delay is clamped to `1000..60000 ms`.
- Retry state is canceled on conversation end, campaign-target loss, replacement conversation, network disconnect and handler disposal.
- Each retry uses a new RequestId/client sequence but preserves ConversationId, NPC, text and turn.
- Built runtime DLL: `48,640` bytes; SHA-256 `99097ed038f8f3c714c17713dc239b616422e9751e6eb299b08ea90ee1a19e29`.
- Bootstrap unchanged: `8,704` bytes; SHA-256 `a2e0d24e5a5c503270ce3406dcc63ae8a71da1b9251ebe1925f44af5932b270d`.
- Deployed to client and disposable server with rollback backup `backups/m0-20260813-203336`; both hashes verified.
- Disposable server started as PID `20260` with log `coop-server-20260813-203344.log`.
- Server reached `CampaignReady` and `SERVING` at `20:34:28`.
- Runtime handshake and successful rate-limit retry are proven; cancellation-before-timer proof remains.

## 0.0.14 cancellation-observability deployment

- Added explicit `AIPort conversation retry canceled` logging with ConversationId, retry attempt and lifecycle reason.
- Cancellation reasons are now distinguishable: `conversation_ended`, `target_lost`, `conversation_replaced`, `network_disconnected`, `dispose` and `retry_rescheduled`.
- Added an explicit diagnostic when a conversation target is a non-hero CharacterObject, including CharacterId and name; non-hero requests are still intentionally not sent.
- Protocol remains 1; no protobuf fields changed.
- Source rollback copy: `backups/source-20260813-212700`.
- Built `AIPort.dll`: 49,152 bytes, SHA-256 `7903e68a7555fac8e13d143b04e0e0a5dd617e49b26d84e83da9cfebc50c39f3`.
- Client and server staging hashes match the build. Bootstrap is unchanged.
- Deployed to client and disposable server with rollback backup `backups/m0-20260813-212758`; both runtime hashes verified.
- New server log `coop-server-20260813-214955.log` loaded build `0.0.14-dev` and reached CampaignReady/SERVING at `21:29:00`.
- Launcher PID `17060` is open; client handshake and cancellation runtime proof are pending.

## 0.0.14 runtime check at 21:46

- Client/server handshake succeeded: RequestId `45631a8dd5424a419a66660ad2431735`, client/server build `0.0.14-dev`, protocol 1, Compatible=true.
- Hero `lord_5_13` / Муйнсер: ConversationId `384544465061496a9c29d79bdd1086f1`; RequestIds `32d2c341f2384119a78b718087a6cff2`, `cd359b7da746431b996c607deb44d7c5`; MemoryTurns 1 -> 2.
- Hero `lord_5_1` / Каладог: ConversationId `dce1a3c5eead453aa647db60de0662ac`; RequestIds `03b7c66255b64400be538d5d30f33351`, `e090bcb5e69e4ba89e28f1087b5136bc`; MemoryTurns 1 -> 2.
- Both hero conversations succeeded without rate limiting, so there was no pending retry to cancel; cancellation remains unproven in this run.
- Non-hero diagnostic is proven: CharacterId `looter`, name `Грабитель`, logged at `21:46:53`; no AIPort request was sent for it.
- Per user preference, do not start the game process; only restart the disposable server when needed.
- Server was restarted after inspection: wrapper PID `28432`, engine PID `28596`, log `coop-server-20260813-214955.log`; build 0.0.14 reached CampaignReady/SERVING at `21:50:48`.

## 0.0.13 runtime proof

### Connection and save-load rebinding

- Client connected as peer `0`; server reset volatile conversation memory at `20:51:01`.
- Handshake RequestId `8fb02151c7ec40a0a38cd2eb8ae26467`: protocol 1, client/server `0.0.13-dev`, `Compatible=true`.
- First listener set at `20:51:37`: Tick `31953303`, Agent `67039599`, End `65654309`.
- Startup probe RequestId `a0bd27d1eb394ff390e8d5339cc1586f` returned retryable `player_unresolved`; retry RequestId `dd1f2c4b5c2c42238e0aee049f4f4795` resolved `Hero_Player` / `MobileParty_Player` and completed with `MemoryTurns=1`.
- After transferred-save loading, AIPort rebound to Tick `14741975`, Agent `41146019`, End `58760031` at `20:51:59`.

### Successful bounded rate-limit retry

- First real conversation: RGL `20:52:08.857` to `20:52:11.531`; NPC `lord_1_17`; ConversationId `4a4c41ac4e0d46b1be202cd39d34795a`.
  - RequestId `532c1f97b7de4a82a4529f5a5ff3d902` -> `MemoryTurns=1`.
  - RequestId `9c74fd94574145229f8b162886df4d3c` -> `MemoryTurns=2`.
- Second conversation: RGL `20:52:12.413` to `20:57:18.884`; same NPC; new ConversationId `23865aec886f429382d6d0877ada088a`.
  - Turn 1 RequestId `403ad65331bb4aa8a71da7e4f1b578b9` -> `MemoryTurns=1`.
  - Initial turn 2 RequestId `c45abbccf41a43df8fdb12e5fa37bd29` was rejected as retryable `rate_limited` with `RetryAfterMs=33450`.
  - Client scheduled attempt 1 for `33700 ms`, preserving NPC, ConversationId, text and turn.
  - At `20:52:46`, retry RequestId `9f438fa80b0842aa870b1f816ad444a0` used the same ConversationId/NPC/turn, was accepted and produced `MemoryTurns=2`.
  - Conversation end cleanup fired at `20:57:18`.
- Built, staged and both deployed `AIPort.dll` files remain `48,640` bytes with SHA-256 `99097ed038f8f3c714c17713dc239b616422e9751e6eb299b08ea90ee1a19e29`.
- Live `saveauto1` still matches its pre-M0 backup: `80,655` bytes, SHA-256 `0d9d79dca420f086b68336df6cb32569d044ba08d50acfab097772db7f8320d5`.
- Backend and all actions remain disabled.

## 0.0.12 runtime proof

### Connection and startup

- Connection attempt: `20:04:09`.
- Handshake: RequestId `2e3a98340eb44e87a360bb90da554126`, `Compatible=true`.
- First listener set at `20:04:46`:
  - TickEventHash `55319230`
  - AgentEventHash `24464410`
  - EndEventHash `29848889`
- Initial probe RequestId `ffa253b2283a4fd992fa2bc67548a71c` returned retryable `player_unresolved`.
- Retry RequestId `d806473fa6ca464a9bfe485de88ef090` resolved authoritative `Hero_Player` / `MobileParty_Player` and completed.
- Second listener set at `20:05:07`, after save transfer:
  - TickEventHash `35887792`
  - AgentEventHash `39896889`
  - EndEventHash `61162259`
- All three hashes changed, proving Coop replaced the campaign event set and AIPort rebound to the new instances.
- Server logged `player entered the campaign: peer 0` at `20:05:07`.

### Real NPC conversation

- RGL conversation: `20:05:56.462` to `20:06:06.805`.
- AIPort observed start at `20:05:56` via `OnAgentJoinedConversationEvent`.
- NPC: `lord_3_1`.
- ConversationId: `afda57694a0c40a18a115fe93de0d451`.
- Turn 1 RequestId: `83c5a43286f1403a97b14476079eef83`; accepted; result received; server `MemoryTurns=1`.
- Turn 2 RequestId: `692cf48541cf4c8da0d131eb9749f138`; accepted; result received; server `MemoryTurns=2`.
- Both results used `SpeakerHeroId=lord_3_1`.
- Client received the conversation-ended event at `20:06:06`.


## Disconnect/reconnect runtime proof

- Peer 0 disconnected at `20:11:27` with `RemoteConnectionClose`.
- Server immediately logged `AIPort conversation memory cleared for disconnected peer PeerId=0`.
- The same client reconnected as peer 1 at `20:11:31`.
- Server logged `AIPort conversation memory reset for connected peer PeerId=1` before request processing.
- Reconnect handshake RequestId `95063f88a2ff41f2878f5a7e42ad115a`: protocol 1, client/server `0.0.12-dev`, compatible.
- Reconnect listener hashes: Tick `25952383`, Agent `28060740`, End `14377911`.
- Startup probe RequestId `ba1fcdb54eaa4cdfbbece66b95c723b8` completed with `MemoryTurns=1`.
- First real NPC conversation after reconnect:
  - RGL interval `20:11:56.321` to `20:12:00.525`;
  - NpcHeroId `lord_3_2`;
  - ConversationId `efb2a59782e7419a99d1ee64e25915c7`;
  - RequestId `575f4486882844a4ab08bf829a179adf` -> `MemoryTurns=1`;
  - RequestId `0e8f4f08c1314ff5934ca7b993ae77cd` -> `MemoryTurns=2`;
  - both results reached the client and end cleanup fired.
- This proves the previous peer's volatile state was removed and the post-reconnect conversation started with clean history.

## Rate-limit boundary found

- A second conversation started only about three seconds later:
  - RGL interval `20:12:03.281` to `20:12:07.186`;
  - NpcHeroId `lord_3_1`;
  - ConversationId `b8bba9a2d60244efbfd8b97038f884cd`.
- Turn 1 RequestId `de3964f532ab4c7c86e65f203247aa86` completed with `MemoryTurns=1`.
- Turn 2 RequestId `eb73ca26166d40e08b9daf217f787459` returned retryable `rate_limited`.
- Server protection behaved correctly, but the temporary client second-turn probe currently does not retry `rate_limited`; add bounded backoff before the next runtime build.

## Cancellation-test attempt at 21:12

- Reconnect cleanup and handshake succeeded on peer `1` with RequestId `d4314a2b56184e5ea0a91400864690db`.
- Hero conversation `78e5da6662994e89b77769520efa4437` reached `MemoryTurns=1 -> 2`.
- The second target was an `Имперский крестьянин` (non-hero), which the current hero-only M0 handler intentionally ignores.
- No rate limit or retry timer was created, so cancellation remains unproven; repeat with a hero target.

## Remaining milestone work

1. Connect the 0.0.14 client, then runtime-prove cancellation with the new explicit reason log.
2. Test two simultaneous clients for memory isolation, queueing and rate limits.
3. Capture real player-selected dialogue text instead of the temporary `Hello.` / `Hello again.` probe texts.
4. Render the model response in the conversation UI.
5. Run one controlled backend narrative-response test using `AIPORT_API_KEY` from the environment only.
6. Add server persistence only after volatile multi-client behavior is stable.
7. Keep diplomacy, dynamic events and gameplay actions disabled until narrative transport is complete and validated.

## 0.0.15 narrative UI candidate (built and staged, not deployed)

- Build marker advanced to `0.0.15-dev`; protocol remains `1` and protobuf field numbers are unchanged.
- The client now subscribes to `ConversationManager.ConsequenceRunned` and captures the text of the actual selected player sentence for hero conversations.
- Removed all synthetic `Hello.` / `Hello again.` startup and conversation probes.
- The selected text is trimmed, capped at `MaximumPlayerTextLength = 4000`, sent to the authoritative Coop server, and retried only for the matching active request.
- Retryable `player_unresolved` now preserves and retries the actual selected text, just like `rate_limited`.
- Completed results are matched by RequestId and ConversationId, queued to the campaign tick, and discarded after conversation end/target replacement.
- On the game thread the client updates `ConversationManager._currentSentenceText`, refreshes the active Mission/Map conversation VM, and sets its writable `DialogText`; reflection confirmed the exact field/property types in Bannerlord 1.4.7.
- Build succeeded without warnings: `AIPort.dll` 52,224 bytes, SHA-256 `6ecbefc64d2a1aedd97bb750bf0f1479586181ab646707255e8bb8c36f138575`.
- Bootstrap unchanged: 8,704 bytes, SHA-256 `a2e0d24e5a5c503270ce3406dcc63ae8a71da1b9251ebe1925f44af5932b270d`.
- Client/server staging hashes match the build. Source rollback: `backups/source-20260813-215942-pre-0.0.15`.
- Deployed with rollback backup `backups/m0-20260813-221843`; disposable server wrapper PID `30344`, engine PID `30464`, log `coop-server-20260813-221853.log`, ready at `22:19:37`.

## 0.0.15 deployment status

- Client and disposable server are deployed with `0.0.15-dev`; protocol remains 1.
- Rollback backup: `backups/m0-20260813-221843`.
- Server wrapper PID `30344`, engine PID `30464`; log `coop-server-20260813-221853.log`; CampaignReady/SERVING at `22:19:37`.
- Next proof requires the user to launch/connect and select dialogue options with a hero NPC.

## 2026-08-13 - 0.0.15 UI proof, stuck-flow defect, and 0.0.16 fix

- Runtime 0.0.15 proved the intended transport and identity path with hero `lord_5_13`, ConversationId `79ee654712bf4e8ea4a60e10c673ced4`.
- Actual selected player lines were captured and sent: turn 1 `Меня зовут Руган, господин. Могу ли я узнать ваше имя?`; turn 2 `Я хочу кое-что обсудить.`
- Server accepted both requests and logged `MemoryTurns=1` then `MemoryTurns=2`; both stub results appeared in the active dialogue UI.
- Defect: after the second result the dialogue became stuck. 0.0.15 changed `ConversationManager._currentSentenceText` and called the VM `Refresh()`, corrupting the native conversation flow while replacing the displayed line.
- 0.0.16 removes both state mutations. The response is now a presentation-only assignment to the active mission/map conversation VM `DialogText`; Bannerlord retains its native sentence, options, continuation and exit state.
- Source rollback: `backups\source-20260813-222657-pre-0.0.16`.
- Build/deployed DLL: 51,712 bytes, SHA-256 `d8becd713ecd998d268f3b8605c1bc909451ddd3c6926e82ad5eab89ccff228f`; protocol remains 1.
- Deployment rollback: `backups\m0-20260813-222805`.
- User-authorized client/launcher PID `23188` was closed; only the disposable server was stopped/restarted.
- Server wrapper PID `31044`, engine PID `31592`, log `coop-server-20260813-222816.log`; 0.0.16 reached CampaignReady/SERVING at `22:28:59`.
- Backend/actions/diplomacy/dynamic events/persistence remain disabled.

## 2026-08-13 - 0.0.16 runtime pass and 0.0.17 free-form input deployment

- User confirmed the 0.0.16 display-only override no longer traps the native dialogue; continuation and exit work. This closes the 0.0.15 stuck-flow defect.
- Added a native `Сказать своими словами…` player option at the standard `hero_main_options` token for active one-to-one hero conversations.
- Selecting it opens the standard Bannerlord `TextInquiryData` input with Send/Cancel actions, non-empty validation and the existing 4000-character protocol limit.
- A static headless-safe bridge connects the campaign dialogue registration to the Coop client handler only when the client handler is active and no request is pending.
- The custom option's own sentence ID is filtered from `ConsequenceRunned`, so only the entered free-form text is sent; the option label is never sent as a duplicate turn.
- Added a native waiting NPC line (`Я слушаю…`) that returns to `hero_main_options`. The authoritative result still changes only the displayed `DialogText`, preserving Bannerlord's internal sentence graph.
- Existing ordinary selected-option capture remains enabled as a secondary path.
- Source rollback: `backups\source-20260813-223814-pre-0.0.17`.
- Build `0.0.17-dev`, protocol 1: `AIPort.dll` 55,296 bytes, SHA-256 `3625c916edde82c4c7caa8f4d68c0777efff9df08d054f54455a05d63af9873c`; bootstrap unchanged.
- Source invariants passed: standard hero token, standard text inquiry, custom-line duplicate filter, 4000-character guard, display-only response, no internal manager mutation and no synthetic Hello probes.
- Closed user-authorized launcher PID `28244`, stopped only the disposable server, and deployed identical client/server DLLs.
- Deployment rollback: `backups\m0-20260813-223917`.
- Server wrapper PID `28624`, engine PID `30168`, log `coop-server-20260813-223937.log`; runtime loaded 0.0.17 and reached CampaignReady/SERVING at `22:40:21`.
- Backend remains disabled; actions, diplomacy, dynamic events and persistence remain disabled.
- Runtime proof pending: option visibility, textbox submission, exact free-form server turn, in-dialogue stub response, return to native options and normal exit.

## 2026-08-13 - 0.0.17 option-registration failure and 0.0.18 bootstrap lifecycle fix

- Runtime test showed no `Сказать своими словами…` option anywhere. Ordinary selected-option capture still worked, but every turn had `Source=native_option`; no free-form registration log appeared.
- Root cause: the deployed SubModule points to `AIPortBootstrapSubModule`. The bootstrap loaded `AIPort.dll` for Coop discovery but did not forward Bannerlord's `OnGameStart` lifecycle to the runtime `AIPortSubModule`, so its `CampaignGameStarter.AddPlayerLine` registration never executed.
- Added a public idempotent runtime entrypoint `RegisterCampaignDialogs(IGameStarter)`, keyed by the exact `CampaignGameStarter` instance.
- Bootstrap now overrides `OnGameStart`, loads the runtime if needed, and invokes that entrypoint via reflection. Bootstrap build references now include `TaleWorlds.Core`.
- Build advanced to `0.0.18-dev`; protocol remains 1.
- Runtime DLL: 55,296 bytes, SHA-256 `0883f87a710a767fd37b01ac0aa76554ae8a420b90d8f630bd42f28e2ddf7374`.
- Bootstrap DLL: 9,728 bytes, SHA-256 `9c37eb49241e26ea43b2564da71ca00a78056d1fabea5f782bfd6f834a84d85f`.
- Source rollback: `backups\source-20260813-225151-pre-0.0.18`; deployment rollback: `backups\m0-20260813-225215`.
- Closed user-authorized launcher PID `32512`, deployed matching client/server runtime and bootstrap hashes, and restarted only the disposable server.
- Server wrapper PID `32408`, engine PID `31844`, log `coop-server-20260813-225229.log`; CampaignReady/SERVING at `22:53:14`.
- Server runtime proof now contains both required lifecycle markers: `Free-form hero dialogue option registered` and `Bootstrap delegated OnGameStart to runtime dialogue registration`.
- Client-side visibility and textbox submission remain to be verified after the user launches 0.0.18.

## 2026-08-13 - 0.0.18 free-form runtime proof passed

- User confirmed the option appeared, accepted typed input, and allowed a normal dialogue exit.
- Client/server handshake matched build `0.0.18-dev`, protocol 1, Compatible=true (`632af7e5b0f245a8bf089b16da00c3e5`).
- Hero target: `lord_5_13`; ConversationId `fb4c3ce382c04b70b3806cd8611e0684`.
- Native opening turn: RequestId `335706e5b79d4e1981eaf27d3dc4b980`, MemoryTurns=1.
- Free-form submission was captured as `Source=free_form`, 12 characters, Turn=2 — not as the custom option label. RequestId `a7fb4741ba3f4a0eab17a13c3143edcd`.
- Server accepted the free-form request under the same conversation and returned the stub with MemoryTurns=2.
- Client queued/applied the authoritative response as a display-only `DialogText` override.
- A subsequent native exit/option turn stayed in the same conversation (Turn=3, MemoryTurns=3), followed by `conversation ended`; native flow was not trapped.
- This proves option registration, textbox input, exact free-form transport, stable identity/memory, server authority, response rendering and safe exit.
- Backend remains disabled; next milestone: explicit waiting/error/timeout UX, then one controlled real-backend turn.

## 2026-08-13 - 0.0.19 async dialogue UX hardening deployed

- Added a 100-second client request watchdog. If no matching result/error arrives, the pending request is released, a best-effort cancel is sent, and a safe retry message is queued in the active dialogue.
- Non-retryable and unsupported retryable errors now always clear pending state and surface a localized dialogue message; `backend_timeout`, `backend_failed`, rate limits and player-resolution failures have explicit text.
- Automatic rate-limit/player-resolution retry now keeps the free-form option unavailable for the entire scheduled retry window. Native dialogue submissions are also ignored while a request or retry is pending.
- Retry waiting and retry exhaustion are visible in the dialogue. Repeated native options can no longer replace/reschedule the queued retry payload.
- The native free-form waiting line now says `Секунду… Я обдумаю ответ.` while preserving normal continuation and exit.
- Exiting a dialogue with a pending request sends `AIConversationCancel`. The server tracks active backend request IDs and suppresses late result/error delivery and memory writes after cancellation. The synchronous HTTP call is not forcibly aborted yet.
- Server now classifies `WebExceptionStatus.Timeout` as `backend_timeout`; other backend failures remain safe `backend_failed` events.
- Source rollback: `backups\source-20260813-231533-pre-0.0.19`.
- Build `0.0.19-dev`, protocol 1. Runtime `AIPort.dll`: 61,440 bytes, SHA-256 `30a87c3ce12c9c8c9e7ba4e09275a75f3d0001aff022ff8f0ed68b8a76ecb111`. Bootstrap remains 9,728 bytes, SHA-256 `9c37eb49241e26ea43b2564da71ca00a78056d1fabea5f782bfd6f834a84d85f`.
- Deployment rollback: `backups\m0-20260813-231606`; matching client/server hashes verified.
- Closed user-authorized launcher PID `32904`, restarted only the disposable M0 server. Wrapper PID `18008`, engine PID `14560`, log `coop-server-20260813-231608.log`.
- Runtime loaded 0.0.19, registered the free-form option and reached CampaignReady/SERVING at `23:16:53`. Backend remains disabled and no API key is present.
- Runtime proof pending: normal stub free-form response with the new waiting copy, normal exit, and (later) controlled error/timeout behavior before one real-backend turn.

## 2026-08-13 - paused-dialogue diagnosis, 0.0.20 realtime UI pump, and 0.0.21 backend safety gate

### 0.0.19 runtime evidence

- User completed several free-form conversations successfully. Stub results, stable conversation IDs, memory growth, safe native exit, retry waiting, bounded rate-limit retry and retry cancellation all appeared in the logs.
- A paused dialogue exposed a real presentation defect: the server result arrived immediately but stayed queued until `CampaignEvents.TickEvent` advanced. Example: RequestId `6f2c450edc37400d9f227da4085ba1d9` was queued at `23:26:39`, while paused campaign time prevented the display override.
- Root cause: UI delivery was pumped only from campaign time, although network delivery and the conversation screen continue while campaign time is paused.

### 0.0.20 implementation and deterministic tests

- Added `AIPortRuntimeLifecycleBridge`. The bootstrap now overrides `MBSubModuleBase.OnApplicationTick(float)` and binds a typed `Action<float>` delegate once at runtime load; there is no per-frame reflection.
- The client applies queued dialogue results from real-time application ticks. Campaign tick remains responsible only for periodic target scanning, so paused campaign time no longer gates response rendering.
- Preserved the proven display-only rule: no `_currentSentenceText` mutation and no conversation-VM `Refresh()`.
- Upgraded the OpenAI-compatible backend path: complete JSON string escaping/parsing (including Unicode escapes), UTF-8 request/response handling, read/write timeout, 8,000-character NPC output bound, and an abortable `HttpWebRequest` handle.
- Server cancellation now aborts an active HTTP request and still suppresses late result/error delivery and memory writes.
- Campaign/Hero facts are snapshotted before the worker thread. The prompt now includes authoritative names, clan, kingdom, culture, home, occupation, age, gold and NPC-to-player relation, quotes untrusted dialogue/history, requires the same response language and keeps actions disabled.
- Deterministic local fake-backend tests passed: valid request JSON, control-character escaping, Russian/Unicode response parsing, quote/backslash/newline handling, 8,000-character clamp and `WebExceptionStatus.RequestCanceled` after abort.
- Lifecycle bridge reflection test passed: attach/tick/detach count `2`, sum `4`, bootstrap override present, build/protocol/bounds correct.
- Intermediate 0.0.20 deployment backup: `backups\m0-20260813-234006`.

### 0.0.21 explicit backend activation safety

- A backend key alone can no longer activate outbound AI traffic. Three conditions are required together: `enabled: true` in server config, a key in the named process environment variable, and an HTTPS or loopback endpoint.
- Plain HTTP to a non-loopback host and malformed endpoints disable the backend. Timeouts clamp to 5–120 seconds, concurrency to 1–4, and per-player rate to 1–60 requests/minute. Diplomacy and dynamic events remain forced off.
- Added secret-free template `config\server.example.json`; no live config and no key were created.
- Seven deterministic settings scenarios passed: key-only disabled, explicit-without-key disabled, HTTPS enabled, remote HTTP blocked, loopback HTTP allowed, limits clamped, malformed endpoint blocked.
- Source rollback: `backups\source-20260813-234230-pre-0.0.21`. Deployment rollback: `backups\m0-20260813-234340`.
- Current build `0.0.21-dev`, protocol 1. Runtime DLL: 69,632 bytes, SHA-256 `f00588885595dfcf80707ffc97b24170245427440b614a281f78a8fea345b56f`. Bootstrap: 10,240 bytes, SHA-256 `8357d5c56766dabba2777d472b87b1919a1925242b6d1f8a00bf99fd653934cf`.
- Matching client/server hashes verified. Disposable server wrapper PID `32196`, engine PID `29612`, log `coop-server-20260813-234341.log`; CampaignReady/SERVING at `23:44:23`.
- Server startup confirms `applicationTickBridge=true`, dialogue registration, `explicitlyEnabled=False`, `enabled=False`, `keyPresent=False`, and allowed HTTPS default endpoint.
- Client runtime proof still required: submit while campaign time is paused and verify that the result replaces the waiting text immediately without unpausing.

## 2026-08-14 - paused UI proof and 0.0.22 request-ownership hardening

- The user confirmed the 0.0.21 Application Tick fix: while campaign time remained paused, the stub immediately replaced the waiting text. The paused-dialogue defect is closed.
- Build advanced to `0.0.22-dev`; protocol remains `1` and no protobuf field numbers changed.
- Server now accepts only canonical 32-hex request and conversation IDs, rejects empty dialogue, bounds NPC identifiers, and rejects duplicate/replayed request IDs through a bounded 8,192-entry cache.
- Active backend requests are bound to the authoritative network peer and exact ConversationId. A cancel from another peer or another conversation cannot abort the request.
- Disconnect now marks every active backend request for that peer as canceled and aborts its HTTP handle. Late results/errors remain suppressed and cannot be written to conversation memory.
- Added `tools/test_0_0_22_security.py`; all 14 build/security invariants passed.
- Build succeeded: `AIPort.dll` 72,192 bytes, SHA-256 `e152e7b2dea36d717877e202be726e956169649d1b347edc73a2100ce4a2e0b9`; bootstrap 10,240 bytes, SHA-256 `8357d5c56766dabba2777d472b87b1919a1925242b6d1f8a00bf99fd653934cf`.
- Source rollback: `backups\source-20260814-001607-pre-0.0.22`; deployment rollback: `backups\m0-20260814-001752`.
- Matching client/server hashes were verified by the guarded deploy. The user-controlled game/launcher was closed for deployment.
- Disposable server wrapper PID `30560`, engine PID `32708`, log `artifacts\runtime-m0\logs\coop-server-20260814-001753.log`.
- Runtime loaded `0.0.22-dev`, registered free-form dialogue, reached CampaignReady and SERVING at `00:18:37`. Backend remains explicitly disabled with no key; actions, diplomacy, dynamic events and persistence remain disabled.

## 0.0.36–0.0.37 audit — 2026-08-14

- Clean rebuild and five relevant suites passed; build/client/server hashes match.
- No AIPort fatal/unhandled exception was found in the inspected logs.
- Open blockers: request field `NpcHeroId` is not validated against a peer-bound authoritative conversation target; regular NPC profiles are archetype-level because generic agents share `CharacterObject.StringId`; recent-event selection has no campaign-age cutoff.
- Full findings: `docs\AUDIT_0_0_36_0_0_37.md`.
- Narrative-only 0.0.37 remains usable; do not enable actions or persistence before the authority issue is fixed.

## 0.0.38 development milestone

Target leases, canonical target-instance identities, bounded memory between dialogues, 14-day recent-event filtering and the narrative-only action authorization gate are implemented. Runtime build/test/deploy results are recorded in `docs/PORT_LOG.md` and `docs/AGENT_HANDOFF.md`.

## Runtime deployment verification — 0.0.38-dev (2026-08-14 09:53 +05:00)

- Disposable server PID `31560` is running on `aiport-m0`; live campaign saves were not touched.
- Dedicated server reached `SERVING` at `09:50:03` with AIPort `0.0.38-dev`, protocol `1`.
- Explicit config path is correct: `E:\BCOOP\aiport-server.json`; backend resolves to Groq / `llama-3.1-8b-instant`.
- Runtime is currently **narrative-disabled** because the restarted process has no `AIPORT_API_KEY`: `keyPresent=False`, `enabled=False`. The key is intentionally not stored in config, scripts, logs, or documentation. Re-provide it only through the launch environment, then restart and re-check `keyPresent=True`, `enabled=True`.
- Client/server runtime SHA-256 match: `c956fd49b0647796bb3b2fc48c7ecf7738ea7146ed23a1b6ca96761814790117`.
- Client/server bootstrap SHA-256 match: `8357d5c56766dabba2777d472b87b1919a1925242b6d1f8a00bf99fd653934cf`.
- Deployment rollback: `backups\m0-20260814-094629`; source rollback: `backups\source-20260814-094212-pre-0.0.38`.

## Groq runtime re-enabled — 0.0.38-dev (2026-08-14 10:00 +05:00)

- The disposable server was restarted with `AIPORT_API_KEY` supplied only in the child process environment; the credential was not written to source, config, scripts, logs, or documentation.
- Current server PID: `7764`; save: `aiport-m0`; live campaign saves remain untouched.
- Sanitized startup proof at `09:59:15`: `configPath="E:\BCOOP\aiport-server.json"`, `backend="Groq"`, `explicitlyEnabled=True`, `enabled=True`, `keyPresent=True`, model `llama-3.1-8b-instant`.
- Dedicated server reached `SERVING` at `09:59:31`. No AIPort fatal/unhandled exception was present in the startup scan.
- The earlier `keyPresent=False` note is retained as historical evidence of the first restart and is superseded by this successful launch.

## 0.0.39-dev hotfix — transient player resolution (2026-08-14 10:10 +05:00)

- Runtime failure reproduced from the first 0.0.38 manual test. At `10:05:22` the client opened target `lord_5_13` while the join baseline was still settling; the authoritative `IPlayerManager` mapping was not ready, so the server correctly rejected the bind with `player_unresolved`.
- Client defect: `AIConversationTargetBound(Accepted=false)` treated this transient condition as permanent, cleared deferred player text, and never retried. No `AIConversationRequest` reached Groq, which explains the missing reply.
- Fix: client retries only `player_unresolved` target binds every `1000 ms`, bounded to `30` attempts, retains deferred text, and submits it automatically after an accepted lease. Permanent target errors are still surfaced and never retried. Server validation, lease ownership, stale-target rejection, protocol version and protobuf field numbers are unchanged.
- Build: `0.0.39-dev`, protocol `1`. Runtime SHA-256: `97ecba351f44f2650761fa764d043c7f8c8efd711060dda988be09d4650e6e46`; bootstrap unchanged: `8357d5c56766dabba2777d472b87b1919a1925242b6d1f8a00bf99fd653934cf`.
- Regression suites 0.0.33–0.0.38 remain green; new 0.0.39 retry suite: `9/9`.
- Source rollback: `backups\source-20260814-100903-pre-0.0.39`. Live deployment waits for the active game client to close.

## 0.0.39 deployment verified (2026-08-14 10:11 +05:00)

- Client and dedicated server deployed from the green 0.0.39 build. Deployment rollback: `backups\m0-20260814-101025`.
- Runtime SHA-256 matches on client/server: `97ecba351f44f2650761fa764d043c7f8c8efd711060dda988be09d4650e6e46`; bootstrap remains `8357d5c56766dabba2777d472b87b1919a1925242b6d1f8a00bf99fd653934cf`.
- Disposable server PID `11424` loaded build `0.0.39-dev`, protocol `1`, Groq `enabled=True`, `keyPresent=True`, and reached `SERVING` at `10:11:10`.
- No AIPort fatal/unhandled exception appeared in the startup scan. The client may be relaunched for the corrected manual test.

## 0.0.40-dev hotfix — authoritative peer resolution and paused retry (2026-08-14 18:25 +05:00)

- Second manual failure reproduced at `18:20`: compatible 0.0.39 client entered campaign, but target `lord_5_13` remained rejected with `player_unresolved`. The client did retry at `18:20:02`, proving the 0.0.39 retry path executed, but retries then stopped because they were driven by `CampaignEvents.TickEvent` while the conversation had paused campaign time. No `AIConversationRequest` or Groq call occurred.
- Server-side diagnosis: direct `PlayerManager.TryGetPlayer(NetPeer)` can miss when Coop replaces the LiteNetLib peer wrapper during join. The player/controller registry remains authoritative.
- 0.0.40 fix: first retain direct authoritative lookup; if it misses, resolve only through `IPlayerManager.Players` plus `TryGetPeer(controllerId)` and require exact live `NetPeer.Id` equality. Client-provided player identity is never used. Target-binding retries now run on the application tick, so campaign pause cannot stop them; the 30-attempt bound remains.
- Security properties remain: server-issued lease, exact peer/conversation/target/instance matching, stale/closed rejection, and narrative-only action gate. Protocol remains `1`; protobuf field numbers are unchanged.
- Tests: all 0.0.33–0.0.39 regressions green after the implementation update; new 0.0.40 suite `10/10`.
- Runtime SHA-256: `653603d07c29234013c8512bb7a857035ddb188530e11172f79df8e4c7dad721`; client/server hashes match. Bootstrap unchanged at `8357d5c56766dabba2777d472b87b1919a1925242b6d1f8a00bf99fd653934cf`.
- Source rollback: `backups\source-20260814-182234-pre-0.0.40`; deployment rollback: `backups\m0-20260814-182343`.
- Disposable server PID `12392` loaded `0.0.40-dev`, Groq `enabled=True`, `keyPresent=True`, and reached `SERVING` at `18:24:28`. Runtime conversation success still requires the next client reconnect test.
## 0.0.41-dev — accepted Coop join identity (2026-08-14)

- 0.0.40 completed all 30 application-tick retries but remained `player_unresolved`; no request reached Groq.
- `coop.debug.players.list` proved the server registry contained one resolved player while AIPort failed. Thus the 0.0.39 transient-race explanation and 0.0.40 `TryGetPeer` fallback were incomplete.
- 0.0.41 captures the controller ID from Coop's own `NetworkClientValidate`/`NetworkTransferNewHero` join flow for the exact connection generation. First identity wins; conflicts, stale connection tokens, disconnects and reused peer IDs are rejected/cleared.
- Fallback resolution requires one live matching `IConnectionCollection` entry already in `CampaignState`/`MissionState`, then resolves that accepted controller through the server `IPlayerManager`. It never trusts `ClaimedPlayerHeroId` and never selects a sole-player candidate.
- Protocol remains 1; protobuf fields are unchanged; target leases, stale/spoof checks and narrative-only actions remain intact.
- New suite: 13/13 structural checks plus 17 executable lifecycle/security scenarios. All 0.0.33–0.0.40 regressions pass.
- Runtime: 120,320 bytes, SHA-256 `affd98051450a4c01960ccda09d3897ca7460a7a9a63cccb40b91eede0de90a2`; bootstrap unchanged `8357d5c56766dabba2777d472b87b1919a1925242b6d1f8a00bf99fd653934cf`.
- Source rollback: `backups\source-20260814-184456-pre-0.0.41`; deployment rollback: `backups\m0-20260814-190457`. Deployed client/server hashes match the build; startup confirmed `0.0.41-dev`, protocol 1, Groq enabled with process-only key, campaign ready and `SERVING`.
## 0.0.42-dev — address-independent authoritative peer resolution (2026-08-14)

- Runtime 0.0.41 proved that accepted join identity capture works: peer 0 opened a generation at 19:16:21, controller `DESKTOP-ADLK0J9-wot_2` was frozen from `NetworkClientValidate` at 19:16:35 and confirmed by `NetworkTransferNewHero` at 19:17:44. Both tested conversations nevertheless exhausted target-bind retries with `player_unresolved`.
- The remaining defect was the 0.0.41 `SameAddress` comparison between the later AIPort message peer wrapper and Coop's stored connection peer. Transport address object equality is not a stable identity relation when Coop/LiteNetLib supplies different wrappers.
- 0.0.42 removes address equality only. Resolution still requires the connected message peer, the controller frozen during the exact current join generation, exactly one live server `IConnectionCollection` entry with the same server-assigned peer ID in `CampaignState` or `MissionState`, and an exact controller lookup in the authoritative `IPlayerManager`. Client `ClaimedPlayerHeroId` and sole-player selection remain forbidden.
- Connection lifecycle was additionally hardened: duplicate `PlayerConnected` for the same token is idempotent; disconnect requires the exact current token; stale disconnect cannot clear a reused peer ID; every new generation clears an old lease and cancels old backend work before accepting traffic.
- Wider audit fixes: failed ThreadPool dispatch now releases inflight/worker bookkeeping and returns a safe error; inactive per-controller rate-limit buckets are pruned. Protocol subscriptions are symmetric, protobuf fields are unique/stable, memory/lease/replay collections remain bounded, credentials remain environment-only, and no action execution or `Hero.MainHero` mutation exists.
- Protocol remains `1`; request fields 1–9 and result fields 1–8 are unchanged.
- Final recent suite: all 11 tests from 0.0.33 through 0.0.42 passed. 0.0.41 executable lifecycle harness passed 22 scenarios; 0.0.42 address/state checks passed 14/14; lifecycle/consistency checks passed 14/14.
- Runtime: 123,392 bytes, SHA-256 `68e539037711cf927528a6a08532a06b10f2a97b93ab16c54406c12c79681989`; bootstrap unchanged at `8357d5c56766dabba2777d472b87b1919a1925242b6d1f8a00bf99fd653934cf`.
- Source rollback: `backups\source-20260814-192803-pre-0.0.42`; deployment rollback: `backups\m0-20260814-201002`. Matching client/server hashes were verified. Server PID 19296 loaded `0.0.42-dev`, protocol 1, Groq enabled with a process-only key, reached campaign ready and `SERVING`; no fatal/unhandled startup error was present. Conversation-path proof remains the next manual check.


## 0.0.43-dev — canonical campaign-object resolution (2026-08-15 00:03 +05:00)

- Raw 0.0.42 log-template analysis proved `PlayerContextResolver` succeeded; `ConversationTargetValidator` emitted the quoted `ErrorCode="player_unresolved"` after generic `MBObjectManager` failed to find the authoritative player hero.
- Bannerlord IL confirms `Hero.Find(id)` uses `Campaign.Current.CampaignObjectManager.Find<Hero>(id)`. 0.0.43 uses that canonical path for authoritative player heroes, hero targets, stale checks and prompt heroes; mobile parties use `CampaignObjectManager.Find<MobileParty>`.
- Peer/controller generation checks and target leases are unchanged; no client identity, sole-player fallback or `Hero.MainHero` substitution was added.
- New suite 15/15; all 12 suites from 0.0.33 through 0.0.43 pass. Build: 123,904 bytes, SHA-256 `d8cdb53436d8708c5ca279f23c030f5a44de94e6dcd624a997a3f65caaf5cfcf`; bootstrap unchanged.
- Source rollback: `backups\source-20260814-235539-pre-0.0.43`. Deployment/runtime proof pending guarded replacement.
- Full report: `docs\HOTFIX_0_0_43_CAMPAIGN_OBJECT_REGISTRY.md`.

## 0.0.43 deployment verified — 2026-08-15 00:05 +05:00

- Guarded deployment completed with rollback `backups\m0-20260815-000420`.
- Build/client/server runtime DLLs are identical: 123,904 bytes, SHA-256 `d8cdb53436d8708c5ca279f23c030f5a44de94e6dcd624a997a3f65caaf5cfcf`.
- Bootstrap remains identical on both sides: SHA-256 `8357d5c56766dabba2777d472b87b1919a1925242b6d1f8a00bf99fd653934cf`.
- Disposable server PID `12808` loaded `0.0.43-dev`, protocol 1, reached CampaignReady and `SERVING`; startup scan found no fatal/unhandled error.
- Log: `artifacts\runtime-m0\logs\coop-server-20260815-000430.log`. Live campaign saves were not touched.
- The replacement process did not inherit `AIPORT_API_KEY`, so sanitized startup reports `keyPresent=False`, `enabled=False`. This is intentional fail-closed behavior; target-bind runtime proof can use the safe stub. Re-enable Groq only by supplying the key through the server process environment, never through config, logs, docs, protocol or chat.
- Next runtime gate: reconnect, open one hero dialogue, and require `target bound` followed by an accepted request and stub result.

## 0.0.44-dev — successful controlled-object runtime proof (2026-08-15 00:42 +05:00)

- Live 0.0.43 proved Bannerlord campaign registries still could not resolve synchronized `Hero_Player`; Coop's own debug command resolved `Hero_Player` and `MobileParty_Player` through its `IObjectManager` and marked both controlled.
- 0.0.44 uses that canonical Coop path and verifies `IPlayerManager.Contains` ownership before validation.
- Live 0.0.44 proof succeeded: compatible handshake, `target bound` for `lord_5_1`, conversation accepted with `Hero_Player`/`MobileParty_Player`, and a 94-character stub result sent with memory turn 1.
- Conversation `a23fa21c52624e27b38a5c03c9c86989`; request `1f7c0d6a9d41494e907625a8166b6709`; log `artifacts\runtime-m0\logs\coop-server-20260815-002737.log`.
- 13/13 suites pass. Client/server SHA-256: `99ba6493e32e3cfe1427b6a264de564e5c477c3573949e175c55c9de7cbb2805`.
- Target resolution is proven fixed. Groq proof remains blocked only by `keyPresent=False`; re-enable exclusively through the server process environment.
- Full report: `docs\HOTFIX_0_0_44_COOP_CONTROLLED_OBJECT_REGISTRY.md`.

## 0.0.45–0.0.47 cumulative foundation deployed (2026-08-15 01:18 +05:00)

- Final build `0.0.47-dev`, protocol 2 adds capability negotiation, strict no-op intents, bounded volatile audit, generation-bound state manifests/snapshots and persistent private player–NPC memory.
- Intent execution is still mutation-free: only exact `no_op` + `{}` validates; unknown type/payload/generation/revision fails closed.
- State files are SHA-256 verified, atomically replaced, campaign/save/time bound and enter read-only recovery on mismatch. Disposable state root only: `artifacts\runtime-m0\aiport-state`.
- Private snapshots are filtered by authoritative player hero; active peer/conversation state is never restored from disk.
- 16 suites from 0.0.33 through 0.0.47 pass. Runtime hash: `aae72b62d9f81e768c35b1f6847f8ec789c7cb99288c64aa4a60c03af56f93ea`.
- Source rollback: `backups\source-20260815-010650-pre-0.0.45-47`; deployment rollback: `backups\m0-20260815-011532`.
- Server PID 36200 reached CampaignReady/SERVING with Groq enabled and a new writable disposable state generation. JIP and runtime completed-save reload are deferred.
- Full report: `docs\MILESTONE_0_0_45_0_0_47_FOUNDATIONS.md`.

## 0.0.48-dev — return-to-vanilla cancellation hotfix (2026-08-15 02:29 +05:00)

- Solo 0.0.47 runtime proved protocol-2 handshake, capability flags 15, real Groq (`Stub=False`), same-NPC memory across turns/conversations, and separate-NPC isolation.
- Defect: selecting `Return to normal dialogue` left the AI request/display path alive until the entire Bannerlord conversation ended. A response could therefore overwrite the vanilla dialogue text after the player had already left the AI branch.
- The finish option now invokes an explicit client bridge callback. It cancels the pending request, cancels retries/timeouts, clears deferred text and queued display, and sends the ownership-bound server cancel before returning to the vanilla dialogue graph.
- The NPC target lease and outer Bannerlord conversation remain open, so the AI option can be selected again safely in the same encounter. Late results no longer match a pending request and cannot be queued into vanilla UI.
- Runtime evidence also showed the final full-conversation-exit attempt already worked server-side: request `11e35916ac3c4c9ea30bf748ce70534e` was canceled, HTTP abort was requested, and the backend result was suppressed with zero remembered turns. The hotfix closes the narrower AI-branch exit race.
- Protocol remains 2; protobuf contracts are unchanged; gameplay mutations remain disabled.
- New 0.0.48 structural suite: 12/12. Build succeeded: `AIPort.dll` 153,600 bytes, SHA-256 `fa7f71b10094a7a14765c9751f92a90820ee9c851775abc9994be70d3f1e4f56`; bootstrap unchanged.
- Source rollback: `backups\source-20260815-022554-pre-0.0.48`. Deployment/runtime proof pending.
- Full report: `docs\HOTFIX_0_0_48_RETURN_TO_VANILLA_CANCEL.md`.

## 0.0.48 deployment verified (2026-08-15 02:31 +05:00)

- Guarded deployment completed with rollback `backups\m0-20260815-022941`.
- Client/server runtime hashes match: `fa7f71b10094a7a14765c9751f92a90820ee9c851775abc9994be70d3f1e4f56`; bootstrap unchanged.
- Disposable server PID `18692` loaded `0.0.48-dev`, protocol 2. Groq is enabled with process-only credentials (`keyPresent=True`), campaign state is writable, and campaign-ready was reached.
- Log: `artifacts\runtime-m0\logs\coop-server-20260815-022952.log`. No live campaign save was touched.
- Launcher was closed for DLL replacement. Manual runtime gate: send a long AI request, immediately choose `Return to normal dialogue`, and verify no AI result overwrites the vanilla branch. Expected logs include `AI dialogue branch closed`, ownership-matched cancel, and either backend suppression or harmless ignored late result.

## 0.0.48 runtime proof passed (2026-08-15 02:37 +05:00)

- Client/server handshake matched `0.0.48-dev`, protocol 2.
- Request `dda0c1c6e56b43dcbb6aa53d10f79f6b` was accepted for conversation `231c3d62e48f4962ba2131b1ed8185eb`.
- Selecting `Return to normal dialogue` emitted the new branch-close marker with `PendingRequestCanceled=true` and sent cancel reason `return_to_vanilla`.
- Server ownership matched, marked the backend request canceled, released inflight state, requested HTTP abort, and suppressed the late result. No AI result was applied to vanilla dialogue.
- The 0.0.48 branch-lifecycle defect is closed.

## 0.0.49-dev — startup state synchronization (2026-08-15 02:43 +05:00)

- Fixed the protocol-2 startup gap where the first private snapshot could return `player_unresolved` before Coop registered `Hero_Player`, leaving snapshot/no-op incomplete for the whole connection.
- Snapshot requests now retry only transient `player_unresolved`, use the server delay hint clamped to 500–5000 ms, and stop after 30 attempts. Retry timers are bound to the local connection generation and canceled on disconnect/dispose/capability refresh.
- Snapshot acceptance now requires exact negotiated campaign generation, non-stale revision, valid SHA-256 over UTF-8 JSON, and a new generation/revision/hash key. Duplicate, stale, wrong-generation and hash-mismatched snapshots fail closed.
- A server-reported generation mismatch triggers a fresh capability negotiation instead of trusting the mismatched snapshot.
- The automatic `no_op` is sent exactly once only after `SnapshotReady`; its result must match the request, generation and revision before `NoOpValidated` is logged.
- Protocol remains 2 and protobuf contracts are unchanged. No gameplay mutations were enabled.
- Test `test_0_0_49_startup_state_sync.py`: 17/17 plus executable retry/deduplication model. Clean build: `AIPort.dll` 159,232 bytes, SHA-256 `c00543f4b0ccd470c773d2f1d643cb305691b7bc1aa3ee5a5f0ca83cf866a15d`; bootstrap unchanged.
- Source rollback: `backups\source-20260815-024037-pre-0.0.49`. Deployment/runtime proof pending.
- Full report: `docs\MILESTONE_0_0_49_STARTUP_STATE_SYNC.md`.

## 0.0.49 deployment verified (2026-08-15 02:47 +05:00)

- MCP recovered and guarded deployment completed with rollback `backups\m0-20260815-024548`.
- Client/server runtime DLL hashes match: `c00543f4b0ccd470c773d2f1d643cb305691b7bc1aa3ee5a5f0ca83cf866a15d`; bootstrap remains `8357d5c56766dabba2777d472b87b1919a1925242b6d1f8a00bf99fd653934cf`.
- Disposable server PID `18988` loaded `0.0.49-dev`, protocol 2, Groq `enabled=True` / `keyPresent=True`, writable state generation `4ea97daf7c4e8ae14149a02cff988e72`, and reached `SERVING`.
- Log: `artifacts\runtime-m0\logs\coop-server-20260815-024558.log`; no fatal/unhandled startup error was found. Live campaign saves were not touched.
- Launcher remains closed after DLL replacement. Runtime gate: reconnect and require transient snapshot retry followed by `SnapshotReady` and `NoOpValidated`.

## 0.0.49 runtime proof passed (2026-08-15 02:50 +05:00)

- Compatible client/server handshake completed on `0.0.49-dev`, protocol 2, capability flags 15.
- The first snapshot correctly hit transient `player_unresolved`; attempts 1–3 retried at 1000 ms without stale application.
- Attempt 4 observed the legitimate Coop transfer-save generation transition from `4ea97daf7c4e8ae14149a02cff988e72` to `1b7043b8d7dff4d51981d03dccc9e9ed`. The client rejected the mismatched snapshot and renegotiated capabilities.
- The refreshed generation produced a SHA-256-verified 25-character private snapshot and logged `SnapshotReady`.
- Correlated no-op request `95e10d334291453eb257fe30eb180684` received server-issued intent `62e315927c0347a2a53b6d2f7f266b02`, status `validated`, reason `no_mutation`, revision 0, followed by `NoOpValidated`.
- This closes the 0.0.49 startup synchronization runtime gate, including the real generation-transition path.

## Solo persistence runtime test prepared (2026-08-15 02:59 +05:00)

- Created and validated an empty disposable baseline after a successful guarded save: generation `f38452afdeecec27e1d29ae5bf77e2e7`, revision 0, record count 0, empty-memory SHA-256 `e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855`.
- Added an allowlisted disposable-server console helper (`status`/`save`) and preflight-tested the save command.
- Added a runtime verifier for manifest integrity, memory hash, record count, unique IDs, player scope, marker and target.
- Player marker for the test is `медный грифон 9042`; full runbook: `docs\SOLO_PERSISTENCE_RUNTIME_TEST_0_0_49.md`.

## Solo persistence save/restart proof — server phase passed (2026-08-15 03:03 +05:00)

- Player recorded marker `медный грифон 9042` with named hero Удрис Винодел, authoritative target `hero:CharacterObject_1649`.
- Two confirmed turns archived under `Hero_Player`; save completed as `saved:2`, revision 2.
- Manifest verification passed: generation `4ea97daf7c4e8ae14149a02cff988e72`, record count 2, unique record IDs, marker and target present, memory SHA-256 `d5caf900ba5cf36407bcf2b7ce4d18088397b8aa3841de8749e50b37c424c47e`.
- Disposable server was fully stopped and restarted. It loaded `0.0.49-dev`, Groq enabled, reached `SERVING`, and restored state as `loaded:2`, the same generation/revision, `ReadOnly=False`.
- Post-restart disk verification passed unchanged. Remaining runtime gate: reconnect and ask the same hero for the marker; then confirm restored history was used by the new Groq request.

## Solo persistent-memory runtime proof passed (2026-08-15 03:05 +05:00)

- After the full server restart, the client reconnected on `0.0.49-dev` / protocol 2.
- Server had restored `loaded:2`, revision 2, writable state. Post-join capability generation transitioned safely to `1b7043b8d7dff4d51981d03dccc9e9ed`; private snapshot was ready with 1,560 characters and revision 2.
- `SnapshotReady` hash: `5869709245a4667a827e6323e68ba38de157eef54074ed8946093c52c4c19d49`; correlated no-op validated with intent `3cddf11120454593a3da9082d4881d71`.
- The player reopened Удрис Винодел, resolving to the same `hero:CharacterObject_1649`, and asked for the pre-restart marker. Real Groq request `abc040bb648a4d9a9ceebfacd3e84b02` completed with `Stub=False`; the player confirmed the NPC recalled `медный грифон 9042`.
- This proves confirmed turn → archive → atomic save/hash → full process restart → `loaded:2` → private snapshot → same-target restored prompt history → real model response. The solo persistence gate is closed; two-client JIP/privacy remains deferred.

## 2026-08-16 - 0.0.50 relation-shadow deployment

- Built and deterministically tested `0.0.50-dev` (protocol 2, intent schema 2).
- Runtime SHA-256: `d9a905310edb452ee94519f175175003ceb889e239498eb0323e9aaa20e21c70`; bootstrap SHA-256: `8357d5c56766dabba2777d472b87b1919a1925242b6d1f8a00bf99fd653934cf`.
- Added gated, non-mutating `relation_change_shadow` and client probes `/relation-shadow +1/-1`.
- Pre-runtime backup: `backups\pre-runtime-0.0.50-20260816-214447`; rollback: `backups\m0-20260816-214447`.
- Matching DLLs deployed to client and disposable server; the gate is enabled only for M0.
- `aiport-m0` loaded `0.0.50-dev`, restored 2 records at revision 2, and reached SERVING.
- Log: `artifacts\runtime-m0\logs\coop-server-20260816-214459.log`.
- Groq is disabled because no API key was present in this launch environment; shadow validation remains available.
- Pending: client flags 31 and manual proof of unchanged relation/revision.
- Details: `docs\MILESTONE_0_0_50_RELATION_SHADOW.md`.

## 2026-08-16 - 0.0.50 live shadow proof

- Client negotiated flags 31. Both `/relation-shadow +1` and `/relation-shadow -1` returned `shadow_validated / mutation_suppressed`.
- Both probes recorded `MutationApplied=False` and revision `2 -> 2` for `hero:CharacterObject_1649`.
- Runtime proof: `artifacts\runtime-m0\logs\coop-server-20260816-214459.log`.
- Server restarted with Groq enabled and restored 2 records at revision 2.
- Current startup log: `artifacts\runtime-m0\logs\coop-server-20260816-215513.log`; wrapper PID 7048.
- Pending only a normal Groq dialogue regression turn.


## 2026-08-16 - 0.0.51 relation confirmation milestone

- Added two-step relation proposal/confirmation in shadow mode; capability 32, total expected flags 63, intent schema 3.
- Proposal and confirmation are peer/player/generation/revision/conversation/lease/target bound, 60-second, single-use and idempotent by request ID.
- Both stages retain `MutationApplied=false`; no native relation API is present.
- Automated 0.0.50 and 0.0.51 suites pass. Runtime SHA-256: `4f27bcf5bf79dc6f62b58d983414a0533b2df2283cb2b85929000286e63eef2c`.
- Source rollback: `backups\source-20260816-220321-pre-0.0.51`; deployment rollback: `backups\m0-20260816-221531`.
- Matching client/server 0.0.51 deployed. Disposable server PID 17840 reached SERVING with Groq enabled and restored revision 2.
- Startup log: `artifacts\runtime-m0\logs\coop-server-20260816-221546.log`.
- Original AIInfluence diplomacy audit recorded in `docs\ORIGINAL_AIINFLUENCE_DIPLOMACY_AUDIT.md`.
- Pending one bundled runtime acceptance pass; no separate micro-checks.


## 2026-08-16 - 0.0.52 runtime revision-sync hotfix

- Found the 0.0.51 runtime failure: a completed dialogue advanced memory revision from 2 to 3 while the client retained revision 2 from initial capabilities, causing `stale_revision` for relation intents.
- Added optional protobuf field 9 (`StateRevision`) to `AIConversationResult`; existing field numbers remain unchanged.
- Server now returns the authoritative post-turn revision and the client adopts it before later intent requests.
- Build `0.0.52-dev`, runtime SHA-256 `cfeb1d212f6e3685c322e9252256ed3dd7676f377d19dfaeb23363f2bf12c69e`.
- Source rollback: `backups\source-20260816-222419-pre-0.0.52`; deployment rollback: `backups\m0-20260816-222530`.
- Runtime proof in `artifacts\runtime-m0\logs\coop-server-20260816-222530.log`: flags 63, snapshot/no-op valid, revision 2 -> 3 after dialogue, proposal +1 required confirmation, confirmation returned `confirmed_shadow / mutation_suppressed`, and shadow -1 returned `shadow_validated / mutation_suppressed`; all relation operations had `MutationApplied=False`, revision stayed 3.
- The one-step `/relation-shadow` command intentionally does not require confirmation; only `/relation-propose` followed by `/relation-confirm` uses the two-step lifecycle.
- Groq was not part of this restarted runtime proof because the new process had no `GROQ_API_KEY`; the dialogue correctly used the stub path.


## 2026-08-16 - 0.0.53 persistent social shadow ledger

- Confirmed relation proposals now produce a private custom social receipt; native Bannerlord relation mutation remains suppressed.
- Added score caps -25..25, five-second per-player/target cooldown, 512-record bound and receipt idempotency.
- Added `social.ndjson`, optional manifest social hash/count, fail-closed corruption handling, private snapshot projection and combined revision restoration.
- All cumulative suites and the new executable persistence/integrity harness pass.
- Build SHA-256: `263b12abd00d4aaa153c2d997f8c6012a53c0aaf314226f9d43675beaef377bd`.
- Source rollback: `backups\source-20260816-223904-pre-0.0.53`; deployment rollback: `backups\m0-20260816-224136`.
- Matching client/server build deployed; startup migrated old state as `loaded:2:social:0`, revision 2, read-only false and reached SERVING.
- Pending one bundled create -> save -> restart -> reconnect persistence gate.


## 2026-08-16 - 0.0.54/0.0.55 persistence identity and diplomacy snapshot

- Fixed transient Coop `TransferSave` identifiers changing the external-state generation. The load-established identity is now stable through saves; the executable harness tests intentionally swapped identifiers.
- Added authoritative read-only `/diplomacy-snapshot` with capability 64 (expected total flags 127), campaign generation/revision binding and authoritative player resolution.
- Snapshot reports kingdoms, player-relative war/peace stance, settlement count and army count; no diplomatic or native campaign mutation API is referenced.
- All cumulative and new structural suites pass.
- Deployed matching client/server `0.0.55-dev`, 189440 bytes, SHA-256 `4554aeab6e98eac336fcfd255ef2d72dccb5d56655c5dcdbf4546c6774bda979`.
- Deployment rollback: `backups\m0-20260816-225841`.
- Disposable server PID 8604 reached SERVING; state `loaded:2:social:0`, revision 2, read-only false.
- Manual testing is deferred until the next multi-feature bundled gate.


## 2026-08-16 - 0.0.56 persistent diplomatic shadow statements

- Added `/diplomacy-propose war`, `/diplomacy-propose peace`, and `/diplomacy-confirm`.
- Added capability 128; expected negotiated flags are now 255.
- Proposal/confirmation is bound to peer, player, generation, revision, dialogue, target lease, target instance and authoritative source/target kingdom pair.
- Added persistent `diplomacy.ndjson`, SHA-256 manifest validation, fail-closed read-only behavior, private snapshot projection, 256-record bound, 30-second cooldown and idempotency.
- War/peace preconditions are validated twice; no native war/peace action is invoked.
- All cumulative and executable suites pass.
- Deployed matching client/server `0.0.56-dev`, size 207872, SHA-256 `cddcb498dfe3337b20940e63e3fc9861de07447213f7baad4382c47b535e49b2`.
- Rollback: `backups\m0-20260816-231202`; PID 1328; startup `loaded:2:social:0:diplomacy:0`, revision 2, read-only false, SERVING.
- Manual testing remains deferred until the bundled diplomacy/social/persistence/JIP/Groq gate.


## 2026-08-16 - 0.0.57 final bundled runtime gate

- Added `/aiport-gate baseline`, `/aiport-gate report` and `/aiport-status`.
- Added capability 256; expected flags are now 511.
- Gate reports build, protocol, generation/revision, persistence health, backend booleans, authoritative player/target, native relation/war state, private record counts, custom score and latest diplomatic statements.
- Baseline comparison directly reports native relation and war-state PASS/FAIL plus memory/social/diplomacy/revision deltas.
- Added secret-safe Groq launcher, disposable save helper, automated log/manifest gate checker and full Russian runtime guide.
- All cumulative 0.0.50-0.0.57 suites pass; helper syntax checks pass.
- Deployed matching client/server `0.0.57-dev`, size 218112, SHA-256 `e3b96d3319339934a28851eb9416a693d5b8794b246587160e7ae15128165195`.
- Rollback: `backups\m0-20260816-232407`; PID 19884; startup `loaded:2:social:0:diplomacy:0`, revision 2, read-only false, SERVING.
- Current smoke server is keyless by design. Start the manual gate with `tools\start_0_0_57_gate_with_groq.cmd`.


## 2026-08-16 — runtime verdict 0.0.57 and 0.0.58 faction-aware follow-up

- Corrected verdict for 0.0.57: `PASS_CORE_WITH_GAPS`. Groq real turn, 5 social receipts across 2 NPCs, reconnect/JIP, private snapshot, stable automatic save and native mutation suppression passed.
- Social cooldown was not reached because confirmation intervals exceeded the old 5-second window; raised to 15 seconds.
- Diplomacy was blocked twice by `player_kingdom_required`; replaced kingdom-only authority with authoritative `Hero.MapFaction` support for independent clans.
- Added structured native relation/war comparison fields to server logs and fixed quoted-field/split-Groq parsing in the checker.
- 1653 Coop ObjectManager warnings were classified as upstream world-sync noise; no AIPort fatal exception occurred.
- Deployment restart itself proved `loaded:3:social:5:diplomacy:0`, revision 8, same canonical generation, read-only false.
- Deployed matching client/server `0.0.58-dev`, size 219648, SHA-256 `8c4e24e6a57147ce7f2d39f39ddb84522e3580c7d918d1b318bbc1c09ae2c5c8`.
- Source rollback `backups\source-20260816-234547-pre-0.0.58`; deployment rollback `backups\m0-20260816-235142`; PID 22372; SERVING.

## 2026-08-17 — 0.0.59 diplomatic authority shadow gate

- Added a server-authoritative diplomatic authority evaluator before any war/peace shadow proposal can reach confirmation or persistence.
- Kingdom statements now require the authoritative player hero to be the current kingdom ruler. Independent-clan statements require the authoritative player hero to be the leader of that clan while it has no kingdom.
- The current NPC must independently be the ruler of the target kingdom or the leader of an independent target clan.
- Authority is evaluated from server campaign objects at proposal time and re-evaluated at confirmation time. Changes between the two stages fail closed as `stale_diplomatic_authority`.
- `/diplomacy-authority` is a read-only alias of `/diplomacy-snapshot`; both now show source/target role and `authority=PASS/FAIL`.
- `/aiport-status` and the validation-gate server audit now include source and target diplomatic authority.
- Added capability `512`; expected cumulative flags are `1023`.
- No native relation, war, peace, ownership or economy mutation API was added. All diplomatic receipts remain shadow-only and `NativeMutationApplied=false`.
- Cumulative suites 0.0.50–0.0.59 passed (8/8 current regression scripts), and the clean deterministic build succeeded.
- Build `0.0.59-dev`: 225280 bytes, SHA-256 `4c0a4976c9b9ecb59a09e6196d58b12b914b721611cd8834e654682c8990b230`.
- Source rollback: `backups\source-20260817-003522-pre-0.0.59`; deployment rollback: `backups\m0-20260817-003716`.
- Client/server hashes match. Disposable server PID `12184`; startup log `artifacts\runtime-m0\logs\coop-server-20260817-003716.log`.
- Startup restored `loaded:3:social:5:diplomacy:0`, generation `4ea97daf7c4e8ae14149a02cff988e72`, revision `8`, `ReadOnly=False`, then reached `SERVING`.
- The smoke server is intentionally keyless (`enabled=False`, `keyPresent=False`). Use `tools\start_aiport_with_groq.cmd` only when a real AI-turn is needed.

## 2026-08-17 — 0.0.60 durable recipient consent

- Confirmed diplomatic proposals now become durable `pending_recipient` negotiations rather than final unilateral shadow statements.
- Each negotiation is persisted with a 24-hour UTC expiry, target leader, lifecycle status, resolution time and resolving hero.
- Only the exact authoritative target leader may accept or reject; recipient identity is derived from the requesting Coop peer and never from client payload.
- Added `/diplomacy-inbox`, `/diplomacy-accept <id>` and `/diplomacy-reject <id>`.
- Acceptance and rejection are idempotent across retries and full process restarts; an opposite second decision fails closed.
- Expired negotiations transition to `expired`; stale revision, generation mismatch, wrong recipient and unavailable persistence all fail closed.
- Private snapshots expose a negotiation only to its source hero and target recipient hero.
- Old diplomacy records load through a backward-compatible `legacy_shadow_recorded` migration path.
- Added capability `1024`; expected cumulative flags are `2047`.
- Native war/peace/relation/ownership/economy mutation remains absent; every lifecycle transition logs `NativeMutationApplied=false`.
- Cumulative suites 0.0.50–0.0.60 passed (9/9 current regression scripts), including an executable expiry/idempotency/import harness.
- Build `0.0.60-dev`: 236032 bytes, SHA-256 `f5d1ecaf18c7391c0a5f38e2062e726d478bca6ab5c140d8e33daffa371ce11c`.
- Source rollback: `backups\source-20260817-004530-pre-0.0.60`; deployment rollback: `backups\m0-20260817-004804`.
- Client/server hashes match. Disposable server PID `2436`; startup log `artifacts\runtime-m0\logs\coop-server-20260817-004805.log`.
- Startup restored `loaded:3:social:5:diplomacy:0`, generation `4ea97daf7c4e8ae14149a02cff988e72`, revision `8`, `ReadOnly=False`, then reached `SERVING`.
- Manual participation is not required for this milestone. A real two-client recipient-consent run remains a later bundled acceptance gate, not a blocking micro-test.

## 2026-08-17 — 0.0.61 diplomacy conflict guard

- Added a direction-independent faction-pair lock: only one active `pending_recipient` negotiation may exist for a faction pair, regardless of direction or war/peace action.
- Reverse-direction and opposite-action conflicts fail closed as `diplomacy_pair_pending`.
- Added per-source and per-recipient bounds of 16 pending negotiations.
- Expired negotiations are durably transitioned to `expired`; each transition advances the unified revision and is included in structured audit logs.
- Acceptance now re-resolves both leaders and both factions, rechecks `PairAuthorized`, exact persisted faction IDs and current war/peace preconditions immediately before resolution.
- Stale authority, faction or precondition state rejects acceptance as `stale_diplomatic_context`, `already_at_war` or `not_at_war`.
- Rejection remains available to the exact recipient without requiring the source context to remain valid.
- Added `/diplomacy-history`; snapshots now include inbox plus the latest visible lifecycle history.
- Added capability `2048`; expected cumulative flags are `4095`.
- No native mutation adapter was added; all paths retain `NativeMutationApplied=false`.
- Cumulative suites 0.0.50–0.0.61 passed (10/10), including an executable conflict/expiry/release harness.
- Build `0.0.61-dev`: 239616 bytes, SHA-256 `dc42dcb854b785f139f4bcb2bbf385ce2efd744d16e141c14b6c3c67fbf17089`.
- Source rollback: `backups\source-20260817-005650-pre-0.0.61`; deployment rollback: `backups\m0-20260817-005744`.
- Client/server hashes match. Disposable server PID `25208`; startup log `artifacts\runtime-m0\logs\coop-server-20260817-005745.log`.
- Startup restored `loaded:3:social:5:diplomacy:0`, generation `4ea97daf7c4e8ae14149a02cff988e72`, revision `8`, `ReadOnly=False`, then reached `SERVING` without fatal/unhandled errors.
- Manual participation is not required for this milestone.

## 2026-08-17 — 0.0.62 diplomacy inbox notifications

- Added typed private `AIDiplomacyInboxNotification` events for incoming bilateral negotiations.
- A connected recipient receives an immediate notification when a new `pending_recipient` negotiation is created.
- Reconnect/JIP private-snapshot completion re-emits the current pending count, restoring awareness after a process or connection interruption.
- Notification routing uses the server-observed current connection token and authoritative player hero mapping; client payload cannot select a recipient.
- Ambiguous duplicate hero mappings fail closed and suppress delivery.
- Notifications contain only campaign generation, unified revision, pending count and latest visible statement ID; no other player's private state is exposed.
- Client validation checks protocol, capability, generation, revision, bounded count and canonical IDs, deduplicates repeated events, then displays the notice from the real-time application tick without altering conversation state.
- Added ledger projections `CountPendingIncoming` and `LatestPendingIncomingId`; resolved records disappear from pending notifications.
- Added capability `4096`; expected cumulative flags are `8191`.
- No native campaign mutation adapter was added.
- Cumulative suites 0.0.50–0.0.62 passed (11/11), including executable recipient-isolation/count/removal checks.
- Build `0.0.62-dev`: 245248 bytes, SHA-256 `9f1ea0aa32618bb3c709efa6d88e69ddc719451ec0745d5a196129cd3702f29f`.
- Source rollback: `backups\source-20260817-010305-pre-0.0.62`; deployment rollback: `backups\m0-20260817-010345`.
- Client/server hashes match. Disposable server PID `24432`; startup log `artifacts\runtime-m0\logs\coop-server-20260817-010346.log`.
- Startup restored `loaded:3:social:5:diplomacy:0`, generation `4ea97daf7c4e8ae14149a02cff988e72`, revision `8`, `ReadOnly=False`, then reached `SERVING` without fatal/unhandled errors.
- Manual participation is not required for this milestone.

## 2026-08-17 — 0.0.70 bundled diplomacy lifecycle + default-off native war adapter

This release intentionally combines the remaining bilateral shadow lifecycle and the first isolated native-war adapter.

### Lifecycle bundle

- Added source withdrawal with `/diplomacy-withdraw <statement-id>`; only the exact initiating hero may withdraw an active negotiation.
- Withdrawal is durable, idempotent and releases the canonical faction-pair lock.
- Added typed lifecycle notifications to both online parties for accept, reject, withdrawal, expiry and verified native commit.
- Reconnect/JIP recovery remains private-snapshot/history based.
- Durable records now retain the final reason, native commit UTC, committing hero and `NativeMutationApplied` audit bit.
- History and runtime gate expose lifecycle status, reason and native/no-native outcome.

### Native war adapter

- Added exactly one isolated native mutation call: `DeclareWarAction.ApplyByDefault` in `NativeWarAdapter.cs`.
- The adapter is default-off and requires both `enableNativeWarAdapter=true` in server config and exact process-environment arming. Neither is present in the deployed environment.
- An accepted bilateral `war` shadow record is mandatory; peace is not implemented by this adapter.
- `/diplomacy-ready <statement-id>` performs a non-mutating authoritative preflight.
- When armed, preflight issues a peer/hero/statement/generation/revision/faction-bound 60-second single-use commit token.
- `/diplomacy-native-war <statement-id> <commit-token>` is the only commit path.
- Authority, persisted faction IDs and current not-at-war precondition are checked before lease issue and again immediately before commit.
- The token is consumed on the first attempt, including unauthorized or stale attempts.
- A native commit is accepted only after `FactionManager.IsAtWarAgainstFaction` confirms the postcondition.
- No native peace, relation, ownership or gold mutation was added.

### Verification / deployment

- Cumulative suites 0.0.50–0.0.70: 12/12 PASS.
- Executable harness covers withdrawal authority/idempotency, pair-lock release, bilateral acceptance, lease binding/replay, durable native status and codec roundtrip.
- Static isolation audit found exactly one `DeclareWarAction.ApplyByDefault` call.
- Build `0.0.70-dev`: 269824 bytes; SHA-256 `fa1b4b8f2139e242ef23cd8b9273f874c09824f9d5a8155715c56c13f922e1a9`.
- Source rollback: `backups\source-20260817-011337-pre-0.0.70`.
- Deployment rollback: `backups\m0-20260817-012100`.
- Disposable server PID `27428`; startup log `artifacts\runtime-m0\logs\coop-server-20260817-012101.log`.
- Client/server hashes match; restored `loaded:3:social:5:diplomacy:0`, generation `4ea97daf7c4e8ae14149a02cff988e72`, revision `8`, `ReadOnly=False`, and reached `SERVING` without fatal/unhandled errors.
- Startup audit: `nativeWarConfigured=False`, `nativeWarEnvironmentArmed=False`, `nativeWarEnabled=False`.
- No native mutation was executed during build, tests, deployment or startup smoke.

## 2026-08-17 - 0.0.91-dev NPC-controlled diplomacy policy

- Restored the primary player-to-NPC ruler diplomacy path.
- NPC rulers are resolved by deterministic server policy; actual player-controlled rulers retain private manual consent.
- War challenges are acknowledged after authority/pair/precondition checks. Peace uses an authoritative relation threshold of -25.
- Raw LLM text cannot authorize state changes. Policy changes only durable shadow lifecycle.
- Cumulative suites 0.0.50-0.0.91: 14/14 PASS; clean build/startup smoke PASS.
- Runtime: 298496 bytes; SHA-256 `fef06ef5303c44dc548c3c38df0a6c80f75435a2eb1865fe0ed0afd6435d9dca`; client/server/artifact parity PASS.
- Source rollback: `backups\source-20260817-023529-pre-0.0.91`; deployment rollback: `backups\m0-20260817-023818`.
- Server PID 3212; log `artifacts\runtime-m0\logs\coop-server-20260817-023819.log`.
- State `loaded:3:social:5:diplomacy:0:nativeJournal:0`; generation `4ea97daf7c4e8ae14149a02cff988e72`; revision 8; writable; SERVING.
- War/peace adapters OFF; generation pin absent; no native mutation executed.
## 2026-08-17 - 0.0.98-dev authoritative NPC diplomacy recipient hotfix

- Runtime blocker found during the 0.0.97 client gate: scheduler offers went to `Player` / `main_hero` instead of the connected `Hero_Player`.
- Recipient selection is now bound to authoritative live `peer -> hero` mappings, with alias exclusion logging and a pre-record guard (`recipient_not_authoritative_online`).
- Cumulative suites `0.0.50..0.0.98`: 21/21 PASS; clean build and startup smoke PASS.
- Runtime: 345088 bytes; SHA-256 `c589cd0e3bb6ce610c543006bccaf46d97fde07d98ecd9bb6858fce68210a5f4`; client/server/artifact parity PASS.
- Source rollback: `backups\source-20260817-063750-pre-0.0.98`; deployment rollback: `backups\m0-20260817-064443`.
- Server PID 11220; log `artifacts\runtime-m0\logs\coop-server-20260817-064457.log`.
- State `loaded:3:social:5:diplomacy:2:nativeJournal:0`; generation `4ea97daf7c4e8ae14149a02cff988e72`; revision 10; writable; SERVING.
- War/peace adapters OFF; gate stub; no native mutation executed.
