using System;
namespace AIPort.Server
{
    public sealed class PersistentSocialRecord
    {
        public string Id{get;set;}
        public string PlayerHeroId{get;set;}
        public string TargetInstanceId{get;set;}
        public int Delta{get;set;}
        public int BeforeValue{get;set;}
        public int AfterValue{get;set;}
        public DateTime OccurredUtc{get;set;}
    }
}
