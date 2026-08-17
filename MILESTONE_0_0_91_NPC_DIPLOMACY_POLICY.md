# Milestone 0.0.91 - NPC-controlled diplomacy recipient policy

## Goal

Restore the primary AIInfluence product path: a player negotiates with an authoritative NPC faction leader. Player-to-player recipient consent remains supported only when the target leader is actually player-controlled.

## Behavior

- The proposal and source confirmation remain bound to the live NPC conversation lease.
- The server re-resolves both heroes, factions, authority and current war state.
- A connected/player-character target remains `pending_recipient` and requires that player's private consent.
- An NPC ruler is resolved immediately by a deterministic server policy:
  - war declaration/challenge is acknowledged when the factions are distinct, authorized and not already at war;
  - peace is accepted at relation `>= -25` and rejected below that threshold;
  - stale authority, wrong precondition or unsupported action fails closed.
- Policy output can change only the durable shadow lifecycle. It never calls native adapters.
- Raw LLM text is not an authorization signal. Native war/peace still requires explicit preflight, an armed default-off adapter and a single-use commit token.

## Safety

The existing player-controlled recipient path, journal, campaign pin, save barrier and native adapter gates are unchanged. War and peace adapters remain default-off.

## Pre-deployment verification

- Source rollback: `backups\source-20260817-023529-pre-0.0.91`
- Cumulative suites 0.0.50-0.0.91: 14/14 PASS.
- Clean build: PASS.
- AIPort.dll: 298496 bytes; SHA-256 `fef06ef5303c44dc548c3c38df0a6c80f75435a2eb1865fe0ed0afd6435d9dca`.
- AIPort.Bootstrap.dll SHA-256: `8357d5c56766dabba2777d472b87b1919a1925242b6d1f8a00bf99fd653934cf`.
- Native war/peace adapter inputs remain default-off.
- Deployment uses the guarded M0 deploy tool, which creates a separate live-file rollback before replacement.

## Deployment

- Deployment rollback: `backups\m0-20260817-023818`
- Runtime log: `artifacts\runtime-m0\logs\coop-server-20260817-023819.log`
- Server PID: `3212`
- State: `loaded:3:social:5:diplomacy:0:nativeJournal:0`; revision 8; writable; SERVING
- Client/server/artifact hash parity: PASS
- Native war: OFF; native peace: OFF; generation pin: absent
- Native mutations during implementation, tests and deployment: zero
