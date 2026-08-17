using Common.Messaging;
using ProtoBuf;

namespace AIPort.Protocol.Messages
{
    [ProtoContract(SkipConstructor = true)]
    public sealed class AIConversationAccepted : IEvent, IMessage
    {
        [ProtoMember(1)] public string RequestId { get; }
        [ProtoMember(2)] public string ConversationId { get; }
        [ProtoMember(3, IsRequired = true)] public int QueuePosition { get; }

        public AIConversationAccepted(string requestId, string conversationId, int queuePosition)
        {
            RequestId = requestId;
            ConversationId = conversationId;
            QueuePosition = queuePosition;
        }
    }
}
