# AIPort agent handoff

## Active handoff — 0.0.99-dev bundled provider/runtime gate

- Current acceptance scope is one connected player only. Player-to-player interaction and any second-client requirement are removed from the active plan.
- `0.0.99-dev` keeps protocol `2`, capability flags `2097151`, and native war/peace OFF.
- The authoritative connected-Hero resolver from the pre-provider `0.0.99` candidate is retained.
- A separate direct Player2 backend and provider router are now implemented beside the unchanged OpenAI-compatible backend.
- Player2 consumes local token/account lists on the server, fixes chat to `https://api.player2.game/v1/chat/completions`, omits `model` from chat payloads, rotates boundedly on `401`/`402`/`429`, and never logs account identity or credential material.
- Source rollback before this work: `backups\source-20260817-071840-pre-player2-provider`.
- Build and static/dummy credential tests are complete; deployment and the one-player runtime gate remain pending in this handoff section until runtime evidence is recorded.

## Historical handoff — 0.0.97-dev candidate / 0.0.95-dev deployed

<!-- previous heading retained below for historical text -->
## Legacy snapshot — 0.0.97-dev candidate / 0.0.95-dev deployed

### Candidate scope completed

- Source rollback: `backups\source-20260817-052827-pre-0.0.97`.
- Prepared runtime-binary rollback: `backups\runtime-20260817-055314-pre-0.0.97`.
- Candidate is staged without deployment at `artifacts\stage-0.0.97`.
- Build string: `0.0.97-dev`; protocol: `2`; full capability flags: `2097151`.
- New bits: typed diplomacy inbox `524288`; NPC initiative scheduler `1048576`.
- Added typed inbox request/response/entry protocol with page size 8 and total pending limit 16.
- Server derives the recipient from authoritative Coop peer/controller resolution; the request never selects another player's Hero.
- Pagination uses newest-first statement-ID cursors. A continuation must match the exact snapshot revision or the client discards accumulation and restarts.
- Client maintains a statement-ID accumulator and registrar-level desired/published sets, so JIP/reconnect and lifecycle changes reconcile the exact pending list.
- `0.0.96` had a real runtime blocker: map UI sent `map_notification_decision`, while the parser accepted only `manual_diplomacy_recipient_decision`. `0.0.97` accepts those two exact audit values and rejects everything else.
- Map items no longer call `ExecuteRemove()` after local submission. They use a local submitting guard and wait for authoritative result/lifecycle; rejection releases the guard.
- Added `NpcDiplomacyInitiativeScheduler`, a pure deterministic selector with no campaign mutation calls.
- Server integration runs only from campaign hourly maintenance and checks campaign readiness, persistence loaded/writable state, save barrier, authority, current war/peace preconditions, pair lock, daily budget, per-recipient budget and cooldown.
- Candidate player targets are collected from alive Heroes and filtered by the existing Coop player-Hero classification/known peer registry; offline canonical Heroes are allowed.
- Scheduler creates only `pending_recipient` shadow statements and calls the existing private notification flow for online recipients.
- Persistent statement metadata now includes `Origin`, `InitiativeReasonCode`, `InitiativeScore`, `CampaignDay` and `CampaignHour`.
- Codec loading remains backward-compatible; old rows default to `Origin=legacy`, day/hour `-1`.
- Daily budget, pair cooldown and minimum campaign-hour interval survive process restart from the diplomacy ledger.

### Default-off settings

```json
{
  "enableNpcDiplomacyInitiative": false,
  "npcDiplomacyDailyBudget": 2,
  "npcDiplomacyMinimumIntervalHours": 6,
  "npcDiplomacyPairCooldownDays": 7,
  "npcDiplomacyMinimumScore": 82
}
```

Backend credentials are process-scoped only. Never copy them into the repository, example config, documentation or logs.

### Verification

- Clean build: PASS.
- `AIPort.dll`: 341504 bytes; SHA-256 `c52f5f0e67a35da1e826f0eb58311831fb028fa4bfa602d22408338a8357f17f`.
- Bootstrap SHA-256: `8357d5c56766dabba2777d472b87b1919a1925242b6d1f8a00bf99fd653934cf`.
- Current tests `0.0.50..0.0.97`: 20/20 PASS.
- Dedicated executable core harness: PASS.
- Actual protobuf-net typed inbox roundtrip: PASS.
- Native call counts: exactly one isolated war call and one isolated peace call.

### Runtime/deployment state

- **Do not claim 0.0.97 is deployed or runtime-proven.**
- Running disposable baseline remains `0.0.95-dev`; last known server PID `23872` and launcher PID `4624`.
- Saved manifest revision is `10`; the live client later reached revision `11` after a stub turn.
- No process was stopped, no DLL was copied and no live config was changed during candidate development.
- Native war/peace remain default-off and must not be tested without separate permission.

### Next work

1. Decide whether revision `11` must be saved before stopping the existing server.
2. Create a deployment rollback and deploy identical `0.0.97` DLLs to disposable client/server only.
3. Run one bundled gate: scheduler -> offline/online durable offer -> full typed inbox -> map notice -> Accept/Reject -> lifecycle -> save/restart/JIP.
4. Exercise multiple simultaneous offers, double-click guard, stale revision restart and exact empty-inbox reconciliation.
5. Require `NativeMutationApplied=false` for the entire gate.
6. This legacy multi-client follow-up is superseded. Native diplomacy remains a later separately authorized single-connected-player gate.

## Historical deployment evidence

The following multi-client records are preserved as historical facts only; they are not current or future acceptance requirements.

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


Updated: 2026-08-14 00:46 +05:00

Start here. Keep all new source, documentation and progress notes only under
`E:\Game\Mount & Blade II Bannerlord\Modules\aiport`.

## Goal and safety boundary

This is a server-authoritative port of AIInfluence 6.0.2 onto Bannerlord Coop 0.1.1 / Bannerlord 1.4.7.

Mandatory constraints:

- Do not install or load `AIInfluence.analysis-clean.dll`.
- Do not substitute `Hero.MainHero` for the authoritative Coop player.
- Resolve player identity from the requesting network peer on the server.
- Never put API keys in client files, server files, logs, docs or protocol messages.
- Keep gameplay actions, diplomacy mutations, dynamic events and persistence disabled.
- The current milestone is narrative-only transport and volatile memory.
- Do not touch the live `saveauto1` campaign.

## Live vs disposable

- Live campaign directory: `C:\Users\wot_2\Documents\Mount and Blade II Bannerlord\CoopData\DedicatedServer`.
- Live save: `saveauto1`; untouched by M0 work.
- Live campaign backup: `backups\live-campaign-20260813-174141`.
- Disposable test data: `artifacts\runtime-m0`.
- Disposable save: `aiport-m0`.
- Network: `localhost` / `127.0.0.1`, UDP 4200, empty password.
- Hook: `AIPort.Bootstrap.dll` is a second Coop submodule; there is no new active module ID.

## Historical deployment snapshot

- Build: `0.0.21-dev`.
- Protocol: `1`.
- Runtime `AIPort.dll`: `69,632` bytes.
- Runtime SHA-256: `f00588885595dfcf80707ffc97b24170245427440b614a281f78a8fea345b56f`.
- Bootstrap: `10,240` bytes.
- Bootstrap SHA-256: `a2e0d24e5a5c503270ce3406dcc63ae8a71da1b9251ebe1925f44af5932b270d`.
- Rollback backup: `backups\m0-20260813-212758`.
- Server wrapper PID: `16116`; engine PID: `26484`.
- Server log: `artifacts\runtime-m0\logs\coop-server-20260813-212816.log`.
- Launcher PID: `17060`; 0.0.14 client handshake pending.
- Client log: `E:\Game\Mount & Blade II Bannerlord\bin\Win64_Shipping_Client\Coop_client.log`.
- Client RGL log: `C:\ProgramData\Mount and Blade II Bannerlord\logs\rgl_log_25620.txt`.
- Backend: OpenRouter-compatible, model `openai/gpt-4o-mini`, `enabled=False`, `keyPresent=False`.

## 0.0.13 rate-limit retry implementation

- `AIConversationError` gained protobuf field 5 `RetryAfterMilliseconds`; no existing field number changed.
- The server's per-player limiter now reports the remaining sliding-window delay, or `1000 ms` for an occupied inflight slot.
- The client stores exact pending request context and only reacts to a matching retryable `rate_limited` RequestId.
- Retry limits:
  - maximum 5 retries;
  - minimum delay `1000 ms`;
  - maximum delay `60000 ms`;
  - server hint plus a small boundary margin;
  - exponential fallback when no server hint is available.
- A retry preserves ConversationId, NpcHeroId, player text and turn, while creating a new RequestId and client sequence.
- Pending request/retry state is canceled when:
  - conversation-ended event fires;
  - campaign polling detects no target;
  - another NPC conversation replaces the current one;
  - network disconnects;
  - handler is disposed.
- Build succeeded at 48,640 bytes with SHA-256 `99097ed038f8f3c714c17713dc239b616422e9751e6eb299b08ea90ee1a19e29`.
- Deployed with backup `m0-20260813-203336`; client/server DLL hashes match.
- Disposable server PID `20260` reached CampaignReady/SERVING at `20:34:28`.
- Runtime handshake and successful rate-limit retry are proven; cancellation-before-timer remains to be proven.

## 0.0.14 current deployment

- Client and disposable server are deployed with `0.0.14-dev`.
- It adds explicit retry-cancellation logs with ConversationId, attempt and lifecycle reason.
- It also logs ignored non-hero CharacterObjects; it does not yet send requests for them.
- Protocol remains 1 and protobuf contracts are unchanged.
- Runtime DLL: 49,152 bytes; SHA-256 `7903e68a7555fac8e13d143b04e0e0a5dd617e49b26d84e83da9cfebc50c39f3`.
- Source rollback: `backups\source-20260813-212700`.
- Rollback backup is `backups\m0-20260813-212758`; server PID `16116` reached CampaignReady/SERVING in `coop-server-20260813-214955.log`.

## 0.0.14 runtime check at 21:46

- Client/server handshake succeeded: RequestId `45631a8dd5424a419a66660ad2431735`, client/server build `0.0.14-dev`, protocol 1, Compatible=true.
- Hero `lord_5_13` / Муйнсер: ConversationId `384544465061496a9c29d79bdd1086f1`; RequestIds `32d2c341f2384119a78b718087a6cff2`, `cd359b7da746431b996c607deb44d7c5`; MemoryTurns 1 -> 2.
- Hero `lord_5_1` / Каладог: ConversationId `dce1a3c5eead453aa647db60de0662ac`; RequestIds `03b7c66255b64400be538d5d30f33351`, `e090bcb5e69e4ba89e28f1087b5136bc`; MemoryTurns 1 -> 2.
- Both hero conversations succeeded without rate limiting, so there was no pending retry to cancel; cancellation remains unproven in this run.
- Non-hero diagnostic is proven: CharacterId `looter`, name `Грабитель`, logged at `21:46:53`; no AIPort request was sent for it.
- Per user preference, do not start the game process; only restart the disposable server when needed.
- Server was restarted after inspection: wrapper PID `28432`, engine PID `28596`, log `coop-server-20260813-214955.log`; build 0.0.14 reached CampaignReady/SERVING at `21:50:48`.

## 0.0.13 runtime evidence

- Handshake RequestId `8fb02151c7ec40a0a38cd2eb8ae26467`: client/server `0.0.13-dev`, protocol 1, compatible.
- Save-load listener replacement: `31953303 / 67039599 / 65654309` -> `14741975 / 41146019 / 58760031`.
- First conversation: NPC `lord_1_17`, ConversationId `4a4c41ac4e0d46b1be202cd39d34795a`, RequestIds `532c1f97b7de4a82a4529f5a5ff3d902` and `9c74fd94574145229f8b162886df4d3c`, memory `1 -> 2`.
- Second conversation: NPC `lord_1_17`, ConversationId `23865aec886f429382d6d0877ada088a`.
  - Turn 1 RequestId `403ad65331bb4aa8a71da7e4f1b578b9` -> `MemoryTurns=1`.
  - Initial turn 2 RequestId `c45abbccf41a43df8fdb12e5fa37bd29` -> `rate_limited`, `RetryAfterMs=33450`.
  - Client scheduled attempt 1 for `33700 ms`.
  - Retry RequestId `9f438fa80b0842aa870b1f816ad444a0` preserved the ConversationId, NPC and turn, was accepted and produced `MemoryTurns=2`.
- RGL confirms the held-open second conversation from `20:52:12.413` to `20:57:18.884`; end cleanup fired.
- Deployed client/server DLLs still match SHA-256 `99097ed038f8f3c714c17713dc239b616422e9751e6eb299b08ea90ee1a19e29`.
- Live `saveauto1` still matches the pre-M0 backup SHA-256 `0d9d79dca420f086b68336df6cb32569d044ba08d50acfab097772db7f8320d5`.

## Latest cancellation-test attempt

- Peer 1 handshake `d4314a2b56184e5ea0a91400864690db` and first hero conversation succeeded.
- The attempted second target was an `Имперский крестьянин`, which has no HeroObject and is intentionally ignored by the current hero-only M0 path.
- No pending retry existed, so cancellation is not yet proven. Repeat with a hero NPC.

## What changed from 0.0.10 to 0.0.12

### 0.0.10-dev

- Added client `CampaignEvents.TickEvent` polling for conversation transitions.
- Added stable per-conversation ID, monotonic client sequence and temporary two-turn texts `Hello.` / `Hello again.`.
- Build: `42,496` bytes; SHA-256 `818d38b55a351d6251d49fbcef0209c8d2a2132935d4ed5db30ca648a726eef0`.
- Backup: `backups\m0-20260813-191203`.
- Runtime result: real conversation occurred, but Tick did not fire while the dialogue screen paused the campaign.

### 0.0.11-dev

- Added `CampaignEvents.OnAgentJoinedConversationEvent` and NPC hero extraction from `IAgent.Character as CharacterObject`.
- Added a separate second-turn timer.
- Build: `44,032` bytes; SHA-256 `8b32adaa9f307b72d44b5788deb6d36e6ae634d9d0eb0cd6b8e218aba1f9fbdc`.
- Backup: `backups\m0-20260813-192603`.
- Runtime result: listener attached to the first pre-load `CampaignEvents` instance, then Coop replaced the event set while loading the transferred save; the stale listener never received the NPC event.

### 0.0.12-dev

- Replaced the one-time `campaignListenersAttached` flag with exact event-instance tracking:
  - `attachedTickEvent : IMbEvent<float>`
  - `attachedAgentJoinedEvent : IMbEvent<IAgent>`
  - `attachedConversationEndedEvent : IMbEvent<IEnumerable<CharacterObject>>`
- `EnsureCampaignListeners()` compares current and attached event objects by `ReferenceEquals`, clears old listeners, and attaches to the current set.
- `ClearCampaignListeners()` clears the stored event instances rather than whichever static set is current.
- Attachment logs include event object hashes so replacement/rebinding is directly observable.

## 0.0.12 runtime evidence

### Handshake and authoritative startup

- Client connection attempt: `20:04:09`.
- Handshake RequestId: `2e3a98340eb44e87a360bb90da554126`.
- Client build: `0.0.12-dev`.
- Server build: `0.0.12-dev`.
- Protocol: `1`.
- `Compatible=true`.
- First startup probe RequestId `ffa253b2283a4fd992fa2bc67548a71c` returned retryable `player_unresolved`.
- Retry RequestId `d806473fa6ca464a9bfe485de88ef090` succeeded.
- Authoritative context:
  - ControllerId `DESKTOP-ADLK0J9-wot_2`
  - PlayerHeroId `Hero_Player`
  - PlayerPartyId `MobileParty_Player`
  - PeerId `0`
- Server logged `player entered the campaign: peer 0` at `20:05:07`.

### Event-set replacement and rebinding

First attachment at `20:04:46`:

- TickEventHash `55319230`
- AgentEventHash `24464410`
- EndEventHash `29848889`

Second attachment at `20:05:07` after transferred-save loading:

- TickEventHash `35887792`
- AgentEventHash `39896889`
- EndEventHash `61162259`

All hashes changed. The second attachment proves AIPort detected Coop's replacement event set and rebound its listeners to the live campaign events.

### Real NPC conversation and memory

- RGL start: `20:05:56.462`.
- RGL end: `20:06:06.805`.
- AIPort observed NPC conversation start at `20:05:56`.
- Source: `agent_joined`.
- NpcHeroId: `lord_3_1`.
- ConversationId: `afda57694a0c40a18a115fe93de0d451`.
- Turn 1 text is the temporary probe `Hello.`.
  - RequestId `83c5a43286f1403a97b14476079eef83`.
  - Accepted and completed.
  - Server `MemoryTurns=1`.
  - Client `SpeakerHeroId=lord_3_1`.
- Turn 2 text is the temporary probe `Hello again.`.
  - RequestId `692cf48541cf4c8da0d131eb9749f138`.
  - Same ConversationId and NPC.
  - Accepted and completed.
  - Server `MemoryTurns=2`.
  - Client `SpeakerHeroId=lord_3_1`.
- Client received the conversation-ended event at `20:06:06` and cleared the current conversation state.

This closes the M0 proof for handshake, authoritative peer resolution, campaign-event rebinding, real NPC capture, stable conversation identity, two-turn volatile memory, result delivery and conversation-end cleanup.


## Disconnect/reconnect proof and rate-limit finding

- Peer 0 disconnected at `20:11:27` (`RemoteConnectionClose`).
- Server logged memory clear for peer 0.
- Client reconnected as peer 1 at `20:11:31`; server logged memory reset for peer 1.
- Reconnect handshake RequestId `95063f88a2ff41f2878f5a7e42ad115a` was compatible on `0.0.12-dev`.
- Client listener hashes after reconnect: Tick `25952383`, Agent `28060740`, End `14377911`.
- Startup probe RequestId `ba1fcdb54eaa4cdfbbece66b95c723b8` returned `MemoryTurns=1`.
- First post-reconnect NPC conversation:
  - NpcHeroId `lord_3_2`;
  - ConversationId `efb2a59782e7419a99d1ee64e25915c7`;
  - RequestIds `575f4486882844a4ab08bf829a179adf` and `0e8f4f08c1314ff5934ca7b993ae77cd`;
  - server memory `1 -> 2`;
  - both client results and end cleanup confirmed.
- A second rapid conversation used NpcHeroId `lord_3_1`, ConversationId `b8bba9a2d60244efbfd8b97038f884cd`.
- Its first request `de3964f532ab4c7c86e65f203247aa86` completed with `MemoryTurns=1`.
- Its second request `eb73ca26166d40e08b9daf217f787459` returned retryable `rate_limited`.
- The server limiter is working; the client test probe needs bounded retry/backoff scoped to the active conversation.

## Conversation memory bounds

`ConversationMemory.cs` is server-only and volatile:

- key: authoritative player hero + NPC + conversation;
- maximum 128 conversations;
- maximum 8 turns per conversation;
- maximum 6000 characters per conversation;
- maximum 3000 characters per message;
- clear on connect/disconnect/reconnect;
- no persistence;
- no gameplay mutations.

## Main code map

- `src\AIPort.Bootstrap\AIPortBootstrapSubModule.cs`: AssemblyResolve and runtime loading.
- `src\AIPort\AIPortSubModule.cs`: module registration.
- `src\AIPort\Protocol\AIPortProtocol.cs`: protocol/build constants.
- `src\AIPort\Protocol\Messages\*`: protobuf handshake and conversation DTOs.
- `src\AIPort\Server\PlayerContextResolver.cs`: authoritative peer-to-player resolution.
- `src\AIPort\Server\ConversationTargetResolver.cs`: current one-to-one NPC resolution.
- `src\AIPort\Server\ConversationMemory.cs`: bounded volatile history.
- `src\AIPort\Server\PromptService.cs`: narrative prompt plus hero and history context.
- `src\AIPort\Server\OpenAiCompatibleBackend.cs`: OpenAI-compatible HTTP backend.
- `src\AIPort\CoopIntegration\Client\AIPortConversationClientHandler.cs`: retries, listener rebinding, NPC lifecycle and test probes.
- `src\AIPort\CoopIntegration\Server\AIPortConversationServerHandler.cs`: validation, authoritative context, memory, rate limits and response delivery.

## Build / deploy / rollback

- Build: `tools\build.py`.
- Stage: `tools\stage_m0.py`.
- Stop disposable server: `tools\stop_m0_server.py --apply`.
- Deploy: `tools\deploy_m0.py --apply`.
- Run: `tools\run_m0_server.py --apply`.
- Roll back: `tools\rollback_m0.py --apply`.

## Next work

1. Connect the 0.0.14 client and runtime-prove retry cancellation by ending a rate-limited hero conversation before its timer fires.
2. Prove disconnect also cancels a pending retry without sending a stale RequestId.
3. Test a second client for player-memory isolation and concurrent queue/rate-limit behavior.
4. Capture actual player-selected dialogue text.
5. Render server/model replies in the conversation UI.
6. Test one real narrative backend response using `AIPORT_API_KEY` from the environment only.
7. Keep actions, diplomacy, dynamic events and persistence disabled until the narrative path is stable.

## 0.0.15 narrative deployment

- Built, staged and deployed `0.0.15-dev` to client and disposable server.
- Runtime DLL: 52,224 bytes; SHA-256 `6ecbefc64d2a1aedd97bb750bf0f1479586181ab646707255e8bb8c36f138575`. Bootstrap unchanged.
- Source rollback: `backups\source-20260813-215942-pre-0.0.15`.
- Client captures real selected player sentences using `ConversationManager.ConsequenceRunned`; synthetic `Hello.` and `Hello again.` probes were removed.
- Results are matched to the active pending RequestId/ConversationId, queued to the game thread, and written to the active mission/map conversation VM `DialogText`; stale results are cleared on every conversation lifecycle exit.
- `player_unresolved` and `rate_limited` retries now preserve the actual selected sentence.
- Protocol remains 1; `AIConversationRequest.NpcHeroId` remains field 5.
- The user authorized closing the game/launcher automatically when needed. Deployment completed with backup `backups\m0-20260813-221843`; server wrapper PID `30344`, engine PID `30464`, log `coop-server-20260813-221853.log`. Do not launch the game; let the user launch/connect.
- Runtime proof should verify: selected native player text reaches server, stub response replaces NPC dialogue text, a second selected line reuses ConversationId and increments memory, and closing the conversation drops a late result.

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
## Temporary normal-Coop toggle

Three double-click launchers are available in the `aiport` project root:

- `AI PORT - OFF FOR NORMAL COOP.bat` removes only the exact AIPort bootstrap blocks from the client/server descriptors and parks both runtime/bootstrap DLL pairs under `artifacts\toggle-aiport\parked`.
- `AI PORT - ON FOR DEVELOPMENT.bat` restores the exact parked DLL snapshot, verifies SHA-256 hashes and reinserts the bootstrap blocks without replacing unrelated descriptor content.
- `AI PORT - STATUS.bat` reports `development-enabled`, `normal-coop` or `mixed-unsafe`.

The switch refuses to run while any process maps one of the six affected files. Close Bannerlord, its launcher and the Coop server first. Both directions are guarded, hash-verified and roll back descriptor/file changes after a failure. The live campaign and save files are never touched. Descriptor round-trip and launcher tests pass.

## 0.0.38 implementation

Combined hardening is in source: target open/bound/close leases, spoof/stale rejection, regular-agent identity, 14-day events, bounded cross-dialogue/reconnect narrative memory, and deny-by-default action gate. Read `docs/SECURITY_MEMORY_0_0_38.md`. Run `tools/test_0_0_38_security_memory.py`, the prior suites, then `tools/build.py`; deploy only matching client/server runtime and bootstrap hashes.

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
## Active handoff — 2026-08-17 0.0.98-dev deployed, awaiting client retest

### Runtime state right now

- Deployed build on both sides: `0.0.98-dev`, protocol `2`, cumulative capability flags `2097151` (unchanged from 0.0.97).
- `AIPort.dll` 345088 bytes, SHA-256 `c589cd0e3bb6ce610c543006bccaf46d97fde07d98ecd9bb6858fce68210a5f4`; `AIPort.Bootstrap.dll` unchanged.
- Client path `Modules\Coop\bin\Win64_Shipping_Client` and server path `E:\BCOOP\engine\Modules\DedicatedServer.Windows\bin\Win64_Shipping_Server` hold the identical hash.
- Disposable server running: PID `11220`, log `artifacts\runtime-m0\logs\coop-server-20260817-064457.log`, endpoint `127.0.0.1:4200`, empty password.
- Restored state `loaded:3:social:5:diplomacy:2:nativeJournal:0`, generation `4ea97daf7c4e8ae14149a02cff988e72`, revision `10`, `ReadOnly=False`, `CampaignReady`, `SERVING`.
- The 0.0.97 defect revisions 11..15 were never persisted, so the ledger is clean for the retest and no faction pair is stuck in cooldown.
- Gate stays stub (`enabled=False`, `keyPresent=False`, Groq key not deployed); native war and peace stay OFF.
- Test-only scheduler settings remain: daily budget 4, minimum interval 1 campaign hour, pair cooldown 1 day, minimum score 0.
- The previous client session is gone: Bannerlord terminated and its leftover `Coop.CrashReporter.exe` was killed because it locked the client DLLs during deployment.

### What 0.0.98-dev changed

- Fixed the 0.0.97 runtime blocker where NPC initiative offers were recorded for stale player duplicates `Player` / `main_hero` while the connected authoritative hero was `Hero_Player`.
- Added `src/AIPort/Server/AuthoritativeDiplomacyRecipientFilter.cs` (pure, no game types) and `AuthoritativeConnectedHeroIds()` in `AIPortConversationServerHandler`.
- `CollectPlayerDiplomacyTargets()` now intersects discovered candidates with authoritative single-peer hero ids and logs excluded aliases.
- Added the pre-record guard `recipient_not_authoritative_online`, which rejects non-authoritative recipients without consuming daily budget.
- Ambiguous peer mappings fail closed; with no client connected the 0.0.95 offline queue behaviour is unchanged.
- Added `tools/harness_0_0_98.cs` and `tools/test_0_0_98_authoritative_recipient.py`; cumulative suites were retargeted to build string `0.0.98-dev`.
- Details: `docs/HOTFIX_0_0_98_AUTHORITATIVE_NPC_RECIPIENT.md`.

### Next step (client runtime gate, unchanged scope)

1. Launch Bannerlord with the Coop module and connect to `127.0.0.1:4200` (no password).
2. Confirm handshake `0.0.98-dev`, protocol `2`, flags `2097151`.
3. Let 1-2 campaign hours pass, then expect `RecipientHeroId="Hero_Player"` with `RecipientOnline=True` and a vanilla-style map notification.
4. Verify typed inbox, Accept/Reject with double-click protection, post-decision reconciliation, then save, restart and reconnect/JIP.
5. Any `NativeMutationApplied=true` line is an immediate stop condition.

### Rollback

- Sources: `backups\source-20260817-063750-pre-0.0.98`.
- Live binaries: `backups\m0-20260817-064443` or `python tools\rollback_m0.py --apply`.
