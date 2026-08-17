using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
namespace AIPort.Server
{
    public sealed class NativeWarAdapterResult{public bool Accepted;public bool NativeCallAttempted;public bool NativeMutationApplied;public string ReasonCode;public bool AtWarBefore;public bool AtWarAfter;}
    public sealed class NativeWarAdapter
    {
        public NativeWarAdapterResult TryApply(bool enabled,IFaction sourceFaction,IFaction targetFaction)
        {
            NativeWarAdapterResult r=new NativeWarAdapterResult{Accepted=false,NativeMutationApplied=false,ReasonCode="native_war_adapter_disabled"};
            if(!enabled)return r;if(sourceFaction==null||targetFaction==null||ReferenceEquals(sourceFaction,targetFaction)){r.ReasonCode="native_war_pair_invalid";return r;}
            try{r.AtWarBefore=FactionManager.IsAtWarAgainstFaction(sourceFaction,targetFaction);}catch{r.ReasonCode="native_war_precondition_failed";return r;}
            if(r.AtWarBefore){r.ReasonCode="already_at_war";return r;}
            r.NativeCallAttempted=true;try{DeclareWarAction.ApplyByDefault(sourceFaction,targetFaction);}catch{r.ReasonCode="native_war_apply_failed";return r;}
            try{r.AtWarAfter=FactionManager.IsAtWarAgainstFaction(sourceFaction,targetFaction);}catch{r.ReasonCode="native_war_postcondition_failed";return r;}
            if(!r.AtWarAfter){r.ReasonCode="native_war_postcondition_failed";return r;}
            r.Accepted=true;r.NativeMutationApplied=true;r.ReasonCode="native_war_applied";return r;
        }
    }
}
