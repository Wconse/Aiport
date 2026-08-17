using System;
using System.Collections.Generic;

namespace AIPort.Server
{
    // Pure authoritative recipient filter for NPC diplomacy initiative.
    // A coop campaign can contain several player-looking Hero objects (for example the
    // authoritative "Hero_Player" plus stale duplicates "Player" and "main_hero").
    // Only hero ids bound to exactly one currently connected peer may receive offers.
    public static class AuthoritativeDiplomacyRecipientFilter
    {
        public static List<string> AuthoritativeHeroIds(IEnumerable<KeyValuePair<int,string>> peerHeroIds,ICollection<int> connectedPeerIds)
        {
            Dictionary<string,int> counts=new Dictionary<string,int>(StringComparer.Ordinal);List<string> ordered=new List<string>();
            if(peerHeroIds!=null)
            {
                foreach(KeyValuePair<int,string> pair in peerHeroIds)
                {
                    string heroId=pair.Value;if(string.IsNullOrWhiteSpace(heroId))continue;if(connectedPeerIds==null||!connectedPeerIds.Contains(pair.Key))continue;
                    int existing;if(counts.TryGetValue(heroId,out existing))counts[heroId]=existing+1;else{counts.Add(heroId,1);ordered.Add(heroId);}
                }
            }
            List<string> result=new List<string>();foreach(string heroId in ordered)if(counts[heroId]==1)result.Add(heroId);
            result.Sort(delegate(string a,string b){return string.CompareOrdinal(a,b);});return result;
        }

        public static bool HasAuthoritativeRecipients(ICollection<string> authoritativeHeroIds)
        {
            return authoritativeHeroIds!=null&&authoritativeHeroIds.Count>0;
        }

        // With no connected authoritative hero the offline queue keeps its previous behaviour.
        public static bool IsAuthoritativeRecipient(ICollection<string> authoritativeHeroIds,string heroId)
        {
            if(!HasAuthoritativeRecipients(authoritativeHeroIds))return true;if(string.IsNullOrWhiteSpace(heroId))return false;
            foreach(string candidate in authoritativeHeroIds)if(string.Equals(candidate,heroId,StringComparison.Ordinal))return true;return false;
        }

        public static List<string> SelectRecipientHeroIds(IEnumerable<string> discoveredCandidateHeroIds,ICollection<string> authoritativeHeroIds,out List<string> excludedHeroIds)
        {
            List<string> discovered=new List<string>();excludedHeroIds=new List<string>();HashSet<string> discoveredSeen=new HashSet<string>(StringComparer.Ordinal);
            if(discoveredCandidateHeroIds!=null)
            {
                foreach(string heroId in discoveredCandidateHeroIds)if(!string.IsNullOrWhiteSpace(heroId)&&discoveredSeen.Add(heroId))discovered.Add(heroId);
            }
            discovered.Sort(delegate(string a,string b){return string.CompareOrdinal(a,b);});
            if(!HasAuthoritativeRecipients(authoritativeHeroIds))return discovered;

            // The live peer mapping is the source of truth. Coop's canonical player object can be
            // absent from the global alive-player enumeration, so do not require it to be in the discovered set.
            List<string> selected=new List<string>();HashSet<string> authoritativeSeen=new HashSet<string>(StringComparer.Ordinal);
            foreach(string heroId in authoritativeHeroIds)if(!string.IsNullOrWhiteSpace(heroId)&&authoritativeSeen.Add(heroId))selected.Add(heroId);
            selected.Sort(delegate(string a,string b){return string.CompareOrdinal(a,b);});
            foreach(string heroId in discovered)if(!IsAuthoritativeRecipient(authoritativeHeroIds,heroId))excludedHeroIds.Add(heroId);
            return selected;
        }
    }
}
