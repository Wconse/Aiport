# Hotfix 0.0.98-dev — authoritative NPC diplomacy recipient

## Blocker

During the 0.0.97-dev client runtime gate the scheduler produced offers, but no notification reached the connected client.
Server log `artifacts\runtime-m0\logs\coop-server-20260817-060526.log` shows four created initiatives with
`RecipientHeroId="Player"` or `RecipientHeroId="main_hero"` and `RecipientOnline=False`, while the manual reject earlier in the
same session proved the authoritative connected hero id was `Hero_Player`. Revisions 11..15 remained shadow-only
(`NativeMutationApplied=false`), so no campaign state was mutated.

## Root cause

`CollectPlayerDiplomacyTargets()` enumerated `Hero.AllAliveHeroes` and accepted every hero matching
`IsPlayerControlledDiplomacyTarget` (`CharacterObject.IsPlayerCharacter`, or a `Hero_Player` / `Player` id prefix).
A coop campaign keeps stale player-looking duplicates, so the selector could pick a hero that no peer controls.
`NotifyRecipientInbox` then found no peer for that id and the durable offer stayed invisible to the online player.

## Fix

1. New pure server class `AIPort.Server.AuthoritativeDiplomacyRecipientFilter` (no TaleWorlds, LiteNetLib or logging types):
   - `AuthoritativeHeroIds` keeps hero ids bound to exactly one live connected peer, so ambiguity fails closed.
   - `IsAuthoritativeRecipient` stays permissive when nobody is connected, preserving the 0.0.95 offline queue.
   - `FilterRecipientHeroIds` intersects discovered candidates with authoritative ids and reports excluded aliases.
2. `AIPortConversationServerHandler.AuthoritativeConnectedHeroIds()` snapshots `connectedHeroIds` / `connectedPeers` under the existing lock.
3. `CollectPlayerDiplomacyTargets()` keeps its previous discovery pass, then filters to authoritative ids and requires a single connected peer per online target. Excluded aliases are logged.
4. A guard before `diplomaticStatements.TryRecord` rejects any non-authoritative recipient with `recipient_not_authoritative_online` without consuming daily budget.

## Regression

- `tools/harness_0_0_98.cs` (executable): authoritative mapping, duplicate `Hero_Player` / `Player` / `main_hero` exclusion, blank recipient rejection, disconnected peers, ambiguity fail-closed, two connected players, offline permissiveness, candidate dedup.
- `tools/test_0_0_98_authoritative_recipient.py`: 15 static checks (build string, unchanged capability flags, guard ordering before the durable record, pure filter, no `Hero.MainHero`, shadow-only initiative) plus harness execution.
- Cumulative `0.0.50..0.0.98`: 21/21 scripts PASS.

## Deployment

- `AIPort.dll` 345088 bytes, SHA-256 `c589cd0e3bb6ce610c543006bccaf46d97fde07d98ecd9bb6858fce68210a5f4`; Bootstrap unchanged `8357d5c56766dabba2777d472b87b1919a1925242b6d1f8a00bf99fd653934cf`.
- Client and server hold the identical hash; disposable server PID 11220, log `artifacts\runtime-m0\logs\coop-server-20260817-064457.log`.
- Startup smoke restored `loaded:3:social:5:diplomacy:2`, revision 10, `ReadOnly=False`, `CampaignReady`, `SERVING`.

## Rollback

- Sources: `backups\source-20260817-063750-pre-0.0.98`.
- Live binaries: `backups\m0-20260817-064443` (or `python tools\rollback_m0.py --apply`).

## Expected retest signature

- `AIPort NPC diplomacy initiative created ... RecipientHeroId="Hero_Player" RecipientOnline=True ... NativeMutationApplied=false`.
- Optional audit line `AIPort NPC diplomacy initiative excluded non-authoritative recipient aliases ... Excluded="Player,main_hero"`.
- No `RecipientOnline=False` line while a client is connected and no `NativeMutationApplied=true` anywhere.
