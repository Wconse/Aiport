using System;
using System.Collections.Generic;

namespace AIPort.Server
{
    public sealed class ConversationTargetBinding
    {
        public int PeerId { get; }
        public string PlayerHeroId { get; }
        public string ConversationId { get; }
        public string TargetLeaseId { get; }
        public string TargetId { get; }
        public string TargetInstanceId { get; }
        public string AuthoritativeLocationId { get; }
        public bool IsHero { get; }
        public DateTime BoundUtc { get; }

        public ConversationTargetBinding(int peerId, string playerHeroId, string conversationId, string targetLeaseId, string targetId, string targetInstanceId, string authoritativeLocationId, bool isHero)
        {
            PeerId = peerId;
            PlayerHeroId = playerHeroId ?? string.Empty;
            ConversationId = conversationId ?? string.Empty;
            TargetLeaseId = targetLeaseId ?? string.Empty;
            TargetId = targetId ?? string.Empty;
            TargetInstanceId = targetInstanceId ?? string.Empty;
            AuthoritativeLocationId = authoritativeLocationId ?? string.Empty;
            IsHero = isHero;
            BoundUtc = DateTime.UtcNow;
        }
    }

    public sealed class ConversationTargetLeaseRegistry
    {
        private readonly object gate = new object();
        private readonly Dictionary<int, ConversationTargetBinding> byPeer = new Dictionary<int, ConversationTargetBinding>();

        public ConversationTargetBinding Bind(int peerId, string playerHeroId, string conversationId, string targetId, string targetInstanceId, string authoritativeLocationId, bool isHero)
        {
            lock (gate)
            {
                ConversationTargetBinding replaced;
                byPeer.TryGetValue(peerId, out replaced);
                byPeer[peerId] = new ConversationTargetBinding(peerId, playerHeroId, conversationId, Guid.NewGuid().ToString("N"), targetId, targetInstanceId, authoritativeLocationId, isHero);
                return replaced;
            }
        }

        public bool TryGet(int peerId, out ConversationTargetBinding binding)
        {
            lock (gate) return byPeer.TryGetValue(peerId, out binding);
        }

        public bool TryAuthorizeRequest(int peerId, string conversationId, string targetLeaseId, string claimedTargetId, string claimedTargetInstanceId, out ConversationTargetBinding binding, out string errorCode)
        {
            lock (gate)
            {
                if (!byPeer.TryGetValue(peerId, out binding))
                {
                    errorCode = "target_not_bound";
                    return false;
                }
                if (!string.Equals(binding.ConversationId, conversationId, StringComparison.Ordinal)
                    || !string.Equals(binding.TargetLeaseId, targetLeaseId, StringComparison.Ordinal))
                {
                    errorCode = "stale_target";
                    return false;
                }
                if (!string.Equals(binding.TargetId, claimedTargetId, StringComparison.Ordinal)
                    || !string.Equals(binding.TargetInstanceId, claimedTargetInstanceId, StringComparison.Ordinal))
                {
                    errorCode = "target_mismatch";
                    return false;
                }
                errorCode = string.Empty;
                return true;
            }
        }

        public bool Close(int peerId, string conversationId, string targetLeaseId, out ConversationTargetBinding binding)
        {
            lock (gate)
            {
                if (!byPeer.TryGetValue(peerId, out binding)) return false;
                if (!string.Equals(binding.ConversationId, conversationId, StringComparison.Ordinal)
                    || !string.Equals(binding.TargetLeaseId, targetLeaseId, StringComparison.Ordinal)) return false;
                byPeer.Remove(peerId);
                return true;
            }
        }

        public bool RemovePeer(int peerId, out ConversationTargetBinding binding)
        {
            lock (gate)
            {
                if (!byPeer.TryGetValue(peerId, out binding)) return false;
                byPeer.Remove(peerId);
                return true;
            }
        }

        public void ClearAll()
        {
            lock (gate) byPeer.Clear();
        }
    }
}
