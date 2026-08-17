# 0.0.48 return-to-vanilla cancellation hotfix

## Runtime finding

The 0.0.47 solo run verified real Groq dialogue and memory behavior, but exposed a branch-lifecycle race. `Return to normal dialogue` changed Bannerlord's dialogue token without notifying the AIPort client handler. AIPort only canceled when `CampaignEvents.ConversationEnded` fired, which occurs when the entire NPC conversation closes. Until then, a pending or queued AI result could still write `DialogText` while the player was already in the vanilla branch.

The full-exit cancellation path itself is healthy. In the final attempt, the server accepted request `11e35916ac3c4c9ea30bf748ce70534e`, received the ownership-matched cancel, requested HTTP abort, suppressed the backend result, and archived zero turns.

## Fix

- Added an explicit `ExitToVanilla` callback to `AIPortConversationInputBridge`.
- Bound the `aiport_finish_player_option` consequence to that callback.
- Added `HandleReturnToVanilla` on the client. It:
  - captures the exact pending request/conversation;
  - cancels retry and timeout state;
  - clears deferred text and queued UI display;
  - sends `AIConversationCancel`;
  - deliberately keeps the target lease and outer NPC conversation alive.
- A late result cannot queue because result handling still requires the exact current pending request ID.

## Safety

No protocol fields changed. Protocol stays at 2. No campaign mutation, raw model action, cross-peer cancellation, or `Hero.MainHero` substitution was introduced.

## Verification

- `tools/test_0_0_48_return_to_vanilla_cancel.py`: 12/12.
- Clean build succeeded.
- Runtime: 153,600 bytes.
- SHA-256: `fa7f71b10094a7a14765c9751f92a90820ee9c851775abc9994be70d3f1e4f56`.
- Source rollback: `backups\source-20260815-022554-pre-0.0.48`.

Runtime deployment and one manual branch-exit retest remain.

## 0.0.48 deployment verified (2026-08-15 02:31 +05:00)

- Guarded deployment completed with rollback `backups\m0-20260815-022941`.
- Client/server runtime hashes match: `fa7f71b10094a7a14765c9751f92a90820ee9c851775abc9994be70d3f1e4f56`; bootstrap unchanged.
- Disposable server PID `18692` loaded `0.0.48-dev`, protocol 2. Groq is enabled with process-only credentials (`keyPresent=True`), campaign state is writable, and campaign-ready was reached.
- Log: `artifacts\runtime-m0\logs\coop-server-20260815-022952.log`. No live campaign save was touched.
- Launcher was closed for DLL replacement. Manual runtime gate: send a long AI request, immediately choose `Return to normal dialogue`, and verify no AI result overwrites the vanilla branch. Expected logs include `AI dialogue branch closed`, ownership-matched cancel, and either backend suppression or harmless ignored late result.

## 0.0.48 runtime proof passed (2026-08-15 02:37 +05:00)

- Client/server handshake matched `0.0.48-dev`, protocol 2.
- Request `dda0c1c6e56b43dcbb6aa53d10f79f6b` was accepted for conversation `231c3d62e48f4962ba2131b1ed8185eb`.
- Selecting `Return to normal dialogue` emitted the new branch-close marker with `PendingRequestCanceled=true` and sent cancel reason `return_to_vanilla`.
- Server ownership matched, marked the backend request canceled, released inflight state, requested HTTP abort, and suppressed the late result. No AI result was applied to vanilla dialogue.
- The 0.0.48 branch-lifecycle defect is closed.
