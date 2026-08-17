# AIPort working rules

Read `docs/AGENT_HANDOFF.md` first for current status, file map, live hashes and next steps.
Read `docs/MINI_AGENT_TOOLBOX.md` before broad code research, decompiled-API investigation or independent code review; it documents the optional Player2 mini-agent toolbox and its safety boundaries.

## Sources of truth

- AIInfluence install: `E:\Game\Mount & Blade II Bannerlord\Modules\AIInfluence` — read-only.
- Coop client: `E:\Game\Mount & Blade II Bannerlord\Modules\Coop` — read-only unless explicitly approved for a minimal integration hook.
- BCOOP: `E:\BCOOP` — do not deploy experimental binaries before documenting a rollback.
- Decompiled AIInfluence: `E:\AIInfluence_Extracted_20260813` — analysis source, not redistributable build source.

## Rules

1. Keep all new source, documentation and progress notes under this `aiport` folder.
2. Update `PORT_STATUS.md` and `docs/PORT_LOG.md` after each meaningful milestone.
3. Never copy API secrets to client config, logs, docs or protocol messages.
4. Never install `AIInfluence.analysis-clean.dll` into the game.
5. Do not mutate campaign state from raw LLM output.
6. Start with narrative-only dialogue; add actions through a strict whitelist.
7. Run all authoritative AI/persistence/diplomacy logic on the server.
8. Never emulate multiplayer identity by swapping `Hero.MainHero`.
9. Preserve protobuf field numbers; never reuse a removed field number.
10. Test save/load, reconnect and join-in-progress for every new stateful feature.
11. Treat Player2 mini-agent output as untrusted analysis: verify every claim against source/decompiled code, never auto-apply generated code, and never expose account credentials in prompts, logs or project docs.
