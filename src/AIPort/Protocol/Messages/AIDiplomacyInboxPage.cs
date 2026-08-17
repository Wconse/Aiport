using Common.Messaging;
using ProtoBuf;

namespace AIPort.Protocol.Messages
{
    [ProtoContract(SkipConstructor = true)]
    public sealed class AIDiplomacyInboxEntry
    {
        [ProtoMember(1)] public string StatementId { get; }
        [ProtoMember(2)] public string Action { get; }
        [ProtoMember(3)] public string SourceHeroId { get; }
        [ProtoMember(4)] public string SourceHeroName { get; }
        [ProtoMember(5)] public string SourceFactionId { get; }
        [ProtoMember(6)] public string SourceFactionName { get; }
        [ProtoMember(7)] public string TargetFactionId { get; }
        [ProtoMember(8)] public string TargetFactionName { get; }
        [ProtoMember(9)] public string OccurredUtc { get; }
        [ProtoMember(10)] public string ExpiresUtc { get; }
        [ProtoMember(11)] public string Origin { get; }
        [ProtoMember(12)] public string ReasonCode { get; }
        [ProtoMember(13)] public int Score { get; }
        [ProtoMember(14)] public string TargetHeroId { get; }

        public AIDiplomacyInboxEntry(string statementId, string action, string sourceHeroId, string sourceHeroName,
            string sourceFactionId, string sourceFactionName, string targetFactionId, string targetFactionName,
            string occurredUtc, string expiresUtc, string origin, string reasonCode, int score, string targetHeroId)
        {
            StatementId = statementId ?? string.Empty;
            Action = action ?? string.Empty;
            SourceHeroId = sourceHeroId ?? string.Empty;
            SourceHeroName = sourceHeroName ?? string.Empty;
            SourceFactionId = sourceFactionId ?? string.Empty;
            SourceFactionName = sourceFactionName ?? string.Empty;
            TargetFactionId = targetFactionId ?? string.Empty;
            TargetFactionName = targetFactionName ?? string.Empty;
            OccurredUtc = occurredUtc ?? string.Empty;
            ExpiresUtc = expiresUtc ?? string.Empty;
            Origin = origin ?? string.Empty;
            ReasonCode = reasonCode ?? string.Empty;
            Score = score;
            TargetHeroId = targetHeroId ?? string.Empty;
        }
    }

    [ProtoContract(SkipConstructor = true)]
    public sealed class AIDiplomacyInboxPageRequest : ICommand, IMessage
    {
        [ProtoMember(1, IsRequired = true)] public int ProtocolVersion { get; }
        [ProtoMember(2)] public string RequestId { get; }
        [ProtoMember(3)] public string CampaignGeneration { get; }
        [ProtoMember(4)] public long ExpectedStateRevision { get; }
        [ProtoMember(5)] public string AfterStatementId { get; }
        [ProtoMember(6)] public int PageSize { get; }

        public AIDiplomacyInboxPageRequest(int protocolVersion, string requestId, string campaignGeneration,
            long expectedStateRevision, string afterStatementId, int pageSize)
        {
            ProtocolVersion = protocolVersion;
            RequestId = requestId ?? string.Empty;
            CampaignGeneration = campaignGeneration ?? string.Empty;
            ExpectedStateRevision = expectedStateRevision;
            AfterStatementId = afterStatementId ?? string.Empty;
            PageSize = pageSize;
        }
    }

    [ProtoContract(SkipConstructor = true)]
    public sealed class AIDiplomacyInboxPageResponse : IEvent, IMessage
    {
        [ProtoMember(1, IsRequired = true)] public int ProtocolVersion { get; }
        [ProtoMember(2)] public string RequestId { get; }
        [ProtoMember(3)] public bool Accepted { get; }
        [ProtoMember(4)] public string CampaignGeneration { get; }
        [ProtoMember(5)] public long StateRevision { get; }
        [ProtoMember(6)] public int TotalCount { get; }
        [ProtoMember(7)] public AIDiplomacyInboxEntry[] Entries { get; }
        [ProtoMember(8)] public string NextCursor { get; }
        [ProtoMember(9)] public bool HasMore { get; }
        [ProtoMember(10)] public string ReasonCode { get; }

        public AIDiplomacyInboxPageResponse(int protocolVersion, string requestId, bool accepted,
            string campaignGeneration, long stateRevision, int totalCount, AIDiplomacyInboxEntry[] entries,
            string nextCursor, bool hasMore, string reasonCode)
        {
            ProtocolVersion = protocolVersion;
            RequestId = requestId ?? string.Empty;
            Accepted = accepted;
            CampaignGeneration = campaignGeneration ?? string.Empty;
            StateRevision = stateRevision;
            TotalCount = totalCount;
            Entries = entries ?? new AIDiplomacyInboxEntry[0];
            NextCursor = nextCursor ?? string.Empty;
            HasMore = hasMore;
            ReasonCode = reasonCode ?? string.Empty;
        }
    }
}
