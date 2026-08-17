# Milestone 0.0.51 - relation confirmation lifecycle

Date: 2026-08-16

## Scope

`0.0.51-dev` adds a two-step, still non-mutating relation proposal lifecycle on top of the validated 0.0.50 shadow intent.

- Protocol remains 2.
- Intent schema is 3.
- New capability bit: 32; expected full flags: 63.
- `/relation-propose +1` and `/relation-propose -1` create a bounded proposal.
- Server result: `confirmation_required / player_confirmation_required`.
- `/relation-confirm` confirms the exact proposal.
- Final result remains `confirmed_shadow / mutation_suppressed`, `MutationApplied=false`.
- Confirmation expires after 60 seconds.
- Proposal is bound to peer, player hero, campaign generation, state revision, conversation, target lease and target instance.
- Proposal is single-use and request replays are idempotent.
- Cross-peer, stale revision, changed target and malformed payloads fail closed.

## Automated proof

`test_0_0_50_relation_shadow.py` and `test_0_0_51_relation_confirmation.py` pass. The executable harness covers proposal, confirmation, no mutation/revision, replay, single use, cross-peer rejection, binding mismatch, stale proposal and preserved no-op behavior.

Build:

- Runtime size: 175616 bytes.
- Runtime SHA-256: `4f27bcf5bf79dc6f62b58d983414a0533b2df2283cb2b85929000286e63eef2c`.
- Bootstrap SHA-256: `8357d5c56766dabba2777d472b87b1919a1925242b6d1f8a00bf99fd653934cf`.
- Source rollback: `backups\source-20260816-220321-pre-0.0.51`.
- Deployment rollback: `backups\m0-20260816-221531`.

## Runtime startup

- Matching client/server hashes verified.
- Disposable save: `aiport-m0`; live campaign untouched.
- Log: `artifacts\runtime-m0\logs\coop-server-20260816-221546.log`.
- Server PID: 17840.
- Runtime loaded 0.0.51-dev, Groq enabled, restored 2 records at revision 2, and reached CampaignReady/SERVING.

## Bundled acceptance gate

One combined client pass must verify flags 63, proposal -> confirmation, old relation shadow, no-op/startup snapshot, one Groq turn, and unchanged revision/native relation for both shadow paths.


## 2026-08-16 - 0.0.52 runtime revision-sync hotfix

- Found the 0.0.51 runtime failure: a completed dialogue advanced memory revision from 2 to 3 while the client retained revision 2 from initial capabilities, causing `stale_revision` for relation intents.
- Added optional protobuf field 9 (`StateRevision`) to `AIConversationResult`; existing field numbers remain unchanged.
- Server now returns the authoritative post-turn revision and the client adopts it before later intent requests.
- Build `0.0.52-dev`, runtime SHA-256 `cfeb1d212f6e3685c322e9252256ed3dd7676f377d19dfaeb23363f2bf12c69e`.
- Source rollback: `backups\source-20260816-222419-pre-0.0.52`; deployment rollback: `backups\m0-20260816-222530`.
- Runtime proof in `artifacts\runtime-m0\logs\coop-server-20260816-222530.log`: flags 63, snapshot/no-op valid, revision 2 -> 3 after dialogue, proposal +1 required confirmation, confirmation returned `confirmed_shadow / mutation_suppressed`, and shadow -1 returned `shadow_validated / mutation_suppressed`; all relation operations had `MutationApplied=False`, revision stayed 3.
- The one-step `/relation-shadow` command intentionally does not require confirmation; only `/relation-propose` followed by `/relation-confirm` uses the two-step lifecycle.
- Groq was not part of this restarted runtime proof because the new process had no `GROQ_API_KEY`; the dialogue correctly used the stub path.
