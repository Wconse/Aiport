using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Conversation;

namespace AIPort.Server
{
    public static class ConversationTargetResolver
    {
        public static string CurrentNpcTargetId()
        {
            if (Campaign.Current == null) return string.Empty;
            ConversationManager manager = Campaign.Current.ConversationManager;
            CharacterObject character = manager == null ? null : manager.OneToOneConversationCharacter;
            if (character == null || character.IsPlayerCharacter) return string.Empty;
            if (character.IsHero && character.HeroObject != null)
            {
                return character.HeroObject.StringId ?? string.Empty;
            }
            return character.StringId ?? string.Empty;
        }

        // Kept for binary/source compatibility with older harnesses.
        public static string CurrentNpcHeroId()
        {
            return CurrentNpcTargetId();
        }
    }
}
