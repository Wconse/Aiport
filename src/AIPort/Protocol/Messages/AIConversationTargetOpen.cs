using Common.Messaging;
using ProtoBuf;

namespace AIPort.Protocol.Messages
{
    [ProtoContract(SkipConstructor = true)]
    public sealed class AIConversationTargetOpen : ICommand, IMessage
    {
        [ProtoMember(1, IsRequired = true)] public int ProtocolVersion { get; }
        [ProtoMember(2)] public string ConversationId { get; }
        [ProtoMember(3)] public string ClaimedTargetId { get; }
        [ProtoMember(4)] public string ClientTargetNonce { get; }

        public AIConversationTargetOpen(int protocolVersion, string conversationId, string claimedTargetId, string clientTargetNonce)
        {
            ProtocolVersion = protocolVersion;
            ConversationId = conversationId;
            ClaimedTargetId = claimedTargetId;
            ClientTargetNonce = clientTargetNonce;
        }
    }
}
