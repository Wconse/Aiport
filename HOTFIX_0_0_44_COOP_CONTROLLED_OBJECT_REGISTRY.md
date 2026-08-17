# Hotfix 0.0.44 — Coop controlled-object registry

Updated: 2026-08-15 00:42 +05:00

## Runtime result that disproved 0.0.43

The first live 0.0.43 attempt used peer 0, accepted controller `DESKTOP-ADLK0J9-wot_2`, compatible protocol/build, conversation `96f3fab0060543d3a40cd9c35a1a24b3`, and target `lord_5_13`. The validator still rejected binding with `PlayerHeroId="Hero_Player"`, `MobilePartyId="MobileParty_Player"`, and `ErrorCode="player_unresolved"`.

A live `coop.debug.players.list` command proved Coop itself resolves both IDs and marks both objects controlled:

- `Hero: Hero_Player resolved, controlled=True`
- `Party: MobileParty_Player resolved, controlled=True`
- `PlayerObjects entries (resolved & controlled): 3`

Therefore 0.0.43's use of Bannerlord `CampaignObjectManager` was still the wrong registry for dynamically synchronized Coop player objects.

## Canonical Coop path

Mono.Cecil inspection of `GameInterface.Services.Players.Commands.PlayerDebugCommands.AppendObject<T>` proves the command resolves each player object by:

1. `GameInterface.Services.ObjectManager.IObjectManager.TryGetObject<T>(id, out value)`
2. `GameInterface.Services.Players.IPlayerManager.Contains(value)`

This is the authoritative runtime path for `Hero_Player`, `MobileParty_Player`, and `Clan_Player`.

## 0.0.44 change

- The server handler receives Coop `IObjectManager` through dependency injection.
- `PlayerContextResolver.TryResolveControlledCampaignObjects` resolves `Player.HeroId` and `Player.MobilePartyId` through Coop `IObjectManager`.
- Both resolved instances must also pass `IPlayerManager.Contains`, preserving control ownership.
- `ConversationTargetValidator` receives the already-resolved authoritative `Hero` and `MobileParty`.
- Prompt construction receives the same authoritative player `Hero` instance.
- Native NPC targets remain server-resolved through Bannerlord campaign data.
- Peer generation, accepted controller, current live connection, target lease, target instance and stale/close protections are unchanged.
- No client hero claim, sole-player fallback or `Hero.MainHero` substitution was added.

## Compatibility and verification

- Build: `0.0.44-dev`; protocol remains `1`.
- Protobuf request fields remain `1–9`; result fields remain `1–8`.
- New controlled-object suite: `16/16`.
- All suites 0.0.33–0.0.44: `13/13`, zero failures.
- Runtime DLL: `125,952` bytes; SHA-256 `99ba6493e32e3cfe1427b6a264de564e5c477c3573949e175c55c9de7cbb2805`.
- Bootstrap: `10,240` bytes; SHA-256 `8357d5c56766dabba2777d472b87b1919a1925242b6d1f8a00bf99fd653934cf`.
- Source rollback: `backups\source-20260815-002255-pre-0.0.44`.
- Deployment rollback: `backups\m0-20260815-002728`.

## Successful live proof

Server PID `29964` loaded 0.0.44 and reached CampaignReady/SERVING. The user reconnected with compatible client build `0.0.44-dev` and completed one dialogue:

- join generation 1 and authoritative controller accepted;
- player entered the campaign at `00:41:13`;
- `target bound` at `00:41:35`: player `Hero_Player`, target `lord_5_1`, instance `hero:lord_5_1`, location `map:Player`;
- `conversation accepted` at `00:41:42` with authoritative party `MobileParty_Player`;
- result sent immediately: 94 characters, memory turns 1, `Stub=True`.

Conversation ID: `a23fa21c52624e27b38a5c03c9c86989`. Request ID: `1f7c0d6a9d41494e907625a8166b6709`. Log: `artifacts\runtime-m0\logs\coop-server-20260815-002737.log`.

This proves the target-bind and server request pipeline are fixed. Groq HTTP is not part of this proof because the replacement process remains fail-closed with `keyPresent=False`, `enabled=False`. The remaining step is an environment-only API-key restart followed by backend HTTP/result and non-stub memory verification.
