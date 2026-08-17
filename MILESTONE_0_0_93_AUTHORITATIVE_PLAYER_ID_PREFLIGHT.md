# Milestone 0.0.93 - authoritative player ID preflight binding

A synchronized Coop player Hero can have an internal `Hero.StringId` that differs from authoritative `Player.HeroId`. Preflight now carries both: authorization compares the persisted statement against `Player.HeroId`, while campaign checks use the already-resolved canonical Hero object. Startup reconciliation remains on the Coop-first resolver introduced in 0.0.92. Native adapters remain default-off.

Source rollback: `backups\source-20260817-032157-pre-0.0.93`
