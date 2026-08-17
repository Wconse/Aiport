# Milestone 0.0.57 - bundled runtime gate

Date: 2026-08-16

## Purpose

This is the final large pre-runtime-test iteration. It adds a server-authoritative baseline/report facility so one manual pass can verify narrative AI, relation shadow receipts, diplomatic statements, native non-mutation, persistence, reconnect/JIP and save/restart.

## Runtime commands

- `/aiport-gate baseline` stores a bounded in-memory baseline for the authoritative player and current leased hero target.
- `/aiport-gate report` compares current native relation and war state against that baseline and shows private persistence deltas.
- `/aiport-status` is an alias for the current report.

The report contains build/protocol/capability flags, campaign generation/revision, persistence health, backend configured/active/key-present booleans, authoritative player/target kingdoms, native relation/war state, player-private memory/social/diplomacy counts, target custom score and up to five recent diplomatic shadow statements. It never renders the API key.

## Safety and authority

- New capability bit 256; expected total flags 511.
- Request binds protocol, request ID, campaign generation and state revision.
- Server resolves the controlled player hero and revalidates the active target lease.
- Baselines are bounded to 256 entries and are lost intentionally on server restart.
- Report is bounded to 7600 characters.
- No native mutation API is present in the gate service.

## Operational tooling

- `tools/start_0_0_57_gate_with_groq.cmd`: hidden interactive key input, process-environment-only injection, no secret persisted.
- `tools/save_0_0_57_gate.cmd`: allowlisted disposable-server save command.
- `tools/check_0_0_57_runtime_gate.cmd`: post-test log/manifest validator.
- Full instructions: `docs/RUNTIME_BUNDLED_GATE_0_0_57.md`.

## Automated verification

All cumulative suites from 0.0.50 through 0.0.57 pass. The new suite verifies typed protobuf fields, capability negotiation, authoritative identity/lease validation, generation/revision rejection, bounded output, private projections, backend key-presence-only reporting and absence of native campaign mutation APIs. Python and PowerShell helper syntax checks pass.

## Build and deployment

- Build: `0.0.57-dev`.
- Runtime size: 218112 bytes.
- SHA-256: `e3b96d3319339934a28851eb9416a693d5b8794b246587160e7ae15128165195`.
- Source rollback: `backups\source-20260816-231614-pre-0.0.57`.
- Deployment rollback: `backups\m0-20260816-232407`.
- Startup log: `artifacts\runtime-m0\logs\coop-server-20260816-232408.log`.
- Server PID: 19884.
- Startup reached SERVING with `loaded:2:social:0:diplomacy:0`, revision 2, read-only false.

The deployment smoke process intentionally has no API key. The user must start the actual gate through the secret-safe launcher before beginning the manual sequence.
