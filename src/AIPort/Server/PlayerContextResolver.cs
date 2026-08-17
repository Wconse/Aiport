using System.Text;
using Coop.Core.Server.Connections;
using Coop.Core.Server.Connections.States;
using GameInterface.Services.Players;
using GameInterface.Services.Players.Data;
using GameInterface.Services.ObjectManager;
using LiteNetLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;

namespace AIPort.Server
{
    public sealed class PlayerContextResolver
    {
        private readonly IPlayerManager players;
        private readonly IConnectionCollection connections;
        private readonly AuthoritativePlayerSessionRegistry sessions;
        private readonly IObjectManager objects;

        public PlayerContextResolver(IPlayerManager players)
            : this(players, null, null, null)
        {
        }

        public PlayerContextResolver(IPlayerManager players, IConnectionCollection connections, AuthoritativePlayerSessionRegistry sessions)
            : this(players, connections, sessions, null)
        {
        }

        public PlayerContextResolver(IPlayerManager players, IConnectionCollection connections, AuthoritativePlayerSessionRegistry sessions, IObjectManager objects)
        {
            this.players = players;
            this.connections = connections;
            this.sessions = sessions;
            this.objects = objects;
        }

        public bool TryResolve(NetPeer peer, out Player player)
        {
            string ignored;
            return TryResolve(peer, out player, out ignored);
        }

        public bool TryResolve(NetPeer peer, out Player player, out string failure)
        {
            player = null;
            failure = string.Empty;
            if (peer == null || players == null)
            {
                failure = "peer_or_player_manager_missing";
                return false;
            }

            if (players.TryGetPlayer(peer, out player) && IsUsable(player)) return true;

            if (peer.ConnectionState != ConnectionState.Connected)
            {
                failure = "message_peer_not_connected";
                player = null;
                return false;
            }

            string controllerId;
            if (sessions == null || !sessions.TryGetControllerId(peer.Id, out controllerId))
            {
                failure = "accepted_join_identity_missing";
                player = null;
                return false;
            }

            if (!HasOneAuthoritativeWorldConnection(peer.Id, out failure))
            {
                player = null;
                return false;
            }

            if (!players.TryGetPlayer(controllerId, out player))
            {
                failure = "controller_not_registered";
                player = null;
                return false;
            }
            if (!IsUsable(player))
            {
                failure = "registered_player_incomplete";
                player = null;
                return false;
            }
            if (!string.Equals(player.ControllerId, controllerId, System.StringComparison.Ordinal))
            {
                failure = "controller_identity_mismatch";
                player = null;
                return false;
            }
            return true;
        }

        public bool TryResolveControlledCampaignObjects(Player player, out Hero hero, out MobileParty mobileParty, out string failure)
        {
            hero = null;
            mobileParty = null;
            failure = string.Empty;
            if (player == null || players == null || objects == null)
            {
                failure = "controlled_object_services_missing";
                return false;
            }
            if (string.IsNullOrWhiteSpace(player.HeroId)
                || !objects.TryGetObject<Hero>(player.HeroId, out hero)
                || hero == null)
            {
                failure = "controlled_player_hero_not_resolved:" + (player.HeroId ?? string.Empty);
                hero = null;
                return false;
            }
            if (!players.Contains(hero))
            {
                failure = "resolved_player_hero_not_controlled:" + player.HeroId;
                hero = null;
                return false;
            }
            if (!string.IsNullOrWhiteSpace(player.MobilePartyId))
            {
                if (!objects.TryGetObject<MobileParty>(player.MobilePartyId, out mobileParty) || mobileParty == null)
                {
                    failure = "controlled_player_party_not_resolved:" + player.MobilePartyId;
                    hero = null;
                    mobileParty = null;
                    return false;
                }
                if (!players.Contains(mobileParty))
                {
                    failure = "resolved_player_party_not_controlled:" + player.MobilePartyId;
                    hero = null;
                    mobileParty = null;
                    return false;
                }
            }
            return true;
        }

        public bool TryResolveCampaignHero(string heroId, out Hero hero)
        {
            hero = null;
            if (string.IsNullOrWhiteSpace(heroId)) return false;
            if (objects != null && objects.TryGetObject<Hero>(heroId, out hero) && hero != null) return true;
            try { hero = Hero.Find(heroId); } catch { hero = null; }
            return hero != null;
        }

        private bool HasOneAuthoritativeWorldConnection(int peerId, out string failure)
        {
            failure = string.Empty;
            if (connections == null)
            {
                failure = "connection_collection_missing";
                return false;
            }
            int idMatches = 0;
            int readyMatches = 0;
            StringBuilder states = new StringBuilder();
            foreach (IConnectionLogic connection in connections)
            {
                if (connection == null || connection.Peer == null || connection.Peer.Id != peerId) continue;
                idMatches++;
                if (states.Length > 0) states.Append(',');
                states.Append(connection.State == null ? "null" : connection.State.GetType().Name);
                if (connection.Peer.ConnectionState == ConnectionState.Connected
                    && (connection.State is CampaignState || connection.State is MissionState)) readyMatches++;
            }
            if (readyMatches == 1) return true;
            failure = "authoritative_connection_not_ready:id_matches=" + idMatches
                + ":ready_matches=" + readyMatches + ":states=" + states;
            return false;
        }

        private static bool IsUsable(Player player)
        {
            return player != null
                && !string.IsNullOrWhiteSpace(player.ControllerId)
                && !string.IsNullOrWhiteSpace(player.HeroId);
        }
    }
}
