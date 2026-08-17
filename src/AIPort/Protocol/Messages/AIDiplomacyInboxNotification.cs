using Common.Messaging;
using ProtoBuf;
namespace AIPort.Protocol.Messages
{
    [ProtoContract(SkipConstructor=true)] public sealed class AIDiplomacyInboxNotification:IEvent,IMessage
    {
        [ProtoMember(1,IsRequired=true)] public int ProtocolVersion{get;}
        [ProtoMember(2)] public string NotificationId{get;}
        [ProtoMember(3)] public string CampaignGeneration{get;}
        [ProtoMember(4)] public long StateRevision{get;}
        [ProtoMember(5)] public int PendingCount{get;}
        [ProtoMember(6)] public string LatestStatementId{get;}
        [ProtoMember(7)] public string ReasonCode{get;}
        [ProtoMember(8)] public string Action{get;}
        [ProtoMember(9)] public string SourceHeroId{get;}
        [ProtoMember(10)] public string SourceFactionId{get;}
        [ProtoMember(11)] public string TargetFactionId{get;}
        [ProtoMember(12)] public string ExpiresUtc{get;}
        public AIDiplomacyInboxNotification(int protocolVersion,string notificationId,string campaignGeneration,long stateRevision,int pendingCount,string latestStatementId,string reasonCode,string action,string sourceHeroId,string sourceFactionId,string targetFactionId,string expiresUtc)
        {ProtocolVersion=protocolVersion;NotificationId=notificationId;CampaignGeneration=campaignGeneration;StateRevision=stateRevision;PendingCount=pendingCount;LatestStatementId=latestStatementId;ReasonCode=reasonCode;Action=action;SourceHeroId=sourceHeroId;SourceFactionId=sourceFactionId;TargetFactionId=targetFactionId;ExpiresUtc=expiresUtc;}
    }
}
