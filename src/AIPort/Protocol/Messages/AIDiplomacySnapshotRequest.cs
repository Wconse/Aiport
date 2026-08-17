using Common.Messaging;
using ProtoBuf;
namespace AIPort.Protocol.Messages
{
    [ProtoContract(SkipConstructor=true)] public sealed class AIDiplomacySnapshotRequest:ICommand,IMessage
    {
        [ProtoMember(1,IsRequired=true)] public int ProtocolVersion{get;}
        [ProtoMember(2)] public string RequestId{get;}
        [ProtoMember(3)] public string CampaignGeneration{get;}
        [ProtoMember(4)] public long ExpectedStateRevision{get;}
        public AIDiplomacySnapshotRequest(int protocolVersion,string requestId,string campaignGeneration,long expectedStateRevision){ProtocolVersion=protocolVersion;RequestId=requestId;CampaignGeneration=campaignGeneration;ExpectedStateRevision=expectedStateRevision;}
    }
}
