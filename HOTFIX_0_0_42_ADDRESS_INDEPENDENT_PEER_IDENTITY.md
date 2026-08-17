# Hotfix 0.0.42 — address-independent authoritative peer identity

## Runtime evidence

0.0.41 successfully froze the controller identity from Coop's real join messages, but two conversations still returned `player_unresolved` for every bind attempt. The accepted identity log was present before campaign entry, so identity capture was no longer the failing stage.

The remaining resolver condition compared `IPAddress` objects belonging to different LiteNetLib peer wrappers. That comparison is transport metadata, not authoritative identity, and can fail even when both wrappers refer to the same live server peer.

## Corrected authority chain

1. `PlayerConnected` opens a token-bound generation for a server-assigned peer ID.
2. The first controller ID observed from Coop's own validate/new-hero join messages is frozen; conflicts are rejected.
3. A later AIPort message must come from a connected peer.
4. The server must have exactly one connected `IConnectionCollection` entry with the same peer ID already in `CampaignState` or `MissionState`.
5. The frozen controller must resolve to a complete `Player` in `IPlayerManager`, with exact controller equality.

No IP-address equality, client hero claim, or sole-player fallback participates in authorization.

## Additional audit fixes

- Duplicate current-token connect notifications are idempotent.
- Disconnect cleanup is exact-token-bound; stale disconnects cannot erase a newer reused peer generation.
- A new generation clears a residual target lease and cancels residual backend work.
- Failed ThreadPool dispatch releases inflight and worker counters.
- Empty rate-limit controller buckets are pruned after their one-minute window.
- Subscription symmetry, protocol fields, bounded collections, secret handling, narrative-only action gate and absence of `Hero.MainHero` mutation were re-audited.

## Compatibility and verification

Protocol remains 1. Request fields 1–9 and result fields 1–8 are unchanged. All 11 recent suites (0.0.33–0.0.42) pass; the generation harness covers 22 cases, plus 14 address/state checks and 14 lifecycle/consistency checks. Runtime SHA-256: `68e539037711cf927528a6a08532a06b10f2a97b93ab16c54406c12c79681989`. Source rollback: `backups\source-20260814-192803-pre-0.0.42`.
## Deployment

Deployed on 2026-08-14 at 20:10 local time. Rollback: `backups\m0-20260814-201002`. Build, client and server runtime hashes match `68e539037711cf927528a6a08532a06b10f2a97b93ab16c54406c12c79681989`; bootstrap hashes match `8357d5c56766dabba2777d472b87b1919a1925242b6d1f8a00bf99fd653934cf`. Server PID 19296 loaded build `0.0.42-dev`, protocol 1, Groq `enabled=True` / `keyPresent=True`, reached campaign ready and `SERVING`, with no fatal or unhandled startup error. Conversation-path runtime proof is pending.
