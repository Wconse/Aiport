using System;
using System.Collections.Generic;

namespace AIPort.Server
{
    public sealed class NpcDiplomacyInitiativeCandidate
    {
        public string SourceHeroId;
        public string TargetHeroId;
        public string SourceFactionId;
        public string TargetFactionId;
        public bool PairAuthorized;
        public bool AtWar;
        public int Relation;
    }

    public sealed class NpcDiplomacyInitiativeDecision
    {
        public bool Selected;
        public NpcDiplomacyInitiativeCandidate Candidate;
        public string Action;
        public string ReasonCode;
        public int Score;
        public string DeterministicKey;
    }

    // Pure deterministic selector. Campaign objects are snapshotted by the server handler before this runs.
    // It creates no records, sends no messages and never calls a native diplomacy mutation API.
    public sealed class NpcDiplomacyInitiativeScheduler
    {
        public NpcDiplomacyInitiativeDecision Select(IList<NpcDiplomacyInitiativeCandidate> candidates,
            string campaignGeneration, long campaignDay, int minimumScore)
        {
            NpcDiplomacyInitiativeDecision best = Empty();
            if (candidates == null) return best;
            int threshold = Math.Max(0, Math.Min(200, minimumScore));
            foreach (NpcDiplomacyInitiativeCandidate candidate in candidates)
            {
                if (!IsEligible(candidate)) continue;
                string action = candidate.AtWar ? "peace" : "war";
                string key = BuildKey(campaignGeneration, campaignDay, candidate, action);
                int jitter = (int)(StableHash(key) % (candidate.AtWar ? 31u : 41u));
                int relation = Math.Max(-100, Math.Min(100, candidate.Relation));
                int score = candidate.AtWar ? 85 + relation + jitter : 55 - relation + jitter;
                if (score < threshold) continue;
                if (!best.Selected || score > best.Score || (score == best.Score && string.CompareOrdinal(key, best.DeterministicKey) < 0))
                {
                    best = new NpcDiplomacyInitiativeDecision
                    {
                        Selected = true,
                        Candidate = candidate,
                        Action = action,
                        ReasonCode = candidate.AtWar ? "npc_initiative_peace_window" : "npc_initiative_war_pressure",
                        Score = score,
                        DeterministicKey = key
                    };
                }
            }
            return best;
        }

        public static uint StableHash(string value)
        {
            unchecked
            {
                uint hash = 2166136261u;
                string text = value ?? string.Empty;
                for (int i = 0; i < text.Length; i++)
                {
                    hash ^= text[i];
                    hash *= 16777619u;
                }
                return hash;
            }
        }

        private static bool IsEligible(NpcDiplomacyInitiativeCandidate candidate)
        {
            return candidate != null && candidate.PairAuthorized
                && !string.IsNullOrWhiteSpace(candidate.SourceHeroId)
                && !string.IsNullOrWhiteSpace(candidate.TargetHeroId)
                && !string.IsNullOrWhiteSpace(candidate.SourceFactionId)
                && !string.IsNullOrWhiteSpace(candidate.TargetFactionId)
                && !string.Equals(candidate.SourceHeroId, candidate.TargetHeroId, StringComparison.Ordinal)
                && !string.Equals(candidate.SourceFactionId, candidate.TargetFactionId, StringComparison.Ordinal);
        }

        private static string BuildKey(string generation, long day, NpcDiplomacyInitiativeCandidate candidate, string action)
        {
            return (generation ?? string.Empty) + "|" + day.ToString(System.Globalization.CultureInfo.InvariantCulture)
                + "|" + candidate.SourceFactionId + "|" + candidate.TargetFactionId
                + "|" + candidate.SourceHeroId + "|" + candidate.TargetHeroId + "|" + action;
        }

        private static NpcDiplomacyInitiativeDecision Empty()
        {
            return new NpcDiplomacyInitiativeDecision
            {
                Selected = false,
                Candidate = null,
                Action = string.Empty,
                ReasonCode = "npc_initiative_no_eligible_candidate",
                Score = 0,
                DeterministicKey = string.Empty
            };
        }
    }
}
