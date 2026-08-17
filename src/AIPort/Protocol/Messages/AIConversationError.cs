using Common.Messaging;
using ProtoBuf;

namespace AIPort.Protocol.Messages
{
    [ProtoContract(SkipConstructor = true)]
    public sealed class AIConversationError : IEvent, IMessage
    {
        [ProtoMember(1)] public string RequestId { get; }
        [ProtoMember(2)] public string ErrorCode { get; }
        [ProtoMember(3)] public string SafeMessage { get; }
        [ProtoMember(4, IsRequired = true)] public bool Retryable { get; }
        [ProtoMember(5, IsRequired = true)] public int RetryAfterMilliseconds { get; }

        public AIConversationError(string requestId, string errorCode, string safeMessage, bool retryable)
            : this(requestId, errorCode, safeMessage, retryable, 0)
        {
        }

        public AIConversationError(string requestId, string errorCode, string safeMessage, bool retryable, int retryAfterMilliseconds)
        {
            RequestId = requestId;
            ErrorCode = errorCode;
            SafeMessage = safeMessage;
            Retryable = retryable;
            RetryAfterMilliseconds = retryAfterMilliseconds;
        }
    }
}
