# Milestone 0.0.97 — typed diplomacy inbox and NPC initiative

## Status

- Source candidate: complete.
- Build: PASS.
- Automated regression: 20/20 current scripts PASS.
- Deployment/runtime proof: pending.
- Deployed baseline remains `0.0.95-dev`.

## Why this milestone exists

`0.0.96` added a useful transient decision window but exposed only the latest pending statement. It also contained two runtime defects that static substring checks did not detect:

1. map decisions used audit reason `map_notification_decision`, while the server parser accepted only `manual_diplomacy_recipient_decision`;
2. the item VM removed the notice immediately after local send, before the server accepted or rejected the request.

`0.0.97` fixes both defects and combines the UI work with the first automatic, shadow-only NPC initiative scheduler so the next game launch can validate an end-to-end feature rather than a cosmetic fragment.

## Typed inbox protocol

New append-only contracts:

- `AIDiplomacyInboxPageRequest` fields 1..6;
- `AIDiplomacyInboxPageResponse` fields 1..10;
- `AIDiplomacyInboxEntry` fields 1..14.

Rules:

- page size is 1..8;
- total pending records per Hero remain bounded at 16;
- ordering is deterministic newest-first;
- cursor is the last statement ID returned by the previous page;
- recipient identity is always derived from the requesting peer's controlled Hero;
- generation must match exactly;
- the first page may move the client to the current server revision;
- every continuation must match that exact revision;
- a stale revision or missing cursor discards the accumulator and starts a fresh first page.

The protobuf executable harness serializes and deserializes a response containing `AIDiplomacyInboxEntry[]` and a page request through the same protobuf-net binaries used by Coop.

## Client reconciliation and decision UX

- Entries accumulate in a statement-ID dictionary with deterministic order.
- The registrar owns desired and published statement-ID sets.
- Full refresh removes resolved/expired notices, adds missing notices and avoids duplicates.
- Desired notices survive absence/recreation of `MapScreen` and publish when the map notification view becomes available.
- Disconnect clears all transient UI state; JIP/reconnect rebuilds it from the server.
- Accept/Reject uses a local submitting guard.
- Local submission never removes the map notice.
- Accepted authoritative result/lifecycle dismisses the exact statement.
- Rejected or stale result releases the submitting guard and schedules a full refresh.
- Text notification remains the fallback when map UI cannot be published.
- No vanilla peace-offer callback is used.

## NPC initiative scheduler

`NpcDiplomacyInitiativeScheduler` is a pure selector over DTO snapshots. It does not create records, send messages or call native diplomacy APIs.

Server integration:

- runs from existing `CampaignEvents.HourlyTickEvent` maintenance;
- requires campaign ready, persistence enabled/loaded/writable and no active save barrier;
- uses `floor(CampaignTime.Now.ToHours)` and `floor(CampaignTime.Now.ToDays)`;
- considers authoritative NPC rulers and leaders of independent clans;
- considers known/player-classified alive Heroes, including offline canonical Heroes;
- rechecks authority, source/target faction IDs and current war/peace state immediately before record creation;
- respects direction-independent pending pair lock;
- respects global daily budget, one-per-recipient-per-day bound, minimum campaign-hour interval and pair cooldown;
- creates a `pending_recipient` record with origin/reason/score/day/hour metadata;
- notifies an online target privately or leaves the record for private snapshot/JIP delivery when offline.

The setting is default-off. It must be deliberately enabled only for the disposable runtime gate.

## Persistence

`PersistentDiplomaticStatementRecord` adds optional fields:

- `Origin`;
- `InitiativeReasonCode`;
- `InitiativeScore`;
- `CampaignDay`;
- `CampaignHour`.

NDJSON writes those fields append-only. Legacy rows remain readable and receive `Origin=legacy`, day/hour `-1`. Scheduler budget, cooldown and minimum interval are reconstructed from the durable ledger after restart; no separate scheduler state file is required.

## Safety boundary

- All scheduler outcomes remain shadow-only.
- `NativeMutationApplied=false` is logged on scheduler and maintenance paths.
- Scheduler code contains no `DeclareWarAction`, `MakePeaceAction`, native adapter or `Hero.MainHero` reference.
- Existing native adapters remain default-off and retain separate config, environment, generation, token, journal and postcondition gates.
- Repository-wide isolated native call counts remain one war call and one peace call.

## Verification

- `python tools\build.py`: PASS.
- `tools\test_0_0_97_typed_inbox_scheduler.py`: PASS.
- Current cumulative `0.0.50..0.0.97`: 20/20 PASS.
- Core executable harness covers strict decision parsing, deterministic selection, pagination/privacy, budgets, pair cooldown, restart continuity, metadata roundtrip and legacy decoding.
- Actual protobuf request/response roundtrip: PASS.
- Artifact: 341504 bytes.
- SHA-256: `c52f5f0e67a35da1e826f0eb58311831fb028fa4bfa602d22408338a8357f17f`.
- Source rollback: `backups\source-20260817-052827-pre-0.0.97`.
- Runtime-binary rollback prepared: `backups\runtime-20260817-055314-pre-0.0.97`.
- Candidate staging prepared: `artifacts\stage-0.0.97`; runtime remained untouched.

## Required runtime gate

One bundled disposable test should prove:

```text
scheduler
-> durable offline/online pending offer
-> typed multi-item inbox
-> map notification
-> Accept/Reject outside conversation
-> authoritative lifecycle reconciliation
-> save/restart/reconnect/JIP reconstruction
-> NativeMutationApplied=false
```

Before stopping the current `0.0.95` server, decide whether its unsaved live revision `11` must be preserved. Do not touch `saveauto1`.
