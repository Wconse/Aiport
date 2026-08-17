using Common.Messaging;
using ProtoBuf;
namespace AIPort.Protocol.Messages
{
    [ProtoContract(SkipConstructor=true)] public sealed class AIPortValidationGateRequest:ICommand,IMessage
    {
        [ProtoMember(1,IsRequired=true)] public int ProtocolVersion{get;}
        [ProtoMember(2)] public string RequestId{get;}
        [ProtoMember(3)] public string CampaignGeneration{get;}
        [ProtoMember(4)] public long ExpectedStateRevision{get;}
        [ProtoMember(5)] public string Mode{get;}
        public AIPortValidationGateRequest(int protocolVersion,string requestId,string campaignGeneration,long expectedStateRevision,string mode){ProtocolVersion=protocolVersion;RequestId=requestId;CampaignGeneration=campaignGeneration;ExpectedStateRevision=expectedStateRevision;Mode=mode;}
    }
}
