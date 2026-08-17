# Hotfix 0.0.43 — campaign-object registry resolution

Updated: 2026-08-15 00:03 +05:00

## Runtime evidence from 0.0.42

The failed attempt at `23:06:47–23:07:18` was initially misclassified as a peer-resolution failure because the client received `player_unresolved`. Raw log-template comparison proves that classification was wrong:

- the resolver-failure branch logs the literal `ErrorCode=player_unresolved` plus `ResolveFailure=...`;
- the live lines used the validator template `ErrorCode={ErrorCode}`, rendering `ErrorCode="player_unresolved"` and no `ResolveFailure`;
- therefore `PlayerContextResolver` had already resolved the authoritative Coop `Player` successfully;
- `ConversationTargetValidator` then failed to turn `Player.HeroId` into a Bannerlord `Hero`.

The current connection generation and accepted controller were valid: peer 0 entered generation 2, controller `DESKTOP-ADLK0J9-wot_2` was accepted from `NetworkClientValidate`, handshake `0.0.42-dev` was compatible, and Coop logged `player entered the campaign` immediately before target binding.

## Root cause

`Player.HeroId` and hero target IDs identify **campaign objects**, but 0.0.42 queried them through the generic `Campaign.Current.ObjectManager.GetObject<Hero>()` registry. Bannerlord 1.4.7 exposes the canonical lookup as `Hero.Find(string)`. IL inspection confirms that `Hero.Find` delegates to `Campaign.Current.CampaignObjectManager.Find<Hero>(string)`.

The same registry mismatch existed in prompt hero resolution. Mobile-party lookups also used the generic object manager even though mobile parties are campaign objects.

## 0.0.43 change

- Authoritative player heroes now resolve through `Hero.Find(player.HeroId)`.
- Hero conversation targets and stale-target rechecks now resolve through `Hero.Find(targetId)`.
- Prompt hero resolution now uses `Hero.Find(heroId)`.
- Authoritative mobile parties now resolve through `Campaign.Current.CampaignObjectManager.Find<MobileParty>(player.MobilePartyId)`.
- Validator rejection logs now include the already-authoritative `PlayerHeroId` and `MobilePartyId`, while resolver failures keep their separate `ResolveFailure` diagnostic.
- Peer generation, accepted-controller identity, exact live-connection checks, leases, target-instance checks and stale/close protection are unchanged.
- `ClaimedPlayerHeroId` is still never used for authorization. There is no sole-player fallback and no `Hero.MainHero` substitution.

## Protocol and safety

- Build: `0.0.43-dev`.
- Protocol remains `1`.
- Request fields remain `1–9`; result fields remain `1–8`.
- Gameplay actions, diplomacy mutations, dynamic events and writable persistence remain disabled.
- API credentials remain environment-only.

## Verification

- New campaign-object resolution suite: `15/15`.
- All suites from 0.0.33 through 0.0.43: `12/12` suites passed.
- Runtime DLL: `123,904` bytes; SHA-256 `d8cdb53436d8708c5ca279f23c030f5a44de94e6dcd624a997a3f65caaf5cfcf`.
- Bootstrap unchanged: `10,240` bytes; SHA-256 `8357d5c56766dabba2777d472b87b1919a1925242b6d1f8a00bf99fd653934cf`.
- Source rollback: `backups\source-20260814-235539-pre-0.0.43`.

## Deployment status

Guardedly deployed with rollback `backups\m0-20260815-000420`. Server PID `12808` loaded 0.0.43-dev and reached CampaignReady/SERVING with matching client/server hashes. Runtime target-bind proof remains; the replacement process is fail-closed with `keyPresent=False`, so the first proof uses the safe stub.
