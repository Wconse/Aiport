using System;
using System.Collections.Generic;

namespace AIPort.Server
{
    public enum AIActionKind
    {
        None = 0,
        ChangeRelation = 1,
        TransferGold = 2,
        TransferItem = 3,
        DeclareWar = 4,
        MakePeace = 5,
        CreateQuest = 6,
        MoveParty = 7
    }

    public sealed class AIActionProposal
    {
        public AIActionKind Kind { get; }
        public string ActorTargetInstanceId { get; }
        public string SubjectId { get; }
        public string ObjectId { get; }
        public int Amount { get; }

        public AIActionProposal(AIActionKind kind, string actorTargetInstanceId, string subjectId, string objectId, int amount)
        {
            Kind = kind;
            ActorTargetInstanceId = actorTargetInstanceId ?? string.Empty;
            SubjectId = subjectId ?? string.Empty;
            ObjectId = objectId ?? string.Empty;
            Amount = amount;
        }
    }

    public sealed class AIActionDecision
    {
        public bool Authorized { get; }
        public string Reason { get; }
        public AIActionDecision(bool authorized, string reason) { Authorized = authorized; Reason = reason ?? string.Empty; }
    }

    public sealed class AIActionGate
    {
        private static readonly HashSet<AIActionKind> KnownKinds = new HashSet<AIActionKind>
        {
            AIActionKind.ChangeRelation, AIActionKind.TransferGold, AIActionKind.TransferItem,
            AIActionKind.DeclareWar, AIActionKind.MakePeace, AIActionKind.CreateQuest, AIActionKind.MoveParty
        };

        // 0.0.38 deliberately installs the authorization boundary without enabling execution.
        public bool NarrativeOnly { get { return true; } }

        public AIActionDecision Evaluate(AIActionProposal proposal, ConversationTargetBinding binding)
        {
            if (proposal == null || !KnownKinds.Contains(proposal.Kind)) return new AIActionDecision(false, "unknown_action");
            if (binding == null || !string.Equals(proposal.ActorTargetInstanceId, binding.TargetInstanceId, StringComparison.Ordinal))
                return new AIActionDecision(false, "actor_not_bound");
            return new AIActionDecision(false, "narrative_only");
        }
    }
}
