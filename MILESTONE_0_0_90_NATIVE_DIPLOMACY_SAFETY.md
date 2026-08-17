# Milestone 0.0.90 — native diplomacy production safety

## Scope

This bundled milestone hardens the existing default-off native war bridge and adds a separately gated default-off native peace bridge. It deliberately does not enable or execute either native action.

## Durable commit journal

Each explicit native commit receives a bounded record with statement/action, authoritative hero and faction IDs, campaign generation, source revision, timestamps, mutation observation, and reason code.

```text
prepared -> applying -> applied -> verified
                         |         |
                         +-> recovery_required
prepared/applying/applied -> failed
prepared -> aborted
```

- `prepared` is atomically persisted before a mutation can begin.
- `applying` is atomically persisted before the mutation API is called.
- `applied` records the adapter's verified postcondition.
- `verified` records agreement between native state, journal, and durable diplomacy ledger.
- Failure to persist before the native call blocks the call and moves external state fail-closed/read-only.
- The journal uses `native-diplomacy-journal.ndjson` and an independent SHA-256 sidecar.

## Recovery

Startup and hourly maintenance inspect only recoverable records:

- a `prepared` record is aborted because the call never began;
- if `applying`/`applied` matches the real faction postcondition, the journal and diplomacy ledger are repaired to verified;
- an unresolved pair or missing postcondition becomes `recovery_required`;
- **recovery never invokes either native adapter and never automatically retries a mutation**.
- If a native call was attempted but its result/postcondition is unknown, the record enters `recovery_required`; it is never marked terminal `failed`.

## Native peace adapter

The sole peace mutation is isolated in `NativePeaceAdapter.cs`:

```csharp
MakePeaceAction.Apply(sourceFaction, targetFaction);
```

The adapter requires `atWar == true` before the call and verifies `atWar == false` afterward. Successful lifecycle state is `committed_native_peace`.

## Arming barriers

War requires all three:

```json
"enableNativeWarAdapter": true
```

```text
AIPORT_ENABLE_NATIVE_WAR=I_UNDERSTAND_NATIVE_WAR
AIPORT_NATIVE_DIPLOMACY_GENERATION=<exact-current-generation>
```

Peace requires all three:

```json
"enableNativePeaceAdapter": true
```

```text
AIPORT_ENABLE_NATIVE_PEACE=I_UNDERSTAND_NATIVE_PEACE
AIPORT_NATIVE_DIPLOMACY_GENERATION=<exact-current-generation>
```

Both additionally require accepted bilateral shadow consent, source/target diplomatic authority, authoritative peer/hero and exact faction binding, correct current war precondition, writable loaded state, no save/load barrier, exact generation/revision, and an unexpired single-use action-bound commit token. Only one active commit may exist per unordered faction pair, with no more than two attempts per hour.

## Validation

- New executable safety harness: PASS.
- Cumulative suites `0.0.50–0.0.90`: 13/13 PASS.
- Clean build: PASS.
- Runtime size: `294912` bytes.
- Runtime SHA-256: `8036c7b9315277cff4f290a046dc7a19272d55b7e8af2b0c3ef8b111273b197d`.
- Bootstrap SHA-256: `8357d5c56766dabba2777d472b87b1919a1925242b6d1f8a00bf99fd653934cf`.
- Client/server/artifact hash parity: PASS.
- Startup smoke: PASS; `SERVING`, state writable, journal empty.
- Live gates: war off, peace off, generation pin absent.
- Native mutations executed during implementation/tests/deploy: zero.

## Deployment

- Source rollback: `backups\source-20260817-013613-pre-0.0.90`
- Deployment rollback: `backups\m0-20260817-014839`
- Disposable save: `aiport-m0`
- Startup log: `artifacts\runtime-m0\logs\coop-server-20260817-014839.log`
- Campaign generation: `4ea97daf7c4e8ae14149a02cff988e72`
- Forbidden save: `saveauto1`
