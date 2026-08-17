using Common.Messaging;
using ProtoBuf;

namespace AIPort.Protocol.Messages
{
    [ProtoContract(SkipConstructor = true)]
    public sealed class AIConversationTargetClose : ICommand, IMessage
    {
        [ProtoMember(1)] public string ConversationId { get; }
        [ProtoMember(2)] public string TargetLeaseId { get; }

        public AIConversationTargetClose(string conversationId, string targetLeaseId)
        {
            ConversationId = conversationId;
            TargetLeaseId = targetLeaseId;
        }
    }
}
