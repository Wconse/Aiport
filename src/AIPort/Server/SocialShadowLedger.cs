using System;
using System.Collections.Generic;
namespace AIPort.Server
{
    public sealed class SocialShadowLedger
    {
        private const int MaximumRecords=512,MinimumScore=-25,MaximumScore=25;
        private static readonly TimeSpan Cooldown=TimeSpan.FromSeconds(15);
        private readonly object gate=new object();
        private readonly Dictionary<string,PersistentSocialRecord> byId=new Dictionary<string,PersistentSocialRecord>(StringComparer.Ordinal);
        private readonly Queue<string> order=new Queue<string>();
        private readonly Dictionary<string,int> scores=new Dictionary<string,int>(StringComparer.Ordinal);
        private readonly Dictionary<string,DateTime> lastApplied=new Dictionary<string,DateTime>(StringComparer.Ordinal);
        public int Count{get{lock(gate)return byId.Count;}}
        public bool TryApply(string id,string playerHeroId,string targetInstanceId,int delta,DateTime utc,out PersistentSocialRecord record,out string reason,out bool newlyApplied)
        {
            lock(gate)
            {
                record=null;reason="invalid_social_receipt";newlyApplied=false;
                PersistentSocialRecord existing;if(byId.TryGetValue(id??string.Empty,out existing)){record=existing;reason="idempotent_replay";return true;}
                string player=Normalize(playerHeroId,160),target=Normalize(targetInstanceId,320);
                if(!IsId(id)||player.Length==0||target.Length==0||delta==0||delta < -2||delta > 2)return false;
                DateTime now=utc.Kind==DateTimeKind.Utc?utc:utc.ToUniversalTime();string key=player+"\u001f"+target;DateTime last;
                if(lastApplied.TryGetValue(key,out last)&&now-last<Cooldown){reason="social_cooldown";return false;}
                int before;scores.TryGetValue(key,out before);int after=Math.Max(MinimumScore,Math.Min(MaximumScore,before+delta));
                if(after==before){reason="social_score_cap";return false;}
                record=new PersistentSocialRecord{Id=id,PlayerHeroId=player,TargetInstanceId=target,Delta=delta,BeforeValue=before,AfterValue=after,OccurredUtc=now};
                byId[id]=record;order.Enqueue(id);scores[key]=after;lastApplied[key]=now;newlyApplied=true;reason="social_shadow_recorded";Trim();return true;
            }
        }
        public List<PersistentSocialRecord> Export(string playerHeroFilter)
        {
            lock(gate){List<PersistentSocialRecord> result=new List<PersistentSocialRecord>();foreach(string id in order){PersistentSocialRecord r;if(!byId.TryGetValue(id,out r))continue;if(!string.IsNullOrWhiteSpace(playerHeroFilter)&&!string.Equals(playerHeroFilter,r.PlayerHeroId,StringComparison.Ordinal))continue;result.Add(r);}return result;}
        }
        public void Import(IList<PersistentSocialRecord> records)
        {
            lock(gate){ClearLocked();if(records==null)return;foreach(PersistentSocialRecord r in records){if(r==null||!IsId(r.Id))continue;string player=Normalize(r.PlayerHeroId,160),target=Normalize(r.TargetInstanceId,320);if(player.Length==0||target.Length==0||r.Delta==0||r.Delta < -2||r.Delta > 2)continue;string key=player+"\u001f"+target;PersistentSocialRecord safe=new PersistentSocialRecord{Id=r.Id,PlayerHeroId=player,TargetInstanceId=target,Delta=r.Delta,BeforeValue=Math.Max(MinimumScore,Math.Min(MaximumScore,r.BeforeValue)),AfterValue=Math.Max(MinimumScore,Math.Min(MaximumScore,r.AfterValue)),OccurredUtc=r.OccurredUtc.Kind==DateTimeKind.Utc?r.OccurredUtc:r.OccurredUtc.ToUniversalTime()};byId[safe.Id]=safe;order.Enqueue(safe.Id);scores[key]=safe.AfterValue;lastApplied[key]=safe.OccurredUtc;Trim();}}
        }
        public int GetScore(string playerHeroId,string targetInstanceId){lock(gate){int score;scores.TryGetValue(Normalize(playerHeroId,160)+"\u001f"+Normalize(targetInstanceId,320),out score);return score;}}
        public void Clear(){lock(gate)ClearLocked();}
        private void ClearLocked(){byId.Clear();order.Clear();scores.Clear();lastApplied.Clear();}
        private void Trim(){while(order.Count>MaximumRecords){string id=order.Dequeue();byId.Remove(id);}}
        private static bool IsId(string s){if(string.IsNullOrWhiteSpace(s)||s.Length!=32)return false;foreach(char c in s)if(!char.IsDigit(c)&&!(c>='a'&&c<='f')&&!(c>='A'&&c<='F'))return false;return true;}
        private static string Normalize(string s,int n){s=(s??string.Empty).Trim().Replace('\u001f',' ');return s.Length<=n?s:s.Substring(0,n);}
    }
}
