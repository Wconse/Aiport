# Milestone 0.0.55 - authoritative diplomacy snapshot

Date: 2026-08-16

## Scope

Adds `/diplomacy-snapshot`, the first political feature of the Coop port. It is server-authoritative and strictly read-only.

- New capability bit: 64; expected total negotiated flags: 127.
- Request is bound to protocol, campaign generation and state revision.
- Server resolves the authoritative player hero and kingdom.
- Response lists current kingdoms, player-relative war/peace stance, settlement count and army count.
- Output is bounded to 6000 characters.
- No original AIInfluence executor is invoked.
- War, peace, relation, gold and settlement-owner mutation APIs are absent from the snapshot service.

## Automated proof

All cumulative relation/social/persistence suites pass. The 0.0.55 structural suite verifies protocol fields, authority resolution, generation/revision rejection, client response paths and absence of native mutation APIs.

## Build and deployment

- Runtime size: 189440 bytes.
- Runtime SHA-256: `4554aeab6e98eac336fcfd255ef2d72dccb5d56655c5dcdbf4546c6774bda979`.
- Source rollback: `backups\source-20260816-225714-pre-0.0.55`.
- Deployment rollback: `backups\m0-20260816-225841`.
- Startup log: `artifacts\runtime-m0\logs\coop-server-20260816-225841.log`.
- Server PID: 8604.
- Startup reached SERVING with `loaded:2:social:0`, revision 2 and read-only false.

The runtime snapshot test is intentionally deferred and will be bundled with diplomatic shadow statements, social persistence/JIP, cooldown and Groq regression.
