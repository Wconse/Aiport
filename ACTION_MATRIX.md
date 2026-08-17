# AI action compatibility matrix

## Candidate after runtime verification

| Action/state | Coop evidence | Initial policy |
|---|---|---|
| War | `NetworkDeclareWar`, stance handlers | Allowed later through adapter |
| Peace | `NetworkMakePeace`, stance handlers | Allowed later through adapter |
| Hero gold | AutoSync `Hero_Gold` | Capped, audited |
| Clan influence | AutoSync `Clan__influence` | Capped, audited |
| Member/prison roster | Coop roster handlers + AutoSync | Atomic transfer only |
| Party inventory | Coop item roster handlers + AutoSync | Atomic delta only |
| Hero state | Hero handlers/AutoSync | Restricted transitions |
| Settlement enter/leave | Dedicated Coop handlers | Test before allowing |

## Custom AIPort projection required

- alliances;
- trade agreements;
- reparations;
- custom tribute schedules/history;
- dynamic events;
- diseases/quarantine;
- RP items;
- NPC memories and Memory Book;
- pending player/kingdom statements.

## Disabled for MVP

- `SetMove*`, follow/patrol/go-to-settlement;
- create/destroy party;
- raid;
- siege;
- map event/battle start;
- kill/death;
- marriage/family spawning;
- quest creation/completion;
- mission tactics and settlement combat;
- TTS/STT/image generation as authoritative behavior.

## Required pipeline

`LLM text -> strict parser -> schema validation -> ID resolution -> whitelist -> authorization -> preconditions -> game-thread execution -> audit -> response`.

Raw `AIActionManager.ParseAndExecuteCommand` must never be exposed to network input or direct LLM output.

## 0.0.38 execution gate

`AIActionGate` now supplies typed proposals and a single authorization boundary. It is deliberately hard-disabled with `narrative_only`; no parser output can reach a campaign mutation. Enabling any action later requires an authoritative target lease, explicit per-action whitelist, authorization, preconditions, game-thread adapter and audit record.
