# Direct Player2 server provider

Updated: 2026-08-17 07:30 +05:00

## Purpose

AIPort `0.0.99-dev` can select either the existing OpenAI-compatible backend or a separate direct Player2 backend. It does not use the Player2 desktop application.

## Selection

Use a server-only config profile:

```json
{
  "enabled": true,
  "backend": "Player2",
  "model": "account-configured",
  "endpoint": "https://api.player2.game/v1/chat/completions",
  "player2TokensPath": "<absolute local token-list path>",
  "player2AccountsPath": "<absolute local account-list path>"
}
```

The configured endpoint cannot redirect Player2 bearer tokens. When `backend` is `Player2`, AIPort always uses the fixed chat endpoint regardless of the endpoint text in config.

## Credential inputs

- Token lines: `email<TAB>access_token`.
- Account lines: `email password`, `email:password`, `email = password`, or JSON with `email` and `password` strings.
- Duplicate entries merge case-insensitively by account identity.
- Token order is preserved and account-only entries are appended.
- Credentials are read only by the dedicated server process.
- Password re-auth uses the fixed process environment variable `AIPORT_PLAYER2_SUPABASE_ANON_KEY` and the fixed Supabase auth endpoint.
- Refreshed tokens remain in memory; AIPort does not rewrite the operator's token list.

Never paste or copy token/account contents into source, config, documentation, logs, protocol messages, client files or chat transcripts.

## Request behavior

- Chat endpoint: `https://api.player2.game/v1/chat/completions`.
- Chat request uses `messages`, `stream: false`, bounded `temperature` and `max_tokens`.
- Chat request deliberately omits `model`; model choice remains account-scoped on Player2.
- Redirects are disabled, keepalive is disabled, requests have unique connection groups, timeout/cancellation are bounded, and responses are limited to 1 MiB before display truncation.
- `401`: refresh from the matching local account when possible, retry once, then rotate.
- `402`: rotate to another supplied account.
- `429`: rotate/fail boundedly; no bypass or unbounded loop.
- Total account attempts are capped and concurrent requests lease separate available entries.

AIPort does not create accounts, claim rewards or automate quota circumvention.

## Disposable runtime launcher

`tools/run_m0_server_player2.py --apply` imports the already-local Player2 public auth client value into the child server environment without printing or persisting it. The backend itself remains direct HTTPS code and consumes the configured token/account lists.

## Verification status

- Clean source build: PASS.
- Player2 static/provider checks: 22/22 PASS.
- Dummy executable credential parser/merge harness: PASS.
- Real provider/runtime acceptance: pending the bundled one-connected-player `0.0.99` gate.
