# Original AIInfluence diplomacy audit

Date: 2026-08-16
Source: read-only decompilation under `E:\AIInfluence_Extracted_20260813`.

## Confirmed architecture

The original mod has two related action paths:

1. General dialogue response actions: `AIResponseActionParser` -> normalizer -> validator -> executor. The public action vocabulary includes `relation`, `social`, `claim`, `lie`, `transfer_gold`, `transfer_item`, and a generic `kingdom` action.
2. A separate large diplomacy subsystem: player/kingdom statements are analyzed into typed `DiplomaticAction` values and parameters, queued or executed by `DiplomacyManager`, then stored through diplomacy persistence.

`PlayerStatementResult` carries action lists, target kingdom IDs, tone/reason, settlement ID, daily tribute and duration, reparations amount, trade duration, quarantine duration, tax rate/scope/settlement, and AI engagement pressure.

`DiplomaticStatementResponse` has the corresponding model-facing JSON fields plus relation changes.

## Confirmed political systems

- AI and player diplomatic statements, delayed publication, response pressure and event-linked schedules.
- War/peace.
- Alliances and trade agreements.
- Tribute schedules and reparations.
- Territory transfer.
- Clan expulsion.
- War statistics/fatigue and peace desire.
- Kingdom tax policy with scopes and settlement overrides.
- Diplomatic dynamic events and quarantine hooks.

## Confirmed native mutation calls

The original implementation eventually calls native campaign mutations directly:

- `DeclareWarAction.ApplyByDefault`.
- `MakePeaceAction.Apply`.
- `ChangeRelationAction.ApplyRelationChangeBetweenHeroes`.
- `GiveGoldAction.ApplyBetweenCharacters` for tribute/reparations.
- `ChangeOwnerOfSettlementAction.ApplyByDefault` for territory transfers.

It also maintains custom persisted state for alliances, trade agreements, tribute, reparations, taxes, statements, pressure and histories. Declaring war can break a custom alliance and terminate a trade agreement first.

## Coop-port decision

Reuse the feature concepts, typed parameters, derived war-fatigue inputs and presentation ideas, but do not reuse the original authority assumptions or invoke its DLL/executor.

For Coop every political operation must be server-authoritative and split into proposal, validation, optional player/ruler confirmation, locked commit, receipt/audit, persistence and projection. `Hero.MainHero` from the original player-statement path is not usable as multiplayer identity.

Planned order:

1. Read-only native diplomacy snapshot and political context.
2. Shadow statements/proposals.
3. Persistent custom negotiation records with expiry.
4. Ruler authorization and two-client consent.
5. One native adapter per milestone: war, peace, then territory transfer.
6. Alliances/trade as custom symmetric state.
7. Tribute/reparations/taxes only after idempotent daily scheduling and crash recovery.
