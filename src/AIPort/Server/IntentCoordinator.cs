using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
namespace AIPort.Server
{
    public sealed class IntentDecision { public string IntentId; public string Status; public string ReasonCode; public long StateRevision; public string TargetInstanceId; public int Delta; public bool MutationApplied; }
    public sealed class IntentAuditRecord { public DateTime Utc; public int PeerId; public string PlayerHeroId; public string RequestId; public string IntentId; public string IntentType; public string Status; public string ReasonCode; public long StateRevision; public string TargetInstanceId; public int Delta; public bool MutationApplied; }
    public sealed class RelationShadowPayload { public string ConversationId; public string TargetLeaseId; public string TargetInstanceId; public int Delta; public string Reason; }
    public sealed class RelationConfirmPayload { public string IntentId; public string ConversationId; public string TargetLeaseId; public string TargetInstanceId; public string Reason; }
    internal sealed class PendingRelationProposal { public string IntentId; public int PeerId; public string PlayerHeroId; public string CampaignGeneration; public long StateRevision; public string ConversationId; public string TargetLeaseId; public string TargetInstanceId; public int Delta; public DateTime ExpiresUtc; public bool Confirmed; }
    public sealed class IntentCoordinator
    {
        private const int MaximumRecords=1024;
        private static readonly TimeSpan ConfirmationLifetime=TimeSpan.FromSeconds(60);
        private static readonly Regex RelationShadowPattern=new Regex(@"\A\{""conversationId"":""([a-f0-9]{32})"",""targetLeaseId"":""([a-f0-9]{32})"",""targetInstanceId"":""([A-Za-z0-9_:.-]{1,320})"",""delta"":(-?[0-9]{1,2}),""reason"":""(manual_dialogue_probe)""\}\z",RegexOptions.CultureInvariant);
        private static readonly Regex RelationProposalPattern=new Regex(@"\A\{""conversationId"":""([a-f0-9]{32})"",""targetLeaseId"":""([a-f0-9]{32})"",""targetInstanceId"":""([A-Za-z0-9_:.-]{1,320})"",""delta"":(-?[0-9]{1,2}),""reason"":""(manual_dialogue_proposal)""\}\z",RegexOptions.CultureInvariant);
        private static readonly Regex RelationConfirmPattern=new Regex(@"\A\{""intentId"":""([a-f0-9]{32})"",""conversationId"":""([a-f0-9]{32})"",""targetLeaseId"":""([a-f0-9]{32})"",""targetInstanceId"":""([A-Za-z0-9_:.-]{1,320})"",""reason"":""(manual_dialogue_confirm)""\}\z",RegexOptions.CultureInvariant);
        private readonly object gate=new object();
        private readonly Dictionary<string,IntentDecision> decisions=new Dictionary<string,IntentDecision>(StringComparer.Ordinal);
        private readonly Queue<string> order=new Queue<string>();
        private readonly Queue<IntentAuditRecord> audit=new Queue<IntentAuditRecord>();
        private readonly Dictionary<string,PendingRelationProposal> pending=new Dictionary<string,PendingRelationProposal>(StringComparer.Ordinal);
        private readonly Queue<string> pendingOrder=new Queue<string>();

        public IntentDecision EvaluateNoMutation(int peerId,string playerHeroId,string requestId,string campaignGeneration,string currentGeneration,long expectedRevision,long currentRevision,string intentType,string payloadJson)
        {
            lock(gate){IntentDecision old;if(decisions.TryGetValue(requestId,out old))return old;string status="rejected",reason="unknown_intent";if(!SameGeneration(campaignGeneration,currentGeneration))reason="generation_mismatch";else if(IsStale(expectedRevision,currentRevision))reason="stale_revision";else if(string.Equals(intentType,"no_op",StringComparison.Ordinal)&&string.Equals((payloadJson??string.Empty).Trim(),"{}",StringComparison.Ordinal)){status="validated";reason="no_mutation";}return Store(peerId,playerHeroId,requestId,intentType,status,reason,currentRevision,string.Empty,0,false);}
        }

        public IntentDecision EvaluateRelationShadow(int peerId,string playerHeroId,string requestId,string campaignGeneration,string currentGeneration,long expectedRevision,long currentRevision,string payloadJson,ConversationTargetBinding binding)
        {
            lock(gate){IntentDecision old;if(decisions.TryGetValue(requestId,out old))return old;RelationShadowPayload parsed;string reason;if(!ValidateRelationEnvelope(playerHeroId,campaignGeneration,currentGeneration,expectedRevision,currentRevision,payloadJson,binding,RelationShadowPattern,out parsed,out reason))return Store(peerId,playerHeroId,requestId,"relation_change_shadow","rejected",reason,currentRevision,parsed==null?string.Empty:parsed.TargetInstanceId,parsed==null?0:parsed.Delta,false);return Store(peerId,playerHeroId,requestId,"relation_change_shadow","shadow_validated","mutation_suppressed",currentRevision,parsed.TargetInstanceId,parsed.Delta,false);}
        }

        public IntentDecision EvaluateRelationProposal(int peerId,string playerHeroId,string requestId,string campaignGeneration,string currentGeneration,long expectedRevision,long currentRevision,string payloadJson,ConversationTargetBinding binding)
        {
            lock(gate){IntentDecision old;if(decisions.TryGetValue(requestId,out old))return old;RelationShadowPayload parsed;string reason;if(!ValidateRelationEnvelope(playerHeroId,campaignGeneration,currentGeneration,expectedRevision,currentRevision,payloadJson,binding,RelationProposalPattern,out parsed,out reason))return Store(peerId,playerHeroId,requestId,"relation_change_proposal","rejected",reason,currentRevision,parsed==null?string.Empty:parsed.TargetInstanceId,parsed==null?0:parsed.Delta,false);IntentDecision d=Store(peerId,playerHeroId,requestId,"relation_change_proposal","confirmation_required","player_confirmation_required",currentRevision,parsed.TargetInstanceId,parsed.Delta,false);PendingRelationProposal p=new PendingRelationProposal{IntentId=d.IntentId,PeerId=peerId,PlayerHeroId=playerHeroId??string.Empty,CampaignGeneration=currentGeneration??string.Empty,StateRevision=currentRevision,ConversationId=parsed.ConversationId,TargetLeaseId=parsed.TargetLeaseId,TargetInstanceId=parsed.TargetInstanceId,Delta=parsed.Delta,ExpiresUtc=DateTime.UtcNow+ConfirmationLifetime};pending[d.IntentId]=p;pendingOrder.Enqueue(d.IntentId);TrimPending();return d;}
        }

        public IntentDecision EvaluateRelationConfirmation(int peerId,string playerHeroId,string requestId,string campaignGeneration,string currentGeneration,long expectedRevision,long currentRevision,string payloadJson,ConversationTargetBinding binding)
        {
            lock(gate){IntentDecision old;if(decisions.TryGetValue(requestId,out old))return old;RelationConfirmPayload parsed;string reason="invalid_payload",target=string.Empty;int delta=0;if(!SameGeneration(campaignGeneration,currentGeneration))reason="generation_mismatch";else if(IsStale(expectedRevision,currentRevision))reason="stale_revision";else if(!TryParseRelationConfirm(payloadJson,out parsed))reason="invalid_payload";else{PendingRelationProposal p;target=parsed.TargetInstanceId;if(!pending.TryGetValue(parsed.IntentId,out p))reason="proposal_not_found";else{delta=p.Delta;if(p.Confirmed)reason="proposal_already_confirmed";else if(DateTime.UtcNow>p.ExpiresUtc)reason="confirmation_expired";else if(p.PeerId!=peerId||!string.Equals(p.PlayerHeroId,playerHeroId,StringComparison.Ordinal))reason="not_authorized";else if(!string.Equals(p.CampaignGeneration,currentGeneration,StringComparison.Ordinal)||p.StateRevision!=currentRevision)reason="stale_proposal";else if(binding==null||!binding.IsHero)reason="hero_target_required";else if(!string.Equals(p.ConversationId,parsed.ConversationId,StringComparison.Ordinal)||!string.Equals(p.TargetLeaseId,parsed.TargetLeaseId,StringComparison.Ordinal)||!string.Equals(p.TargetInstanceId,parsed.TargetInstanceId,StringComparison.Ordinal))reason="confirmation_binding_mismatch";else if(!string.Equals(binding.ConversationId,p.ConversationId,StringComparison.Ordinal)||!string.Equals(binding.TargetLeaseId,p.TargetLeaseId,StringComparison.Ordinal)||!string.Equals(binding.TargetInstanceId,p.TargetInstanceId,StringComparison.Ordinal))reason="stale_target";else{p.Confirmed=true;return Store(peerId,playerHeroId,requestId,"relation_change_confirm","confirmed_shadow","mutation_suppressed",currentRevision,p.TargetInstanceId,p.Delta,false);}}}return Store(peerId,playerHeroId,requestId,"relation_change_confirm","rejected",reason,currentRevision,target,delta,false);}
        }

        private static bool ValidateRelationEnvelope(string playerHeroId,string campaignGeneration,string currentGeneration,long expectedRevision,long currentRevision,string payloadJson,ConversationTargetBinding binding,Regex pattern,out RelationShadowPayload parsed,out string reason)
        {
            parsed=null;reason="invalid_payload";if(!SameGeneration(campaignGeneration,currentGeneration)){reason="generation_mismatch";return false;}if(IsStale(expectedRevision,currentRevision)){reason="stale_revision";return false;}if(!TryParseRelation(payloadJson,pattern,out parsed)){reason="invalid_payload";return false;}if(binding==null){reason="target_not_bound";return false;}if(!binding.IsHero){reason="hero_target_required";return false;}if(!string.Equals(binding.PlayerHeroId,playerHeroId,StringComparison.Ordinal)){reason="player_mismatch";return false;}if(!string.Equals(binding.ConversationId,parsed.ConversationId,StringComparison.Ordinal)||!string.Equals(binding.TargetLeaseId,parsed.TargetLeaseId,StringComparison.Ordinal)){reason="stale_target";return false;}if(!string.Equals(binding.TargetInstanceId,parsed.TargetInstanceId,StringComparison.Ordinal)){reason="target_mismatch";return false;}if(parsed.Delta==0||parsed.Delta < -2||parsed.Delta > 2){reason="delta_out_of_range";return false;}return true;
        }

        public static bool TryParseRelationShadow(string payloadJson,out RelationShadowPayload payload){return TryParseRelation(payloadJson,RelationShadowPattern,out payload);}
        public static bool TryParseRelationProposal(string payloadJson,out RelationShadowPayload payload){return TryParseRelation(payloadJson,RelationProposalPattern,out payload);}
        private static bool TryParseRelation(string payloadJson,Regex pattern,out RelationShadowPayload payload){payload=null;string text=(payloadJson??string.Empty).Trim();if(text.Length>768)return false;Match m=pattern.Match(text);if(!m.Success)return false;int delta;if(!int.TryParse(m.Groups[4].Value,NumberStyles.AllowLeadingSign,CultureInfo.InvariantCulture,out delta))return false;payload=new RelationShadowPayload{ConversationId=m.Groups[1].Value,TargetLeaseId=m.Groups[2].Value,TargetInstanceId=m.Groups[3].Value,Delta=delta,Reason=m.Groups[5].Value};return true;}
        public static bool TryParseRelationConfirm(string payloadJson,out RelationConfirmPayload payload){payload=null;string text=(payloadJson??string.Empty).Trim();if(text.Length>768)return false;Match m=RelationConfirmPattern.Match(text);if(!m.Success)return false;payload=new RelationConfirmPayload{IntentId=m.Groups[1].Value,ConversationId=m.Groups[2].Value,TargetLeaseId=m.Groups[3].Value,TargetInstanceId=m.Groups[4].Value,Reason=m.Groups[5].Value};return true;}

        private IntentDecision Store(int peerId,string playerHeroId,string requestId,string intentType,string status,string reason,long revision,string target,int delta,bool mutationApplied)
        {
            IntentDecision d=new IntentDecision{IntentId=Guid.NewGuid().ToString("N"),Status=status,ReasonCode=reason,StateRevision=revision,TargetInstanceId=target??string.Empty,Delta=delta,MutationApplied=mutationApplied};decisions[requestId]=d;order.Enqueue(requestId);while(order.Count>MaximumRecords)decisions.Remove(order.Dequeue());audit.Enqueue(new IntentAuditRecord{Utc=DateTime.UtcNow,PeerId=peerId,PlayerHeroId=playerHeroId??string.Empty,RequestId=requestId,IntentId=d.IntentId,IntentType=intentType??string.Empty,Status=status,ReasonCode=reason,StateRevision=revision,TargetInstanceId=d.TargetInstanceId,Delta=delta,MutationApplied=mutationApplied});while(audit.Count>MaximumRecords)audit.Dequeue();return d;
        }
        private void TrimPending(){while(pendingOrder.Count>MaximumRecords)pending.Remove(pendingOrder.Dequeue());while(pendingOrder.Count>0){string id=pendingOrder.Peek();PendingRelationProposal p;if(!pending.TryGetValue(id,out p)){pendingOrder.Dequeue();continue;}if(DateTime.UtcNow<=p.ExpiresUtc)break;pending.Remove(id);pendingOrder.Dequeue();}}
        private static bool SameGeneration(string a,string b){return string.Equals(a??string.Empty,b??string.Empty,StringComparison.Ordinal);}
        private static bool IsStale(long expected,long current){return expected!=0&&expected!=current;}
        public int AuditCount{get{lock(gate)return audit.Count;}}
        public int PendingCount{get{lock(gate){TrimPending();return pending.Count;}}}
    }
}
