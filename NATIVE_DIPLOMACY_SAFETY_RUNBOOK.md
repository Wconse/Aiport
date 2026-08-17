# Native diplomacy armed validation runbook

> Destructive test gate. Do not run automatically. Use only the disposable `aiport-m0` runtime. Never use or touch `saveauto1`.

## Current scope

The previous multi-client validation sequence is superseded. Player-to-player interaction is not part of the current AIPort plan. Native diplomacy is also **not** part of the `0.0.99` runtime gate and must remain disarmed.

If native validation is separately authorized later, the supported test shape is one connected player plus a server-owned NPC counterpart. The server must resolve the connected player's authoritative Hero, and an explicit operator decision must authorize the disposable world-state mutation. No second player or second client is required.

## Preconditions for any future armed test

1. Obtain explicit operator approval for the exact native mutation.
2. Confirm the client and server load the same approved DLL hash.
3. Confirm the server uses the disposable `aiport-m0` state and the exact intended campaign generation.
4. Back up disposable deployment and state immediately before the test.
5. Confirm exactly one authoritative connected player Hero and a valid NPC-controlled counterpart faction.
6. Arm only one adapter action at a time.
7. Keep a written rollback and reconciliation plan.

## Arm war only

Set live config:

```json
"enableNativeWarAdapter": true,
"enableNativePeaceAdapter": false
```

Start the disposable server with:

```text
AIPORT_ENABLE_NATIVE_WAR=I_UNDERSTAND_NATIVE_WAR
AIPORT_NATIVE_DIPLOMACY_GENERATION=<exact generation printed by this startup>
```

Do not set `AIPORT_ENABLE_NATIVE_PEACE`.

## Arm peace only

Set live config:

```json
"enableNativeWarAdapter": false,
"enableNativePeaceAdapter": true
```

Start the disposable server with:

```text
AIPORT_ENABLE_NATIVE_PEACE=I_UNDERSTAND_NATIVE_PEACE
AIPORT_NATIVE_DIPLOMACY_GENERATION=<exact generation printed by this startup>
```

Do not set `AIPORT_ENABLE_NATIVE_WAR`.

## Single-connected-player sequence

1. Resolve the connected player's authoritative Hero and the server-owned NPC counterpart.
2. Create and resolve the exact shadow proposal with explicit player/operator approval as required by policy.
3. Request a fresh preflight token and verify generation, state revision, authority, current native precondition and pair lock.
4. Execute the action-specific explicit commit command once.
5. Verify native faction state, `verified` journal phase, durable `committed_native_war` or `committed_native_peace`, reconnect/JIP reconstruction and idempotent replay rejection for the same player.
6. Test restart reconciliation only with a deliberately prepared fault-injection build; never force-kill a production commit without a rollback snapshot.

## Immediate disarm

After the test:

1. Stop the disposable server.
2. Set both config flags to false.
3. Remove all native diplomacy environment variables.
4. Restart and verify logs show war off, peace off and generation pin absent.
5. Retain logs/journal and restore from the disposable rollback if any invariant failed.

Do not run this gate unless the operator explicitly approves the native world-state mutation.
