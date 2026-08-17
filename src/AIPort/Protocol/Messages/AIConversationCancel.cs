using Common.Messaging;
using ProtoBuf;

namespace AIPort.Protocol.Messages
{
    [ProtoContract(SkipConstructor = true)]
    public sealed class AIConversationCancel : ICommand, IMessage
    {
        [ProtoMember(1)] public string RequestId { get; }
        [ProtoMember(2)] public string ConversationId { get; }

        public AIConversationCancel(string requestId, string conversationId)
        {
            RequestId = requestId;
            ConversationId = conversationId;
        }
    }
}
