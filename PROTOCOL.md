# Protocol v1 draft

Namespace root: `AIPort.Protocol.Messages`.

## Rules

- IDs are strings to avoid serializing TaleWorlds objects.
- `RequestId` and `ConversationId` are GUID strings in `N` format.
- Every request carries `ProtocolVersion`.
- Claimed player IDs are hints only; the server resolves identity from the peer.
- New fields get new protobuf numbers. Existing numbers are never reused.
- Unknown message/action types are rejected.

## M0 messages

- `AIPortHandshakeRequest`: client -> server.
- `AIPortHandshakeResponse`: server -> requesting client.

The client sends one handshake after receiving Coop's `NetworkClientValidated`. At that point module validation has completed. For an existing hero, the server has already associated the peer with the controller. The handshake does not touch campaign state and may run before save/world catch-up completes.

The client correlates the response by `RequestId`, logs protocol/build/compatibility and resets on `NetworkDisconnected`. A reconnect creates a new request ID.

## Dialogue messages

- `AIConversationRequest`
- `AIConversationAccepted`
- `AIConversationResult`
- `AIConversationError`
- `AIConversationCancel`

Streaming chunks are deliberately excluded from v1. The initial implementation sends one final result.

Dialogue requests must not use handshake timing. They wait for full campaign entry and an authoritative player resolver.

## Validation checklist

- protocol version;
- peer is connected and campaign-ready;
- resolve actual Coop player hero from peer/controller;
- NPC exists and is available;
- text size and rate limits;
- unique request ID;
- per-conversation sequence ordering;
- no client-provided action authorization.

## 0.0.38 target lease extension

`AIConversationTargetOpen` -> `AIConversationTargetBound` establishes one server lease per peer. `AIConversationTargetClose` revokes it. `AIConversationRequest` retains fields 1-7 and appends field 8 `TargetLeaseId` and field 9 `TargetInstanceId`; `AIConversationResult` appends field 8 `SpeakerTargetInstanceId`. Requests are rejected unless peer, conversation, lease, target and target instance all match.

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

## Protocol 2 — 0.0.45–0.0.47 foundation

Protocol 2 adds capability, shadow-intent and private-state snapshot messages. Existing conversation field numbers are unchanged.

| Message | Fields |
|---|---|
| `AIPortCapabilitiesRequest` | 1 protocolVersion; 2 requestId; 3 clientCapabilityFlags; 4 stateSchemaVersion |
| `AIPortCapabilitiesResponse` | 1 protocolVersion; 2 requestId; 3 accepted; 4 serverCapabilityFlags; 5 intentSchemaVersion; 6 stateSchemaVersion; 7 campaignGeneration; 8 stateRevision; 9 message |
| `AIIntentProposalRequest` | 1 protocolVersion; 2 requestId; 3 campaignGeneration; 4 expectedStateRevision; 5 intentType; 6 payloadJson |
| `AIIntentProposalResult` | 1 requestId; 2 intentId; 3 status; 4 reasonCode; 5 stateRevision |
| `AIPortStateSnapshotRequest` | 1 protocolVersion; 2 requestId; 3 campaignGeneration; 4 knownRevision |
| `AIPortStateSnapshotResponse` | 1 requestId; 2 ready; 3 campaignGeneration; 4 stateRevision; 5 snapshotJson; 6 contentSha256; 7 reasonCode; 8 retryAfterMilliseconds |

Capability flags: narrative=1, no-op intent=2, state snapshot=4, persistent memory=8. Intent schema 1 recognizes only `no_op` with exact `{}` and has no mutation adapter. State schema 1 stores private bounded dialogue records. Removed protobuf numbers remain reserved; no existing number was reused.

## Protocol 2 extension — 0.0.97 typed diplomacy inbox

Capability additions:

- `CapabilityDiplomacyInboxList = 524288`;
- `CapabilityNpcDiplomacyInitiativeScheduler = 1048576`;
- cumulative flags with all current capabilities: `2097151`.

New messages are append-only and protocol version remains `2`.

### `AIDiplomacyInboxPageRequest`

1. protocol version
2. request ID
3. campaign generation
4. expected state revision
5. after-statement cursor (empty for first page)
6. page size (1..8)

### `AIDiplomacyInboxPageResponse`

1. protocol version
2. correlated request ID
3. accepted
4. campaign generation
5. state revision
6. total visible pending count
7. typed entry array
8. next cursor
9. has-more flag
10. reason code

### `AIDiplomacyInboxEntry`

1. statement ID
2. action
3. source Hero ID
4. source Hero display name
5. source faction ID
6. source faction display name
7. target faction ID
8. target faction display name
9. occurred UTC
10. expires UTC
11. durable origin
12. initiative reason code
13. initiative score
14. target Hero ID

Privacy and consistency rules:

- the server derives the recipient from the peer/controller mapping;
- a request cannot enumerate another Hero's inbox;
- the first page may return a newer current revision;
- continuation requires the response snapshot revision exactly;
- revision movement, unknown cursor or generation mismatch requires a complete client refresh;
- page size is bounded at 8 and total pending visibility at 16.

Recipient decision audit metadata now accepts exactly two values: `manual_diplomacy_recipient_decision` and `map_notification_decision`.
