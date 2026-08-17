using System;
using System.Collections.Generic;

namespace AIPort.Server
{
    // Tracks the controller identity presented during Coop's own join handshake.
    // Every mutation is bound to the exact connection token for the current peer-id generation.
    public sealed class AuthoritativePlayerSessionRegistry
    {
        private sealed class Entry
        {
            public object ConnectionToken;
            public string ControllerId;
            public long Generation;
        }

        private readonly object gate = new object();
        private readonly Dictionary<int, Entry> entries = new Dictionary<int, Entry>();
        private long nextGeneration;

        public long Connect(int peerId, object connectionToken)
        {
            bool ignored;
            return Connect(peerId, connectionToken, out ignored);
        }

        public long Connect(int peerId, object connectionToken, out bool newGeneration)
        {
            newGeneration = false;
            if (peerId < 0 || connectionToken == null) return 0;
            lock (gate)
            {
                Entry current;
                if (entries.TryGetValue(peerId, out current)
                    && ReferenceEquals(current.ConnectionToken, connectionToken)) return current.Generation;
                long generation = ++nextGeneration;
                entries[peerId] = new Entry
                {
                    ConnectionToken = connectionToken,
                    ControllerId = null,
                    Generation = generation
                };
                newGeneration = true;
                return generation;
            }
        }

        public bool TryObserveJoinIdentity(int peerId, object connectionToken, string controllerId, out bool conflict)
        {
            conflict = false;
            if (peerId < 0 || connectionToken == null || string.IsNullOrWhiteSpace(controllerId)) return false;
            string normalized = controllerId.Trim();
            lock (gate)
            {
                Entry entry;
                if (!entries.TryGetValue(peerId, out entry)
                    || !ReferenceEquals(entry.ConnectionToken, connectionToken)) return false;
                if (string.IsNullOrEmpty(entry.ControllerId))
                {
                    entry.ControllerId = normalized;
                    return true;
                }
                if (string.Equals(entry.ControllerId, normalized, StringComparison.Ordinal)) return true;
                conflict = true;
                return false;
            }
        }

        public bool TryGetControllerId(int peerId, out string controllerId)
        {
            controllerId = null;
            lock (gate)
            {
                Entry entry;
                if (!entries.TryGetValue(peerId, out entry) || string.IsNullOrEmpty(entry.ControllerId)) return false;
                controllerId = entry.ControllerId;
                return true;
            }
        }

        public bool Disconnect(int peerId, object connectionToken)
        {
            if (peerId < 0 || connectionToken == null) return false;
            lock (gate)
            {
                Entry entry;
                if (!entries.TryGetValue(peerId, out entry)
                    || !ReferenceEquals(entry.ConnectionToken, connectionToken)) return false;
                return entries.Remove(peerId);
            }
        }

        public void Clear()
        {
            lock (gate) entries.Clear();
        }
    }
}
