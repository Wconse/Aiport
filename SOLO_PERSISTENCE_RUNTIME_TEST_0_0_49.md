# Solo persistence runtime test — 0.0.49

## Prepared baseline

- Disposable save only: `aiport-m0`.
- Baseline manifest generation: `f38452afdeecec27e1d29ae5bf77e2e7`.
- Baseline revision: 0.
- Baseline records: 0.
- Baseline memory SHA-256: `e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855`.
- Baseline artifact: `artifacts/runtime-m0/persistence-test-baseline.json`.

## Prepared tooling

- `tools/m0_server_command.py`: allowlisted `status`/`save` input to the disposable server console only. The save path was preflight-tested successfully.
- `tools/verify_0_0_49_persistence_runtime.py`: validates manifest JSON, memory SHA-256, record count, unique IDs, player scope, expected marker and expected target.
- Existing guarded stop/run scripts handle the full restart.

## Player phase A

1. Use one named hero and remember the displayed name.
2. Send exactly: `Запомни для проверки постоянной памяти: кодовая фраза — «медный грифон 9042». Просто подтверди.`
3. Wait for the complete NPC response.
4. Fully close the NPC conversation so the confirmed turn is archived.
5. Reopen the same hero and ask: `Какую кодовую фразу я просил тебя запомнить?`
6. Wait for the response, then fully close the conversation.
7. Report the hero name.

## Agent phase B

- Prove both turns committed and identify the exact target instance.
- Issue guarded disposable-server save.
- Verify manifest, record count, revision, marker, target and SHA-256.
- Stop the disposable server and restart it against `aiport-m0` with the same state root.
- Require `loaded:N`, writable state, matching generation/revision, `SnapshotReady`, and `NoOpValidated`.

## Player phase C

Reconnect, speak to the same hero and ask: `Мы разговаривали до перезапуска сервера. Какую кодовую фразу я просил тебя запомнить?`

Success requires the NPC to recall `медный грифон 9042`, with the server prompt containing the restored private record.

## Solo persistence save/restart proof — server phase passed (2026-08-15 03:03 +05:00)

- Player recorded marker `медный грифон 9042` with named hero Удрис Винодел, authoritative target `hero:CharacterObject_1649`.
- Two confirmed turns archived under `Hero_Player`; save completed as `saved:2`, revision 2.
- Manifest verification passed: generation `4ea97daf7c4e8ae14149a02cff988e72`, record count 2, unique record IDs, marker and target present, memory SHA-256 `d5caf900ba5cf36407bcf2b7ce4d18088397b8aa3841de8749e50b37c424c47e`.
- Disposable server was fully stopped and restarted. It loaded `0.0.49-dev`, Groq enabled, reached `SERVING`, and restored state as `loaded:2`, the same generation/revision, `ReadOnly=False`.
- Post-restart disk verification passed unchanged. Remaining runtime gate: reconnect and ask the same hero for the marker; then confirm restored history was used by the new Groq request.

## Solo persistent-memory runtime proof passed (2026-08-15 03:05 +05:00)

- After the full server restart, the client reconnected on `0.0.49-dev` / protocol 2.
- Server had restored `loaded:2`, revision 2, writable state. Post-join capability generation transitioned safely to `1b7043b8d7dff4d51981d03dccc9e9ed`; private snapshot was ready with 1,560 characters and revision 2.
- `SnapshotReady` hash: `5869709245a4667a827e6323e68ba38de157eef54074ed8946093c52c4c19d49`; correlated no-op validated with intent `3cddf11120454593a3da9082d4881d71`.
- The player reopened Удрис Винодел, resolving to the same `hero:CharacterObject_1649`, and asked for the pre-restart marker. Real Groq request `abc040bb648a4d9a9ceebfacd3e84b02` completed with `Stub=False`; the player confirmed the NPC recalled `медный грифон 9042`.
- This proves confirmed turn → archive → atomic save/hash → full process restart → `loaded:2` → private snapshot → same-target restored prompt history → real model response. The solo persistence gate is closed; two-client JIP/privacy remains deferred.
