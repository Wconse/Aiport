# AIPort

## Current `0.0.99-dev` scope

- One connected player only; no player-to-player proposals, consent, inbox, lifecycle or acceptance tests.
- Selectable server AI provider: existing OpenAI-compatible path or separate direct Player2 path.
- Player2 reads operator-supplied local token/account lists, uses fixed HTTPS endpoints and keeps all credential material server-only.
- Protocol remains `2`, capability flags remain `2097151`, and native war/peace remain OFF.
- The bundled runtime gate covers one real Player2 reply, authoritative `Hero_Player` recipient resolution, offer decision, duplicate protection, save/restart/reconnect and JIP reconstruction.

## Current candidate: 0.0.97-dev

AIPort is a server-authoritative Coop-compatible reimplementation of selected AIInfluence ideas. Protocol remains `2`.

The `0.0.97-dev` source candidate is built and regression-tested, but it is **not deployed**. The current disposable client/server runtime remains `0.0.95-dev` until one bundled runtime gate is scheduled.

## 0.0.97 milestone

- Added a private typed inbox for every pending diplomatic statement, not only the latest offer.
- Added bounded newest-first pagination: 8 entries per page, 16 pending entries maximum, statement-ID cursors and exact continuation revision checks.
- Added client accumulation, deduplication and full reconciliation of map notifications after reconnect/JIP and lifecycle changes.
- Fixed the `0.0.96` map-decision payload mismatch: the strict parser now accepts only `manual_diplomacy_recipient_decision` or `map_notification_decision`.
- Accept/Reject notifications stay visible while the authoritative request is in flight. Rejected or stale requests release the local submitting guard; only a server result/lifecycle removes a resolved item.
- Added a deterministic server campaign-hour NPC initiative scheduler for player targets, including durable offline recipients.
- Scheduler budgets, minimum interval and pair cooldown survive restart through diplomacy-ledger metadata.
- Added durable `Origin`, initiative reason/score, campaign day and campaign hour fields with backward-compatible NDJSON loading.

## Safety state

- Scheduler is default-off: `enableNpcDiplomacyInitiative = false`.
- Scheduler and typed inbox create/read only AIPort shadow state.
- No raw LLM output can invoke campaign mutations.
- Native war and peace adapters remain independently configured, environment-armed, generation-pinned and default-off.
- Isolated native call counts remain exactly one war call and one peace call.
- `Hero.MainHero` is not used for Coop player authority.
- Backend credentials must remain process-scoped environment data and must never be written to source, config examples, documentation or logs.

## Verification

- Build: PASS.
- `AIPort.dll`: 341504 bytes.
- SHA-256: `c52f5f0e67a35da1e826f0eb58311831fb028fa4bfa602d22408338a8357f17f`.
- Bootstrap SHA-256: `8357d5c56766dabba2777d472b87b1919a1925242b6d1f8a00bf99fd653934cf`.
- Current cumulative regression set `0.0.50..0.0.97`: 20/20 scripts PASS.
- Dedicated 0.0.97 test includes executable scheduler/ledger/codec/parser tests and an actual protobuf request/response roundtrip.
- Source rollback: `backups\source-20260817-052827-pre-0.0.97`.
- Prepared runtime-binary rollback: `backups\runtime-20260817-055314-pre-0.0.97`.
- Tested deployment staging: `artifacts\stage-0.0.97`; no runtime file was modified.

## Next bundled runtime gate

Do not launch the game for a cosmetic-only check. Test the complete scenario in one disposable session:

1. Decide whether live revision `11` on the running `0.0.95` server must be saved before shutdown.
2. Create deployment rollback, then place the same `0.0.97` DLL on client and server.
3. Enable the scheduler only in the disposable server configuration with a small budget.
4. Prove automatic NPC selection -> durable offline/online offer -> full typed inbox -> map notice.
5. Prove Accept and Reject outside conversation, double-click protection, stale-revision recovery and lifecycle removal.
6. Save, restart and reconnect/JIP; require the exact remaining inbox to be reconstructed.
7. Require `NativeMutationApplied=false` throughout.

Native war/peace testing remains a separate explicitly authorized milestone.
