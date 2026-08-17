# AIPort 0.0.39 player-resolution hotfix

## 0.0.39-dev hotfix — transient player resolution (2026-08-14 10:10 +05:00)

- Runtime failure reproduced from the first 0.0.38 manual test. At `10:05:22` the client opened target `lord_5_13` while the join baseline was still settling; the authoritative `IPlayerManager` mapping was not ready, so the server correctly rejected the bind with `player_unresolved`.
- Client defect: `AIConversationTargetBound(Accepted=false)` treated this transient condition as permanent, cleared deferred player text, and never retried. No `AIConversationRequest` reached Groq, which explains the missing reply.
- Fix: client retries only `player_unresolved` target binds every `1000 ms`, bounded to `30` attempts, retains deferred text, and submits it automatically after an accepted lease. Permanent target errors are still surfaced and never retried. Server validation, lease ownership, stale-target rejection, protocol version and protobuf field numbers are unchanged.
- Build: `0.0.39-dev`, protocol `1`. Runtime SHA-256: `97ecba351f44f2650761fa764d043c7f8c8efd711060dda988be09d4650e6e46`; bootstrap unchanged: `8357d5c56766dabba2777d472b87b1919a1925242b6d1f8a00bf99fd653934cf`.
- Regression suites 0.0.33–0.0.38 remain green; new 0.0.39 retry suite: `9/9`.
- Source rollback: `backups\source-20260814-100903-pre-0.0.39`. Live deployment waits for the active game client to close.

## Runtime evidence

The failing trace contained a compatible 0.0.38 handshake, campaign entry, and then `AIPort target bind rejected ... ErrorCode="player_unresolved"`. No conversation request or Groq call followed. The rejection occurred during the large join-baseline transfer and is therefore a transient authoritative-player registration race, not an invalid NPC or backend failure.

## Safety properties retained

- The server still resolves player identity from `IPlayerManager`; no client player claim is trusted.
- Only `player_unresolved` is retried.
- Retry count and interval are bounded.
- Deferred text remains local until a server-issued lease is accepted.
- Spoofed, cross-peer, mismatched, closed and stale leases remain rejected.

## 0.0.39 deployment verified (2026-08-14 10:11 +05:00)

- Client and dedicated server deployed from the green 0.0.39 build. Deployment rollback: `backups\m0-20260814-101025`.
- Runtime SHA-256 matches on client/server: `97ecba351f44f2650761fa764d043c7f8c8efd711060dda988be09d4650e6e46`; bootstrap remains `8357d5c56766dabba2777d472b87b1919a1925242b6d1f8a00bf99fd653934cf`.
- Disposable server PID `11424` loaded build `0.0.39-dev`, protocol `1`, Groq `enabled=True`, `keyPresent=True`, and reached `SERVING` at `10:11:10`.
- No AIPort fatal/unhandled exception appeared in the startup scan. The client may be relaunched for the corrected manual test.

