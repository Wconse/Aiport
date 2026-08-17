using System;
using System.Collections.Generic;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
namespace AIPort.Server
{
    public static class AIPortStateCodec
    {
        public const int MaximumSnapshotCharacters=65536;
        public static string SerializeSnapshot(IList<PersistentMemoryRecord> records){return SerializeSnapshot(records,new List<PersistentSocialRecord>(),new List<PersistentDiplomaticStatementRecord>());}
        public static string SerializeSnapshot(IList<PersistentMemoryRecord> records,IList<PersistentSocialRecord> social){return SerializeSnapshot(records,social,new List<PersistentDiplomaticStatementRecord>());}
        public static string SerializeSnapshot(IList<PersistentMemoryRecord> records,IList<PersistentSocialRecord> social,IList<PersistentDiplomaticStatementRecord> diplomacy)
        {
            StringBuilder text=new StringBuilder(1024);text.Append("{\"schema\":1,\"records\":[");
            for(int i=0;i<records.Count;i++){if(i>0)text.Append(',');text.Append(SerializeRecord(records[i]));CheckSize(text);}
            text.Append("],\"socialRecords\":[");
            for(int i=0;i<social.Count;i++){if(i>0)text.Append(',');text.Append(SerializeSocialRecord(social[i]));CheckSize(text);}
            text.Append("],\"diplomacyStatements\":[");
            for(int i=0;i<diplomacy.Count;i++){if(i>0)text.Append(',');text.Append(SerializeDiplomaticRecord(diplomacy[i]));CheckSize(text);}
            text.Append("]}");CheckSize(text);return text.ToString();
        }
        public static string SerializeLines(IList<PersistentMemoryRecord> records){StringBuilder text=new StringBuilder();foreach(PersistentMemoryRecord r in records)text.AppendLine(SerializeRecord(r));return text.ToString();}
        public static string SerializeSocialLines(IList<PersistentSocialRecord> records){StringBuilder text=new StringBuilder();foreach(PersistentSocialRecord r in records)text.AppendLine(SerializeSocialRecord(r));return text.ToString();}
        public static string SerializeDiplomacyLines(IList<PersistentDiplomaticStatementRecord> records){StringBuilder text=new StringBuilder();foreach(PersistentDiplomaticStatementRecord r in records)text.AppendLine(SerializeDiplomaticRecord(r));return text.ToString();}
        public static string SerializeNativeDiplomacyJournalLines(IList<PersistentNativeDiplomacyCommitRecord> records){StringBuilder text=new StringBuilder();foreach(PersistentNativeDiplomacyCommitRecord r in records)text.AppendLine(SerializeNativeDiplomacyCommitRecord(r));return text.ToString();}
        public static List<PersistentMemoryRecord> ParseLines(string text)
        {
            List<PersistentMemoryRecord> result=new List<PersistentMemoryRecord>();string[] lines=(text??string.Empty).Split(new[]{'\r','\n'},StringSplitOptions.RemoveEmptyEntries);foreach(string line in lines){if(result.Count>=512)break;PersistentMemoryRecord r=ParseRecord(line);if(r!=null)result.Add(r);}return result;
        }
        public static List<PersistentSocialRecord> ParseSocialLines(string text)
        {
            List<PersistentSocialRecord> result=new List<PersistentSocialRecord>();string[] lines=(text??string.Empty).Split(new[]{'\r','\n'},StringSplitOptions.RemoveEmptyEntries);foreach(string line in lines){if(result.Count>=512)break;PersistentSocialRecord r=ParseSocialRecord(line);if(r!=null)result.Add(r);}return result;
        }
        public static List<PersistentDiplomaticStatementRecord> ParseDiplomacyLines(string text)
        {
            List<PersistentDiplomaticStatementRecord> result=new List<PersistentDiplomaticStatementRecord>();string[] lines=(text??string.Empty).Split(new[]{'\r','\n'},StringSplitOptions.RemoveEmptyEntries);foreach(string line in lines){if(result.Count>=256)break;PersistentDiplomaticStatementRecord r=ParseDiplomaticRecord(line);if(r!=null)result.Add(r);}return result;
        }
        public static List<PersistentNativeDiplomacyCommitRecord> ParseNativeDiplomacyJournalLines(string text){List<PersistentNativeDiplomacyCommitRecord> result=new List<PersistentNativeDiplomacyCommitRecord>();string[] lines=(text??string.Empty).Split(new[]{'\r','\n'},StringSplitOptions.RemoveEmptyEntries);foreach(string line in lines){if(result.Count>=256)break;PersistentNativeDiplomacyCommitRecord r=ParseNativeDiplomacyCommitRecord(line);if(r!=null)result.Add(r);}return result;}
        public static string Sha256(string value){using(SHA256 sha=SHA256.Create()){byte[] h=sha.ComputeHash(Encoding.UTF8.GetBytes(value??string.Empty));StringBuilder s=new StringBuilder(64);foreach(byte b in h)s.Append(b.ToString("x2",CultureInfo.InvariantCulture));return s.ToString();}}
        private static string SerializeRecord(PersistentMemoryRecord r){return "{\"id\":\""+B64(r.Id)+"\",\"player\":\""+B64(r.PlayerHeroId)+"\",\"target\":\""+B64(r.TargetInstanceId)+"\",\"playerText\":\""+B64(r.PlayerText)+"\",\"npcText\":\""+B64(r.NpcText)+"\",\"utc\":\""+r.OccurredUtc.ToUniversalTime().ToString("o",CultureInfo.InvariantCulture)+"\"}";}
        private static string SerializeSocialRecord(PersistentSocialRecord r){return "{\"id\":\""+B64(r.Id)+"\",\"player\":\""+B64(r.PlayerHeroId)+"\",\"target\":\""+B64(r.TargetInstanceId)+"\",\"delta\":\""+r.Delta.ToString(CultureInfo.InvariantCulture)+"\",\"before\":\""+r.BeforeValue.ToString(CultureInfo.InvariantCulture)+"\",\"after\":\""+r.AfterValue.ToString(CultureInfo.InvariantCulture)+"\",\"utc\":\""+r.OccurredUtc.ToUniversalTime().ToString("o",CultureInfo.InvariantCulture)+"\"}";}
        private static string SerializeDiplomaticRecord(PersistentDiplomaticStatementRecord r)
        {
            return "{\"id\":\""+B64(r.Id)+"\",\"player\":\""+B64(r.PlayerHeroId)+"\",\"targetHero\":\""+B64(r.TargetHeroId)
                +"\",\"sourceKingdom\":\""+B64(r.SourceKingdomId)+"\",\"targetKingdom\":\""+B64(r.TargetKingdomId)
                +"\",\"action\":\""+B64(r.Action)+"\",\"utc\":\""+r.OccurredUtc.ToUniversalTime().ToString("o",CultureInfo.InvariantCulture)
                +"\",\"status\":\""+B64(r.Status)+"\",\"expiresUtc\":\""+FormatOptionalDate(r.ExpiresUtc)
                +"\",\"resolvedUtc\":\""+FormatOptionalDate(r.ResolvedUtc)+"\",\"resolvedBy\":\""+B64(r.ResolvedByHeroId)
                +"\",\"lastReason\":\""+B64(r.LastReasonCode)+"\",\"nativeCommitUtc\":\""+FormatOptionalDate(r.NativeCommitUtc)
                +"\",\"nativeCommittedBy\":\""+B64(r.NativeCommittedByHeroId)+"\",\"nativeMutation\":\""+(r.NativeMutationApplied?"1":"0")
                +"\",\"origin\":\""+B64(r.Origin)+"\",\"initiativeReason\":\""+B64(r.InitiativeReasonCode)
                +"\",\"initiativeScore\":\""+r.InitiativeScore.ToString(CultureInfo.InvariantCulture)
                +"\",\"campaignDay\":\""+r.CampaignDay.ToString(CultureInfo.InvariantCulture)
                +"\",\"campaignHour\":\""+r.CampaignHour.ToString(CultureInfo.InvariantCulture)+"\"}";
        }
        private static string SerializeNativeDiplomacyCommitRecord(PersistentNativeDiplomacyCommitRecord r){return "{\"id\":\""+B64(r.Id)+"\",\"statementId\":\""+B64(r.StatementId)+"\",\"action\":\""+B64(r.Action)+"\",\"sourceHero\":\""+B64(r.SourceHeroId)+"\",\"targetHero\":\""+B64(r.TargetHeroId)+"\",\"sourceFaction\":\""+B64(r.SourceFactionId)+"\",\"targetFaction\":\""+B64(r.TargetFactionId)+"\",\"generation\":\""+B64(r.CampaignGeneration)+"\",\"preparedRevision\":\""+r.PreparedRevision.ToString(CultureInfo.InvariantCulture)+"\",\"phase\":\""+B64(r.Phase)+"\",\"preparedUtc\":\""+FormatOptionalDate(r.PreparedUtc)+"\",\"updatedUtc\":\""+FormatOptionalDate(r.UpdatedUtc)+"\",\"reason\":\""+B64(r.ReasonCode)+"\",\"nativeMutation\":\""+(r.NativeMutationApplied?"1":"0")+"\",\"atWarObserved\":\""+(r.AtWarObserved?"1":"0")+"\"}";}
        private static PersistentNativeDiplomacyCommitRecord ParseNativeDiplomacyCommitRecord(string line){try{string id=FromB64(Read(line,"id")),statement=FromB64(Read(line,"statementId")),action=FromB64(Read(line,"action")),sourceHero=FromB64(Read(line,"sourceHero")),targetHero=FromB64(Read(line,"targetHero")),sourceFaction=FromB64(Read(line,"sourceFaction")),targetFaction=FromB64(Read(line,"targetFaction")),generation=FromB64(Read(line,"generation")),phase=FromB64(Read(line,"phase")),reason=FromB64Optional(ReadOptional(line,"reason"));long revision;DateTime prepared=ParseOptionalDate(ReadOptional(line,"preparedUtc")),updated=ParseOptionalDate(ReadOptional(line,"updatedUtc"));if(id.Length!=32||statement.Length!=32||!long.TryParse(Read(line,"preparedRevision"),NumberStyles.None,CultureInfo.InvariantCulture,out revision)||prepared==DateTime.MinValue)return null;return new PersistentNativeDiplomacyCommitRecord{Id=Bound(id,32),StatementId=Bound(statement,32),Action=Bound(action,16),SourceHeroId=Bound(sourceHero,160),TargetHeroId=Bound(targetHero,160),SourceFactionId=Bound(sourceFaction,160),TargetFactionId=Bound(targetFaction,160),CampaignGeneration=Bound(generation,64),PreparedRevision=revision,Phase=Bound(phase,32),PreparedUtc=prepared,UpdatedUtc=updated==DateTime.MinValue?prepared:updated,ReasonCode=Bound(reason,96),NativeMutationApplied=string.Equals(ReadOptional(line,"nativeMutation"),"1",StringComparison.Ordinal),AtWarObserved=string.Equals(ReadOptional(line,"atWarObserved"),"1",StringComparison.Ordinal)};}catch{return null;}}
        private static PersistentMemoryRecord ParseRecord(string line)
        {
            try{string id=FromB64(Read(line,"id")),player=FromB64(Read(line,"player")),target=FromB64(Read(line,"target")),pt=FromB64(Read(line,"playerText")),nt=FromB64(Read(line,"npcText"));DateTime utc;if(id.Length!=32||player.Length==0||target.Length==0||!DateTime.TryParse(Read(line,"utc"),CultureInfo.InvariantCulture,DateTimeStyles.RoundtripKind,out utc))return null;return new PersistentMemoryRecord{Id=id,PlayerHeroId=Bound(player,160),TargetInstanceId=Bound(target,320),PlayerText=Bound(pt,3000),NpcText=Bound(nt,3000),OccurredUtc=utc.ToUniversalTime()};}catch{return null;}
        }
        private static PersistentSocialRecord ParseSocialRecord(string line)
        {
            try{string id=FromB64(Read(line,"id")),player=FromB64(Read(line,"player")),target=FromB64(Read(line,"target"));int delta,before,after;DateTime utc;if(id.Length!=32||player.Length==0||target.Length==0||!int.TryParse(Read(line,"delta"),NumberStyles.AllowLeadingSign,CultureInfo.InvariantCulture,out delta)||!int.TryParse(Read(line,"before"),NumberStyles.AllowLeadingSign,CultureInfo.InvariantCulture,out before)||!int.TryParse(Read(line,"after"),NumberStyles.AllowLeadingSign,CultureInfo.InvariantCulture,out after)||!DateTime.TryParse(Read(line,"utc"),CultureInfo.InvariantCulture,DateTimeStyles.RoundtripKind,out utc))return null;return new PersistentSocialRecord{Id=id,PlayerHeroId=Bound(player,160),TargetInstanceId=Bound(target,320),Delta=delta,BeforeValue=before,AfterValue=after,OccurredUtc=utc.ToUniversalTime()};}catch{return null;}
        }
        private static PersistentDiplomaticStatementRecord ParseDiplomaticRecord(string line)
        {
            try
            {
                string id=FromB64(Read(line,"id")),player=FromB64(Read(line,"player")),targetHero=FromB64(Read(line,"targetHero")),source=FromB64(Read(line,"sourceKingdom")),target=FromB64(Read(line,"targetKingdom")),action=FromB64(Read(line,"action")),status=FromB64Optional(ReadOptional(line,"status")),resolvedBy=FromB64Optional(ReadOptional(line,"resolvedBy")),lastReason=FromB64Optional(ReadOptional(line,"lastReason")),nativeBy=FromB64Optional(ReadOptional(line,"nativeCommittedBy")),origin=FromB64Optional(ReadOptional(line,"origin")),initiativeReason=FromB64Optional(ReadOptional(line,"initiativeReason"));
                DateTime utc,expires=ParseOptionalDate(ReadOptional(line,"expiresUtc")),resolved=ParseOptionalDate(ReadOptional(line,"resolvedUtc")),nativeUtc=ParseOptionalDate(ReadOptional(line,"nativeCommitUtc"));
                bool nativeApplied=string.Equals(ReadOptional(line,"nativeMutation"),"1",StringComparison.Ordinal);
                int initiativeScore=0;long campaignDay=-1,campaignHour=-1;
                int.TryParse(ReadOptional(line,"initiativeScore"),NumberStyles.AllowLeadingSign,CultureInfo.InvariantCulture,out initiativeScore);
                long parsedDay;if(long.TryParse(ReadOptional(line,"campaignDay"),NumberStyles.AllowLeadingSign,CultureInfo.InvariantCulture,out parsedDay))campaignDay=parsedDay;long parsedHour;if(long.TryParse(ReadOptional(line,"campaignHour"),NumberStyles.AllowLeadingSign,CultureInfo.InvariantCulture,out parsedHour))campaignHour=parsedHour;
                if(id.Length!=32||player.Length==0||targetHero.Length==0||source.Length==0||target.Length==0||!DateTime.TryParse(Read(line,"utc"),CultureInfo.InvariantCulture,DateTimeStyles.RoundtripKind,out utc))return null;
                if(status.Length==0)status="legacy_shadow_recorded";if(origin.Length==0)origin="legacy";
                return new PersistentDiplomaticStatementRecord{Id=id,PlayerHeroId=Bound(player,160),TargetHeroId=Bound(targetHero,160),SourceKingdomId=Bound(source,160),TargetKingdomId=Bound(target,160),Action=Bound(action,16),OccurredUtc=utc.ToUniversalTime(),Status=Bound(status,32),ExpiresUtc=expires,ResolvedUtc=resolved,ResolvedByHeroId=Bound(resolvedBy,160),LastReasonCode=Bound(lastReason,64),NativeCommitUtc=nativeUtc,NativeCommittedByHeroId=Bound(nativeBy,160),NativeMutationApplied=nativeApplied,Origin=Bound(origin,32),InitiativeReasonCode=Bound(initiativeReason,64),InitiativeScore=Math.Max(-1000,Math.Min(1000,initiativeScore)),CampaignDay=campaignDay,CampaignHour=campaignHour};
            }
            catch{return null;}
        }
        private static void CheckSize(StringBuilder text){if(text.Length>MaximumSnapshotCharacters)throw new InvalidOperationException("snapshot_too_large");}
        private static string Read(string json,string key){string value=ReadOptional(json,key);if(value==null)throw new FormatException();return value;}
        private static string ReadOptional(string json,string key){string n="\""+key+"\":\"";int i=json.IndexOf(n,StringComparison.Ordinal);if(i<0)return null;i+=n.Length;int e=json.IndexOf('\"',i);return e<0?null:json.Substring(i,e-i);}
        private static string FormatOptionalDate(DateTime value){return value==DateTime.MinValue?string.Empty:value.ToUniversalTime().ToString("o",CultureInfo.InvariantCulture);}
        private static DateTime ParseOptionalDate(string value){DateTime parsed;return !string.IsNullOrWhiteSpace(value)&&DateTime.TryParse(value,CultureInfo.InvariantCulture,DateTimeStyles.RoundtripKind,out parsed)?parsed.ToUniversalTime():DateTime.MinValue;}
        private static string B64(string s){return Convert.ToBase64String(Encoding.UTF8.GetBytes(s??string.Empty));}
        private static string FromB64(string s){return Encoding.UTF8.GetString(Convert.FromBase64String(s??string.Empty));}
        private static string FromB64Optional(string s){return string.IsNullOrEmpty(s)?string.Empty:FromB64(s);}
        private static string Bound(string s,int n){s=(s??string.Empty).Trim();return s.Length<=n?s:s.Substring(0,n);}
    }
}
