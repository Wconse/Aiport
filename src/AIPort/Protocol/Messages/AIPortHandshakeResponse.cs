using Common.Messaging;
using ProtoBuf;

namespace AIPort.Protocol.Messages
{
    [ProtoContract(SkipConstructor = true)]
    public sealed class AIPortHandshakeResponse : IEvent, IMessage
    {
        [ProtoMember(1, IsRequired = true)]
        public int ProtocolVersion { get; }

        [ProtoMember(2)]
        public string RequestId { get; }

        [ProtoMember(3)]
        public bool Compatible { get; }

        [ProtoMember(4)]
        public string ServerBuild { get; }

        [ProtoMember(5)]
        public string Message { get; }

        public AIPortHandshakeResponse(int protocolVersion, string requestId, bool compatible, string serverBuild, string message)
        {
            ProtocolVersion = protocolVersion;
            RequestId = requestId;
            Compatible = compatible;
            ServerBuild = serverBuild;
            Message = message;
        }
    }
}
