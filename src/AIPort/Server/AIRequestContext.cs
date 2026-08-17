using System.Threading;

namespace AIPort.Server
{
    // TaleWorlds objects will be added after the authoritative Coop player resolver is proven.
    public sealed class AIRequestContext
    {
        public string RequestId { get; }
        public string ControllerId { get; }
        public string PlayerHeroId { get; }
        public string PlayerPartyId { get; }
        public string NpcHeroId { get; }
        public string ConversationId { get; }
        public CancellationToken CancellationToken { get; }

        public AIRequestContext(string requestId, string controllerId, string playerHeroId, string playerPartyId, string npcHeroId, string conversationId, CancellationToken cancellationToken)
        {
            RequestId = requestId;
            ControllerId = controllerId;
            PlayerHeroId = playerHeroId;
            PlayerPartyId = playerPartyId;
            NpcHeroId = npcHeroId;
            ConversationId = conversationId;
            CancellationToken = cancellationToken;
        }
    }
}
