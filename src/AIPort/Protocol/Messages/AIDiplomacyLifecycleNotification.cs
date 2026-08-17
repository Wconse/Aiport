using Common.Messaging;
using ProtoBuf;
namespace AIPort.Protocol.Messages
{
    [ProtoContract(SkipConstructor=true)] public sealed class AIDiplomacyLifecycleNotification:IEvent,IMessage
    {
        [ProtoMember(1,IsRequired=true)] public int ProtocolVersion{get;}
        [ProtoMember(2)] public string NotificationId{get;}
        [ProtoMember(3)] public string CampaignGeneration{get;}
        [ProtoMember(4)] public long StateRevision{get;}
        [ProtoMember(5)] public string StatementId{get;}
        [ProtoMember(6)] public string Status{get;}
        [ProtoMember(7)] public string Action{get;}
        [ProtoMember(8)] public string ReasonCode{get;}
        [ProtoMember(9)] public bool NativeMutationApplied{get;}
        public AIDiplomacyLifecycleNotification(int protocolVersion,string notificationId,string campaignGeneration,long stateRevision,string statementId,string status,string action,string reasonCode,bool nativeMutationApplied){ProtocolVersion=protocolVersion;NotificationId=notificationId;CampaignGeneration=campaignGeneration;StateRevision=stateRevision;StatementId=statementId;Status=status;Action=action;ReasonCode=reasonCode;NativeMutationApplied=nativeMutationApplied;}
    }
}
