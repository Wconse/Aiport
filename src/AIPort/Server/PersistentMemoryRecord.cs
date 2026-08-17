using System;
namespace AIPort.Server { public sealed class PersistentMemoryRecord { public string Id{get;set;} public string PlayerHeroId{get;set;} public string TargetInstanceId{get;set;} public string PlayerText{get;set;} public string NpcText{get;set;} public DateTime OccurredUtc{get;set;} } }
