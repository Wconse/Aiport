# Hotfix 0.0.41 — authoritative identity from accepted Coop join

0.0.40 proved retry timing was fixed but player resolution was not. The live server had a resolved player, while both peer-keyed lookups failed for later AIPort messages.

The new registry records the first controller ID seen in Coop's own join messages for the exact `PlayerConnected` generation. Resolution uses it only when the current server connection is live and already in `CampaignState` or `MissionState`, then resolves the player by controller ID from the server registry. Conflicts, stale tokens, disconnects and peer-ID reuse are denied. Client hero claims and sole-player fallbacks are never authority.

Protocol stays 1; request fields 1–9 and result fields 1–8 are unchanged. The lifecycle harness passed 17 scenarios; structural checks passed 13/13; all prior relevant suites pass. Runtime SHA-256: `affd98051450a4c01960ccda09d3897ca7460a7a9a63cccb40b91eede0de90a2`. Source rollback: `backups\source-20260814-184456-pre-0.0.41`.
## Deployment

Deployed on 2026-08-14 at 19:04 local time. Rollback: `backups\m0-20260814-190457`. Client, server and build artifact SHA-256 values match. Startup confirmed build `0.0.41-dev`, protocol 1, Groq enabled with process-only credential, campaign ready and `SERVING`. Conversation-path runtime proof remains the next manual check.
