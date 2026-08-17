using Common.Messaging;
using ProtoBuf;

namespace AIPort.Protocol.Messages
{
    [ProtoContract(SkipConstructor = true)]
    public sealed class AIPortHandshakeRequest : ICommand, IMessage
    {
        [ProtoMember(1, IsRequired = true)]
        public int ProtocolVersion { get; }

        [ProtoMember(2)]
        public string RequestId { get; }

        [ProtoMember(3)]
        public string ClientBuild { get; }

        public AIPortHandshakeRequest(int protocolVersion, string requestId, string clientBuild)
        {
            ProtocolVersion = protocolVersion;
            RequestId = requestId;
            ClientBuild = clientBuild;
        }
    }
}
