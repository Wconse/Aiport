using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using TaleWorlds.CampaignSystem;
namespace AIPort.Server
{
    internal sealed class RuntimeValidationBaseline
    {
        public string PlayerHeroId,TargetInstanceId,SourceFactionId,TargetFactionId;public int NativeRelation,SocialScore,MemoryCount,SocialCount,DiplomacyCount;public bool AtWar;public long Revision;public DateTime CapturedUtc;
    }
    public sealed class RuntimeValidationGateResult
    {
        public string Text,Reason;public bool HasBaseline,SameTarget,NativeRelationUnchanged,NativeWarStateUnchanged,SourceDiplomaticAuthority,TargetDiplomaticAuthority;public long RevisionDelta;public int MemoryDelta,SocialDelta,DiplomacyDelta,CustomScoreDelta;
    }
    public sealed class RuntimeValidationGate
    {
        private const int MaximumBaselines=256,MaximumText=7600;private readonly object gate=new object();private readonly Dictionary<string,RuntimeValidationBaseline> baselines=new Dictionary<string,RuntimeValidationBaseline>(StringComparer.Ordinal);private readonly Queue<string> order=new Queue<string>();
        public bool TryBuild(string mode,int capabilityFlags,AIPortServerSettings settings,AIPortStateStore stateStore,ConversationMemory memory,SocialShadowLedger social,DiplomaticStatementLedger diplomacy,NativeDiplomacyCommitJournal nativeJournal,Hero playerHero,ConversationTargetBinding binding,out RuntimeValidationGateResult result)
        {
            result=new RuntimeValidationGateResult{Text=string.Empty,Reason="invalid_gate_mode"};string normalized=(mode??string.Empty).Trim().ToLowerInvariant();if(normalized!="baseline"&&normalized!="report")return false;if(playerHero==null){result.Reason="player_unresolved";return false;}
            Hero targetHero=binding==null||!binding.IsHero?null:Hero.Find(binding.TargetId);Current current=BuildCurrent(playerHero,targetHero,binding,memory,social,diplomacy,stateStore.Revision);DiplomacyAuthorityContext authority=new DiplomacyAuthorityService().Evaluate(playerHero,targetHero);result.SourceDiplomaticAuthority=authority.SourceAuthorized;result.TargetDiplomaticAuthority=authority.TargetAuthorized;string key=current.PlayerHeroId+"\u001f"+current.TargetInstanceId;RuntimeValidationBaseline baseline=null;
            lock(gate)
            {
                if(normalized=="baseline")
                {
                    if(targetHero==null){result.Reason="hero_target_required";return false;}baseline=current.ToBaseline();if(!baselines.ContainsKey(key))order.Enqueue(key);baselines[key]=baseline;while(order.Count>MaximumBaselines)baselines.Remove(order.Dequeue());
                }
                else baselines.TryGetValue(key,out baseline);
            }
            result.HasBaseline=baseline!=null;result.SameTarget=baseline!=null&&string.Equals(baseline.PlayerHeroId,current.PlayerHeroId,StringComparison.Ordinal)&&string.Equals(baseline.TargetInstanceId,current.TargetInstanceId,StringComparison.Ordinal);result.NativeRelationUnchanged=baseline!=null&&baseline.NativeRelation==current.NativeRelation;result.NativeWarStateUnchanged=baseline!=null&&baseline.AtWar==current.AtWar&&string.Equals(baseline.SourceFactionId,current.SourceFactionId,StringComparison.Ordinal)&&string.Equals(baseline.TargetFactionId,current.TargetFactionId,StringComparison.Ordinal);
            if(baseline!=null){result.RevisionDelta=current.Revision-baseline.Revision;result.MemoryDelta=current.MemoryCount-baseline.MemoryCount;result.SocialDelta=current.SocialCount-baseline.SocialCount;result.DiplomacyDelta=current.DiplomacyCount-baseline.DiplomacyCount;result.CustomScoreDelta=current.SocialScore-baseline.SocialScore;}
            StringBuilder b=new StringBuilder(1800);b.AppendLine("AIPort — объединённый runtime gate");b.Append("Build=").Append(AIPort.Protocol.AIPortProtocol.Build).Append("; protocol=").Append(AIPort.Protocol.AIPortProtocol.Version).Append("; flags=").Append(capabilityFlags).AppendLine();
            b.Append("State: generation=").Append(stateStore.CampaignGeneration).Append("; revision=").Append(stateStore.Revision).Append("; loaded=").Append(stateStore.Loaded).Append("; readOnly=").Append(stateStore.ReadOnly).Append("; saving=").Append(stateStore.IsSaving).AppendLine();
            b.Append("Backend: configured=").Append(settings.ExplicitlyEnabled).Append("; active=").Append(settings.Enabled).Append("; keyPresent=").Append(!string.IsNullOrWhiteSpace(settings.ApiKey)).Append("; credentialsPresent=").Append(settings.CredentialsPresent).Append("; player2Refresh=").Append(settings.Player2RefreshAvailable).Append("; provider=").Append(Safe(settings.Backend)).Append("; model=").Append(Safe(settings.Model)).AppendLine();
            b.Append("Native diplomacy adapters: warConfigured=").Append(settings.NativeWarAdapterConfigured).Append("; warEnvironmentArmed=").Append(settings.NativeWarAdapterEnvironmentArmed).Append("; peaceConfigured=").Append(settings.NativePeaceAdapterConfigured).Append("; peaceEnvironmentArmed=").Append(settings.NativePeaceAdapterEnvironmentArmed).Append("; generationPinned=").Append(!string.IsNullOrWhiteSpace(settings.NativeDiplomacyGenerationPin)&&string.Equals(settings.NativeDiplomacyGenerationPin,stateStore.CampaignGeneration,StringComparison.Ordinal)).Append("; journal=").Append(nativeJournal==null?0:nativeJournal.Count).Append("; recoverable=").Append(nativeJournal==null?0:nativeJournal.Recoverable().Count).Append("; commitMode=explicit-single-use-60s").AppendLine();
            b.Append("Player: ").Append(SafeName(playerHero)).Append(" [").Append(current.PlayerHeroId).Append("]; faction=").Append(current.SourceFactionId.Length==0?"none":current.SourceFactionId).AppendLine();
            b.Append("Target: ").Append(targetHero==null?"none":SafeName(targetHero)).Append(" [").Append(current.TargetInstanceId.Length==0?"none":current.TargetInstanceId).Append("]; faction=").Append(current.TargetFactionId.Length==0?"none":current.TargetFactionId).AppendLine();
            b.Append("Native now: relation=").Append(current.NativeRelation).Append("; atWar=").Append(current.AtWar).AppendLine();b.Append("Diplomatic authority: source=").Append(result.SourceDiplomaticAuthority?"PASS":"FAIL").Append("; target=").Append(result.TargetDiplomaticAuthority?"PASS":"FAIL").Append("; pair=").Append(authority.PairAuthorized?"PASS":"FAIL").AppendLine();b.Append("Private state: memory=").Append(current.MemoryCount).Append("; social=").Append(current.SocialCount).Append("; diplomacy=").Append(current.DiplomacyCount).Append("; targetCustomScore=").Append(current.SocialScore).AppendLine();
            if(normalized=="baseline")b.Append("Baseline: STORED at revision ").Append(current.Revision).Append("; utc=").Append(current.CapturedUtc.ToString("o",CultureInfo.InvariantCulture)).AppendLine();
            else if(baseline==null)b.AppendLine("Baseline comparison: NO_BASELINE (run /aiport-gate baseline in this dialogue first)");
            else{b.Append("Baseline comparison: target=").Append(result.SameTarget?"PASS":"FAIL").Append("; nativeRelationUnchanged=").Append(result.NativeRelationUnchanged?"PASS":"FAIL").Append("; nativeWarStateUnchanged=").Append(result.NativeWarStateUnchanged?"PASS":"FAIL").AppendLine();b.Append("Deltas: revision=").Append(result.RevisionDelta).Append("; memory=").Append(result.MemoryDelta).Append("; social=").Append(result.SocialDelta).Append("; diplomacy=").Append(result.DiplomacyDelta).Append("; targetCustomScore=").Append(result.CustomScoreDelta).AppendLine();}
            List<PersistentDiplomaticStatementRecord> statements=diplomacy.Export(current.PlayerHeroId);if(statements.Count>0){b.AppendLine("Latest diplomatic shadow statements:");int start=Math.Max(0,statements.Count-5);for(int i=start;i<statements.Count;i++){PersistentDiplomaticStatementRecord r=statements[i];b.Append("- ").Append(r.Action).Append(": ").Append(r.SourceKingdomId).Append(" -> ").Append(r.TargetKingdomId).Append("; status=").Append(r.Status).Append("; reason=").Append(r.LastReasonCode).Append("; native=").Append(r.NativeMutationApplied?"YES":"NO").Append("; id=").Append(r.Id).Append(" @ ").Append(r.OccurredUtc.ToUniversalTime().ToString("o",CultureInfo.InvariantCulture)).AppendLine();}}
            b.AppendLine("NativeMutationApplied=false. This report performs no campaign mutation.");result.Text=b.Length<=MaximumText?b.ToString():b.ToString(0,MaximumText);result.Reason=normalized=="baseline"?"baseline_stored":"validation_report";return true;
        }
        private sealed class Current
        {
            public string PlayerHeroId,TargetInstanceId,SourceFactionId,TargetFactionId;public int NativeRelation,SocialScore,MemoryCount,SocialCount,DiplomacyCount;public bool AtWar;public long Revision;public DateTime CapturedUtc;
            public RuntimeValidationBaseline ToBaseline(){return new RuntimeValidationBaseline{PlayerHeroId=PlayerHeroId,TargetInstanceId=TargetInstanceId,SourceFactionId=SourceFactionId,TargetFactionId=TargetFactionId,NativeRelation=NativeRelation,SocialScore=SocialScore,MemoryCount=MemoryCount,SocialCount=SocialCount,DiplomacyCount=DiplomacyCount,AtWar=AtWar,Revision=Revision,CapturedUtc=CapturedUtc};}
        }
        private static Current BuildCurrent(Hero playerHero,Hero targetHero,ConversationTargetBinding binding,ConversationMemory memory,SocialShadowLedger social,DiplomaticStatementLedger diplomacy,long revision)
        {
            string playerId=playerHero.StringId??string.Empty,targetInstance=binding==null?string.Empty:binding.TargetInstanceId;IFaction source=null,target=null;try{source=playerHero.MapFaction;target=targetHero==null?null:targetHero.MapFaction;}catch{}int nativeRelation=0;try{if(targetHero!=null)nativeRelation=playerHero.GetRelation(targetHero);}catch{}bool atWar=SafeWar(source,target);return new Current{PlayerHeroId=playerId,TargetInstanceId=targetInstance,SourceFactionId=source==null?string.Empty:(source.StringId??string.Empty),TargetFactionId=target==null?string.Empty:(target.StringId??string.Empty),NativeRelation=nativeRelation,AtWar=atWar,SocialScore=social.GetScore(playerId,targetInstance),MemoryCount=memory.ExportPersistentRecords(playerId).Count,SocialCount=social.Export(playerId).Count,DiplomacyCount=diplomacy.Export(playerId).Count,Revision=revision,CapturedUtc=DateTime.UtcNow};
        }
        private static bool SafeWar(IFaction source,IFaction target){try{return source!=null&&target!=null&&FactionManager.IsAtWarAgainstFaction(source,target);}catch{return false;}}
        private static string SafeName(Hero hero){try{return Safe(hero.Name==null?string.Empty:hero.Name.ToString());}catch{return "unknown";}}private static string Safe(string value){string s=(value??string.Empty).Replace('\r',' ').Replace('\n',' ');return s.Length<=160?s:s.Substring(0,160);}
    }
}
