# 0.0.38 target security, individual profiles and narrative memory

## Implemented

- A dialogue now starts with `AIConversationTargetOpen`; the server resolves the requesting peer, validates the target against authoritative campaign objects and co-location, and returns a server-generated lease.
- Every `AIConversationRequest` must match peer, conversation, lease, target ID and canonical target-instance ID. A forged target, another peer's lease, a closed conversation or a replaced target is rejected before rate limiting and before prompt construction.
- Closing the vanilla dialogue sends `AIConversationTargetClose`; the server invalidates the lease, cancels matching work and archives bounded narrative memory.
- Existing protobuf fields 1-7 were not changed. Request fields 8-9 and result field 8 were appended. Protocol number remains 1; build is 0.0.38-dev.
- Regular NPCs receive a per-agent nonce from the client, but the server canonicalizes it with the authoritative settlement and character archetype. Reopening the same live agent preserves identity; another same-archetype agent gets a different profile and memory key.
- Earlier dialogue is remembered across separate conversations and reconnects for the same authoritative player hero and target instance. This memory is bounded and server-session-only; it is not yet written into campaign saves.
- Recent events are now limited to 14 in-game days as well as five results / 96 scanned entries. Prisoner and war/peace relevance was narrowed.
- `AIActionGate` defines typed proposals and the authorization boundary. Every action remains denied with `narrative_only`; no campaign mutation path was enabled.

## Security boundary

The dedicated server does not own each client's local mission conversation UI. Therefore the initial target-open signal is still client-attested. The server now prevents request-time substitution, replay and stale-target use and validates target existence/co-location, but cryptographic proof of the exact local mission agent would require a minimal Coop mission-authority hook. Actions remain disabled until that hook or equivalent authoritative evidence exists.

## Deterministic tests

`tools/test_0_0_38_security_memory.py` compiles and runs `TargetLeaseHarness.cs`. It proves correct-lease acceptance, spoof rejection, cross-peer rejection, post-close rejection, cross-conversation memory, reconnect retention and player isolation. Structural checks cover protobuf numbering, event age, instance profiles and the deny-by-default action gate.

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

