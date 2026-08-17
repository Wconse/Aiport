# Milestone 0.0.95 - offline server NPC offer queue

Extends the 0.0.94 server simulation so an operator can address an authoritative synchronized player Hero while that player is offline. The server resolves the player Hero and NPC ruler from the campaign registry, validates authority/factions/war preconditions, persists a private `pending_recipient` record, and delivers the existing inbox notification on the player's next private-snapshot completion. No player-to-player conversation and no free-form dialogue option are involved. Native mutation remains impossible on this path.

Source rollback: `backups\source-20260817-034733-pre-0.0.95`

## Runtime proof

Deployed build `0.0.95-dev`. The server accepted the offline command for `Hero_Player`, created statement `2c3dee6807d14af8a6962fa244b2460d` from `lord_5_1`, advanced revision to 10, and reported `NativeMutationApplied=false`. Save/restart restored diplomacy count 2 and revision 10.
