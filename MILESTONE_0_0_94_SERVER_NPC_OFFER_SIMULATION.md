# Milestone 0.0.94 - server-simulated NPC-to-player diplomacy offer

Adds a server-console development command that creates an incoming NPC ruler offer without requiring a second player or a player-to-player conversation:

`aiport.simulate_npc_offer <peer-id|player-hero-id> <npc-hero-id> <war|peace>`

The server resolves the connected recipient and controlled campaign objects authoritatively, resolves the NPC from the campaign registry, verifies both faction leaders, pair eligibility and current war state, writes a durable `pending_recipient` statement, advances the unified revision, and sends the existing private inbox notification to that player. The path never calls a native adapter and always records `NativeMutationApplied=false`.

Source rollback: `backups\source-20260817-034220-pre-0.0.94`

## Runtime proof

Deployed build `0.0.95-dev`. The server accepted the offline command for `Hero_Player`, created statement `2c3dee6807d14af8a6962fa244b2460d` from `lord_5_1`, advanced revision to 10, and reported `NativeMutationApplied=false`. Save/restart restored diplomacy count 2 and revision 10.
