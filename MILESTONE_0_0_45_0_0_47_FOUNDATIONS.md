# Milestones 0.0.45–0.0.47 — safe intent, generation state and persistent memory foundations

> Scope override (2026-08-17): any historical multi-client follow-up below is superseded. Current acceptance uses one connected player and excludes player-to-player interaction.


Updated: 2026-08-15 01:18 +05:00

## Delivered as one cumulative build

Final build is `0.0.47-dev`, protocol `2`. Intermediate capabilities corresponding to 0.0.45 and 0.0.46 are present in the same runtime; no intermediate DLL was deployed.

## 0.0.45 — capability and no-mutation intent foundation

- Added capability negotiation with explicit narrative, no-op intent, state snapshot and persistent-memory flags.
- Added intent/state schema versions independent of the transport protocol.
- Added server-issued `IntentId`, bounded request replay cache and bounded in-memory audit ring.
- The only recognized intent is `no_op` with the exact empty JSON payload `{}`.
- Unknown intent types, payload fields, campaign generations and stale revisions fail closed.
- Save barriers reject new requests with `save_in_progress`.
- No command adapter or campaign mutation is reachable.

## 0.0.46 — generation-bound state foundation

- Added server-only state root selected through absolute `AIPORT_STATE_PATH`.
- Disposable runtime uses `artifacts\runtime-m0\aiport-state`; live campaign saves are untouched.
- Added handlers for Coop `GameLoaded`, `GameSaved`, `GameSaveStateChanged` and `AllGameObjectsRegistered`.
- Generation identity binds Coop campaign ID and save name; manifest additionally binds campaign-time evidence.
- Memory and manifest use SHA-256 verification, temporary files, `Flush(true)` and atomic replacement with backup.
- Mismatch/corruption enters read-only recovery instead of loading another generation.
- Added private per-player state snapshot messages with 65,536-character bound and SHA-256 content digest.
- JIP runtime verification is explicitly deferred until the second PC is available.

## 0.0.47 — persistent private player–NPC memory

- Confirmed dialogue turns now carry stable record IDs, authoritative player hero ID, target-instance ID and UTC timestamp.
- Export includes committed active and archived turns without duplication by record ID.
- Load restores records only into remembered history; active conversation/peer state is never resurrected.
- Snapshots filter by authoritative player hero, preventing another player's private dyad records from being sent.
- Existing bounds remain: 256 remembered targets, 12 remembered turns/target, 9,000 characters/target and 3,000 characters/message.
- No Memory Book UI, summarization, social mutation or gameplay action is enabled yet.

## Protocol 2 ledger

- `AIPortCapabilitiesRequest`: fields 1–4.
- `AIPortCapabilitiesResponse`: fields 1–9.
- `AIIntentProposalRequest`: fields 1–6.
- `AIIntentProposalResult`: fields 1–5.
- `AIPortStateSnapshotRequest`: fields 1–4.
- `AIPortStateSnapshotResponse`: fields 1–8.
- Existing conversation request fields remain 1–9 and result fields remain 1–8.

## Verification

- 0.0.45 suite: 16/16.
- 0.0.46 suite: 15/15.
- 0.0.47 executable save/load/privacy harness: 9/9.
- All suites 0.0.33–0.0.47: 16/16 suites, zero failures.
- Runtime DLL: 153,088 bytes; SHA-256 `aae72b62d9f81e768c35b1f6847f8ec789c7cb99288c64aa4a60c03af56f93ea`.
- Bootstrap: 10,240 bytes; SHA-256 `8357d5c56766dabba2777d472b87b1919a1925242b6d1f8a00bf99fd653934cf`.
- Source/docs rollback: `backups\source-20260815-010650-pre-0.0.45-47`.
- Deployment rollback: `backups\m0-20260815-011532`.

## Runtime smoke

- Disposable server PID `36200` loaded `0.0.47-dev`, protocol 2.
- Backend startup is enabled with sanitized proof `keyPresent=True`; no credential was written to config, source or documentation.
- External state initialized a new generation `4ea97daf7c4e8ae14149a02cff988e72`, revision 0, `ReadOnly=False`.
- CampaignReady and SERVING reached without fatal/unhandled AIPort failure.
- Client/server/build hashes match.
- Log: `artifacts\runtime-m0\logs\coop-server-20260815-011546.log`.
- A forced autosave command was accepted and queued, but no `GameSaved` completion event appeared in the observation window. Deterministic file write/load/mismatch/privacy behavior is proven; runtime save completion and JIP remain deferred gates.

## Safety state

Gameplay actions, native relation changes, gold/item transfers, quests, diplomacy mutations, dynamic events, diseases and writable live-campaign state remain disabled. The deployed write path is confined to the disposable AIPort state root and only activates for persistent private dialogue memory.
