using System;
using System.Globalization;
using System.Text;
using TaleWorlds.CampaignSystem;
namespace AIPort.Server
{
    public sealed class DiplomacySnapshotService
    {
        private const int MaximumKingdoms=16,MaximumCharacters=6000;
        public string Build(Hero authoritativePlayerHero,Hero authoritativeTargetHero)
        {
            if(authoritativePlayerHero==null)return "Дипломатическая сводка недоступна: персонаж игрока не определён.";
            IFaction ownFaction=null;Kingdom ownKingdom=null;try{ownFaction=authoritativePlayerHero.MapFaction;ownKingdom=ownFaction as Kingdom;}catch{}
            StringBuilder text=new StringBuilder(1024);text.AppendLine("Дипломатическая сводка (только чтение)");
            text.Append("Игрок: ").Append(SafeName(authoritativePlayerHero.Name)).Append("; фракция: ").Append(ownFaction==null?"отсутствует":SafeName(ownFaction.Name));text.Append("; королевство: ").AppendLine(ownKingdom==null?"отсутствует":SafeName(ownKingdom.Name));
            int shown=0;
            foreach(Kingdom kingdom in Kingdom.All)
            {
                if(kingdom==null)continue;if(shown++>=MaximumKingdoms)break;string stance=ownFaction==null?"не определено":ReferenceEquals(ownFaction,kingdom)?"своё":SafeWar(ownFaction,kingdom)?"война":"мир";
                int settlements=0,armies=0;try{settlements=kingdom.Settlements.Count;}catch{}try{armies=kingdom.Armies.Count;}catch{}
                text.Append("- ").Append(SafeName(kingdom.Name)).Append(": ").Append(stance).Append(", поселений=").Append(settlements.ToString(CultureInfo.InvariantCulture)).Append(", армий=").Append(armies.ToString(CultureInfo.InvariantCulture)).AppendLine();
                if(text.Length>MaximumCharacters)break;
            }
            DiplomacyAuthorityService authority=new DiplomacyAuthorityService();DiplomacyAuthorityContext context=authority.Evaluate(authoritativePlayerHero,authoritativeTargetHero);text.AppendLine();text.AppendLine(authority.BuildDisplay(context));
            text.Append("Никакие дипломатические действия этой командой не выполняются.");return text.Length<=MaximumCharacters?text.ToString():text.ToString(0,MaximumCharacters);
        }
        private static bool SafeWar(IFaction a,IFaction b){try{return a!=null&&b!=null&&FactionManager.IsAtWarAgainstFaction(a,b);}catch{return false;}}
        private static string SafeName(TaleWorlds.Localization.TextObject name){try{string s=name==null?string.Empty:name.ToString();return string.IsNullOrWhiteSpace(s)?"без имени":s.Replace('\r',' ').Replace('\n',' ');}catch{return "без имени";}}
    }
}
