# Milestone 0.0.92 - Coop canonical native preflight resolution

## Defect

A valid NPC-policy war statement reached `accepted_shadow`, but `/diplomacy-ready` returned `stale_diplomatic_context`. Proposal handling used the canonical Coop `IObjectManager`, while native preflight attempted `Hero.Find` for the synchronized player hero.

## Fix

- Native preflight now receives the already-authoritative controlled `Hero` resolved from the requesting peer.
- The persisted source ID must exactly match that authoritative hero.
- Target and startup-reconciliation heroes resolve through `IObjectManager` first, with `Hero.Find` only as an NPC-compatible fallback.
- Authority, faction pair, war state, generation, revision, journal and default-off adapter barriers are unchanged.
- No native mutation is enabled or executed by this fix.

Source rollback: `backups\source-20260817-025910-pre-0.0.92`
