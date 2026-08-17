# Milestone 0.0.53 - persistent social shadow ledger

Date: 2026-08-16

## Scope

Confirmed relation proposals now create a private, server-authoritative custom social receipt while native Bannerlord relation mutation remains disabled.

- Bounded ledger: 512 receipts.
- Custom score bounds: -25 through +25.
- Per player/target cooldown: 5 seconds.
- Receipt IDs are idempotent.
- Each receipt stores player hero, target instance, delta, before/after custom score and UTC time.
- A newly recorded receipt advances unified state revision exactly once.
- `MutationApplied=false` and `NativeMutationApplied=false` remain explicit.

## Persistence and projection

- New `social.ndjson` beside `memory.ndjson`.
- Manifest schema remains 1 and gains optional `socialSha256` and `socialRecordCount`, preserving compatibility with old manifests.
- Social hash mismatch fails closed and marks state read-only.
- Private state snapshots contain `socialRecords` filtered by authoritative player hero.
- Save/load restores receipt history, custom score, and combined revision.

## Automated proof

Cumulative 0.0.50 and 0.0.51 suites pass. New executable `test_0_0_53_social_shadow_persistence.py` covers:

- first receipt and before/after score;
- idempotent replay;
- cooldown;
- second receipt;
- unified revision;
- save/load restoration;
- private snapshot projection;
- social-file integrity failure.

## Build and deployment

- Build: `0.0.53-dev`.
- Runtime size: 183296 bytes.
- Runtime SHA-256: `263b12abd00d4aaa153c2d997f8c6012a53c0aaf314226f9d43675beaef377bd`.
- Source rollback: `backups\source-20260816-223904-pre-0.0.53`.
- Deployment rollback: `backups\m0-20260816-224136`.
- Startup log: `artifacts\runtime-m0\logs\coop-server-20260816-224137.log`.
- Server PID: 14832.
- Existing schema-1 state migrated successfully as `loaded:2:social:0`, revision 2, read-only false.
- Server reached `SERVING`.

## Runtime acceptance gate

The bundled test must create one confirmed receipt, save, restart, reconnect, verify `loaded:*:social:1`, private snapshot restoration, custom score restoration and no native relation change.
