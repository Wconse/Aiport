using Common.Messaging;
using ProtoBuf;

namespace AIPort.Protocol.Messages
{
    [ProtoContract(SkipConstructor = true)]
    public sealed class AIConversationRequest : ICommand, IMessage
    {
        [ProtoMember(1, IsRequired = true)] public int ProtocolVersion { get; }
        [ProtoMember(2)] public string RequestId { get; }
        [ProtoMember(3)] public string ConversationId { get; }
        [ProtoMember(4)] public string ClaimedPlayerHeroId { get; }
        [ProtoMember(5)] public string NpcHeroId { get; }
        [ProtoMember(6)] public string PlayerText { get; }
        [ProtoMember(7, IsRequired = true)] public long ClientSequence { get; }
        // Added in 0.0.38. Existing protobuf numbers are unchanged.
        [ProtoMember(8)] public string TargetLeaseId { get; }
        [ProtoMember(9)] public string TargetInstanceId { get; }

        public string NpcTargetId { get { return NpcHeroId; } }

        public AIConversationRequest(int protocolVersion, string requestId, string conversationId, string claimedPlayerHeroId, string npcHeroId, string playerText, long clientSequence)
            : this(protocolVersion, requestId, conversationId, claimedPlayerHeroId, npcHeroId, playerText, clientSequence, string.Empty, string.Empty)
        {
        }

        public AIConversationRequest(int protocolVersion, string requestId, string conversationId, string claimedPlayerHeroId, string npcTargetId, string playerText, long clientSequence, string targetLeaseId, string targetInstanceId)
        {
            ProtocolVersion = protocolVersion;
            RequestId = requestId;
            ConversationId = conversationId;
            ClaimedPlayerHeroId = claimedPlayerHeroId;
            NpcHeroId = npcTargetId;
            PlayerText = playerText;
            ClientSequence = clientSequence;
            TargetLeaseId = targetLeaseId;
            TargetInstanceId = targetInstanceId;
        }
    }
}
