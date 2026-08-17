# Default-off native war adapter runbook

## Safety state

The adapter must remain off for ordinary development. Missing either independent gate makes native commit impossible:

- server config: `enableNativeWarAdapter`;
- process environment: `AIPORT_ENABLE_NATIVE_WAR=I_UNDERSTAND_NATIVE_WAR`.

Never arm against `saveauto1`. The first armed test must use disposable `aiport-m0` and two player-controlled faction leaders.

## Kill switch

Remove the process environment arming value and restart the dedicated server. Config alone cannot activate the adapter.

## Preconditions for a future armed test

- current client/server hashes match;
- server state is writable and loaded;
- source and recipient are current authoritative faction leaders;
- bilateral `war` shadow proposal has status `accepted_shadow`;
- factions are distinct, eligible and not already at war;
- no save is in progress;
- rollback exists.

## Explicit sequence

1. Run `/diplomacy-ready <statement-id>` as the initiating leader.
2. Confirm the runtime report and 60-second single-use token.
3. Run `/diplomacy-native-war <statement-id> <commit-token>` from the same peer and hero before revision changes.
4. Confirm lifecycle status `committed_native_war`, `NativeMutationApplied=true` and the native war postcondition.
5. Save/restart/reconnect and verify persistence and replication before considering native peace work.

The adapter never auto-commits, never accepts an unbound token and does not implement peace.
