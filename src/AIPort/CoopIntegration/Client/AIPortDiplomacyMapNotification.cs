using System;
using System.Collections.Generic;
using System.Globalization;
using SandBox.View.Map;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.ViewModelCollection.Map.MapNotificationTypes;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TaleWorlds.ScreenSystem;

namespace Coop.Core.Client.Services.AIPort.Handlers
{
    public sealed class AIPortDiplomacyMapNotification : InformationData
    {
        public string StatementId { get; private set; }
        public string ActionKind { get; private set; }
        public string SourceHeroId { get; private set; }
        public string SourceHeroName { get; private set; }
        public string SourceFactionId { get; private set; }
        public string SourceFactionName { get; private set; }
        public string TargetFactionId { get; private set; }
        public string TargetFactionName { get; private set; }
        public string ExpiresUtc { get; private set; }
        public string Origin { get; private set; }
        public string InitiativeReasonCode { get; private set; }
        public int InitiativeScore { get; private set; }
        public int PendingCount { get; private set; }

        public override TextObject TitleText
        { get { return new TextObject(ActionKind == "war" ? "{=aiport_war_offer}War proposal" : "{=aiport_peace_offer}Peace proposal"); } }
        public override string SoundEventPath { get { return "event:/ui/notification/peace_offer"; } }

        public AIPortDiplomacyMapNotification(string statementId,string actionKind,string sourceHeroId,string sourceFactionId,string targetFactionId,string expiresUtc,int pendingCount)
            : this(statementId,actionKind,sourceHeroId,string.Empty,sourceFactionId,string.Empty,targetFactionId,string.Empty,expiresUtc,"notification",string.Empty,0,pendingCount) {}

        public AIPortDiplomacyMapNotification(string statementId,string actionKind,string sourceHeroId,string sourceHeroName,string sourceFactionId,string sourceFactionName,string targetFactionId,string targetFactionName,string expiresUtc,string origin,string initiativeReasonCode,int initiativeScore,int pendingCount)
            : base(new TextObject(BuildDescription(actionKind,Display(sourceHeroName,sourceHeroId),Display(sourceFactionName,sourceFactionId),Display(targetFactionName,targetFactionId),pendingCount)))
        {
            StatementId=statementId??string.Empty;ActionKind=actionKind??string.Empty;SourceHeroId=sourceHeroId??string.Empty;SourceHeroName=sourceHeroName??string.Empty;SourceFactionId=sourceFactionId??string.Empty;SourceFactionName=sourceFactionName??string.Empty;TargetFactionId=targetFactionId??string.Empty;TargetFactionName=targetFactionName??string.Empty;ExpiresUtc=expiresUtc??string.Empty;Origin=origin??string.Empty;InitiativeReasonCode=initiativeReasonCode??string.Empty;InitiativeScore=initiativeScore;PendingCount=pendingCount;
        }

        public string BuildInquiryText()
        {
            string sourceName=Display(SourceHeroName,SourceHeroId);if(string.IsNullOrWhiteSpace(SourceHeroName))try{Hero hero=Hero.Find(SourceHeroId);if(hero!=null&&hero.Name!=null)sourceName=hero.Name.ToString();}catch{}
            string actionText=ActionKind=="war"?"declare war":"make peace",expiry=ExpiresUtc;DateTime parsed;if(DateTime.TryParse(ExpiresUtc,CultureInfo.InvariantCulture,DateTimeStyles.RoundtripKind,out parsed))expiry=parsed.ToLocalTime().ToString("g",CultureInfo.CurrentCulture);
            string audit=string.Equals(Origin,"npc_scheduler",StringComparison.Ordinal)?"\nNPC initiative: "+InitiativeReasonCode+" (score "+InitiativeScore.ToString(CultureInfo.InvariantCulture)+")":string.Empty;
            return "From: "+sourceName+"\nFactions: "+Display(SourceFactionName,SourceFactionId)+" -> "+Display(TargetFactionName,TargetFactionId)+"\nProposal: "+actionText+"\nExpires: "+expiry+audit+"\n\nAccept or reject this shadow diplomatic proposal? This UI does not invoke Bannerlord's native war or peace callbacks.";
        }

        private static string BuildDescription(string actionKind,string sourceHero,string sourceFaction,string targetFaction,int pendingCount)
        {string actionText=actionKind=="war"?"war":"peace",suffix=pendingCount>1?" ("+pendingCount.ToString(CultureInfo.InvariantCulture)+" pending)":string.Empty;return "Incoming "+actionText+" proposal from "+sourceHero+" / "+sourceFaction+" to "+targetFaction+suffix+".";}
        private static string Display(string preferred,string fallback){return string.IsNullOrWhiteSpace(preferred)?(fallback??string.Empty):preferred;}
    }

    public sealed class AIPortDiplomacyMapNotificationItemVM : MapNotificationItemBaseVM
    {
        private readonly AIPortDiplomacyMapNotification notice;private bool submitting;
        public AIPortDiplomacyMapNotificationItemVM(AIPortDiplomacyMapNotification data) : base(data){notice=data;NotificationIdentifier="ransom";_onInspect=ShowDecisionInquiry;AIPortDiplomacyMapNotificationRegistrar.Track(this,data.StatementId);}
        private void ShowDecisionInquiry()
        {
            if(notice==null)return;if(submitting){InformationManager.DisplayMessage(new InformationMessage("AIPort is waiting for the authoritative server decision."));return;}
            InquiryData inquiry=new InquiryData(notice.TitleText.ToString(),notice.BuildInquiryText(),true,true,"Accept","Reject",delegate{SubmitDecision("accept");},delegate{SubmitDecision("reject");},notice.SoundEventPath);InformationManager.ShowInquiry(inquiry,false,true);
        }
        private void SubmitDecision(string decision)
        {
            if(submitting)return;submitting=true;if(!AIPortDiplomacyDecisionBridge.TrySubmit(notice.StatementId,decision)){submitting=false;InformationManager.DisplayMessage(new InformationMessage("AIPort cannot submit this decision right now."));}
        }
        internal void ReleaseSubmission(){submitting=false;}
        public override void OnFinalize(){AIPortDiplomacyMapNotificationRegistrar.Untrack(this);base.OnFinalize();}
    }

    public static class AIPortDiplomacyDecisionBridge
    {
        private static readonly object Gate=new object();private static Func<string,string,bool> submit;
        public static void Attach(Func<string,string,bool> callback){lock(Gate)submit=callback;}
        public static void Detach(Func<string,string,bool> callback){lock(Gate)if(submit==callback)submit=null;}
        public static bool TrySubmit(string statementId,string decision){Func<string,string,bool> callback;lock(Gate)callback=submit;return callback!=null&&callback(statementId,decision);}
    }

    public static class AIPortDiplomacyMapNotificationRegistrar
    {
        private static readonly object Gate=new object();
        private static readonly Dictionary<AIPortDiplomacyMapNotificationItemVM,string> Items=new Dictionary<AIPortDiplomacyMapNotificationItemVM,string>();
        private static readonly Dictionary<string,AIPortDiplomacyMapNotification> Desired=new Dictionary<string,AIPortDiplomacyMapNotification>(StringComparer.Ordinal);
        private static readonly HashSet<string> PublishedStatementIds=new HashSet<string>(StringComparer.Ordinal);
        private static bool initialized;

        public static void Initialize(){lock(Gate){if(initialized)return;initialized=true;ScreenManager.OnPushScreen+=OnPushScreen;}RegisterCurrent();}
        public static bool Publish(AIPortDiplomacyMapNotification notice){if(notice==null||string.IsNullOrWhiteSpace(notice.StatementId))return false;Initialize();lock(Gate)Desired[notice.StatementId]=notice;return PublishDesired();}
        public static bool Reconcile(IList<AIPortDiplomacyMapNotification> notices)
        {
            Initialize();Dictionary<string,AIPortDiplomacyMapNotification> next=new Dictionary<string,AIPortDiplomacyMapNotification>(StringComparer.Ordinal);if(notices!=null)foreach(AIPortDiplomacyMapNotification notice in notices)if(notice!=null&&!string.IsNullOrWhiteSpace(notice.StatementId))next[notice.StatementId]=notice;
            List<AIPortDiplomacyMapNotificationItemVM> remove=new List<AIPortDiplomacyMapNotificationItemVM>();lock(Gate){foreach(KeyValuePair<AIPortDiplomacyMapNotificationItemVM,string> item in Items)if(!next.ContainsKey(item.Value))remove.Add(item.Key);Desired.Clear();foreach(KeyValuePair<string,AIPortDiplomacyMapNotification> pair in next)Desired[pair.Key]=pair.Value;PublishedStatementIds.RemoveWhere(delegate(string id){return !next.ContainsKey(id);});}
            foreach(AIPortDiplomacyMapNotificationItemVM item in remove)try{item.ExecuteRemove();}catch{}return PublishDesired();
        }
        public static void Dismiss(string statementId)
        {
            if(string.IsNullOrWhiteSpace(statementId))return;List<AIPortDiplomacyMapNotificationItemVM> copy=new List<AIPortDiplomacyMapNotificationItemVM>();lock(Gate){Desired.Remove(statementId);PublishedStatementIds.Remove(statementId);foreach(KeyValuePair<AIPortDiplomacyMapNotificationItemVM,string> pair in Items)if(string.Equals(pair.Value,statementId,StringComparison.Ordinal))copy.Add(pair.Key);}foreach(AIPortDiplomacyMapNotificationItemVM item in copy)try{item.ExecuteRemove();}catch{}
        }
        public static void ReleaseDecision(string statementId){if(string.IsNullOrWhiteSpace(statementId))return;List<AIPortDiplomacyMapNotificationItemVM> copy=new List<AIPortDiplomacyMapNotificationItemVM>();lock(Gate)foreach(KeyValuePair<AIPortDiplomacyMapNotificationItemVM,string> pair in Items)if(string.Equals(pair.Value,statementId,StringComparison.Ordinal))copy.Add(pair.Key);foreach(AIPortDiplomacyMapNotificationItemVM item in copy)try{item.ReleaseSubmission();}catch{}}
        public static void DismissAll(){List<AIPortDiplomacyMapNotificationItemVM> copy;lock(Gate){copy=new List<AIPortDiplomacyMapNotificationItemVM>(Items.Keys);Desired.Clear();PublishedStatementIds.Clear();}foreach(AIPortDiplomacyMapNotificationItemVM item in copy)try{item.ExecuteRemove();}catch{}}
        internal static void Track(AIPortDiplomacyMapNotificationItemVM item,string statementId){if(item==null)return;lock(Gate){Items[item]=statementId??string.Empty;if(!string.IsNullOrWhiteSpace(statementId))PublishedStatementIds.Add(statementId);}}
        internal static void Untrack(AIPortDiplomacyMapNotificationItemVM item){if(item==null)return;lock(Gate){string id;if(!Items.TryGetValue(item,out id))return;Items.Remove(item);bool stillTracked=false;foreach(string value in Items.Values)if(string.Equals(value,id,StringComparison.Ordinal)){stillTracked=true;break;}if(!stillTracked)PublishedStatementIds.Remove(id);}}
        private static void OnPushScreen(ScreenBase screen){MapScreen map=screen as MapScreen;if(map==null||map.MapNotificationView==null)return;map.MapNotificationView.RegisterMapNotificationType(typeof(AIPortDiplomacyMapNotification),typeof(AIPortDiplomacyMapNotificationItemVM));PublishDesired();}
        private static void RegisterCurrent(){MapScreen map=MapScreen.Instance;if(map!=null&&map.MapNotificationView!=null)map.MapNotificationView.RegisterMapNotificationType(typeof(AIPortDiplomacyMapNotification),typeof(AIPortDiplomacyMapNotificationItemVM));}
        private static bool PublishDesired()
        {
            MapScreen screen=MapScreen.Instance;if(screen==null||screen.MapNotificationView==null)return false;screen.MapNotificationView.RegisterMapNotificationType(typeof(AIPortDiplomacyMapNotification),typeof(AIPortDiplomacyMapNotificationItemVM));List<AIPortDiplomacyMapNotification> publish=new List<AIPortDiplomacyMapNotification>();lock(Gate)foreach(KeyValuePair<string,AIPortDiplomacyMapNotification> pair in Desired)if(!PublishedStatementIds.Contains(pair.Key)){PublishedStatementIds.Add(pair.Key);publish.Add(pair.Value);}bool success=true;foreach(AIPortDiplomacyMapNotification notice in publish)try{MBInformationManager.AddNotice(notice);}catch{success=false;lock(Gate)PublishedStatementIds.Remove(notice.StatementId);}return success;
        }
    }
}
