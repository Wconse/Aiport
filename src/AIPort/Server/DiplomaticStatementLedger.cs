using System;
using System.Collections.Generic;
using System.Text;
namespace AIPort.Server
{
    public sealed class DiplomaticStatementLedger
    {
        private const int MaximumRecords=256,MaximumPendingPerHero=16;
        private static readonly TimeSpan Cooldown=TimeSpan.FromSeconds(30),NegotiationLifetime=TimeSpan.FromHours(24);
        private readonly object gate=new object();
        private readonly Dictionary<string,PersistentDiplomaticStatementRecord> byId=new Dictionary<string,PersistentDiplomaticStatementRecord>(StringComparer.Ordinal);
        private readonly Queue<string> order=new Queue<string>();
        private readonly Dictionary<string,DateTime> lastApplied=new Dictionary<string,DateTime>(StringComparer.Ordinal);
        public int Count{get{lock(gate)return byId.Count;}}

        public bool TryRecord(string id,string playerHeroId,string targetHeroId,string sourceKingdomId,string targetKingdomId,string action,DateTime utc,out PersistentDiplomaticStatementRecord record,out string reason,out bool fresh)
        {
            return TryRecord(id,playerHeroId,targetHeroId,sourceKingdomId,targetKingdomId,action,utc,"manual_player","manual_diplomacy_proposal",0,-1,-1,out record,out reason,out fresh);
        }

        public bool TryRecord(string id,string playerHeroId,string targetHeroId,string sourceKingdomId,string targetKingdomId,string action,DateTime utc,string origin,string initiativeReason,int initiativeScore,long campaignDay,long campaignHour,out PersistentDiplomaticStatementRecord record,out string reason,out bool fresh)
        {
            lock(gate)
            {
                record=null;reason="invalid_diplomatic_statement";fresh=false;PersistentDiplomaticStatementRecord old;
                if(byId.TryGetValue(id??string.Empty,out old)){record=old;reason="idempotent_replay";return true;}
                string player=Bound(playerHeroId,160),targetHero=Bound(targetHeroId,160),source=Bound(sourceKingdomId,160),target=Bound(targetKingdomId,160),kind=(action??string.Empty).Trim().ToLowerInvariant();
                string safeOrigin=Bound(origin,32),safeInitiativeReason=Bound(initiativeReason,64);
                if(!IsId(id)||player.Length==0||targetHero.Length==0||source.Length==0||target.Length==0||source==target||(kind!="war"&&kind!="peace"))return false;
                if(safeOrigin.Length==0)safeOrigin="manual_player";
                DateTime now=Utc(utc);string pair=PairKey(source,target);int playerPending=0,targetPending=0;
                foreach(string existingId in order){PersistentDiplomaticStatementRecord existing;if(!byId.TryGetValue(existingId,out existing)||!IsActive(existing,now))continue;if(PairKey(existing.SourceKingdomId,existing.TargetKingdomId)==pair){reason="diplomacy_pair_pending";return false;}if(Same(existing.PlayerHeroId,player))playerPending++;if(Same(existing.TargetHeroId,targetHero))targetPending++;}
                if(playerPending>=MaximumPendingPerHero||targetPending>=MaximumPendingPerHero){reason="diplomacy_pending_limit";return false;}
                string key=source+"\u001f"+target+"\u001f"+kind;DateTime last;if(lastApplied.TryGetValue(key,out last)&&now-last<Cooldown){reason="diplomacy_cooldown";return false;}
                record=new PersistentDiplomaticStatementRecord{Id=id,PlayerHeroId=player,TargetHeroId=targetHero,SourceKingdomId=source,TargetKingdomId=target,Action=kind,OccurredUtc=now,Status="pending_recipient",ExpiresUtc=now+NegotiationLifetime,ResolvedUtc=DateTime.MinValue,ResolvedByHeroId=string.Empty,LastReasonCode="recipient_consent_required",NativeCommitUtc=DateTime.MinValue,NativeCommittedByHeroId=string.Empty,NativeMutationApplied=false,Origin=safeOrigin,InitiativeReasonCode=safeInitiativeReason,InitiativeScore=Math.Max(-1000,Math.Min(1000,initiativeScore)),CampaignDay=campaignDay,CampaignHour=campaignHour};
                byId[id]=record;order.Enqueue(id);lastApplied[key]=now;fresh=true;reason="recipient_consent_required";while(order.Count>MaximumRecords)byId.Remove(order.Dequeue());return true;
            }
        }
        public bool TryGet(string id,out PersistentDiplomaticStatementRecord record){lock(gate)return byId.TryGetValue(id??string.Empty,out record);}
        public int ExpireDue(DateTime utc){return ExpireDue(utc,null);}
        public int ExpireDue(DateTime utc,IList<PersistentDiplomaticStatementRecord> expiredRecords)
        {
            lock(gate){DateTime now=Utc(utc);int changed=0;foreach(PersistentDiplomaticStatementRecord r in byId.Values){if(r==null||!Same(r.Status,"pending_recipient"))continue;if(r.ExpiresUtc!=DateTime.MinValue&&now>r.ExpiresUtc){r.Status="expired";r.ResolvedUtc=now;r.ResolvedByHeroId=string.Empty;r.LastReasonCode="negotiation_expired";changed++;if(expiredRecords!=null)expiredRecords.Add(r);}}return changed;}
        }
        public bool IsIdempotentResolution(string id,string recipientHeroId,string decision,out PersistentDiplomaticStatementRecord record)
        {lock(gate){record=null;PersistentDiplomaticStatementRecord r;if(!byId.TryGetValue(id??string.Empty,out r)||!Same(r.TargetHeroId,recipientHeroId))return false;string expected=NormalizeDecision(decision)=="accept"?"accepted_shadow":"rejected_shadow";if(!Same(r.Status,expected))return false;record=r;return true;}}
        public bool TryResolve(string id,string recipientHeroId,string decision,DateTime utc,out PersistentDiplomaticStatementRecord record,out string reason,out bool changed)
        {
            lock(gate){record=null;reason="negotiation_not_found";changed=false;PersistentDiplomaticStatementRecord r;if(!byId.TryGetValue(id??string.Empty,out r))return false;record=r;string recipient=Bound(recipientHeroId,160),kind=NormalizeDecision(decision);if(recipient.Length==0||!Same(r.TargetHeroId,recipient)){reason="recipient_not_authorized";return false;}if(kind.Length==0){reason="invalid_recipient_decision";return false;}DateTime now=Utc(utc);string desired=kind=="accept"?"accepted_shadow":"rejected_shadow";if(Same(r.Status,desired)){reason="idempotent_recipient_replay";return true;}if(!Same(r.Status,"pending_recipient")){reason="negotiation_already_resolved";return false;}if(r.ExpiresUtc!=DateTime.MinValue&&now>r.ExpiresUtc){r.Status="expired";r.ResolvedUtc=now;r.ResolvedByHeroId=recipient;r.LastReasonCode="negotiation_expired";changed=true;reason="negotiation_expired";return false;}r.Status=desired;r.ResolvedUtc=now;r.ResolvedByHeroId=recipient;r.LastReasonCode=kind=="accept"?"recipient_accepted_shadow":"recipient_rejected_shadow";changed=true;reason=r.LastReasonCode;return true;}
        }
        public bool TryResolveByNpcPolicy(string id,string recipientHeroId,bool accept,string policyReason,DateTime utc,out PersistentDiplomaticStatementRecord record,out string reason,out bool changed)
        {
            lock(gate)
            {
                record=null;reason="negotiation_not_found";changed=false;PersistentDiplomaticStatementRecord r;if(!byId.TryGetValue(id??string.Empty,out r))return false;record=r;string recipient=Bound(recipientHeroId,160);if(recipient.Length==0||!Same(r.TargetHeroId,recipient)){reason="recipient_not_authorized";return false;}string desired=accept?"accepted_shadow":"rejected_shadow";string durableReason=Bound(policyReason,64);if(durableReason.Length==0)durableReason=accept?"npc_policy_accepted":"npc_policy_rejected";if(Same(r.Status,desired)){reason=durableReason;return true;}if(!Same(r.Status,"pending_recipient")){reason="negotiation_already_resolved";return false;}DateTime now=Utc(utc);if(r.ExpiresUtc!=DateTime.MinValue&&now>r.ExpiresUtc){r.Status="expired";r.ResolvedUtc=now;r.ResolvedByHeroId=recipient;r.LastReasonCode="negotiation_expired";changed=true;reason="negotiation_expired";return false;}r.Status=desired;r.ResolvedUtc=now;r.ResolvedByHeroId=recipient;r.LastReasonCode=durableReason;changed=true;reason=durableReason;return true;
            }
        }
        public bool IsIdempotentWithdrawal(string id,string sourceHeroId,out PersistentDiplomaticStatementRecord record)
        {lock(gate){record=null;PersistentDiplomaticStatementRecord r;if(!byId.TryGetValue(id??string.Empty,out r)||!Same(r.PlayerHeroId,sourceHeroId)||!Same(r.Status,"withdrawn_shadow"))return false;record=r;return true;}}
        public bool TryWithdraw(string id,string sourceHeroId,DateTime utc,out PersistentDiplomaticStatementRecord record,out string reason,out bool changed)
        {
            lock(gate){record=null;reason="negotiation_not_found";changed=false;PersistentDiplomaticStatementRecord r;if(!byId.TryGetValue(id??string.Empty,out r))return false;record=r;string source=Bound(sourceHeroId,160);if(source.Length==0||!Same(r.PlayerHeroId,source)){reason="source_not_authorized";return false;}if(Same(r.Status,"withdrawn_shadow")){reason="idempotent_source_withdrawal";return true;}if(!Same(r.Status,"pending_recipient")){reason="negotiation_already_resolved";return false;}DateTime now=Utc(utc);if(r.ExpiresUtc!=DateTime.MinValue&&now>r.ExpiresUtc){r.Status="expired";r.ResolvedUtc=now;r.ResolvedByHeroId=source;r.LastReasonCode="negotiation_expired";changed=true;reason="negotiation_expired";return false;}r.Status="withdrawn_shadow";r.ResolvedUtc=now;r.ResolvedByHeroId=source;r.LastReasonCode="source_withdrawn_shadow";changed=true;reason="source_withdrawn_shadow";return true;}
        }
        public bool TryMarkNativeWarCommitted(string id,string sourceHeroId,DateTime utc,out PersistentDiplomaticStatementRecord record,out string reason,out bool changed)
        {
            lock(gate){record=null;reason="negotiation_not_found";changed=false;PersistentDiplomaticStatementRecord r;if(!byId.TryGetValue(id??string.Empty,out r))return false;record=r;string source=Bound(sourceHeroId,160);if(source.Length==0||!Same(r.PlayerHeroId,source)){reason="source_not_authorized";return false;}if(Same(r.Status,"committed_native_war")&&r.NativeMutationApplied){reason="idempotent_native_war_commit";return true;}if(!Same(r.Status,"accepted_shadow")||!Same(r.Action,"war")){reason="native_war_status_not_eligible";return false;}DateTime now=Utc(utc);r.Status="committed_native_war";r.NativeCommitUtc=now;r.NativeCommittedByHeroId=source;r.NativeMutationApplied=true;r.LastReasonCode="native_war_applied";changed=true;reason="native_war_applied";return true;}
        }
        public bool TryMarkNativePeaceCommitted(string id,string sourceHeroId,DateTime utc,out PersistentDiplomaticStatementRecord record,out string reason,out bool changed)
        {
            lock(gate){record=null;reason="negotiation_not_found";changed=false;PersistentDiplomaticStatementRecord r;if(!byId.TryGetValue(id??string.Empty,out r))return false;record=r;string source=Bound(sourceHeroId,160);if(source.Length==0||!Same(r.PlayerHeroId,source)){reason="source_not_authorized";return false;}if(Same(r.Status,"committed_native_peace")&&r.NativeMutationApplied){reason="idempotent_native_peace_commit";return true;}if(!Same(r.Status,"accepted_shadow")||!Same(r.Action,"peace")){reason="native_peace_status_not_eligible";return false;}DateTime now=Utc(utc);r.Status="committed_native_peace";r.NativeCommitUtc=now;r.NativeCommittedByHeroId=source;r.NativeMutationApplied=true;r.LastReasonCode="native_peace_applied";changed=true;reason="native_peace_applied";return true;}
        }
        public int CountPendingIncoming(string recipientHeroId,DateTime utc){lock(gate){string recipient=Bound(recipientHeroId,160);DateTime now=Utc(utc);int count=0;foreach(PersistentDiplomaticStatementRecord r in byId.Values)if(IsActive(r,now)&&Same(r.TargetHeroId,recipient))count++;return count;}}
        public string LatestPendingIncomingId(string recipientHeroId,DateTime utc){lock(gate){string recipient=Bound(recipientHeroId,160);DateTime now=Utc(utc),latestTime=DateTime.MinValue;string latest=string.Empty;foreach(PersistentDiplomaticStatementRecord r in byId.Values){if(!IsActive(r,now)||!Same(r.TargetHeroId,recipient))continue;if(r.OccurredUtc>=latestTime){latestTime=r.OccurredUtc;latest=r.Id;}}return latest;}}
        public bool TryGetPendingIncomingPage(string recipientHeroId,DateTime utc,string afterStatementId,int pageSize,out List<PersistentDiplomaticStatementRecord> records,out string nextCursor,out bool hasMore,out int totalCount,out string reason)
        {
            lock(gate)
            {
                records=new List<PersistentDiplomaticStatementRecord>();nextCursor=string.Empty;hasMore=false;totalCount=0;reason="inbox_ready";
                string recipient=Bound(recipientHeroId,160),cursor=(afterStatementId??string.Empty).Trim();DateTime now=Utc(utc);int limit=Math.Max(1,Math.Min(8,pageSize));
                if(recipient.Length==0){reason="recipient_required";return false;}if(cursor.Length>0&&!IsId(cursor)){reason="invalid_cursor";return false;}
                List<PersistentDiplomaticStatementRecord> visible=new List<PersistentDiplomaticStatementRecord>();
                foreach(string id in order){PersistentDiplomaticStatementRecord r;if(byId.TryGetValue(id,out r)&&IsActive(r,now)&&Same(r.TargetHeroId,recipient))visible.Add(r);}
                visible.Reverse();totalCount=visible.Count;int startIndex=0;
                if(cursor.Length>0){startIndex=-1;for(int i=0;i<visible.Count;i++)if(Same(visible[i].Id,cursor)){startIndex=i+1;break;}if(startIndex<0){reason="cursor_not_found";return false;}}
                for(int i=startIndex;i<visible.Count&&records.Count<limit;i++)records.Add(CloneRecord(visible[i]));
                hasMore=startIndex+records.Count<visible.Count;if(records.Count>0)nextCursor=records[records.Count-1].Id;return true;
            }
        }

        public bool HasPendingPair(string sourceFactionId,string targetFactionId,DateTime utc)
        {lock(gate){string pair=PairKey(Bound(sourceFactionId,160),Bound(targetFactionId,160));DateTime now=Utc(utc);foreach(PersistentDiplomaticStatementRecord r in byId.Values)if(IsActive(r,now)&&PairKey(r.SourceKingdomId,r.TargetKingdomId)==pair)return true;return false;}}

        public int CountNpcInitiativesForCampaignDay(long campaignDay)
        {lock(gate){int count=0;foreach(PersistentDiplomaticStatementRecord r in byId.Values)if(r!=null&&Same(r.Origin,"npc_scheduler")&&r.CampaignDay==campaignDay)count++;return count;}}

        public long LatestNpcInitiativeCampaignHour()
        {lock(gate){long latest=-1;foreach(PersistentDiplomaticStatementRecord r in byId.Values)if(r!=null&&Same(r.Origin,"npc_scheduler")&&r.CampaignHour>latest)latest=r.CampaignHour;return latest;}}

        public int CountNpcInitiativesForTarget(string targetHeroId,long campaignDay)
        {lock(gate){string target=Bound(targetHeroId,160);int count=0;foreach(PersistentDiplomaticStatementRecord r in byId.Values)if(r!=null&&Same(r.Origin,"npc_scheduler")&&r.CampaignDay==campaignDay&&Same(r.TargetHeroId,target))count++;return count;}}

        public bool HasRecentNpcInitiativeForPair(string sourceFactionId,string targetFactionId,long campaignDay,int cooldownDays)
        {lock(gate){string pair=PairKey(Bound(sourceFactionId,160),Bound(targetFactionId,160));long earliest=campaignDay-Math.Max(1,cooldownDays);foreach(PersistentDiplomaticStatementRecord r in byId.Values)if(r!=null&&Same(r.Origin,"npc_scheduler")&&r.CampaignDay>=earliest&&r.CampaignDay<=campaignDay&&PairKey(r.SourceKingdomId,r.TargetKingdomId)==pair)return true;return false;}}

        public string BuildInbox(string recipientHeroId,DateTime utc)
        {
            lock(gate){string recipient=Bound(recipientHeroId,160);DateTime now=Utc(utc);StringBuilder b=new StringBuilder(900);b.AppendLine("Входящие дипломатические shadow-предложения:");int shown=0;foreach(string id in order){PersistentDiplomaticStatementRecord r;if(!byId.TryGetValue(id,out r)||!Same(r.TargetHeroId,recipient)||!IsActive(r,now))continue;if(shown++>=10)break;b.Append("- id=").Append(r.Id).Append("; ").Append(r.Action).Append("; ").Append(r.SourceKingdomId).Append(" -> ").Append(r.TargetKingdomId).Append("; expires=").Append(r.ExpiresUtc.ToUniversalTime().ToString("o")).AppendLine();}if(shown==0)b.AppendLine("- нет активных предложений");b.AppendLine("Принять: /diplomacy-accept <id>");b.Append("Отклонить: /diplomacy-reject <id>");return b.ToString();}
        }
        public string BuildHistory(string heroId,DateTime utc)
        {
            lock(gate){string hero=Bound(heroId,160);StringBuilder b=new StringBuilder(1600);b.AppendLine("История дипломатических переговоров:");int shown=0;List<PersistentDiplomaticStatementRecord> visible=new List<PersistentDiplomaticStatementRecord>();foreach(string id in order){PersistentDiplomaticStatementRecord r;if(byId.TryGetValue(id,out r)&&(Same(r.PlayerHeroId,hero)||Same(r.TargetHeroId,hero)))visible.Add(r);}for(int i=visible.Count-1;i>=0&&shown<10;i--,shown++){PersistentDiplomaticStatementRecord r=visible[i];b.Append("- id=").Append(r.Id).Append("; ").Append(r.Action).Append("; ").Append(r.SourceKingdomId).Append(" -> ").Append(r.TargetKingdomId).Append("; status=").Append(r.Status).Append("; reason=").Append(r.LastReasonCode).Append("; native=").Append(r.NativeMutationApplied?"YES":"NO").AppendLine();}if(shown==0)b.AppendLine("- записей нет");return b.ToString();}
        }
        public List<PersistentDiplomaticStatementRecord> Export(string playerFilter){lock(gate){List<PersistentDiplomaticStatementRecord> result=new List<PersistentDiplomaticStatementRecord>();foreach(string id in order){PersistentDiplomaticStatementRecord r;if(!byId.TryGetValue(id,out r))continue;if(!string.IsNullOrWhiteSpace(playerFilter)&&!Same(playerFilter,r.PlayerHeroId)&&!Same(playerFilter,r.TargetHeroId))continue;result.Add(r);}return result;}}
        public void Import(IList<PersistentDiplomaticStatementRecord> records)
        {
            lock(gate){ClearLocked();if(records==null)return;foreach(PersistentDiplomaticStatementRecord r in records){if(r==null||!IsId(r.Id))continue;string action=(r.Action??string.Empty).Trim().ToLowerInvariant(),status=NormalizeStatus(r.Status);if((action!="war"&&action!="peace")||status.Length==0)continue;PersistentDiplomaticStatementRecord safe=new PersistentDiplomaticStatementRecord{Id=r.Id,PlayerHeroId=Bound(r.PlayerHeroId,160),TargetHeroId=Bound(r.TargetHeroId,160),SourceKingdomId=Bound(r.SourceKingdomId,160),TargetKingdomId=Bound(r.TargetKingdomId,160),Action=action,OccurredUtc=Utc(r.OccurredUtc),Status=status,ExpiresUtc=r.ExpiresUtc==DateTime.MinValue?Utc(r.OccurredUtc):Utc(r.ExpiresUtc),ResolvedUtc=r.ResolvedUtc==DateTime.MinValue?DateTime.MinValue:Utc(r.ResolvedUtc),ResolvedByHeroId=Bound(r.ResolvedByHeroId,160),LastReasonCode=Bound(r.LastReasonCode,64),NativeCommitUtc=r.NativeCommitUtc==DateTime.MinValue?DateTime.MinValue:Utc(r.NativeCommitUtc),NativeCommittedByHeroId=Bound(r.NativeCommittedByHeroId,160),NativeMutationApplied=r.NativeMutationApplied,Origin=Bound(r.Origin,32),InitiativeReasonCode=Bound(r.InitiativeReasonCode,64),InitiativeScore=Math.Max(-1000,Math.Min(1000,r.InitiativeScore)),CampaignDay=r.CampaignDay,CampaignHour=r.CampaignHour};if(safe.Origin.Length==0)safe.Origin="legacy";if(safe.PlayerHeroId.Length==0||safe.TargetHeroId.Length==0||safe.SourceKingdomId.Length==0||safe.TargetKingdomId.Length==0||safe.SourceKingdomId==safe.TargetKingdomId)continue;if((safe.Status=="committed_native_war"||safe.Status=="committed_native_peace")&&!safe.NativeMutationApplied)continue;byId[safe.Id]=safe;order.Enqueue(safe.Id);lastApplied[safe.SourceKingdomId+""+safe.TargetKingdomId+""+safe.Action]=safe.OccurredUtc;while(order.Count>MaximumRecords)byId.Remove(order.Dequeue());}}
        }
        public void Clear(){lock(gate)ClearLocked();}private void ClearLocked(){byId.Clear();order.Clear();lastApplied.Clear();}
        private static PersistentDiplomaticStatementRecord CloneRecord(PersistentDiplomaticStatementRecord r)
        {
            return r==null?null:new PersistentDiplomaticStatementRecord{Id=r.Id,PlayerHeroId=r.PlayerHeroId,TargetHeroId=r.TargetHeroId,SourceKingdomId=r.SourceKingdomId,TargetKingdomId=r.TargetKingdomId,Action=r.Action,OccurredUtc=r.OccurredUtc,Status=r.Status,ExpiresUtc=r.ExpiresUtc,ResolvedUtc=r.ResolvedUtc,ResolvedByHeroId=r.ResolvedByHeroId,LastReasonCode=r.LastReasonCode,NativeCommitUtc=r.NativeCommitUtc,NativeCommittedByHeroId=r.NativeCommittedByHeroId,NativeMutationApplied=r.NativeMutationApplied,Origin=r.Origin,InitiativeReasonCode=r.InitiativeReasonCode,InitiativeScore=r.InitiativeScore,CampaignDay=r.CampaignDay,CampaignHour=r.CampaignHour};
        }
        private static string NormalizeDecision(string s){s=(s??string.Empty).Trim().ToLowerInvariant();return s=="accept"||s=="reject"?s:string.Empty;}
        private static string NormalizeStatus(string s){s=(s??string.Empty).Trim().ToLowerInvariant();if(s.Length==0)return "legacy_shadow_recorded";return s=="pending_recipient"||s=="accepted_shadow"||s=="rejected_shadow"||s=="withdrawn_shadow"||s=="expired"||s=="committed_native_war"||s=="committed_native_peace"||s=="legacy_shadow_recorded"?s:string.Empty;}
        private static bool IsActive(PersistentDiplomaticStatementRecord r,DateTime now){return r!=null&&Same(r.Status,"pending_recipient")&&(r.ExpiresUtc==DateTime.MinValue||now<=r.ExpiresUtc);}
        private static string PairKey(string a,string b){return string.CompareOrdinal(a??string.Empty,b??string.Empty)<=0?(a??string.Empty)+""+(b??string.Empty):(b??string.Empty)+""+(a??string.Empty);}
        private static DateTime Utc(DateTime d){return d==DateTime.MinValue?DateTime.MinValue:(d.Kind==DateTimeKind.Utc?d:d.ToUniversalTime());}private static bool Same(string a,string b){return string.Equals(a??string.Empty,b??string.Empty,StringComparison.Ordinal);}private static bool IsId(string s){if(string.IsNullOrWhiteSpace(s)||s.Length!=32)return false;foreach(char c in s)if(!char.IsDigit(c)&&!(c>='a'&&c<='f')&&!(c>='A'&&c<='F'))return false;return true;}private static string Bound(string s,int n){s=(s??string.Empty).Trim().Replace('',' ');return s.Length<=n?s:s.Substring(0,n);}
    }
}
