# AIPort 0.0.40 authoritative peer/retry hotfix


## 0.0.40-dev hotfix — authoritative peer resolution and paused retry (2026-08-14 18:25 +05:00)

- Second manual failure reproduced at `18:20`: compatible 0.0.39 client entered campaign, but target `lord_5_13` remained rejected with `player_unresolved`. The client did retry at `18:20:02`, proving the 0.0.39 retry path executed, but retries then stopped because they were driven by `CampaignEvents.TickEvent` while the conversation had paused campaign time. No `AIConversationRequest` or Groq call occurred.
- Server-side diagnosis: direct `PlayerManager.TryGetPlayer(NetPeer)` can miss when Coop replaces the LiteNetLib peer wrapper during join. The player/controller registry remains authoritative.
- 0.0.40 fix: first retain direct authoritative lookup; if it misses, resolve only through `IPlayerManager.Players` plus `TryGetPeer(controllerId)` and require exact live `NetPeer.Id` equality. Client-provided player identity is never used. Target-binding retries now run on the application tick, so campaign pause cannot stop them; the 30-attempt bound remains.
- Security properties remain: server-issued lease, exact peer/conversation/target/instance matching, stale/closed rejection, and narrative-only action gate. Protocol remains `1`; protobuf field numbers are unchanged.
- Tests: all 0.0.33–0.0.39 regressions green after the implementation update; new 0.0.40 suite `10/10`.
- Runtime SHA-256: `653603d07c29234013c8512bb7a857035ddb188530e11172f79df8e4c7dad721`; client/server hashes match. Bootstrap unchanged at `8357d5c56766dabba2777d472b87b1919a1925242b6d1f8a00bf99fd653934cf`.
- Source rollback: `backups\source-20260814-182234-pre-0.0.40`; deployment rollback: `backups\m0-20260814-182343`.
- Disposable server PID `12392` loaded `0.0.40-dev`, Groq `enabled=True`, `keyPresent=True`, and reached `SERVING` at `18:24:28`. Runtime conversation success still requires the next client reconnect test.

## Acceptance trace required

The next manual request must show, in order: compatible `0.0.40-dev` handshake; target bind accepted (possibly after bounded `player_unresolved` retries); `AIConversationRequest` accepted; Groq completion; committed result. Any failure before that sequence remains a blocker.
