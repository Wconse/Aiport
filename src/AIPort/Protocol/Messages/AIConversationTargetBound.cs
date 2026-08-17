using Common.Messaging;
using ProtoBuf;

namespace AIPort.Protocol.Messages
{
    [ProtoContract(SkipConstructor = true)]
    public sealed class AIConversationTargetBound : IEvent, IMessage
    {
        [ProtoMember(1)] public string ConversationId { get; }
        [ProtoMember(2)] public string TargetLeaseId { get; }
        [ProtoMember(3)] public string TargetId { get; }
        [ProtoMember(4)] public string TargetInstanceId { get; }
        [ProtoMember(5, IsRequired = true)] public bool Accepted { get; }
        [ProtoMember(6)] public string ErrorCode { get; }

        public AIConversationTargetBound(string conversationId, string targetLeaseId, string targetId, string targetInstanceId, bool accepted, string errorCode)
        {
            ConversationId = conversationId;
            TargetLeaseId = targetLeaseId;
            TargetId = targetId;
            TargetInstanceId = targetInstanceId;
            Accepted = accepted;
            ErrorCode = errorCode;
        }
    }
}
