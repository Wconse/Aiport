using System;
using System.Text;
using TaleWorlds.CampaignSystem;
namespace AIPort.Server
{
    public sealed class DiplomacyAuthorityContext
    {
        public string PlayerHeroId,TargetHeroId,SourceFactionId,TargetFactionId,SourceFactionName,TargetFactionName,SourceRole,TargetRole;
        public bool SourceFactionEligible,TargetFactionEligible,DistinctFactions,SourceAuthorized,TargetAuthorized;
        public bool PairAuthorized{get{return SourceFactionEligible&&TargetFactionEligible&&DistinctFactions&&SourceAuthorized&&TargetAuthorized;}}
    }
    public sealed class DiplomacyAuthorityService
    {
        private const int MaximumCharacters=2200;
        public DiplomacyAuthorityContext Evaluate(Hero playerHero,Hero targetHero)
        {
            IFaction source=SafeFaction(playerHero),target=SafeFaction(targetHero);string sourceRole,targetRole;
            bool sourceAuthorized=IsAuthorizedRepresentative(playerHero,source,out sourceRole),targetAuthorized=IsAuthorizedRepresentative(targetHero,target,out targetRole);
            return new DiplomacyAuthorityContext{
                PlayerHeroId=SafeId(playerHero),TargetHeroId=SafeId(targetHero),SourceFactionId=SafeFactionId(source),TargetFactionId=SafeFactionId(target),
                SourceFactionName=SafeFactionName(source),TargetFactionName=SafeFactionName(target),SourceRole=sourceRole,TargetRole=targetRole,
                SourceFactionEligible=IsEligible(source),TargetFactionEligible=IsEligible(target),DistinctFactions=source!=null&&target!=null&&!ReferenceEquals(source,target),
                SourceAuthorized=sourceAuthorized,TargetAuthorized=targetAuthorized};
        }
        public string BuildDisplay(DiplomacyAuthorityContext c)
        {
            if(c==null)return "Дипломатические полномочия недоступны.";
            StringBuilder b=new StringBuilder(900);b.AppendLine("Дипломатические полномочия (только чтение)");
            b.Append("Игрок: ").Append(c.PlayerHeroId.Length==0?"не определён":c.PlayerHeroId).Append("; фракция=").Append(c.SourceFactionName).Append(" [").Append(c.SourceFactionId.Length==0?"none":c.SourceFactionId).Append("]; роль=").Append(c.SourceRole).Append("; authority=").Append(c.SourceAuthorized?"PASS":"FAIL").AppendLine();
            b.Append("Собеседник: ").Append(c.TargetHeroId.Length==0?"не определён":c.TargetHeroId).Append("; фракция=").Append(c.TargetFactionName).Append(" [").Append(c.TargetFactionId.Length==0?"none":c.TargetFactionId).Append("]; роль=").Append(c.TargetRole).Append("; authority=").Append(c.TargetAuthorized?"PASS":"FAIL").AppendLine();
            b.Append("Пара: eligible=").Append(c.SourceFactionEligible&&c.TargetFactionEligible?"PASS":"FAIL").Append("; distinct=").Append(c.DistinctFactions?"PASS":"FAIL").Append("; authorized=").Append(c.PairAuthorized?"PASS":"FAIL").AppendLine();
            b.AppendLine("Для дипломатического shadow-заявления игрок должен быть правителем королевства или лидером независимого клана, а собеседник — полномочным лидером своей фракции.");
            b.Append("NativeMutationApplied=false. Эта проверка не меняет кампанию.");string text=b.ToString();return text.Length<=MaximumCharacters?text:text.Substring(0,MaximumCharacters);
        }
        private static bool IsAuthorizedRepresentative(Hero hero,IFaction faction,out string role)
        {
            role="none";if(hero==null||faction==null||!IsEligible(faction))return false;Kingdom kingdom=faction as Kingdom;if(kingdom!=null){role="kingdom_ruler";try{return ReferenceEquals(kingdom.Leader,hero);}catch{return false;}}
            Clan clan=faction as Clan;if(clan!=null){role="independent_clan_leader";try{return ReferenceEquals(clan.Leader,hero)&&clan.Kingdom==null;}catch{return false;}}
            role="unsupported_faction";return false;
        }
        private static IFaction SafeFaction(Hero hero){try{return hero==null?null:hero.MapFaction;}catch{return null;}}
        private static bool IsEligible(IFaction faction){try{return faction!=null&&!faction.IsBanditFaction;}catch{return false;}}
        private static string SafeId(Hero hero){try{return Bound(hero==null?string.Empty:hero.StringId,160);}catch{return string.Empty;}}
        private static string SafeFactionId(IFaction faction){try{return Bound(faction==null?string.Empty:faction.StringId,160);}catch{return string.Empty;}}
        private static string SafeFactionName(IFaction faction){try{string s=faction==null||faction.Name==null?string.Empty:faction.Name.ToString();return s.Length==0?"отсутствует":Bound(s,160);}catch{return "отсутствует";}}
        private static string Bound(string value,int max){string s=(value??string.Empty).Trim().Replace('\r',' ').Replace('\n',' ');return s.Length<=max?s:s.Substring(0,max);}
    }
}
