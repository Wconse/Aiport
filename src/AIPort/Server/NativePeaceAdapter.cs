using System;using TaleWorlds.CampaignSystem;using TaleWorlds.CampaignSystem.Actions;
namespace AIPort.Server
{
 public sealed class NativePeaceAdapterResult{public bool Accepted;public bool NativeCallAttempted;public bool NativeMutationApplied;public string ReasonCode;public bool AtWarBefore;public bool AtWarAfter;}
 public sealed class NativePeaceAdapter
 {
  public NativePeaceAdapterResult TryApply(bool enabled,IFaction sourceFaction,IFaction targetFaction)
  {
   NativePeaceAdapterResult r=new NativePeaceAdapterResult{Accepted=false,NativeMutationApplied=false,ReasonCode="native_peace_adapter_disabled"};if(!enabled)return r;if(sourceFaction==null||targetFaction==null||ReferenceEquals(sourceFaction,targetFaction)){r.ReasonCode="native_peace_pair_invalid";return r;}
   try{r.AtWarBefore=FactionManager.IsAtWarAgainstFaction(sourceFaction,targetFaction);}catch{r.ReasonCode="native_peace_precondition_failed";return r;}if(!r.AtWarBefore){r.ReasonCode="not_at_war";return r;}
   r.NativeCallAttempted=true;try{MakePeaceAction.Apply(sourceFaction,targetFaction);}catch{r.ReasonCode="native_peace_apply_failed";return r;}
   try{r.AtWarAfter=FactionManager.IsAtWarAgainstFaction(sourceFaction,targetFaction);}catch{r.ReasonCode="native_peace_postcondition_failed";return r;}if(r.AtWarAfter){r.ReasonCode="native_peace_postcondition_failed";return r;}r.Accepted=true;r.NativeMutationApplied=true;r.ReasonCode="native_peace_applied";return r;
  }
 }
}
