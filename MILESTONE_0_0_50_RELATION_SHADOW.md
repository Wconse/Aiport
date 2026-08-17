# Milestone 0.0.50 - relation shadow intent

Date: 2026-08-16 21:46 +05:00

## Scope

`0.0.50-dev` adds server-authoritative `relation_change_shadow` validation without calling any Bannerlord relation mutation API.

## Protocol and safety

- Protocol `2`; intent schema `2`.
- Capability bit `16`; expected negotiated flags with the gate enabled: `31`.
- Config gate: `enableRelationShadowIntents`; default is `false`.
- Manual probes: `/relation-shadow +1` and `/relation-shadow -1`.
- Hero targets only; delta `-2..2`, excluding zero.
- Bound to conversation, target lease, target instance, campaign generation and state revision.
- Accepted result: `shadow_validated / mutation_suppressed`, `MutationApplied=false`.
- Validation does not increment persistent-state revision.
- Request replay is idempotent; audit storage is bounded to 1024 records.

## Deterministic proof

`python tools\test_0_0_50_relation_shadow.py` passed all structural checks and executable C# harness scenarios.

- `AIPort.dll`: 166400 bytes; SHA-256 `d9a905310edb452ee94519f175175003ceb889e239498eb0323e9aaa20e21c70`.
- Bootstrap: 10240 bytes; SHA-256 `8357d5c56766dabba2777d472b87b1919a1925242b6d1f8a00bf99fd653934cf`.

## Deployment and startup

- Pre-runtime backup: `backups\pre-runtime-0.0.50-20260816-214447`.
- Deployment rollback: `backups\m0-20260816-214447`.
- Matching client/server hashes verified.
- Gate enabled only in `E:\BCOOP\aiport-server.json` for disposable M0 testing.
- Disposable save `aiport-m0`; live campaign untouched.
- Log: `artifacts\runtime-m0\logs\coop-server-20260816-214459.log`.
- Runtime loaded `0.0.50-dev`, restored `loaded:2` at revision `2`, and reached CampaignReady/SERVING.
- Startup PIDs: wrapper `1060`, engine `17716`.
- No API key was present in the launch environment, so Groq is safely disabled for this shadow-only probe.

## Pending live proof

Connect a client, verify flags `31`, run both probes in an active hero dialogue, and confirm `MutationApplied=false`, unchanged real relation, and unchanged revision.

## Live runtime proof - 2026-08-16

- Client `0.0.50-dev` negotiated protocol 2 and capability flags `31`.
- Positive probe: target `hero:CharacterObject_1649`, delta `+1`, status `shadow_validated`, reason `mutation_suppressed`, `MutationApplied=False`, revision `2 -> 2`.
- Negative probe: same target, delta `-1`, status `shadow_validated`, reason `mutation_suppressed`, `MutationApplied=False`, revision `2 -> 2`.
- Runtime proof log: `artifacts\runtime-m0\logs\coop-server-20260816-214459.log`.
- Relation-shadow milestone is runtime validated; no real relation mutation was enabled.

## Backend restart

- Disposable server restarted with Groq available only to the child process.
- Startup log: `artifacts\runtime-m0\logs\coop-server-20260816-215513.log`.
- Sanitized settings: backend `Groq`, enabled `True`, key present `True`, model `llama-3.1-8b-instant`.
- External state restored `loaded:2`, revision `2`, ReadOnly `False`; server reached SERVING.
- Current wrapper PID: `7048`.
- Pending: one normal post-restart Groq dialogue turn and regression verification.
