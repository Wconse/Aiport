using Common.Messaging;
using ProtoBuf;

namespace AIPort.Protocol.Messages
{
    [ProtoContract(SkipConstructor = true)]
    public sealed class AIConversationResult : IEvent, IMessage
    {
        [ProtoMember(1)] public string RequestId { get; }
        [ProtoMember(2)] public string ConversationId { get; }
        [ProtoMember(3, IsRequired = true)] public long ServerSequence { get; }
        [ProtoMember(4)] public string SpeakerHeroId { get; }
        [ProtoMember(5)] public string DisplayText { get; }
        [ProtoMember(6)] public string[] ActionSummaries { get; }
        [ProtoMember(7, IsRequired = true)] public bool Completed { get; }
        // Added in 0.0.38. Field 4 remains for wire compatibility.
        [ProtoMember(8)] public string SpeakerTargetInstanceId { get; }
        // Added in 0.0.52. Carries the authoritative memory revision after this turn.
        [ProtoMember(9)] public long StateRevision { get; }

        public string SpeakerTargetId { get { return SpeakerHeroId; } }

        public AIConversationResult(string requestId, string conversationId, long serverSequence, string speakerHeroId, string displayText, string[] actionSummaries, bool completed)
            : this(requestId, conversationId, serverSequence, speakerHeroId, displayText, actionSummaries, completed, string.Empty, 0)
        {
        }

        public AIConversationResult(string requestId, string conversationId, long serverSequence, string speakerTargetId, string displayText, string[] actionSummaries, bool completed, string speakerTargetInstanceId)
            : this(requestId, conversationId, serverSequence, speakerTargetId, displayText, actionSummaries, completed, speakerTargetInstanceId, 0)
        {
        }

        public AIConversationResult(string requestId, string conversationId, long serverSequence, string speakerTargetId, string displayText, string[] actionSummaries, bool completed, string speakerTargetInstanceId, long stateRevision)
        {
            RequestId = requestId;
            ConversationId = conversationId;
            ServerSequence = serverSequence;
            SpeakerHeroId = speakerTargetId;
            DisplayText = displayText;
            ActionSummaries = actionSummaries;
            Completed = completed;
            SpeakerTargetInstanceId = speakerTargetInstanceId;
            StateRevision = stateRevision;
        }
    }
}
