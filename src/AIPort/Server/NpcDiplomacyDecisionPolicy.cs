using System;
namespace AIPort.Server
{
    public sealed class NpcDiplomacyPolicyDecision
    {
        public bool RequiresPlayerDecision;
        public bool Accepted;
        public string ReasonCode;
        public int Score;
    }

    // Pure deterministic policy. It may approve only the shadow recipient decision;
    // it never calls a Bannerlord mutation API and never consumes raw LLM output.
    public sealed class NpcDiplomacyDecisionPolicy
    {
        public NpcDiplomacyPolicyDecision Evaluate(string action, bool targetPlayerControlled, bool pairAuthorized, bool atWar, int targetRelationToSource)
        {
            string kind=(action??string.Empty).Trim().ToLowerInvariant();
            if(targetPlayerControlled) return Decision(true,false,"player_recipient_consent_required",0);
            if(!pairAuthorized) return Decision(false,false,"npc_policy_authority_rejected",0);
            if(kind=="war")
            {
                if(atWar)return Decision(false,false,"already_at_war",0);
                // A declaration/challenge does not require an AI ruler to consent to becoming a target.
                return Decision(false,true,"npc_policy_war_acknowledged",0);
            }
            if(kind=="peace")
            {
                if(!atWar)return Decision(false,false,"not_at_war",0);
                int score=Math.Max(-100,Math.Min(100,targetRelationToSource));
                return score>=-25?Decision(false,true,"npc_policy_peace_accepted",score):Decision(false,false,"npc_policy_peace_rejected",score);
            }
            return Decision(false,false,"npc_policy_action_invalid",0);
        }
        private static NpcDiplomacyPolicyDecision Decision(bool manual,bool accepted,string reason,int score)
        {return new NpcDiplomacyPolicyDecision{RequiresPlayerDecision=manual,Accepted=accepted,ReasonCode=reason,Score=score};}
    }
}
