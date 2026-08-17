using System;

namespace AIPort.Server
{
    public sealed class PersistentDiplomaticStatementRecord
    {
        public string Id { get; set; }
        public string PlayerHeroId { get; set; }
        public string TargetHeroId { get; set; }
        public string SourceKingdomId { get; set; }
        public string TargetKingdomId { get; set; }
        public string Action { get; set; }
        public DateTime OccurredUtc { get; set; }
        public string Status { get; set; }
        public DateTime ExpiresUtc { get; set; }
        public DateTime ResolvedUtc { get; set; }
        public string ResolvedByHeroId { get; set; }
        public string LastReasonCode { get; set; }
        public DateTime NativeCommitUtc { get; set; }
        public string NativeCommittedByHeroId { get; set; }
        public bool NativeMutationApplied { get; set; }
        public string Origin { get; set; }
        public string InitiativeReasonCode { get; set; }
        public int InitiativeScore { get; set; }
        public long CampaignDay { get; set; }
        public long CampaignHour { get; set; }
    }
}
