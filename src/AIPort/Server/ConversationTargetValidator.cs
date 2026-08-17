using System;
using GameInterface.Services.Players.Data;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace AIPort.Server
{
    public sealed class ValidatedConversationTarget
    {
        public string TargetId { get; }
        public string TargetInstanceId { get; }
        public string AuthoritativeLocationId { get; }
        public bool IsHero { get; }

        public ValidatedConversationTarget(string targetId, string targetInstanceId, string authoritativeLocationId, bool isHero)
        {
            TargetId = targetId;
            TargetInstanceId = targetInstanceId;
            AuthoritativeLocationId = authoritativeLocationId;
            IsHero = isHero;
        }
    }

    public static class ConversationTargetValidator
    {
        private const float MaximumMapConversationDistance = 2.5f;

        public static bool TryValidate(Player player, Hero playerHero, MobileParty playerParty, string claimedTargetId, string clientTargetNonce, out ValidatedConversationTarget target, out string errorCode)
        {
            target = null;
            errorCode = "invalid_target";
            if (player == null || Campaign.Current == null || string.IsNullOrWhiteSpace(claimedTargetId)) return false;
            if (playerHero == null) { errorCode = "player_unresolved"; return false; }
            string targetId = claimedTargetId.Trim();

            Hero hero = Hero.Find(targetId);
            if (hero != null)
            {
                if (hero == playerHero || hero.CharacterObject == null || hero.CharacterObject.IsPlayerCharacter) return false;
                string locationId;
                if (!TryValidateHeroCoLocation(player, playerHero, playerParty, hero, out locationId))
                {
                    errorCode = "target_not_colocated";
                    return false;
                }
                target = new ValidatedConversationTarget(targetId, "hero:" + targetId, locationId, true);
                errorCode = string.Empty;
                return true;
            }

            CharacterObject character = Campaign.Current.ObjectManager.GetObject<CharacterObject>(targetId);
            if (character == null || character.IsHero || character.IsPlayerCharacter) return false;
            if (!IsCorrelationId(clientTargetNonce)) { errorCode = "invalid_target_nonce"; return false; }
            Settlement settlement = ResolvePlayerSettlement(playerHero, playerParty);
            if (settlement == null) { errorCode = "target_not_colocated"; return false; }
            string settlementId = settlement.StringId ?? string.Empty;
            target = new ValidatedConversationTarget(targetId, "regular:" + targetId + ":" + settlementId + ":" + clientTargetNonce.ToLowerInvariant(), settlementId, false);
            errorCode = string.Empty;
            return true;
        }

        public static bool IsStillEligible(Player player, Hero playerHero, MobileParty playerParty, ConversationTargetBinding binding)
        {
            if (player == null || playerHero == null || binding == null || Campaign.Current == null) return false;
            if (binding.IsHero)
            {
                Hero hero = Hero.Find(binding.TargetId);
                string locationId;
                return hero != null && TryValidateHeroCoLocation(player, playerHero, playerParty, hero, out locationId)
                    && string.Equals(locationId, binding.AuthoritativeLocationId, StringComparison.Ordinal);
            }
            CharacterObject character = Campaign.Current.ObjectManager.GetObject<CharacterObject>(binding.TargetId);
            Settlement settlement = ResolvePlayerSettlement(playerHero, playerParty);
            return character != null && !character.IsHero && settlement != null
                && string.Equals(settlement.StringId ?? string.Empty, binding.AuthoritativeLocationId, StringComparison.Ordinal);
        }

        private static bool TryValidateHeroCoLocation(Player player, Hero playerHero, MobileParty playerParty, Hero targetHero, out string locationId)
        {
            locationId = string.Empty;
            Settlement playerSettlement = ResolvePlayerSettlement(playerHero, playerParty);
            if (playerSettlement != null)
            {
                Settlement targetSettlement = targetHero.CurrentSettlement;
                if (targetSettlement == null && targetHero.PartyBelongedTo != null) targetSettlement = targetHero.PartyBelongedTo.CurrentSettlement;
                if (targetSettlement != playerSettlement) return false;
                locationId = playerSettlement.StringId ?? string.Empty;
                return true;
            }

            MobileParty targetParty = targetHero.PartyBelongedTo;
            if (playerParty == null || targetParty == null) return false;
            try
            {
                float distance = playerParty.Position.Distance(targetParty.Position);
                if (distance > MaximumMapConversationDistance) return false;
            }
            catch { return false; }
            locationId = "map:" + (playerParty.StringId ?? string.Empty);
            return true;
        }

        private static Settlement ResolvePlayerSettlement(Hero playerHero, MobileParty playerParty)
        {
            if (playerHero.CurrentSettlement != null) return playerHero.CurrentSettlement;
            if (playerParty != null && playerParty.CurrentSettlement != null) return playerParty.CurrentSettlement;
            return null;
        }

        private static bool IsCorrelationId(string value)
        {
            if (string.IsNullOrWhiteSpace(value) || value.Length != 32) return false;
            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];
                if (!((c >= '0' && c <= '9') || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F'))) return false;
            }
            return true;
        }
    }
}
