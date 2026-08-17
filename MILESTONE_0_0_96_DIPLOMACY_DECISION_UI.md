# Milestone 0.0.96 — transient diplomacy decision UI

Date: 2026-08-17  
Status: built and regression-tested; not deployed; runtime UI gate deferred for bundling.

## Goal

Let the authoritative recipient inspect and accept/reject an incoming durable NPC-to-player war/peace proposal without entering a conversation. Keep the feature private, reconnect-safe and shadow-only by default.

## Protocol

Protocol remains `2`. `AIDiplomacyInboxNotification` preserves fields `1..7` and appends:

| Field | Value |
|---:|---|
| 8 | action (`war` or `peace`) |
| 9 | source hero ID |
| 10 | source faction ID |
| 11 | target faction ID |
| 12 | expiry UTC, round-trip format |

New capability: `CapabilityDiplomacyDecisionUi = 262144`. Full cumulative flags: `524287`.

Older peers without the capability retain the text fallback. The existing protocol number is unchanged because the fields are optional append-only metadata and behavior is capability-negotiated.

## Server behavior

`NotifyPeerInbox` still derives the recipient from the server's authoritative online registry and private ledger projection. It now resolves the latest pending statement using `DiplomaticStatementLedger.TryGet` and attaches only metadata from that already-authorized record.

The decision request payload contains only statement ID, decision and audit reason. Server-side recipient processing remains responsible for:

- peer -> authoritative hero identity;
- campaign generation;
- unified revision;
- exact target recipient;
- pending status and expiry;
- faction authority and current war/peace precondition;
- idempotent durable lifecycle transition.

The map UI path does not carry or require a conversation lease because recipient authority is independent of an NPC dialogue.

## Client behavior

`AIPortDiplomacyMapNotification` is a custom `InformationData` type. Its registrar:

- subscribes to `ScreenManager.OnPushScreen`;
- registers data/item types with `MapScreen.MapNotificationView`;
- also registers the current map screen if already present;
- publishes through `MBInformationManager.AddNotice`.

It intentionally does not call `CampaignInformationManager.NewMapNoticeAdded`; the notice is transient and not serialized into the campaign save. Reconnect/JIP restores awareness through the private snapshot notification path.

The item VM uses presentation identifier `ransom` and sound `event:/ui/notification/peace_offer`, but does not reuse vanilla peace-offer behavior. Inspect opens a prioritized, non-pausing `InquiryData` with Accept/Reject actions routed through `AIPortDiplomacyDecisionBridge`.

Successful local submission removes the clicked notice. Authoritative lifecycle events remove any matching notice again, safely. Canceling the inquiry leaves the notice intact. Disconnect removes all stale transient notices. If the map view cannot accept the custom notice, the existing `InformationMessage` is shown instead.

## Explicitly excluded

- No `CampaignEventDispatcher.OnPeaceOfferedToPlayer`.
- No `MakePeaceKingdomDecision`.
- No automatic `DeclareWarAction` or `MakePeaceAction`.
- No custom save type.
- No deployment, server restart or client launch in this milestone's static phase.

The isolated native adapters still exist elsewhere, default-off behind their independent configuration, environment, generation, preflight and commit-token gates.

## Files

- `src\AIPort\Protocol\AIPortProtocol.cs`
- `src\AIPort\Protocol\Messages\AIDiplomacyInboxNotification.cs`
- `src\AIPort\CoopIntegration\Server\AIPortConversationServerHandler.cs`
- `src\AIPort\CoopIntegration\Client\AIPortHandshakeClientHandler.cs`
- `src\AIPort\CoopIntegration\Client\AIPortConversationClientHandler.cs`
- `src\AIPort\CoopIntegration\Client\AIPortDiplomacyMapNotification.cs`
- `src\AIPort\AIPort.csproj`
- `tools\build.py`
- `tools\test_0_0_96_diplomacy_decision_ui.py`

## Verification

- New static suite: 13/13 PASS.
- Current cumulative scripts `0.0.50..0.0.96`: 19/19 PASS.
- Roslyn build: PASS.
- `AIPort.dll`: 311808 bytes.
- SHA-256: `a8275534e82d313384eaba6adb0535b8d362dfd271f7d3073c08947c85ab06bb`.
- Bootstrap SHA-256: `8357d5c56766dabba2777d472b87b1919a1925242b6d1f8a00bf99fd653934cf`.
- Source rollback: `backups\source-20260817-044652-pre-0.0.96`.

## Deferred bundled runtime gate

Use disposable `aiport-m0`; never `saveauto1`.

1. Preserve the current state and create a deployment rollback.
2. Deploy the exact candidate DLL to both client and dedicated server.
3. Confirm `0.0.96-dev`, protocol `2`, capability flags `524287` and hash parity.
4. Reconnect the authoritative recipient and receive the pending map notice after private snapshot completion.
5. Inspect the notice and confirm correct source/factions/action/expiry/statement.
6. Accept or reject without opening an NPC conversation.
7. Confirm the server derives the correct recipient, persists the lifecycle transition and reports `NativeMutationApplied=false`.
8. Confirm the notice disappears, reconnect does not restore resolved work, and save/restart/JIP preserves the result.
9. Confirm canceling an inquiry leaves an unresolved notice available.

Bundle this session with the NPC-initiative scheduler and full pending-inbox list so the user does not need to launch the game for a cosmetic-only test.
