# Milestone 0.0.56 - persistent diplomatic shadow statements

Date: 2026-08-16

Adds an authoritative two-step war/peace statement path without changing native diplomacy.

## Commands

- `/diplomacy-propose war`
- `/diplomacy-propose peace`
- `/diplomacy-confirm`

The active dialogue target must be a server-authorized hero in a different kingdom. War statements are rejected if the kingdoms are already at war; peace statements are rejected if they are not at war.

## Authority and confirmation

- Capability bit 128 (`CapabilityDiplomacyStatements`), expected total flags 255.
- Strict typed payload schema.
- Peer, player, campaign generation, revision, conversation, target lease, target instance and kingdom pair binding.
- 60-second one-use confirmation.
- Authoritative player and target kingdom resolution at proposal and confirmation time.
- Native diplomacy mutations remain absent.

## Persistence

- New `diplomacy.ndjson` per stable campaign generation.
- Optional manifest fields `diplomacySha256` and `diplomacyRecordCount`; old generations migrate as empty ledgers.
- Hash mismatch fails closed into read-only mode.
- Maximum 256 records and 30-second source/target/action cooldown.
- Request/receipt idempotency.
- Private state snapshots include player-filtered `diplomacyStatements`.
- A new statement advances the shared state revision by one.

## Verification and deployment

All cumulative relation, confirmation, social persistence, stable identity, diplomacy snapshot and diplomatic statement suites pass. The executable coordinator harness covers parsing, confirmation, idempotent replay, stale revision and lease mismatch.

- Build: `0.0.56-dev`.
- Runtime size: 207872 bytes.
- SHA-256: `cddcb498dfe3337b20940e63e3fc9861de07447213f7baad4382c47b535e49b2`.
- Source rollback: `backups\source-20260816-230702-pre-0.0.56`.
- Deployment rollback: `backups\m0-20260816-231202`.
- Startup log: `artifacts\runtime-m0\logs\coop-server-20260816-231203.log`.
- Server PID: 1328.
- Startup state: `loaded:2:social:0:diplomacy:0`, revision 2, read-only false, SERVING.

Manual runtime validation remains deferred for the multi-feature bundled gate.
