# Next steps

Updated: 2026-08-17 07:30 +05:00

## Scope override

The earlier multi-client acceptance plan is superseded. Current development and runtime acceptance use **one connected player only** inside the Coop-hosted campaign.

Out of scope for the current product plan:

- player-to-player proposals, consent, inboxes, lifecycle or notifications;
- cross-player cancellation/authorization tests;
- simultaneous-player acceptance gates;
- any requirement to launch a second client.

Historical runtime records remain evidence of what was tested in older builds; they are not current requirements.

## Current bundled gate — `0.0.99-dev`

1. Keep protocol `2`, capability flags `2097151`, source build `0.0.99-dev` and native war/peace adapters OFF.
2. Keep the OpenAI-compatible backend for Groq/OpenRouter/local test profiles.
3. Add direct `Player2` as a selectable server backend with separate code, fixed HTTPS endpoints, local token/account sources, bounded account rotation and no credential disclosure.
4. Build and run the cumulative `0.0.50..0.0.99` tests plus the Player2 credential/provider harness.
5. Deploy identical `0.0.99-dev` DLLs to the disposable client/server runtime with rollback.
6. Start the disposable server with Player2 selected and verify provider readiness without printing identity, token, password, auth header or raw provider error bodies.
7. With one connected player, prove a bounded real Player2 reply and the authoritative `Hero_Player` scheduler/recipient path.
8. Complete the existing offer flow: private map notification/inbox, Accept or Reject, duplicate-click protection, save/restart/reconnect and JIP reconstruction for the same player.
9. Require `NativeMutationApplied=false` throughout the gate.
10. Record final DLL hashes, state revision, generation and runtime evidence in the handoff/status/log documents.

## Native diplomacy

Native war and peace remain a separate destructive gate requiring explicit operator approval. They are not part of `0.0.99` and must remain disabled.
