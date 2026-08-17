using Common.Messaging;
using ProtoBuf;
namespace AIPort.Protocol.Messages
{
    [ProtoContract(SkipConstructor=true)] public sealed class AIDiplomacySnapshotResponse:IEvent,IMessage
    {
        [ProtoMember(1)] public string RequestId{get;}
        [ProtoMember(2,IsRequired=true)] public bool Accepted{get;}
        [ProtoMember(3)] public string DisplayText{get;}
        [ProtoMember(4)] public string ReasonCode{get;}
        [ProtoMember(5)] public long StateRevision{get;}
        public AIDiplomacySnapshotResponse(string requestId,bool accepted,string displayText,string reasonCode,long stateRevision){RequestId=requestId;Accepted=accepted;DisplayText=displayText;ReasonCode=reasonCode;StateRevision=stateRevision;}
    }
}
