# 0.0.49 startup state synchronization

## Defect

Capability negotiation completed before the authoritative Coop player objects were always registered. The first private snapshot could therefore return `player_unresolved`. The 0.0.47 client retained the failed request ID and never tried again, so the snapshot and automatic no-op remained incomplete for that connection.

## Implementation

- Bounded snapshot retry for `player_unresolved` only: 30 attempts, server hint clamped to 500–5000 ms.
- Retry ownership is tied to a local connection generation; disconnect, dispose and capability refresh cancel the timer.
- Responses require exact request correlation.
- Ready snapshots require exact negotiated campaign generation, a non-stale revision and a valid SHA-256 of the UTF-8 JSON payload.
- Generation/revision/hash keys suppress duplicate application.
- `generation_mismatch` causes capability renegotiation; the mismatched payload is never applied.
- `no_op` is emitted exactly once after `SnapshotReady` and accepted only through a correlated result for the current generation/revision.

## Safety and verification

Protocol remains 2; protobuf fields did not change; campaign mutations remain disabled. The new suite passed 17/17 and its executable retry/deduplication model passed. Clean build is 159,232 bytes with SHA-256 `c00543f4b0ccd470c773d2f1d643cb305691b7bc1aa3ee5a5f0ca83cf866a15d`. Source rollback: `backups\source-20260815-024037-pre-0.0.49`.

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
