using System;
using System.Collections.Generic;
using System.Net;
using System.Threading;
using AIPort.Protocol;
using AIPort.Protocol.Messages;
using AIPort.Server;
using Common.Logging;
using Common.Messaging;
using Common.Network;
using Common.Network.Messages;
using Coop.Core.Server.Connections;
using Coop.Core.Server.Connections.Messages;
using GameInterface.Services.GameState.Messages;
using GameInterface.Services.Heroes.Messages;
using GameInterface.Services.Save.Messages;
using GameInterface.Registry.Messages;
using GameInterface.CoopSessionData;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players;
using GameInterface.Services.Players.Data;
using LiteNetLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using Serilog;

namespace Coop.Core.Server.Services.AIPort.Handlers
{
    internal sealed class AIPortConversationServerHandler : IHandler, IDisposable
    {
        private static readonly ILogger Logger = LogManager.GetLogger<AIPortConversationServerHandler>();
        private readonly IMessageBroker messageBroker;
        private readonly INetwork network;
        private readonly PlayerContextResolver resolver;
        private readonly AuthoritativePlayerSessionRegistry playerSessions = new AuthoritativePlayerSessionRegistry();
        private readonly AIPortServerSettings settings;
        private readonly PromptService prompts = new PromptService();
        private readonly AIPortBackendRouter backend = new AIPortBackendRouter();
        private readonly ConversationMemory memory = new ConversationMemory();
        private readonly ConversationTargetLeaseRegistry targetLeases = new ConversationTargetLeaseRegistry();
        private readonly AIActionGate actionGate = new AIActionGate();
        private readonly IntentCoordinator intents = new IntentCoordinator();
        private readonly SocialShadowLedger socialLedger = new SocialShadowLedger();
        private readonly DiplomacySnapshotService diplomacySnapshots = new DiplomacySnapshotService();
        private readonly DiplomacyAuthorityService diplomacyAuthority = new DiplomacyAuthorityService();
        private readonly DiplomaticStatementLedger diplomaticStatements = new DiplomaticStatementLedger();
        private readonly DiplomacyStatementCoordinator diplomacyCoordinator = new DiplomacyStatementCoordinator();
        private readonly RuntimeValidationGate validationGate = new RuntimeValidationGate();
        private readonly NativeWarAdapter nativeWarAdapter = new NativeWarAdapter();
        private readonly NativePeaceAdapter nativePeaceAdapter = new NativePeaceAdapter();
        private readonly NativeDiplomacyCommitJournal nativeDiplomacyJournal = new NativeDiplomacyCommitJournal();
        private readonly NpcDiplomacyDecisionPolicy npcDiplomacyPolicy = new NpcDiplomacyDecisionPolicy();
        private readonly NpcDiplomacyInitiativeScheduler npcDiplomacyInitiativeScheduler = new NpcDiplomacyInitiativeScheduler();
        private readonly NativeWarCommitLeaseRegistry nativeWarCommitLeases = new NativeWarCommitLeaseRegistry();
        private readonly ICoopSessionProvider coopSessionProvider;
        private readonly AIPortStateStore stateStore;
        private string pendingLoadedSaveName = string.Empty;
        private readonly object gate = new object();
        private readonly Dictionary<string, Queue<DateTime>> recent = new Dictionary<string, Queue<DateTime>>();
        private const int MaximumRememberedRequestIds = 8192;
        private const int MaximumBackendWorkers = 4;
        private readonly Dictionary<string, ActiveBackendRequest> activeBackendRequests = new Dictionary<string, ActiveBackendRequest>(StringComparer.Ordinal);
        private readonly HashSet<string> seenRequestIds = new HashSet<string>(StringComparer.Ordinal);
        private readonly Dictionary<int,NetPeer> connectedPeers = new Dictionary<int,NetPeer>();
        private readonly Dictionary<int,string> connectedHeroIds = new Dictionary<int,string>();
        private readonly Queue<string> seenRequestOrder = new Queue<string>();
        private int inflight;
        private int backendWorkers;
        private bool campaignReady;
        private bool hourlyMaintenanceAttached;
        private long lastNpcInitiativeCampaignHour = long.MinValue;

        private sealed class ActiveBackendRequest
        {
            public int PeerId;
            public string ConversationId;
            public HttpWebRequest Request;
            public bool Canceled;
            public bool InflightReleased;
        }

        public AIPortConversationServerHandler(IMessageBroker messageBroker, INetwork network, IPlayerManager players, IConnectionCollection connections, IObjectManager objects, ICoopSessionProvider coopSessionProvider)
        {
            this.messageBroker = messageBroker;
            this.network = network;
            this.coopSessionProvider = coopSessionProvider;
            resolver = new PlayerContextResolver(players, connections, playerSessions, objects);
            settings = AIPortServerSettings.Load();
            stateStore = new AIPortStateStore(settings.EnablePersistentMemory, settings.StatePath);
            Logger.Information("AIPort server settings configPath={ConfigPath} backend={Backend} explicitlyEnabled={ExplicitlyEnabled} enabled={Enabled} model={Model} keyPresent={KeyPresent} credentialsPresent={CredentialsPresent} player2TokenFilePresent={Player2TokenFilePresent} player2AccountFilePresent={Player2AccountFilePresent} player2RefreshAvailable={Player2RefreshAvailable} endpointAllowed={EndpointAllowed} endpointScheme={EndpointScheme} endpointHost={EndpointHost} timeoutSeconds={TimeoutSeconds} maxConcurrent={MaxConcurrent} maxCompletionTokens={MaxCompletionTokens} maxRequestsPerMinute={MaxRequestsPerMinute} nativeWarConfigured={NativeWarConfigured} nativeWarEnvironmentArmed={NativeWarEnvironmentArmed} nativeWarEnabled={NativeWarEnabled} nativePeaceConfigured={NativePeaceConfigured} nativePeaceEnvironmentArmed={NativePeaceEnvironmentArmed} nativePeaceEnabled={NativePeaceEnabled} nativeGenerationPinPresent={NativeGenerationPinPresent}", settings.ConfigPath, settings.Backend, settings.ExplicitlyEnabled, settings.Enabled, settings.Model, !string.IsNullOrWhiteSpace(settings.ApiKey), settings.CredentialsPresent, settings.Player2TokenFilePresent, settings.Player2AccountFilePresent, settings.Player2RefreshAvailable, settings.EndpointAllowed, settings.Endpoint == null ? string.Empty : settings.Endpoint.Scheme, settings.Endpoint == null ? string.Empty : settings.Endpoint.Host, (int)settings.RequestTimeout.TotalSeconds, settings.MaxConcurrentRequests, settings.MaxCompletionTokens, settings.MaxRequestsPerPlayerPerMinute, settings.NativeWarAdapterConfigured, settings.NativeWarAdapterEnvironmentArmed, settings.EnableNativeWarAdapter, settings.NativePeaceAdapterConfigured, settings.NativePeaceAdapterEnvironmentArmed, settings.EnableNativePeaceAdapter, !string.IsNullOrWhiteSpace(settings.NativeDiplomacyGenerationPin));
            Logger.Information("AIPort NPC diplomacy initiative settings Enabled={Enabled} DailyBudget={DailyBudget} MinimumIntervalHours={MinimumIntervalHours} PairCooldownDays={PairCooldownDays} MinimumScore={MinimumScore} NativeMutationApplied=false", settings.EnableNpcDiplomacyInitiative, settings.NpcDiplomacyDailyBudget, settings.NpcDiplomacyMinimumIntervalHours, settings.NpcDiplomacyPairCooldownDays, settings.NpcDiplomacyMinimumScore);
            messageBroker.Subscribe<CampaignReady>(Handle);
            messageBroker.Subscribe<PlayerConnected>(Handle);
            messageBroker.Subscribe<NetworkClientValidate>(Handle);
            messageBroker.Subscribe<NetworkTransferNewHero>(Handle);
            messageBroker.Subscribe<PlayerDisconnected>(Handle);
            messageBroker.Subscribe<AIConversationTargetOpen>(Handle);
            messageBroker.Subscribe<AIConversationTargetClose>(Handle);
            messageBroker.Subscribe<AIConversationRequest>(Handle);
            messageBroker.Subscribe<AIConversationCancel>(Handle);
            messageBroker.Subscribe<AIPortCapabilitiesRequest>(Handle);
            messageBroker.Subscribe<AIIntentProposalRequest>(Handle);
            messageBroker.Subscribe<AIPortStateSnapshotRequest>(Handle);
            messageBroker.Subscribe<AIDiplomacySnapshotRequest>(Handle);
            messageBroker.Subscribe<AIDiplomacyInboxPageRequest>(Handle);
            messageBroker.Subscribe<AIPortValidationGateRequest>(Handle);
            messageBroker.Subscribe<GameLoaded>(Handle);
            messageBroker.Subscribe<GameSaved>(Handle);
            messageBroker.Subscribe<GameSaveStateChanged>(Handle);
            messageBroker.Subscribe<AllGameObjectsRegistered>(Handle);
            AIPortServerSimulationBridge.Bind(SimulateNpcOffer);
        }

        public void Dispose()
        {
            AIPortServerSimulationBridge.Unbind(SimulateNpcOffer);
            messageBroker.Unsubscribe<CampaignReady>(Handle);
            messageBroker.Unsubscribe<PlayerConnected>(Handle);
            messageBroker.Unsubscribe<NetworkClientValidate>(Handle);
            messageBroker.Unsubscribe<NetworkTransferNewHero>(Handle);
            messageBroker.Unsubscribe<PlayerDisconnected>(Handle);
            messageBroker.Unsubscribe<AIConversationTargetOpen>(Handle);
            messageBroker.Unsubscribe<AIConversationTargetClose>(Handle);
            messageBroker.Unsubscribe<AIConversationRequest>(Handle);
            messageBroker.Unsubscribe<AIConversationCancel>(Handle);
            messageBroker.Unsubscribe<AIPortCapabilitiesRequest>(Handle);
            messageBroker.Unsubscribe<AIIntentProposalRequest>(Handle);
            messageBroker.Unsubscribe<AIPortStateSnapshotRequest>(Handle);
            messageBroker.Unsubscribe<AIDiplomacySnapshotRequest>(Handle);
            messageBroker.Unsubscribe<AIDiplomacyInboxPageRequest>(Handle);
            messageBroker.Unsubscribe<AIPortValidationGateRequest>(Handle);
            messageBroker.Unsubscribe<GameLoaded>(Handle);
            messageBroker.Unsubscribe<GameSaved>(Handle);
            messageBroker.Unsubscribe<GameSaveStateChanged>(Handle);
            messageBroker.Unsubscribe<AllGameObjectsRegistered>(Handle);
            if(hourlyMaintenanceAttached)try{CampaignEvents.HourlyTickEvent.ClearListeners(this);}catch{}
            memory.ClearAll();
            socialLedger.Clear();
            diplomaticStatements.Clear();
            nativeDiplomacyJournal.Clear();
            nativeWarCommitLeases.Clear();
            targetLeases.ClearAll();
            playerSessions.Clear();
            List<HttpWebRequest> requestsToAbort = new List<HttpWebRequest>();
            lock (gate)
            {
                foreach (ActiveBackendRequest state in activeBackendRequests.Values)
                {
                    state.Canceled = true;
                    if (state.Request != null) requestsToAbort.Add(state.Request);
                }
                seenRequestIds.Clear();
                seenRequestOrder.Clear();
                connectedPeers.Clear();
                connectedHeroIds.Clear();
            }
            foreach (HttpWebRequest request in requestsToAbort)
            {
                AbortBackendRequest(request);
            }
        }

        private void Handle(MessagePayload<CampaignReady> payload)
        {
            campaignReady = true;
            if(!hourlyMaintenanceAttached)try{CampaignEvents.HourlyTickEvent.AddNonSerializedListener(this,HandleHourlyDiplomacyMaintenance);hourlyMaintenanceAttached=true;}catch(Exception ex){Logger.Warning(ex,"AIPort could not attach hourly diplomacy maintenance");}
            Logger.Information("AIPort campaign ready for dialogue requests HourlyDiplomacyMaintenanceAttached={HourlyDiplomacyMaintenanceAttached}",hourlyMaintenanceAttached);
        }

        private void Handle(MessagePayload<PlayerConnected> payload)
        {
            NetPeer peer = payload.What.PlayerPeer;
            if (peer == null) return;
            bool newGeneration;
            long generation = playerSessions.Connect(peer.Id, peer, out newGeneration);
            if (!newGeneration)
            {
                Logger.Debug("AIPort ignored duplicate PlayerConnected for current connection generation PeerId={PeerId} Generation={Generation}", peer.Id, generation);
                return;
            }
            lock(gate){connectedPeers[peer.Id]=peer;connectedHeroIds.Remove(peer.Id);}
            ConversationTargetBinding replaced;
            int rememberedTurns = 0;
            if (targetLeases.RemovePeer(peer.Id, out replaced))
                rememberedTurns = memory.ArchiveConversation(peer.Id, replaced.PlayerHeroId, replaced.TargetInstanceId, replaced.ConversationId);
            memory.ClearPeer(peer.Id);
            int aborted = CancelActiveRequestsForPeer(peer.Id);
            Logger.Information("AIPort opened Coop join identity generation PeerId={PeerId} Generation={Generation} PreviousLeaseCleared={PreviousLeaseCleared} ActiveRequestsAborted={ActiveRequestsAborted} RememberedTurns={RememberedTurns}", peer.Id, generation, replaced != null, aborted, rememberedTurns);
        }

        private void Handle(MessagePayload<NetworkClientValidate> payload)
        {
            ObserveCoopJoinIdentity(payload.Who as NetPeer, payload.What.PlayerId, "client_validate");
        }

        private void Handle(MessagePayload<NetworkTransferNewHero> payload)
        {
            ObserveCoopJoinIdentity(payload.Who as NetPeer, payload.What.PlayerId, "new_hero_transfer");
        }

        private void ObserveCoopJoinIdentity(NetPeer peer, string controllerId, string source)
        {
            if (peer == null) return;
            bool conflict;
            if (playerSessions.TryObserveJoinIdentity(peer.Id, peer, controllerId, out conflict))
            {
                Logger.Information("AIPort observed Coop join identity PeerId={PeerId} ControllerId={ControllerId} Source={Source}", peer.Id, controllerId, source);
            }
            else if (conflict)
            {
                Logger.Warning("AIPort rejected conflicting Coop join identity PeerId={PeerId} ClaimedControllerId={ControllerId} Source={Source}", peer.Id, controllerId, source);
            }
        }

        private void Handle(MessagePayload<PlayerDisconnected> payload)
        {
            NetPeer peer = payload.What.PlayerId;
            if (peer == null) return;
            if (!playerSessions.Disconnect(peer.Id, peer))
            {
                Logger.Warning("AIPort ignored stale PlayerDisconnected from a non-current connection generation PeerId={PeerId}", peer.Id);
                return;
            }
            lock(gate){NetPeer current;if(connectedPeers.TryGetValue(peer.Id,out current)&&ReferenceEquals(current,peer)){connectedPeers.Remove(peer.Id);connectedHeroIds.Remove(peer.Id);}}
            ConversationTargetBinding binding;
            int rememberedTurns = 0;
            if (targetLeases.RemovePeer(peer.Id, out binding))
                rememberedTurns = memory.ArchiveConversation(peer.Id, binding.PlayerHeroId, binding.TargetInstanceId, binding.ConversationId);
            memory.ClearPeer(peer.Id);
            int aborted = CancelActiveRequestsForPeer(peer.Id);
            int nativeWarLeasesInvalidated = nativeWarCommitLeases.RemovePeer(peer.Id);
            Logger.Information("AIPort conversation state cleared for disconnected peer PeerId={PeerId} ActiveRequestsAborted={ActiveRequestsAborted} RememberedTurns={RememberedTurns} NativeWarLeasesInvalidated={NativeWarLeasesInvalidated}", peer.Id, aborted, rememberedTurns, nativeWarLeasesInvalidated);
        }

        private void Handle(MessagePayload<AIConversationTargetOpen> payload)
        {
            NetPeer peer = payload.Who as NetPeer;
            AIConversationTargetOpen request = payload.What;
            if (peer == null) return;
            if (request.ProtocolVersion != AIPortProtocol.Version || !IsCorrelationId(request.ConversationId)
                || !IsSafeIdentifier(request.ClaimedTargetId, AIPortProtocol.MaximumTargetIdLength))
            {
                network.SendImmediate(peer, new AIConversationTargetBound(request.ConversationId, string.Empty, string.Empty, string.Empty, false, "invalid_target_open"));
                return;
            }
            Player player;
            string resolveFailure = campaignReady ? string.Empty : "campaign_not_ready";
            if (!campaignReady || !resolver.TryResolve(peer, out player, out resolveFailure))
            {
                network.SendImmediate(peer, new AIConversationTargetBound(request.ConversationId, string.Empty, string.Empty, string.Empty, false, "player_unresolved"));
                Logger.Information("AIPort target bind rejected PeerId={PeerId} ConversationId={ConversationId} ClaimedTargetId={ClaimedTargetId} ErrorCode=player_unresolved ResolveFailure={ResolveFailure}", peer.Id, request.ConversationId, request.ClaimedTargetId, resolveFailure);
                return;
            }
            Hero playerHero;
            MobileParty playerParty;
            string controlledObjectFailure;
            if (!resolver.TryResolveControlledCampaignObjects(player, out playerHero, out playerParty, out controlledObjectFailure))
            {
                network.SendImmediate(peer, new AIConversationTargetBound(request.ConversationId, string.Empty, string.Empty, string.Empty, false, "player_unresolved"));
                Logger.Information("AIPort target bind rejected after authoritative player resolution PeerId={PeerId} ConversationId={ConversationId} PlayerHeroId={PlayerHeroId} MobilePartyId={MobilePartyId} ControlledObjectFailure={ControlledObjectFailure}", peer.Id, request.ConversationId, player.HeroId, player.MobilePartyId, controlledObjectFailure);
                return;
            }
            ValidatedConversationTarget validated;
            string errorCode;
            if (!ConversationTargetValidator.TryValidate(player, playerHero, playerParty, request.ClaimedTargetId, request.ClientTargetNonce, out validated, out errorCode))
            {
                network.SendImmediate(peer, new AIConversationTargetBound(request.ConversationId, string.Empty, string.Empty, string.Empty, false, errorCode));
                Logger.Warning("AIPort target bind rejected PeerId={PeerId} ConversationId={ConversationId} ClaimedTargetId={ClaimedTargetId} ErrorCode={ErrorCode} PlayerHeroId={PlayerHeroId} MobilePartyId={MobilePartyId}", peer.Id, request.ConversationId, request.ClaimedTargetId, errorCode, player.HeroId, player.MobilePartyId);
                return;
            }
            memory.AttachPeer(peer.Id, player.HeroId);
            ConversationTargetBinding replaced = targetLeases.Bind(peer.Id, player.HeroId, request.ConversationId, validated.TargetId, validated.TargetInstanceId, validated.AuthoritativeLocationId, validated.IsHero);
            if (replaced != null)
            {
                memory.ArchiveConversation(peer.Id, replaced.PlayerHeroId, replaced.TargetInstanceId, replaced.ConversationId);
                CancelActiveRequestsForConversation(peer.Id, replaced.ConversationId);
            }
            ConversationTargetBinding binding;
            targetLeases.TryGet(peer.Id, out binding);
            network.SendImmediate(peer, new AIConversationTargetBound(binding.ConversationId, binding.TargetLeaseId, binding.TargetId, binding.TargetInstanceId, true, string.Empty));
            Logger.Information("AIPort target bound PeerId={PeerId} PlayerHeroId={PlayerHeroId} ConversationId={ConversationId} TargetId={TargetId} TargetInstanceId={TargetInstanceId} LocationId={LocationId} IsHero={IsHero}", peer.Id, player.HeroId, binding.ConversationId, binding.TargetId, binding.TargetInstanceId, binding.AuthoritativeLocationId, binding.IsHero);
        }

        private void Handle(MessagePayload<AIConversationTargetClose> payload)
        {
            NetPeer peer = payload.Who as NetPeer;
            if (peer == null || !IsCorrelationId(payload.What.ConversationId) || !IsCorrelationId(payload.What.TargetLeaseId)) return;
            ConversationTargetBinding binding;
            if (!targetLeases.Close(peer.Id, payload.What.ConversationId, payload.What.TargetLeaseId, out binding))
            {
                Logger.Warning("AIPort stale target close ignored PeerId={PeerId} ConversationId={ConversationId}", peer.Id, payload.What.ConversationId);
                return;
            }
            int aborted = CancelActiveRequestsForConversation(peer.Id, binding.ConversationId);
            int rememberedTurns = memory.ArchiveConversation(peer.Id, binding.PlayerHeroId, binding.TargetInstanceId, binding.ConversationId);
            Logger.Information("AIPort target lease closed PeerId={PeerId} ConversationId={ConversationId} TargetId={TargetId} TargetInstanceId={TargetInstanceId} ActiveRequestsAborted={ActiveRequestsAborted} RememberedTurns={RememberedTurns}", peer.Id, binding.ConversationId, binding.TargetId, binding.TargetInstanceId, aborted, rememberedTurns);
        }

        private void Handle(MessagePayload<AIConversationCancel> payload)
        {
            NetPeer peer = payload.Who as NetPeer;
            if (peer == null) return;
            if (!IsCorrelationId(payload.What.RequestId) || !IsCorrelationId(payload.What.ConversationId))
            {
                Logger.Warning("AIPort ignored malformed conversation cancel PeerId={PeerId}", peer.Id);
                return;
            }
            bool marked = false;
            bool ownershipMatched = false;
            bool inflightReleased = false;
            HttpWebRequest requestToAbort = null;
            lock (gate)
            {
                ActiveBackendRequest state;
                if (activeBackendRequests.TryGetValue(payload.What.RequestId, out state))
                {
                    ownershipMatched = state.PeerId == peer.Id
                        && string.Equals(state.ConversationId, payload.What.ConversationId, StringComparison.Ordinal);
                    if (ownershipMatched)
                    {
                        state.Canceled = true;
                        requestToAbort = state.Request;
                        marked = true;
                        inflightReleased = ReleaseActiveInflightLocked(state);
                    }
                }
            }
            bool abortRequested = false;
            if (requestToAbort != null)
            {
                abortRequested = AbortBackendRequest(requestToAbort);
            }
            Logger.Information("AIPort conversation cancel RequestId={RequestId} ConversationId={ConversationId} PeerId={PeerId} OwnershipMatched={OwnershipMatched} ActiveBackendMarked={ActiveBackendMarked} InflightReleased={InflightReleased} HttpAbortRequested={HttpAbortRequested}", payload.What.RequestId, payload.What.ConversationId, peer == null ? -1 : peer.Id, ownershipMatched, marked, inflightReleased, abortRequested);
        }

        private void Handle(MessagePayload<AIConversationRequest> payload)
        {
            NetPeer peer = payload.Who as NetPeer;
            AIConversationRequest request = payload.What;
            if (peer == null)
            {
                Logger.Warning("AIPort conversation ignored because message source was not a NetPeer");
                return;
            }
            if (request.ProtocolVersion != AIPortProtocol.Version)
            {
                Reject(peer, request.RequestId, "protocol_mismatch", "AIPort protocol mismatch", false);
                return;
            }
            if (!IsCorrelationId(request.RequestId))
            {
                Reject(peer, request.RequestId, "invalid_request_id", "AIPort request ID is invalid", false);
                return;
            }
            if (!IsCorrelationId(request.ConversationId))
            {
                Reject(peer, request.RequestId, "invalid_conversation_id", "AIPort conversation ID is invalid", false);
                return;
            }
            if (!TryRememberRequest(request.RequestId))
            {
                Reject(peer, request.RequestId, "duplicate_request", "AIPort request was already processed", false);
                return;
            }
            if (!campaignReady)
            {
                Reject(peer, request.RequestId, "campaign_not_ready", "AIPort campaign is not ready", true);
                return;
            }
            if (stateStore.IsSaving)
            {
                Reject(peer, request.RequestId, "save_in_progress", "AIPort state is crossing a save barrier", true, 1000);
                return;
            }
            string text = request.PlayerText ?? string.Empty;
            if (string.IsNullOrWhiteSpace(text))
            {
                Reject(peer, request.RequestId, "empty_text", "AIPort player text is empty", false);
                return;
            }
            if (text.Length > AIPortProtocol.MaximumPlayerTextLength)
            {
                Reject(peer, request.RequestId, "text_too_long", "AIPort player text exceeds the limit", false);
                return;
            }
            Player player;
            string resolveFailure;
            if (!resolver.TryResolve(peer, out player, out resolveFailure))
            {
                Logger.Information("AIPort conversation player resolution failed RequestId={RequestId} PeerId={PeerId} ResolveFailure={ResolveFailure}", request.RequestId, peer.Id, resolveFailure);
                Reject(peer, request.RequestId, "player_unresolved", "AIPort could not resolve the authoritative player hero", true);
                return;
            }

            Hero playerHero;
            MobileParty playerParty;
            string controlledObjectFailure;
            if (!resolver.TryResolveControlledCampaignObjects(player, out playerHero, out playerParty, out controlledObjectFailure))
            {
                Logger.Information("AIPort conversation controlled-object resolution failed RequestId={RequestId} PeerId={PeerId} PlayerHeroId={PlayerHeroId} MobilePartyId={MobilePartyId} ControlledObjectFailure={ControlledObjectFailure}", request.RequestId, peer.Id, player.HeroId, player.MobilePartyId, controlledObjectFailure);
                Reject(peer, request.RequestId, "player_unresolved", "AIPort could not resolve the authoritative controlled player objects", true);
                return;
            }

            memory.AttachPeer(peer.Id, player.HeroId);
            if (!string.IsNullOrWhiteSpace(request.ClaimedPlayerHeroId) && !string.Equals(request.ClaimedPlayerHeroId, player.HeroId, StringComparison.Ordinal))
            {
                Logger.Warning("AIPort ignored claimed player hero ClaimedPlayerHeroId={ClaimedPlayerHeroId} ResolvedHeroId={ResolvedHeroId} ControllerId={ControllerId}", request.ClaimedPlayerHeroId, player.HeroId, player.ControllerId);
            }
            ConversationTargetBinding targetBinding;
            string targetError;
            if (!targetLeases.TryAuthorizeRequest(peer.Id, request.ConversationId, request.TargetLeaseId, request.NpcTargetId, request.TargetInstanceId, out targetBinding, out targetError))
            {
                Reject(peer, request.RequestId, targetError, "AIPort conversation target is not bound or is stale", false);
                return;
            }
            if (!ConversationTargetValidator.IsStillEligible(player, playerHero, playerParty, targetBinding))
            {
                ConversationTargetBinding removed;
                targetLeases.Close(peer.Id, targetBinding.ConversationId, targetBinding.TargetLeaseId, out removed);
                memory.ArchiveConversation(peer.Id, targetBinding.PlayerHeroId, targetBinding.TargetInstanceId, targetBinding.ConversationId);
                Reject(peer, request.RequestId, "stale_target", "AIPort conversation target is no longer available", false);
                return;
            }
            string npcHeroId = targetBinding.TargetId;
            string targetInstanceId = targetBinding.TargetInstanceId;
            int retryAfterMilliseconds;
            if (!TryEnterRateLimit(player.ControllerId, out retryAfterMilliseconds))
            {
                Reject(peer, request.RequestId, "rate_limited", "AIPort rate limit reached", true, retryAfterMilliseconds);
                return;
            }

            Logger.Information("AIPort conversation accepted RequestId={RequestId} ControllerId={ControllerId} PlayerHeroId={PlayerHeroId} PlayerPartyId={PlayerPartyId} NpcTargetId={NpcTargetId} TargetInstanceId={TargetInstanceId} BackendEnabled={BackendEnabled}", request.RequestId, player.ControllerId, player.HeroId, player.MobilePartyId, npcHeroId, targetInstanceId, settings.Enabled);
            network.SendImmediate(peer, new AIConversationAccepted(request.RequestId, request.ConversationId, 0));
            if (!settings.Enabled)
            {
                try
                {
                    const string reply = "AIPort dialogue pipeline is ready. No API key is configured, so this is a narrative-only stub.";
                    int memoryTurns = memory.AddTurn(peer.Id, player.HeroId, targetInstanceId, request.ConversationId, text, reply);
                    network.SendImmediate(peer, new AIConversationResult(request.RequestId, request.ConversationId, request.ClientSequence, npcHeroId, reply, Array.Empty<string>(), true, targetInstanceId, memory.Revision));
                    Logger.Information("AIPort conversation result sent RequestId={RequestId} Chars={Chars} MemoryTurns={MemoryTurns} Stub=True", request.RequestId, reply.Length, memoryTurns);
                }
                finally
                {
                    ReleaseInflight();
                }
                return;
            }
            string history;
            string systemPrompt;
            string userPrompt;
            try
            {
                // Snapshot all campaign/Hero data before leaving the authoritative handler thread.
                history = memory.BuildHistory(peer.Id, player.HeroId, targetInstanceId, request.ConversationId);
                systemPrompt = prompts.BuildSystemPrompt();
                userPrompt = prompts.BuildUserPrompt(player, playerHero, npcHeroId, targetInstanceId, request.PlayerText, history);
            }
            catch (Exception ex)
            {
                Logger.Warning(ex, "AIPort prompt snapshot failed RequestId={RequestId}", request.RequestId);
                Reject(peer, request.RequestId, "prompt_failed", "AIPort could not prepare the character context", false);
                ReleaseInflight();
                return;
            }
            lock (gate)
            {
                activeBackendRequests[request.RequestId] = new ActiveBackendRequest
                {
                    PeerId = peer.Id,
                    ConversationId = request.ConversationId,
                    Request = null,
                    Canceled = false,
                    InflightReleased = false
                };
                backendWorkers++;
            }
            bool queued = false;
            try
            {
                queued = ThreadPool.QueueUserWorkItem(_ => CompleteBackend(peer, request, player, npcHeroId, targetInstanceId, systemPrompt, userPrompt));
            }
            catch (Exception ex)
            {
                Logger.Warning(ex, "AIPort backend worker dispatch failed RequestId={RequestId}", request.RequestId);
            }
            if (!queued)
            {
                lock (gate)
                {
                    ActiveBackendRequest state;
                    if (activeBackendRequests.TryGetValue(request.RequestId, out state))
                    {
                        ReleaseActiveInflightLocked(state);
                        activeBackendRequests.Remove(request.RequestId);
                    }
                    backendWorkers = Math.Max(0, backendWorkers - 1);
                }
                Reject(peer, request.RequestId, "backend_failed", "AIPort backend worker was unavailable", true);
            }
        }

        private void CompleteBackend(NetPeer peer, AIConversationRequest request, Player player, string npcHeroId, string targetInstanceId, string systemPrompt, string userPrompt)
        {
            try
            {
                string reply = backend.Complete(settings, systemPrompt, userPrompt, handle => RegisterBackendRequestHandle(request.RequestId, handle));
                if (string.IsNullOrWhiteSpace(reply)) reply = "...";
                int memoryTurns;
                if (!TryCommitBackendResult(peer, request, player, npcHeroId, targetInstanceId, reply, out memoryTurns))
                {
                    Logger.Information("AIPort backend result suppressed after cancellation or ownership loss RequestId={RequestId} ConversationId={ConversationId}", request.RequestId, request.ConversationId);
                    return;
                }
                Logger.Information("AIPort conversation result sent RequestId={RequestId} Chars={Chars} MemoryTurns={MemoryTurns} Stub=False", request.RequestId, reply.Length, memoryTurns);
            }
            catch (WebException ex)
            {
                bool timedOut = ex.Status == WebExceptionStatus.Timeout;
                bool delivered = TryRejectActiveBackend(peer, request, timedOut ? "backend_timeout" : "backend_failed", timedOut ? "AIPort backend timed out" : "AIPort backend failed", !timedOut);
                if (delivered) Logger.Warning(ex, timedOut ? "AIPort backend timed out RequestId={RequestId}" : "AIPort backend HTTP request failed RequestId={RequestId}", request.RequestId);
                else Logger.Information("AIPort backend failure suppressed after cancellation or ownership loss RequestId={RequestId} ConversationId={ConversationId}", request.RequestId, request.ConversationId);
            }
            catch (Exception ex)
            {
                bool delivered = TryRejectActiveBackend(peer, request, "backend_failed", "AIPort backend failed", true);
                if (delivered) Logger.Warning(ex, "AIPort backend failed RequestId={RequestId}", request.RequestId);
                else Logger.Information("AIPort backend exception suppressed after cancellation or ownership loss RequestId={RequestId} ConversationId={ConversationId}", request.RequestId, request.ConversationId);
            }
            finally
            {
                lock (gate)
                {
                    ActiveBackendRequest state;
                    if (activeBackendRequests.TryGetValue(request.RequestId, out state))
                    {
                        ReleaseActiveInflightLocked(state);
                        activeBackendRequests.Remove(request.RequestId);
                    }
                    backendWorkers = Math.Max(0, backendWorkers - 1);
                }
            }
        }

        private void RegisterBackendRequestHandle(string requestId, HttpWebRequest request)
        {
            bool registered = false;
            bool canceled = false;
            lock (gate)
            {
                ActiveBackendRequest state;
                if (activeBackendRequests.TryGetValue(requestId, out state))
                {
                    state.Request = request;
                    registered = true;
                    canceled = state.Canceled;
                }
            }
            if (!registered || canceled)
            {
                AbortBackendRequest(request);
            }
            Logger.Debug("AIPort backend HTTP handle registered RequestId={RequestId} Registered={Registered} AlreadyCanceled={AlreadyCanceled}", requestId, registered, canceled);
        }

        private bool TryCommitBackendResult(NetPeer peer, AIConversationRequest request, Player player, string npcHeroId, string targetInstanceId, string reply, out int memoryTurns)
        {
            memoryTurns = 0;
            lock (gate)
            {
                ActiveBackendRequest state;
                if (!activeBackendRequests.TryGetValue(request.RequestId, out state)
                    || state.Canceled
                    || state.PeerId != peer.Id
                    || !string.Equals(state.ConversationId, request.ConversationId, StringComparison.Ordinal)) return false;
                // Cancellation/disconnect is mutually exclusive with the final memory write and network send.
                memoryTurns = memory.AddTurn(peer.Id, player.HeroId, targetInstanceId, request.ConversationId, request.PlayerText, reply);
                network.SendImmediate(peer, new AIConversationResult(request.RequestId, request.ConversationId, request.ClientSequence, npcHeroId, reply, Array.Empty<string>(), true, targetInstanceId, memory.Revision));
                return true;
            }
        }

        private bool TryRejectActiveBackend(NetPeer peer, AIConversationRequest request, string errorCode, string safeMessage, bool retryable)
        {
            lock (gate)
            {
                ActiveBackendRequest state;
                if (!activeBackendRequests.TryGetValue(request.RequestId, out state)
                    || state.Canceled
                    || state.PeerId != peer.Id
                    || !string.Equals(state.ConversationId, request.ConversationId, StringComparison.Ordinal)) return false;
                Reject(peer, request.RequestId, errorCode, safeMessage, retryable);
                return true;
            }
        }

        private int CancelActiveRequestsForConversation(int peerId, string conversationId)
        {
            List<HttpWebRequest> requests = new List<HttpWebRequest>();
            int canceled = 0;
            lock (gate)
            {
                foreach (ActiveBackendRequest state in activeBackendRequests.Values)
                {
                    if (state.PeerId != peerId || state.Canceled || !string.Equals(state.ConversationId, conversationId, StringComparison.Ordinal)) continue;
                    state.Canceled = true;
                    ReleaseActiveInflightLocked(state);
                    canceled++;
                    if (state.Request != null) requests.Add(state.Request);
                }
            }
            foreach (HttpWebRequest request in requests) AbortBackendRequest(request);
            return canceled;
        }

        private int CancelActiveRequestsForPeer(int peerId)
        {
            List<HttpWebRequest> requests = new List<HttpWebRequest>();
            int canceled = 0;
            lock (gate)
            {
                foreach (ActiveBackendRequest state in activeBackendRequests.Values)
                {
                    if (state.PeerId != peerId || state.Canceled) continue;
                    state.Canceled = true;
                    ReleaseActiveInflightLocked(state);
                    canceled++;
                    if (state.Request != null) requests.Add(state.Request);
                }
            }
            foreach (HttpWebRequest request in requests)
            {
                AbortBackendRequest(request);
            }
            return canceled;
        }


        private bool ReleaseActiveInflightLocked(ActiveBackendRequest state)
        {
            if (state == null || state.InflightReleased) return false;
            state.InflightReleased = true;
            inflight = Math.Max(0, inflight - 1);
            return true;
        }

        private static bool AbortBackendRequest(HttpWebRequest request)
        {
            if (request == null) return false;
            bool requested = false;
            try
            {
                request.Abort();
                requested = true;
            }
            catch { }
            try
            {
                ServicePoint servicePoint = request.ServicePoint;
                string connectionGroupName = request.ConnectionGroupName;
                if (servicePoint != null && !string.IsNullOrWhiteSpace(connectionGroupName))
                {
                    servicePoint.CloseConnectionGroup(connectionGroupName);
                    requested = true;
                }
            }
            catch { }
            return requested;
        }

        private bool TryRememberRequest(string requestId)
        {
            string key = requestId;
            lock (gate)
            {
                if (!seenRequestIds.Add(key)) return false;
                seenRequestOrder.Enqueue(key);
                while (seenRequestOrder.Count > MaximumRememberedRequestIds)
                {
                    seenRequestIds.Remove(seenRequestOrder.Dequeue());
                }
                return true;
            }
        }

        private static bool IsCorrelationId(string value)
        {
            if (string.IsNullOrWhiteSpace(value) || value.Length != 32) return false;
            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];
                if (!((c >= '0' && c <= '9') || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F'))) return false;
            }
            return true;
        }

        private static bool IsSafeIdentifier(string value, int maximumLength)
        {
            if (string.IsNullOrWhiteSpace(value) || value.Length > maximumLength) return false;
            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];
                if (char.IsControl(c) || char.IsWhiteSpace(c)) return false;
            }
            return true;
        }

        private void Handle(MessagePayload<GameLoaded> payload)
        {
            pendingLoadedSaveName = payload.What.SaveName ?? string.Empty;
            lastNpcInitiativeCampaignHour = long.MinValue;
            memory.ClearAll();
            stateStore.BeginLoad(pendingLoadedSaveName, CurrentCampaignId());
            Logger.Information("AIPort external state load opened SaveName={SaveName} CampaignGeneration={CampaignGeneration} PersistentEnabled={PersistentEnabled}", pendingLoadedSaveName, stateStore.CampaignGeneration, stateStore.Enabled);
        }

        private void Handle(MessagePayload<AllGameObjectsRegistered> payload)
        {
            string campaignId = CurrentCampaignId();
            if (string.IsNullOrWhiteSpace(stateStore.CampaignGeneration)) stateStore.BeginLoad(pendingLoadedSaveName, campaignId);
            string result = stateStore.LoadAfterObjectsRegistered(CurrentCampaignTime(), memory, socialLedger, diplomaticStatements, nativeDiplomacyJournal);
            int reconciled=stateStore.Loaded&&!stateStore.ReadOnly?ReconcileNativeDiplomacyJournal("startup"):0;
            Logger.Information("AIPort external state load completed Result={Result} CampaignGeneration={CampaignGeneration} Revision={Revision} ReadOnly={ReadOnly} NativeJournalCount={NativeJournalCount} Reconciled={Reconciled}", result, stateStore.CampaignGeneration, memory.Revision, stateStore.ReadOnly,nativeDiplomacyJournal.Count,reconciled);
        }

        private void Handle(MessagePayload<GameSaveStateChanged> payload)
        {
            if (payload.What.IsSaving) stateStore.BeginSaveBarrier(); else stateStore.EndSaveBarrier();
            Logger.Information("AIPort save barrier changed IsSaving={IsSaving} Revision={Revision}", payload.What.IsSaving, memory.Revision);
        }

        private void Handle(MessagePayload<GameSaved> payload)
        {
            string observedCampaignId = CurrentCampaignId();
            string result = stateStore.Save(payload.What.SaveName, observedCampaignId, CurrentCampaignTime(), memory, socialLedger, diplomaticStatements, nativeDiplomacyJournal);
            Logger.Information("AIPort external state save completed ObservedSaveName={ObservedSaveName} ObservedCampaignId={ObservedCampaignId} Result={Result} StableCampaignGeneration={CampaignGeneration} Revision={Revision}", payload.What.SaveName, observedCampaignId, result, stateStore.CampaignGeneration, memory.Revision);
        }

        private void Handle(MessagePayload<AIPortCapabilitiesRequest> payload)
        {
            NetPeer peer = payload.Who as NetPeer; if (peer == null) return; AIPortCapabilitiesRequest request = payload.What;
            bool accepted = request.ProtocolVersion == AIPortProtocol.Version && IsCorrelationId(request.RequestId) && request.StateSchemaVersion <= AIPortProtocol.StateSchemaVersion;
            int flags = AIPortProtocol.CapabilityNarrative;
            if (settings.EnableIntentFoundation) flags |= AIPortProtocol.CapabilityNoOpIntent;
            if (settings.EnableStateSnapshots) flags |= AIPortProtocol.CapabilityStateSnapshot;
            if (settings.EnablePersistentMemory) flags |= AIPortProtocol.CapabilityPersistentMemory;
            if (settings.EnableRelationShadowIntents) flags |= AIPortProtocol.CapabilityRelationShadowIntent | AIPortProtocol.CapabilityRelationConfirmation;
            if (settings.EnableIntentFoundation) flags |= AIPortProtocol.CapabilityDiplomacySnapshot;
            if (settings.EnableIntentFoundation && settings.EnablePersistentMemory) flags |= AIPortProtocol.CapabilityDiplomacyStatements;
            if (settings.EnableIntentFoundation && settings.EnablePersistentMemory) flags |= AIPortProtocol.CapabilityValidationGate | AIPortProtocol.CapabilityDiplomacyAuthority | AIPortProtocol.CapabilityDiplomacyRecipientConsent | AIPortProtocol.CapabilityDiplomacyConflictGuard | AIPortProtocol.CapabilityDiplomacyInboxNotification | AIPortProtocol.CapabilityDiplomacyLifecycleBundle | AIPortProtocol.CapabilityNativeWarAdapter | AIPortProtocol.CapabilityNativeDiplomacyJournal | AIPortProtocol.CapabilityNativePeaceAdapter | AIPortProtocol.CapabilityNpcDiplomacyPolicy | AIPortProtocol.CapabilityDiplomacyDecisionUi | AIPortProtocol.CapabilityDiplomacyInboxList | AIPortProtocol.CapabilityNpcDiplomacyInitiativeScheduler;
            network.SendImmediate(peer, new AIPortCapabilitiesResponse(AIPortProtocol.Version, request.RequestId, accepted, flags, AIPortProtocol.IntentSchemaVersion, AIPortProtocol.StateSchemaVersion, stateStore.CampaignGeneration, memory.Revision, accepted ? "capabilities_ready" : "capabilities_rejected"));
            Logger.Information("AIPort capabilities negotiated PeerId={PeerId} Accepted={Accepted} Flags={Flags} CampaignGeneration={CampaignGeneration} Revision={Revision}", peer.Id, accepted, flags, stateStore.CampaignGeneration, memory.Revision);
        }

        private void Handle(MessagePayload<AIIntentProposalRequest> payload)
        {
            NetPeer peer = payload.Who as NetPeer; if (peer == null) return; AIIntentProposalRequest request = payload.What;
            if (request.ProtocolVersion != AIPortProtocol.Version || !IsCorrelationId(request.RequestId)) return;
            Player player; string failure;
            if (!settings.EnableIntentFoundation || !campaignReady || stateStore.IsSaving || !resolver.TryResolve(peer, out player, out failure))
            {
                network.SendImmediate(peer, new AIIntentProposalResult(request.RequestId, string.Empty, "rejected", stateStore.IsSaving ? "save_in_progress" : "not_authorized", memory.Revision));
                return;
            }
            RegisterResolvedPeerHero(peer,player.HeroId);
            bool diplomacyIntent = string.Equals(request.IntentType, "diplomacy_statement_proposal", StringComparison.Ordinal)
                || string.Equals(request.IntentType, "diplomacy_statement_confirm", StringComparison.Ordinal)
                || string.Equals(request.IntentType, "diplomacy_recipient_accept", StringComparison.Ordinal)
                || string.Equals(request.IntentType, "diplomacy_recipient_reject", StringComparison.Ordinal)
                || string.Equals(request.IntentType, "diplomacy_source_withdraw", StringComparison.Ordinal)
                || string.Equals(request.IntentType, "diplomacy_native_war_preflight", StringComparison.Ordinal)
                || string.Equals(request.IntentType, "diplomacy_native_preflight", StringComparison.Ordinal)
                || string.Equals(request.IntentType, "diplomacy_native_war_commit", StringComparison.Ordinal)
                || string.Equals(request.IntentType, "diplomacy_native_peace_commit", StringComparison.Ordinal);
            if (diplomacyIntent)
            {
                long revisionBefore=memory.Revision;ConversationTargetBinding binding;targetLeases.TryGet(peer.Id,out binding);Hero playerHero;MobileParty playerParty;string objectFailure;
                if(!resolver.TryResolveControlledCampaignObjects(player,out playerHero,out playerParty,out objectFailure))
                {network.SendImmediate(peer,new AIIntentProposalResult(request.RequestId,string.Empty,"rejected","player_unresolved",memory.Revision));return;}
                DiplomacyStatementDecision diplomaticDecision;
                if(string.Equals(request.IntentType,"diplomacy_statement_proposal",StringComparison.Ordinal))
                {
                    DiplomacyProposalPayload parsed;IFaction sourceFaction=null,targetFaction=null;Hero targetHero=binding==null?null:Hero.Find(binding.TargetId);
                    bool parsedOk=DiplomacyStatementCoordinator.TryParseProposal(request.PayloadJson,out parsed);try{sourceFaction=playerHero.MapFaction;targetFaction=targetHero==null?null:targetHero.MapFaction;}catch{}DiplomacyAuthorityContext authority=diplomacyAuthority.Evaluate(playerHero,targetHero);
                    string domainFailure=string.Empty;if(!parsedOk)domainFailure="invalid_payload";else if(sourceFaction==null)domainFailure="player_faction_required";else if(targetFaction==null)domainFailure="target_faction_required";else if(sourceFaction.IsBanditFaction||targetFaction.IsBanditFaction)domainFailure="faction_ineligible";else if(ReferenceEquals(sourceFaction,targetFaction))domainFailure="distinct_faction_required";else if(!authority.SourceAuthorized)domainFailure="player_faction_authority_required";else if(!authority.TargetAuthorized)domainFailure="target_faction_authority_required";else{bool atWar=false;try{atWar=FactionManager.IsAtWarAgainstFaction(sourceFaction,targetFaction);}catch{}if(parsed.Action=="war"&&atWar)domainFailure="already_at_war";else if(parsed.Action=="peace"&&!atWar)domainFailure="not_at_war";}
                    if(domainFailure.Length>0)diplomaticDecision=new DiplomacyStatementDecision{IntentId=Guid.NewGuid().ToString("N"),Status="rejected",ReasonCode=domainFailure,StateRevision=memory.Revision,MutationApplied=false};
                    else diplomaticDecision=diplomacyCoordinator.Propose(peer.Id,player.HeroId,request.RequestId,request.CampaignGeneration,stateStore.CampaignGeneration,request.ExpectedStateRevision,memory.Revision,parsed,sourceFaction.StringId,targetFaction.StringId,binding);
                }
                else if(string.Equals(request.IntentType,"diplomacy_statement_confirm",StringComparison.Ordinal))
                {
                    DiplomacyConfirmPayload parsed;DiplomacyStatementCoordinator.TryParseConfirm(request.PayloadJson,out parsed);diplomaticDecision=diplomacyCoordinator.Confirm(peer.Id,player.HeroId,request.RequestId,request.CampaignGeneration,stateStore.CampaignGeneration,request.ExpectedStateRevision,memory.Revision,parsed,binding);
                    if(string.Equals(diplomaticDecision.Status,"confirmed_shadow",StringComparison.Ordinal))
                    {
                        Hero targetHero=binding==null?null:Hero.Find(binding.TargetId);IFaction sourceFaction=null,targetFaction=null;try{sourceFaction=playerHero.MapFaction;targetFaction=targetHero==null?null:targetHero.MapFaction;}catch{}
                        bool pairValid=sourceFaction!=null&&targetFaction!=null&&!sourceFaction.IsBanditFaction&&!targetFaction.IsBanditFaction&&string.Equals(sourceFaction.StringId,diplomaticDecision.SourceKingdomId,StringComparison.Ordinal)&&string.Equals(targetFaction.StringId,diplomaticDecision.TargetKingdomId,StringComparison.Ordinal);DiplomacyAuthorityContext authority=diplomacyAuthority.Evaluate(playerHero,targetHero);bool atWar=false;if(pairValid)try{atWar=FactionManager.IsAtWarAgainstFaction(sourceFaction,targetFaction);}catch{}
                        if(!pairValid){diplomaticDecision.Status="rejected";diplomaticDecision.ReasonCode="stale_faction_pair";}
                        else if(!authority.SourceAuthorized||!authority.TargetAuthorized){diplomaticDecision.Status="rejected";diplomaticDecision.ReasonCode="stale_diplomatic_authority";}
                        else if(diplomaticDecision.Action=="war"&&atWar){diplomaticDecision.Status="rejected";diplomaticDecision.ReasonCode="already_at_war";}
                        else if(diplomaticDecision.Action=="peace"&&!atWar){diplomaticDecision.Status="rejected";diplomaticDecision.ReasonCode="not_at_war";}
                        else if(!stateStore.Enabled||!stateStore.Loaded||stateStore.ReadOnly){diplomaticDecision.Status="rejected";diplomaticDecision.ReasonCode="persistent_state_unavailable";}
                        else
                        {
                            DateTime now=DateTime.UtcNow;int expired=ExpireDiplomacyDue(now);PersistentDiplomaticStatementRecord receipt;string ledgerReason;bool fresh;
                            if(!diplomaticStatements.TryRecord(diplomaticDecision.IntentId,player.HeroId,diplomaticDecision.TargetHeroId,diplomaticDecision.SourceKingdomId,diplomaticDecision.TargetKingdomId,diplomaticDecision.Action,now,out receipt,out ledgerReason,out fresh))
                            {diplomaticDecision.Status="rejected";diplomaticDecision.ReasonCode=ledgerReason;diplomaticDecision.StateRevision=memory.Revision;}
                            else
                            {
                                if(fresh)memory.AdvanceRevision(1);
                                diplomaticDecision.StateRevision=memory.Revision;
                                bool targetPlayerControlled=IsPlayerControlledDiplomacyTarget(targetHero)||FindSinglePeerForHero(receipt.TargetHeroId)!=null;
                                int relation=0;try{relation=targetHero==null?0:targetHero.GetRelation(playerHero);}catch{}
                                NpcDiplomacyPolicyDecision npcDecision=npcDiplomacyPolicy.Evaluate(receipt.Action,targetPlayerControlled,authority.PairAuthorized,atWar,relation);
                                if(npcDecision.RequiresPlayerDecision)
                                {
                                    diplomaticDecision.Status="pending_recipient";diplomaticDecision.ReasonCode="recipient_consent_required";
                                    if(fresh)NotifyRecipientInbox(receipt.TargetHeroId,"new_pending_negotiation");
                                }
                                else
                                {
                                    bool policyChanged;string policyReason;bool policyOk=diplomaticStatements.TryResolveByNpcPolicy(receipt.Id,receipt.TargetHeroId,npcDecision.Accepted,npcDecision.ReasonCode,DateTime.UtcNow,out receipt,out policyReason,out policyChanged);
                                    if(policyChanged)memory.AdvanceRevision(1);
                                    diplomaticDecision.StateRevision=memory.Revision;diplomaticDecision.Status=policyOk?(npcDecision.Accepted?"npc_recipient_accepted_shadow":"npc_recipient_rejected_shadow"):"rejected";diplomaticDecision.ReasonCode=policyOk?policyReason:policyReason;
                                    if(policyChanged&&receipt!=null)NotifyDiplomacyLifecycle(receipt,policyReason);
                                    Logger.Information("AIPort NPC diplomacy policy StatementId={StatementId} TargetHeroId={TargetHeroId} Action={Action} Accepted={Accepted} Score={Score} Status={Status} Reason={Reason} Revision={Revision} NativeMutationApplied=false",receipt==null?string.Empty:receipt.Id,diplomaticDecision.TargetHeroId,diplomaticDecision.Action,npcDecision.Accepted,npcDecision.Score,diplomaticDecision.Status,diplomaticDecision.ReasonCode,memory.Revision);
                                }
                                Logger.Information("AIPort diplomatic negotiation recorded ReceiptId={ReceiptId} PlayerHeroId={PlayerHeroId} TargetHeroId={TargetHeroId} SourceFactionId={SourceFactionId} TargetFactionId={TargetFactionId} Action={Action} Status={Status} ExpiresUtc={ExpiresUtc} ExpiredTransitions={ExpiredTransitions} Revision={Revision} NativeMutationApplied=false",receipt.Id,receipt.PlayerHeroId,receipt.TargetHeroId,receipt.SourceKingdomId,receipt.TargetKingdomId,receipt.Action,receipt.Status,receipt.ExpiresUtc,expired,memory.Revision);
                            }
                        }
                    }
                }
                else if(string.Equals(request.IntentType,"diplomacy_recipient_accept",StringComparison.Ordinal)||string.Equals(request.IntentType,"diplomacy_recipient_reject",StringComparison.Ordinal))
                {
                    DiplomacyRecipientDecisionPayload parsed;bool parsedOk=DiplomacyStatementCoordinator.TryParseRecipientDecision(request.PayloadJson,out parsed);string expectedDecision=string.Equals(request.IntentType,"diplomacy_recipient_accept",StringComparison.Ordinal)?"accept":"reject";PersistentDiplomaticStatementRecord resolved=null;bool changed=false,ok=false;string reason="invalid_payload";
                    if(!parsedOk||!string.Equals(parsed.Decision,expectedDecision,StringComparison.Ordinal))diplomaticDecision=new DiplomacyStatementDecision{IntentId=Guid.NewGuid().ToString("N"),Status="rejected",ReasonCode="invalid_payload",StateRevision=memory.Revision,MutationApplied=false};
                    else if(!string.Equals(request.CampaignGeneration,stateStore.CampaignGeneration,StringComparison.Ordinal))diplomaticDecision=new DiplomacyStatementDecision{IntentId=parsed.StatementId,Status="rejected",ReasonCode="generation_mismatch",StateRevision=memory.Revision,MutationApplied=false};
                    else if(diplomaticStatements.IsIdempotentResolution(parsed.StatementId,player.HeroId,parsed.Decision,out resolved))diplomaticDecision=new DiplomacyStatementDecision{IntentId=parsed.StatementId,Status=parsed.Decision=="accept"?"recipient_accepted_shadow":"recipient_rejected_shadow",ReasonCode="mutation_suppressed",StateRevision=memory.Revision,MutationApplied=false};
                    else if(request.ExpectedStateRevision!=0&&request.ExpectedStateRevision!=memory.Revision)diplomaticDecision=new DiplomacyStatementDecision{IntentId=parsed.StatementId,Status="rejected",ReasonCode="stale_revision",StateRevision=memory.Revision,MutationApplied=false};
                    else if(!stateStore.Enabled||!stateStore.Loaded||stateStore.ReadOnly)diplomaticDecision=new DiplomacyStatementDecision{IntentId=parsed.StatementId,Status="rejected",ReasonCode="persistent_state_unavailable",StateRevision=memory.Revision,MutationApplied=false};
                    else if(!diplomaticStatements.TryGet(parsed.StatementId,out resolved))diplomaticDecision=new DiplomacyStatementDecision{IntentId=parsed.StatementId,Status="rejected",ReasonCode="negotiation_not_found",StateRevision=memory.Revision,MutationApplied=false};
                    else
                    {
                        string contextFailure=string.Empty;if(parsed.Decision=="accept"){Hero sourceHero=Hero.Find(resolved.PlayerHeroId);DiplomacyAuthorityContext authority=diplomacyAuthority.Evaluate(sourceHero,playerHero);IFaction sourceFaction=null,targetFaction=null;try{sourceFaction=sourceHero==null?null:sourceHero.MapFaction;targetFaction=playerHero.MapFaction;}catch{}bool pairValid=sourceFaction!=null&&targetFaction!=null&&!sourceFaction.IsBanditFaction&&!targetFaction.IsBanditFaction&&string.Equals(sourceFaction.StringId,resolved.SourceKingdomId,StringComparison.Ordinal)&&string.Equals(targetFaction.StringId,resolved.TargetKingdomId,StringComparison.Ordinal);bool atWar=false;if(pairValid)try{atWar=FactionManager.IsAtWarAgainstFaction(sourceFaction,targetFaction);}catch{}if(!pairValid||!authority.PairAuthorized)contextFailure="stale_diplomatic_context";else if(resolved.Action=="war"&&atWar)contextFailure="already_at_war";else if(resolved.Action=="peace"&&!atWar)contextFailure="not_at_war";}
                        if(contextFailure.Length>0)diplomaticDecision=new DiplomacyStatementDecision{IntentId=parsed.StatementId,Status="rejected",ReasonCode=contextFailure,StateRevision=memory.Revision,Action=resolved.Action,TargetHeroId=resolved.TargetHeroId,SourceKingdomId=resolved.SourceKingdomId,TargetKingdomId=resolved.TargetKingdomId,MutationApplied=false};
                        else{ok=diplomaticStatements.TryResolve(parsed.StatementId,player.HeroId,parsed.Decision,DateTime.UtcNow,out resolved,out reason,out changed);if(changed)memory.AdvanceRevision(1);diplomaticDecision=new DiplomacyStatementDecision{IntentId=parsed.StatementId,Status=ok?(parsed.Decision=="accept"?"recipient_accepted_shadow":"recipient_rejected_shadow"):"rejected",ReasonCode=ok?"mutation_suppressed":reason,StateRevision=memory.Revision,Action=resolved==null?string.Empty:resolved.Action,TargetHeroId=resolved==null?string.Empty:resolved.TargetHeroId,SourceKingdomId=resolved==null?string.Empty:resolved.SourceKingdomId,TargetKingdomId=resolved==null?string.Empty:resolved.TargetKingdomId,MutationApplied=false};if(changed&&resolved!=null)NotifyDiplomacyLifecycle(resolved,resolved.LastReasonCode);}
                        Logger.Information("AIPort diplomatic recipient decision StatementId={StatementId} RecipientHeroId={RecipientHeroId} Decision={Decision} Status={Status} Reason={Reason} Changed={Changed} Revision={Revision} NativeMutationApplied=false",parsed.StatementId,player.HeroId,parsed.Decision,diplomaticDecision.Status,diplomaticDecision.ReasonCode,changed,memory.Revision);
                    }
                }
                else
                {
                    diplomaticDecision=EvaluateDiplomacyLifecycleIntent(peer,player,playerHero,request);
                }
                network.SendImmediate(peer,new AIIntentProposalResult(request.RequestId,diplomaticDecision.IntentId,diplomaticDecision.Status,diplomaticDecision.ReasonCode,diplomaticDecision.StateRevision));
                Logger.Information("AIPort diplomacy statement evaluated Type={IntentType} PeerId={PeerId} RequestId={RequestId} IntentId={IntentId} Action={Action} SourceFactionId={SourceFactionId} TargetFactionId={TargetFactionId} Status={Status} ReasonCode={ReasonCode} RevisionBefore={RevisionBefore} RevisionAfter={RevisionAfter} DiplomacyCount={DiplomacyCount} MutationApplied=false",request.IntentType,peer.Id,request.RequestId,diplomaticDecision.IntentId,diplomaticDecision.Action,diplomaticDecision.SourceKingdomId,diplomaticDecision.TargetKingdomId,diplomaticDecision.Status,diplomaticDecision.ReasonCode,revisionBefore,memory.Revision,diplomaticStatements.Count);
                return;
            }
            IntentDecision decision;
            bool relationIntent = string.Equals(request.IntentType, "relation_change_shadow", StringComparison.Ordinal)
                || string.Equals(request.IntentType, "relation_change_proposal", StringComparison.Ordinal)
                || string.Equals(request.IntentType, "relation_change_confirm", StringComparison.Ordinal);
            if (relationIntent)
            {
                long revisionBefore = memory.Revision;
                ConversationTargetBinding binding;
                targetLeases.TryGet(peer.Id, out binding);
                if (!settings.EnableRelationShadowIntents)
                    decision = new IntentDecision { IntentId = Guid.NewGuid().ToString("N"), Status = "rejected", ReasonCode = "capability_disabled", StateRevision = memory.Revision, MutationApplied = false };
                else if (string.Equals(request.IntentType, "relation_change_proposal", StringComparison.Ordinal))
                    decision = intents.EvaluateRelationProposal(peer.Id, player.HeroId, request.RequestId, request.CampaignGeneration, stateStore.CampaignGeneration, request.ExpectedStateRevision, memory.Revision, request.PayloadJson, binding);
                else if (string.Equals(request.IntentType, "relation_change_confirm", StringComparison.Ordinal))
                {
                    decision = intents.EvaluateRelationConfirmation(peer.Id, player.HeroId, request.RequestId, request.CampaignGeneration, stateStore.CampaignGeneration, request.ExpectedStateRevision, memory.Revision, request.PayloadJson, binding);
                    if (string.Equals(decision.Status, "confirmed_shadow", StringComparison.Ordinal))
                    {
                        PersistentSocialRecord receipt; string socialReason; bool newlyApplied;
                        if (!stateStore.Enabled || !stateStore.Loaded || stateStore.ReadOnly)
                        {
                            decision.Status = "rejected"; decision.ReasonCode = "persistent_state_unavailable"; decision.StateRevision = memory.Revision;
                        }
                        else if (!socialLedger.TryApply(decision.IntentId, player.HeroId, decision.TargetInstanceId, decision.Delta, DateTime.UtcNow, out receipt, out socialReason, out newlyApplied))
                        {
                            decision.Status = "rejected"; decision.ReasonCode = socialReason; decision.StateRevision = memory.Revision;
                        }
                        else if (newlyApplied)
                        {
                            memory.AdvanceRevision(1); decision.StateRevision = memory.Revision;
                            Logger.Information("AIPort social shadow receipt recorded ReceiptId={ReceiptId} PlayerHeroId={PlayerHeroId} TargetInstanceId={TargetInstanceId} Delta={Delta} Before={Before} After={After} Revision={Revision} NativeMutationApplied=false", receipt.Id, receipt.PlayerHeroId, receipt.TargetInstanceId, receipt.Delta, receipt.BeforeValue, receipt.AfterValue, memory.Revision);
                        }
                    }
                }
                else
                    decision = intents.EvaluateRelationShadow(peer.Id, player.HeroId, request.RequestId, request.CampaignGeneration, stateStore.CampaignGeneration, request.ExpectedStateRevision, memory.Revision, request.PayloadJson, binding);
                Logger.Information("AIPort relation intent evaluated Type={IntentType} PeerId={PeerId} RequestId={RequestId} IntentId={IntentId} TargetInstanceId={TargetInstanceId} Delta={Delta} Status={Status} ReasonCode={ReasonCode} MutationApplied={MutationApplied} RevisionBefore={RevisionBefore} RevisionAfter={RevisionAfter} PendingCount={PendingCount} AuditCount={AuditCount} SocialCount={SocialCount}", request.IntentType, peer.Id, request.RequestId, decision.IntentId, decision.TargetInstanceId, decision.Delta, decision.Status, decision.ReasonCode, decision.MutationApplied, revisionBefore, memory.Revision, intents.PendingCount, intents.AuditCount, socialLedger.Count);
            }
            else
            {
                decision = intents.EvaluateNoMutation(peer.Id, player.HeroId, request.RequestId, request.CampaignGeneration, stateStore.CampaignGeneration, request.ExpectedStateRevision, memory.Revision, request.IntentType, request.PayloadJson);
                Logger.Information("AIPort no-mutation intent evaluated PeerId={PeerId} RequestId={RequestId} IntentId={IntentId} Status={Status} ReasonCode={ReasonCode} AuditCount={AuditCount}", peer.Id, request.RequestId, decision.IntentId, decision.Status, decision.ReasonCode, intents.AuditCount);
            }
            network.SendImmediate(peer, new AIIntentProposalResult(request.RequestId, decision.IntentId, decision.Status, decision.ReasonCode, decision.StateRevision));
        }

        private void Handle(MessagePayload<AIPortValidationGateRequest> payload)
        {
            NetPeer peer=payload.Who as NetPeer;if(peer==null)return;AIPortValidationGateRequest request=payload.What;
            if(request.ProtocolVersion!=AIPortProtocol.Version||!IsCorrelationId(request.RequestId))return;
            if(!campaignReady||!settings.EnableIntentFoundation||!settings.EnablePersistentMemory)
            {network.SendImmediate(peer,new AIPortValidationGateResponse(request.RequestId,false,string.Empty,"gate_unavailable",memory.Revision));return;}
            if(!string.Equals(request.CampaignGeneration,stateStore.CampaignGeneration,StringComparison.Ordinal))
            {network.SendImmediate(peer,new AIPortValidationGateResponse(request.RequestId,false,string.Empty,"generation_mismatch",memory.Revision));return;}
            if(request.ExpectedStateRevision!=0&&request.ExpectedStateRevision!=memory.Revision)
            {network.SendImmediate(peer,new AIPortValidationGateResponse(request.RequestId,false,string.Empty,"stale_revision",memory.Revision));return;}
            Player player;string failure;Hero playerHero;MobileParty playerParty;string objectFailure;
            if(!resolver.TryResolve(peer,out player,out failure)||!resolver.TryResolveControlledCampaignObjects(player,out playerHero,out playerParty,out objectFailure))
            {network.SendImmediate(peer,new AIPortValidationGateResponse(request.RequestId,false,string.Empty,"player_unresolved",memory.Revision));return;}
            ConversationTargetBinding binding;targetLeases.TryGet(peer.Id,out binding);if(binding!=null&&!ConversationTargetValidator.IsStillEligible(player,playerHero,playerParty,binding))binding=null;
            int flags=AIPortProtocol.CapabilityNarrative|AIPortProtocol.CapabilityNoOpIntent;if(settings.EnableStateSnapshots)flags|=AIPortProtocol.CapabilityStateSnapshot;if(settings.EnablePersistentMemory)flags|=AIPortProtocol.CapabilityPersistentMemory;if(settings.EnableRelationShadowIntents)flags|=AIPortProtocol.CapabilityRelationShadowIntent|AIPortProtocol.CapabilityRelationConfirmation;if(settings.EnableIntentFoundation)flags|=AIPortProtocol.CapabilityDiplomacySnapshot;if(settings.EnableIntentFoundation&&settings.EnablePersistentMemory)flags|=AIPortProtocol.CapabilityDiplomacyStatements|AIPortProtocol.CapabilityValidationGate|AIPortProtocol.CapabilityDiplomacyAuthority|AIPortProtocol.CapabilityDiplomacyRecipientConsent|AIPortProtocol.CapabilityDiplomacyConflictGuard|AIPortProtocol.CapabilityDiplomacyInboxNotification|AIPortProtocol.CapabilityDiplomacyLifecycleBundle|AIPortProtocol.CapabilityNativeWarAdapter | AIPortProtocol.CapabilityNativeDiplomacyJournal | AIPortProtocol.CapabilityNativePeaceAdapter | AIPortProtocol.CapabilityNpcDiplomacyPolicy | AIPortProtocol.CapabilityDiplomacyDecisionUi | AIPortProtocol.CapabilityDiplomacyInboxList | AIPortProtocol.CapabilityNpcDiplomacyInitiativeScheduler;
            RuntimeValidationGateResult gateResult;bool accepted=validationGate.TryBuild(request.Mode,flags,settings,stateStore,memory,socialLedger,diplomaticStatements,nativeDiplomacyJournal,playerHero,binding,out gateResult);
            string display=gateResult==null?string.Empty:gateResult.Text,reason=gateResult==null?"validation_failed":gateResult.Reason;network.SendImmediate(peer,new AIPortValidationGateResponse(request.RequestId,accepted,display,reason,memory.Revision));
            Logger.Information("AIPort validation gate Mode={Mode} PeerId={PeerId} PlayerHeroId={PlayerHeroId} TargetInstanceId={TargetInstanceId} Accepted={Accepted} Reason={Reason} Revision={Revision} SocialCount={SocialCount} DiplomacyCount={DiplomacyCount} HasBaseline={HasBaseline} SameTarget={SameTarget} NativeRelationUnchanged={NativeRelationUnchanged} NativeWarStateUnchanged={NativeWarStateUnchanged} RevisionDelta={RevisionDelta} MemoryDelta={MemoryDelta} SocialDelta={SocialDelta} DiplomacyDelta={DiplomacyDelta} CustomScoreDelta={CustomScoreDelta} SourceDiplomaticAuthority={SourceDiplomaticAuthority} TargetDiplomaticAuthority={TargetDiplomaticAuthority} MutationApplied=false",request.Mode,peer.Id,player.HeroId,binding==null?string.Empty:binding.TargetInstanceId,accepted,reason,memory.Revision,socialLedger.Export(player.HeroId).Count,diplomaticStatements.Export(player.HeroId).Count,gateResult!=null&&gateResult.HasBaseline,gateResult!=null&&gateResult.SameTarget,gateResult!=null&&gateResult.NativeRelationUnchanged,gateResult!=null&&gateResult.NativeWarStateUnchanged,gateResult==null?0:gateResult.RevisionDelta,gateResult==null?0:gateResult.MemoryDelta,gateResult==null?0:gateResult.SocialDelta,gateResult==null?0:gateResult.DiplomacyDelta,gateResult==null?0:gateResult.CustomScoreDelta,gateResult!=null&&gateResult.SourceDiplomaticAuthority,gateResult!=null&&gateResult.TargetDiplomaticAuthority);
        }

        private void Handle(MessagePayload<AIDiplomacySnapshotRequest> payload)
        {
            NetPeer peer=payload.Who as NetPeer;if(peer==null)return;AIDiplomacySnapshotRequest request=payload.What;
            if(request.ProtocolVersion!=AIPortProtocol.Version||!IsCorrelationId(request.RequestId))return;
            if(!campaignReady||!settings.EnableIntentFoundation)
            {
                network.SendImmediate(peer,new AIDiplomacySnapshotResponse(request.RequestId,false,string.Empty,"not_ready",memory.Revision));return;
            }
            if(!string.Equals(request.CampaignGeneration,stateStore.CampaignGeneration,StringComparison.Ordinal))
            {
                network.SendImmediate(peer,new AIDiplomacySnapshotResponse(request.RequestId,false,string.Empty,"generation_mismatch",memory.Revision));return;
            }
            if(request.ExpectedStateRevision!=0&&request.ExpectedStateRevision!=memory.Revision)
            {
                network.SendImmediate(peer,new AIDiplomacySnapshotResponse(request.RequestId,false,string.Empty,"stale_revision",memory.Revision));return;
            }
            Player player;string failure;Hero playerHero;MobileParty playerParty;string objectFailure;
            if(!resolver.TryResolve(peer,out player,out failure)||!resolver.TryResolveControlledCampaignObjects(player,out playerHero,out playerParty,out objectFailure))
            {
                network.SendImmediate(peer,new AIDiplomacySnapshotResponse(request.RequestId,false,string.Empty,"player_unresolved",memory.Revision));return;
            }
            ConversationTargetBinding binding;targetLeases.TryGet(peer.Id,out binding);if(binding!=null&&!ConversationTargetValidator.IsStillEligible(player,playerHero,playerParty,binding))binding=null;Hero targetHero=binding==null||!binding.IsHero?null:Hero.Find(binding.TargetId);DiplomacyAuthorityContext authority=diplomacyAuthority.Evaluate(playerHero,targetHero);
            DateTime now=DateTime.UtcNow;int expired=ExpireDiplomacyDue(now);string display=diplomacySnapshots.Build(playerHero,targetHero)+"\n\n"+diplomaticStatements.BuildInbox(player.HeroId,now)+"\n\n"+diplomaticStatements.BuildHistory(player.HeroId,now);
            network.SendImmediate(peer,new AIDiplomacySnapshotResponse(request.RequestId,true,display,"read_only_snapshot",memory.Revision));
            Logger.Information("AIPort diplomacy snapshot sent PeerId={PeerId} PlayerHeroId={PlayerHeroId} TargetHeroId={TargetHeroId} RequestId={RequestId} Revision={Revision} SourceAuthority={SourceAuthority} TargetAuthority={TargetAuthority} PairAuthorized={PairAuthorized} ExpiredTransitions={ExpiredTransitions} Chars={Chars} MutationApplied=false",peer.Id,player.HeroId,authority.TargetHeroId,request.RequestId,memory.Revision,authority.SourceAuthorized,authority.TargetAuthorized,authority.PairAuthorized,expired,display==null?0:display.Length);
        }

        private void Handle(MessagePayload<AIDiplomacyInboxPageRequest> payload)
        {
            NetPeer peer=payload.Who as NetPeer;if(peer==null)return;AIDiplomacyInboxPageRequest request=payload.What;if(request==null)return;
            if(request.ProtocolVersion!=AIPortProtocol.Version||!IsCorrelationId(request.RequestId))return;
            if(!campaignReady||!settings.EnableIntentFoundation||!settings.EnablePersistentMemory||!stateStore.Loaded||stateStore.ReadOnly)
            {SendDiplomacyInboxPageRejected(peer,request,"inbox_unavailable");return;}
            if(stateStore.IsSaving){SendDiplomacyInboxPageRejected(peer,request,"save_in_progress");return;}
            if(!string.Equals(request.CampaignGeneration,stateStore.CampaignGeneration,StringComparison.Ordinal))
            {SendDiplomacyInboxPageRejected(peer,request,"generation_mismatch");return;}
            bool continuation=!string.IsNullOrWhiteSpace(request.AfterStatementId);
            if(request.PageSize<1||request.PageSize>AIPortProtocol.MaximumDiplomacyInboxPageSize||(continuation&&!IsCorrelationId(request.AfterStatementId)))
            {SendDiplomacyInboxPageRejected(peer,request,"invalid_page_request");return;}
            int expired=ExpireDiplomacyDue(DateTime.UtcNow);
            if(request.ExpectedStateRevision>memory.Revision||(continuation&&request.ExpectedStateRevision!=memory.Revision))
            {SendDiplomacyInboxPageRejected(peer,request,"stale_revision");return;}
            Player player;string failure;Hero playerHero;MobileParty playerParty;string objectFailure;
            if(!resolver.TryResolve(peer,out player,out failure)||!resolver.TryResolveControlledCampaignObjects(player,out playerHero,out playerParty,out objectFailure))
            {SendDiplomacyInboxPageRejected(peer,request,"player_unresolved");return;}
            RegisterResolvedPeerHero(peer,player.HeroId);
            List<PersistentDiplomaticStatementRecord> page;string nextCursor,reason;bool hasMore;int totalCount;
            if(!diplomaticStatements.TryGetPendingIncomingPage(player.HeroId,DateTime.UtcNow,request.AfterStatementId,request.PageSize,out page,out nextCursor,out hasMore,out totalCount,out reason))
            {SendDiplomacyInboxPageRejected(peer,request,reason);return;}
            AIDiplomacyInboxEntry[] entries=new AIDiplomacyInboxEntry[page.Count];for(int i=0;i<page.Count;i++)entries[i]=BuildDiplomacyInboxEntry(page[i]);
            network.SendImmediate(peer,new AIDiplomacyInboxPageResponse(AIPortProtocol.Version,request.RequestId,true,stateStore.CampaignGeneration,memory.Revision,totalCount,entries,nextCursor,hasMore,"inbox_ready"));
            Logger.Information("AIPort typed diplomacy inbox page sent PeerId={PeerId} PlayerHeroId={PlayerHeroId} RequestId={RequestId} Cursor={Cursor} Entries={Entries} TotalCount={TotalCount} HasMore={HasMore} Revision={Revision} ExpiredTransitions={ExpiredTransitions}",peer.Id,player.HeroId,request.RequestId,request.AfterStatementId,page.Count,totalCount,hasMore,memory.Revision,expired);
        }

        private void SendDiplomacyInboxPageRejected(NetPeer peer,AIDiplomacyInboxPageRequest request,string reason)
        {
            if(peer==null||request==null)return;network.SendImmediate(peer,new AIDiplomacyInboxPageResponse(AIPortProtocol.Version,request.RequestId,false,stateStore.CampaignGeneration,memory.Revision,0,new AIDiplomacyInboxEntry[0],string.Empty,false,reason??"inbox_rejected"));
            Logger.Information("AIPort typed diplomacy inbox page rejected PeerId={PeerId} RequestId={RequestId} Reason={Reason} Revision={Revision}",peer.Id,request.RequestId,reason,memory.Revision);
        }

        private AIDiplomacyInboxEntry BuildDiplomacyInboxEntry(PersistentDiplomaticStatementRecord record)
        {
            Hero sourceHero=null,targetHero=null;resolver.TryResolveCampaignHero(record==null?string.Empty:record.PlayerHeroId,out sourceHero);resolver.TryResolveCampaignHero(record==null?string.Empty:record.TargetHeroId,out targetHero);
            IFaction sourceFaction=null,targetFaction=null;try{sourceFaction=sourceHero==null?null:sourceHero.MapFaction;targetFaction=targetHero==null?null:targetHero.MapFaction;}catch{}
            return new AIDiplomacyInboxEntry(record==null?string.Empty:record.Id,record==null?string.Empty:record.Action,record==null?string.Empty:record.PlayerHeroId,SafeDisplayName(sourceHero),record==null?string.Empty:record.SourceKingdomId,SafeFactionDisplayName(sourceFaction),record==null?string.Empty:record.TargetKingdomId,SafeFactionDisplayName(targetFaction),FormatUtc(record==null?DateTime.MinValue:record.OccurredUtc),FormatUtc(record==null?DateTime.MinValue:record.ExpiresUtc),record==null?string.Empty:record.Origin,record==null?string.Empty:record.InitiativeReasonCode,record==null?0:record.InitiativeScore,record==null?string.Empty:record.TargetHeroId);
        }

        private void Handle(MessagePayload<AIPortStateSnapshotRequest> payload)
        {
            NetPeer peer = payload.Who as NetPeer; if (peer == null) return; AIPortStateSnapshotRequest request = payload.What;
            if (request.ProtocolVersion != AIPortProtocol.Version || !IsCorrelationId(request.RequestId) || !settings.EnableStateSnapshots) return;
            Player player; string failure;
            if (!campaignReady || !resolver.TryResolve(peer, out player, out failure))
            {
                network.SendImmediate(peer, new AIPortStateSnapshotResponse(request.RequestId, false, stateStore.CampaignGeneration, memory.Revision, string.Empty, string.Empty, "player_unresolved", 1000)); return;
            }
            if (!string.IsNullOrWhiteSpace(request.CampaignGeneration) && !string.Equals(request.CampaignGeneration, stateStore.CampaignGeneration, StringComparison.Ordinal))
            {
                network.SendImmediate(peer, new AIPortStateSnapshotResponse(request.RequestId, false, stateStore.CampaignGeneration, memory.Revision, string.Empty, string.Empty, "generation_mismatch", 0)); return;
            }
            RegisterResolvedPeerHero(peer,player.HeroId);
            string json, hash, reason; bool ready = stateStore.TryBuildPrivateSnapshot(memory, socialLedger, diplomaticStatements, player.HeroId, out json, out hash, out reason);
            network.SendImmediate(peer, new AIPortStateSnapshotResponse(request.RequestId, ready, stateStore.CampaignGeneration, memory.Revision, ready ? json : string.Empty, ready ? hash : string.Empty, ready ? string.Empty : reason, ready ? 0 : 1000));
            Logger.Information("AIPort private state snapshot PeerId={PeerId} PlayerHeroId={PlayerHeroId} Ready={Ready} Revision={Revision} Chars={Chars} Reason={Reason}", peer.Id, player.HeroId, ready, memory.Revision, json == null ? 0 : json.Length, reason);
            if(ready)NotifyPeerInbox(peer,player.HeroId,"snapshot_pending_negotiations");
        }

        private DiplomacyStatementDecision EvaluateDiplomacyLifecycleIntent(NetPeer peer,Player player,Hero playerHero,AIIntentProposalRequest request)
        {
            string type=request.IntentType??string.Empty;DiplomacyLifecycleCommandPayload command=null;bool withdrawal=string.Equals(type,"diplomacy_source_withdraw",StringComparison.Ordinal),preflight=string.Equals(type,"diplomacy_native_preflight",StringComparison.Ordinal)||string.Equals(type,"diplomacy_native_war_preflight",StringComparison.Ordinal),warCommit=string.Equals(type,"diplomacy_native_war_commit",StringComparison.Ordinal),peaceCommit=string.Equals(type,"diplomacy_native_peace_commit",StringComparison.Ordinal);bool parsed=withdrawal?DiplomacyStatementCoordinator.TryParseWithdraw(request.PayloadJson,out command):preflight?DiplomacyStatementCoordinator.TryParseNativePreflight(request.PayloadJson,out command):warCommit?DiplomacyStatementCoordinator.TryParseNativeCommit(request.PayloadJson,out command):peaceCommit&&DiplomacyStatementCoordinator.TryParseNativePeaceCommit(request.PayloadJson,out command);
            if(!parsed||command==null)return LifecycleDecision(Guid.NewGuid().ToString("N"),"rejected","invalid_payload",memory.Revision,null,false);
            if(!string.Equals(request.CampaignGeneration,stateStore.CampaignGeneration,StringComparison.Ordinal))return LifecycleDecision(command.StatementId,"rejected","generation_mismatch",memory.Revision,null,false);
            PersistentDiplomaticStatementRecord record;if(!diplomaticStatements.TryGet(command.StatementId,out record))return LifecycleDecision(command.StatementId,"rejected","negotiation_not_found",memory.Revision,null,false);if(!string.Equals(record.PlayerHeroId,player.HeroId,StringComparison.Ordinal))return LifecycleDecision(command.StatementId,"rejected","source_not_authorized",memory.Revision,record,false);
            if(withdrawal)
            {
                PersistentDiplomaticStatementRecord idempotent;if(diplomaticStatements.IsIdempotentWithdrawal(command.StatementId,player.HeroId,out idempotent))return LifecycleDecision(command.StatementId,"source_withdrawn_shadow","mutation_suppressed",memory.Revision,idempotent,false);if(request.ExpectedStateRevision!=0&&request.ExpectedStateRevision!=memory.Revision)return LifecycleDecision(command.StatementId,"rejected","stale_revision",memory.Revision,record,false);if(!stateStore.Enabled||!stateStore.Loaded||stateStore.ReadOnly||stateStore.IsSaving)return LifecycleDecision(command.StatementId,"rejected","persistent_state_unavailable",memory.Revision,record,false);bool changed;string reason;bool ok=diplomaticStatements.TryWithdraw(command.StatementId,player.HeroId,DateTime.UtcNow,out record,out reason,out changed);if(changed)memory.AdvanceRevision(1);if(changed&&record!=null)NotifyDiplomacyLifecycle(record,reason);Logger.Information("AIPort diplomacy source withdrawal StatementId={StatementId} SourceHeroId={SourceHeroId} Accepted={Accepted} Changed={Changed} Reason={Reason} Revision={Revision} NativeMutationApplied=false",command.StatementId,player.HeroId,ok,changed,reason,memory.Revision);return LifecycleDecision(command.StatementId,ok?"source_withdrawn_shadow":"rejected",ok?"mutation_suppressed":reason,memory.Revision,record,false);
            }
            if(request.ExpectedStateRevision!=0&&request.ExpectedStateRevision!=memory.Revision)return LifecycleDecision(command.StatementId,"rejected","stale_revision",memory.Revision,record,false);if(!stateStore.Enabled||!stateStore.Loaded||stateStore.ReadOnly||stateStore.IsSaving)return LifecycleDecision(command.StatementId,"rejected","persistent_state_unavailable",memory.Revision,record,false);
            string expectedAction=warCommit?"war":peaceCommit?"peace":record.Action;if((warCommit||peaceCommit)&&!string.Equals(record.Action,expectedAction,StringComparison.Ordinal))return LifecycleDecision(command.StatementId,"rejected","native_diplomacy_action_mismatch",memory.Revision,record,false);
            string committedStatus=record.Action=="war"?"committed_native_war":"committed_native_peace",committedReason=record.Action=="war"?"native_war_applied":"native_peace_applied";if(string.Equals(record.Status,committedStatus,StringComparison.Ordinal)&&record.NativeMutationApplied)return LifecycleDecision(record.Id,record.Action=="war"?"native_war_committed":"native_peace_committed",committedReason,memory.Revision,record,true);
            IFaction sourceFaction,targetFaction;Hero sourceHero,targetHero;string contextFailure;if(!TryValidateNativeDiplomacyContext(record,player.HeroId,playerHero,out sourceHero,out targetHero,out sourceFaction,out targetFaction,out contextFailure))return LifecycleDecision(command.StatementId,"rejected",contextFailure,memory.Revision,record,false);
            if(preflight)
            {
                string armFailure;if(!IsNativeDiplomacyActionArmed(record.Action,out armFailure)){Logger.Information("AIPort native diplomacy dry-run ready StatementId={StatementId} Action={Action} SourceHeroId={SourceHeroId} SourceFactionId={SourceFactionId} TargetFactionId={TargetFactionId} ArmFailure={ArmFailure} NativeMutationApplied=false",record.Id,record.Action,player.HeroId,record.SourceKingdomId,record.TargetKingdomId,armFailure);return LifecycleDecision(record.Id,record.Action=="war"?"native_war_dry_run_ready":"native_peace_dry_run_ready",armFailure,memory.Revision,record,false);}
                NativeWarCommitLease lease=nativeWarCommitLeases.Issue(peer.Id,player.HeroId,record.Id,record.Action,stateStore.CampaignGeneration,memory.Revision,record.SourceKingdomId,record.TargetKingdomId,DateTime.UtcNow);Logger.Warning("AIPort native diplomacy commit lease issued StatementId={StatementId} Action={Action} SourceHeroId={SourceHeroId} Revision={Revision} ExpiresUtc={ExpiresUtc} NativeMutationApplied=false",record.Id,record.Action,player.HeroId,memory.Revision,lease.ExpiresUtc);return LifecycleDecision(lease.Token,record.Action=="war"?"native_war_commit_ready":"native_peace_commit_ready","explicit_native_diplomacy_commit_required",memory.Revision,record,false);
            }
            string armedFailure;if(!IsNativeDiplomacyActionArmed(record.Action,out armedFailure))return LifecycleDecision(record.Id,"rejected",armedFailure,memory.Revision,record,false);NativeWarCommitLease consumed;string leaseReason;if(!nativeWarCommitLeases.TryConsume(command.CommitToken,peer.Id,player.HeroId,record.Id,record.Action,stateStore.CampaignGeneration,memory.Revision,record.SourceKingdomId,record.TargetKingdomId,DateTime.UtcNow,out consumed,out leaseReason))return LifecycleDecision(record.Id,"rejected",leaseReason,memory.Revision,record,false);if(!TryValidateNativeDiplomacyContext(record,player.HeroId,playerHero,out sourceHero,out targetHero,out sourceFaction,out targetFaction,out contextFailure))return LifecycleDecision(record.Id,"rejected",contextFailure,memory.Revision,record,false);return ExecuteNativeDiplomacyCommit(record,player.HeroId,sourceFaction,targetFaction);
        }
        private bool TryValidateNativeDiplomacyContext(PersistentDiplomaticStatementRecord record,string authoritativeSourceHeroId,Hero authoritativeSourceHero,out Hero sourceHero,out Hero targetHero,out IFaction sourceFaction,out IFaction targetFaction,out string failure)
        {
            sourceHero=null;targetHero=null;sourceFaction=null;targetFaction=null;failure="native_diplomacy_context_invalid";if(record==null||(record.Action!="war"&&record.Action!="peace")){failure="native_diplomacy_action_required";return false;}if(!string.Equals(record.Status,"accepted_shadow",StringComparison.Ordinal)){failure=record.Action=="war"?"native_war_status_not_eligible":"native_peace_status_not_eligible";return false;}if(authoritativeSourceHero==null||string.IsNullOrWhiteSpace(authoritativeSourceHeroId)||!string.Equals(record.PlayerHeroId,authoritativeSourceHeroId,StringComparison.Ordinal)){failure="source_not_authorized";return false;}sourceHero=authoritativeSourceHero;if(!resolver.TryResolveCampaignHero(record.TargetHeroId,out targetHero)||targetHero==null){failure="stale_diplomatic_context";return false;}try{sourceFaction=sourceHero.MapFaction;targetFaction=targetHero.MapFaction;}catch{}if(sourceFaction==null||targetFaction==null||sourceFaction.IsBanditFaction||targetFaction.IsBanditFaction||ReferenceEquals(sourceFaction,targetFaction)||!string.Equals(sourceFaction.StringId,record.SourceKingdomId,StringComparison.Ordinal)||!string.Equals(targetFaction.StringId,record.TargetKingdomId,StringComparison.Ordinal)){failure="stale_faction_pair";return false;}DiplomacyAuthorityContext authority=diplomacyAuthority.Evaluate(sourceHero,targetHero);if(!authority.PairAuthorized){failure="stale_diplomatic_authority";return false;}bool atWar;try{atWar=FactionManager.IsAtWarAgainstFaction(sourceFaction,targetFaction);}catch{failure="native_diplomacy_precondition_failed";return false;}if(record.Action=="war"&&atWar){failure="already_at_war";return false;}if(record.Action=="peace"&&!atWar){failure="not_at_war";return false;}failure=string.Empty;return true;
        }
        private bool IsNativeDiplomacyActionArmed(string action,out string failure)
        {
            failure=action=="peace"?"native_peace_adapter_disabled":"native_war_adapter_disabled";bool adapter=action=="peace"?settings.EnableNativePeaceAdapter:settings.EnableNativeWarAdapter;if(!adapter)return false;if(string.IsNullOrWhiteSpace(settings.NativeDiplomacyGenerationPin)||!string.Equals(settings.NativeDiplomacyGenerationPin,stateStore.CampaignGeneration,StringComparison.Ordinal)){failure="native_diplomacy_generation_not_pinned";return false;}failure=string.Empty;return true;
        }
        private DiplomacyStatementDecision ExecuteNativeDiplomacyCommit(PersistentDiplomaticStatementRecord record,string sourceHeroId,IFaction sourceFaction,IFaction targetFaction)
        {
            DateTime now=DateTime.UtcNow;PersistentNativeDiplomacyCommitRecord journalRecord;string journalReason;bool fresh;if(!nativeDiplomacyJournal.TryPrepare(record.Id,record.Action,record.PlayerHeroId,record.TargetHeroId,record.SourceKingdomId,record.TargetKingdomId,stateStore.CampaignGeneration,memory.Revision,now,out journalRecord,out journalReason,out fresh))return LifecycleDecision(record.Id,"rejected",journalReason,memory.Revision,record,false);if(!fresh){if(journalRecord.Phase=="verified"&&journalRecord.NativeMutationApplied)return LifecycleDecision(record.Id,record.Action=="war"?"native_war_committed":"native_peace_committed",record.Action=="war"?"native_war_applied":"native_peace_applied",memory.Revision,record,true);return LifecycleDecision(record.Id,"rejected",journalReason,memory.Revision,record,false);}
            string persistReason;if(!stateStore.PersistNativeJournal(nativeDiplomacyJournal,out persistReason))return LifecycleDecision(record.Id,"rejected",persistReason,memory.Revision,record,false);PersistentNativeDiplomacyCommitRecord transitioned;string transitionFailure;if(!nativeDiplomacyJournal.TryTransition(journalRecord.Id,"applying",DateTime.UtcNow,"native_commit_applying",false,record.Action=="peace",out transitioned,out transitionFailure)||!stateStore.PersistNativeJournal(nativeDiplomacyJournal,out persistReason))return LifecycleDecision(record.Id,"rejected",transitionFailure.Length>0?transitionFailure:persistReason,memory.Revision,record,false);
            bool accepted,callAttempted,mutation,atWarAfter;string applyReason;if(record.Action=="war"){NativeWarAdapterResult result=nativeWarAdapter.TryApply(true,sourceFaction,targetFaction);accepted=result.Accepted;callAttempted=result.NativeCallAttempted;mutation=result.NativeMutationApplied;atWarAfter=result.AtWarAfter;applyReason=result.ReasonCode;}else{NativePeaceAdapterResult result=nativePeaceAdapter.TryApply(true,sourceFaction,targetFaction);accepted=result.Accepted;callAttempted=result.NativeCallAttempted;mutation=result.NativeMutationApplied;atWarAfter=result.AtWarAfter;applyReason=result.ReasonCode;}
            if(!accepted||!mutation){string failurePhase=callAttempted?"recovery_required":"failed";nativeDiplomacyJournal.TryTransition(journalRecord.Id,failurePhase,DateTime.UtcNow,applyReason,false,atWarAfter,out transitioned,out transitionFailure);stateStore.PersistNativeJournal(nativeDiplomacyJournal,out persistReason);Logger.Error("AIPort native diplomacy adapter rejected StatementId={StatementId} Action={Action} Reason={Reason} NativeCallAttempted={NativeCallAttempted} JournalPhase={JournalPhase} AtWarAfter={AtWarAfter} NativeMutationApplied={NativeMutationApplied}",record.Id,record.Action,applyReason,callAttempted,failurePhase,atWarAfter,mutation);return LifecycleDecision(record.Id,"rejected",applyReason,memory.Revision,record,false);}
            nativeDiplomacyJournal.TryTransition(journalRecord.Id,"applied",DateTime.UtcNow,applyReason,true,atWarAfter,out transitioned,out transitionFailure);bool journalAppliedPersisted=stateStore.PersistNativeJournal(nativeDiplomacyJournal,out persistReason);bool ledgerChanged,ledgerOk;string ledgerReason;if(record.Action=="war")ledgerOk=diplomaticStatements.TryMarkNativeWarCommitted(record.Id,sourceHeroId,DateTime.UtcNow,out record,out ledgerReason,out ledgerChanged);else ledgerOk=diplomaticStatements.TryMarkNativePeaceCommitted(record.Id,sourceHeroId,DateTime.UtcNow,out record,out ledgerReason,out ledgerChanged);if(ledgerChanged)memory.AdvanceRevision(1);
            if(!ledgerOk){nativeDiplomacyJournal.TryTransition(journalRecord.Id,"recovery_required",DateTime.UtcNow,"native_ledger_transition_failed",true,atWarAfter,out transitioned,out transitionFailure);stateStore.PersistNativeJournal(nativeDiplomacyJournal,out persistReason);Logger.Fatal("AIPort native diplomacy applied but ledger transition failed StatementId={StatementId} Action={Action} Reason={Reason}",record.Id,record.Action,ledgerReason);return LifecycleDecision(record.Id,"native_diplomacy_committed_unrecorded","native_ledger_transition_failed",memory.Revision,record,true);}
            if(!journalAppliedPersisted){Logger.Fatal("AIPort native diplomacy applied but journal applied-phase persist failed StatementId={StatementId} Action={Action} Reason={Reason}",record.Id,record.Action,persistReason);NotifyDiplomacyLifecycle(record,"native_journal_persist_failed");return LifecycleDecision(record.Id,"native_diplomacy_committed_unjournaled","native_journal_persist_failed",memory.Revision,record,true);}
            nativeDiplomacyJournal.TryTransition(journalRecord.Id,"verified",DateTime.UtcNow,ledgerReason,true,atWarAfter,out transitioned,out transitionFailure);if(!stateStore.PersistNativeJournal(nativeDiplomacyJournal,out persistReason)){Logger.Fatal("AIPort native diplomacy verified but final journal persist failed StatementId={StatementId} Action={Action} Reason={Reason}",record.Id,record.Action,persistReason);NotifyDiplomacyLifecycle(record,"native_journal_persist_failed");return LifecycleDecision(record.Id,"native_diplomacy_committed_unjournaled","native_journal_persist_failed",memory.Revision,record,true);}NotifyDiplomacyLifecycle(record,ledgerReason);Logger.Warning("AIPort native diplomacy committed StatementId={StatementId} Action={Action} SourceHeroId={SourceHeroId} SourceFactionId={SourceFactionId} TargetFactionId={TargetFactionId} Revision={Revision} JournalId={JournalId} NativeMutationApplied=true",record.Id,record.Action,sourceHeroId,record.SourceKingdomId,record.TargetKingdomId,memory.Revision,journalRecord.Id);return LifecycleDecision(record.Id,record.Action=="war"?"native_war_committed":"native_peace_committed",ledgerReason,memory.Revision,record,true);
        }
        private int ReconcileNativeDiplomacyJournal(string source)
        {
            int changed=0;List<PersistentNativeDiplomacyCommitRecord> recoverable=nativeDiplomacyJournal.Recoverable();foreach(PersistentNativeDiplomacyCommitRecord item in recoverable){PersistentNativeDiplomacyCommitRecord transitioned;string transitionFailure;if(!string.Equals(item.CampaignGeneration,stateStore.CampaignGeneration,StringComparison.Ordinal)){if(nativeDiplomacyJournal.TryTransition(item.Id,item.Phase=="prepared"?"aborted":"failed",DateTime.UtcNow,"recovery_generation_mismatch",false,item.AtWarObserved,out transitioned,out transitionFailure))changed++;continue;}if(item.Phase=="prepared"){if(nativeDiplomacyJournal.TryTransition(item.Id,"aborted",DateTime.UtcNow,"startup_prepared_without_apply",false,item.AtWarObserved,out transitioned,out transitionFailure))changed++;continue;}Hero sourceHero,targetHero;resolver.TryResolveCampaignHero(item.SourceHeroId,out sourceHero);resolver.TryResolveCampaignHero(item.TargetHeroId,out targetHero);IFaction sourceFaction=null,targetFaction=null;try{sourceFaction=sourceHero==null?null:sourceHero.MapFaction;targetFaction=targetHero==null?null:targetHero.MapFaction;}catch{}bool pairValid=sourceFaction!=null&&targetFaction!=null&&string.Equals(sourceFaction.StringId,item.SourceFactionId,StringComparison.Ordinal)&&string.Equals(targetFaction.StringId,item.TargetFactionId,StringComparison.Ordinal);bool atWar=false;if(pairValid)try{atWar=FactionManager.IsAtWarAgainstFaction(sourceFaction,targetFaction);}catch{pairValid=false;}bool expected=pairValid&&(item.Action=="war"?atWar:!atWar);if(expected){PersistentDiplomaticStatementRecord statement;bool ledgerChanged,ledgerOk;string ledgerReason;if(item.Action=="war")ledgerOk=diplomaticStatements.TryMarkNativeWarCommitted(item.StatementId,item.SourceHeroId,DateTime.UtcNow,out statement,out ledgerReason,out ledgerChanged);else ledgerOk=diplomaticStatements.TryMarkNativePeaceCommitted(item.StatementId,item.SourceHeroId,DateTime.UtcNow,out statement,out ledgerReason,out ledgerChanged);if(ledgerChanged)memory.AdvanceRevision(1);if(ledgerOk&&nativeDiplomacyJournal.TryTransition(item.Id,"verified",DateTime.UtcNow,"startup_postcondition_verified",true,atWar,out transitioned,out transitionFailure)){changed++;if(statement!=null)NotifyDiplomacyLifecycle(statement,"startup_postcondition_verified");}}else if(item.Phase!="recovery_required"&&nativeDiplomacyJournal.TryTransition(item.Id,"recovery_required",DateTime.UtcNow,pairValid?"startup_postcondition_missing":"startup_pair_unresolved",item.NativeMutationApplied,atWar,out transitioned,out transitionFailure))changed++;}
            if(changed>0){string persistReason;if(!stateStore.PersistNativeJournal(nativeDiplomacyJournal,out persistReason))Logger.Error("AIPort native diplomacy reconciliation persist failed Source={Source} Reason={Reason}",source,persistReason);}if(recoverable.Count>0)Logger.Warning("AIPort native diplomacy reconciliation Source={Source} Recoverable={Recoverable} Changed={Changed} JournalCount={JournalCount} NativeMutationApplied=false",source,recoverable.Count,changed,nativeDiplomacyJournal.Count);return changed;
        }
        private string SimulateNpcOffer(List<string> args)
        {
            if(args==null||args.Count!=3)return "Usage: aiport.simulate_npc_offer <peer-id|player-hero-id> <npc-hero-id> <war|peace>";
            if(!campaignReady)return "rejected:not_ready";
            if(!stateStore.Enabled||!stateStore.Loaded||stateStore.ReadOnly||stateStore.IsSaving)return "rejected:persistent_state_unavailable";
            string recipient=(args[0]??string.Empty).Trim(),npcId=(args[1]??string.Empty).Trim(),action=(args[2]??string.Empty).Trim().ToLowerInvariant();
            if(action!="war"&&action!="peace")return "rejected:invalid_action";
            NetPeer peer=null;int peerId;lock(gate){if(int.TryParse(recipient,out peerId))connectedPeers.TryGetValue(peerId,out peer);else{foreach(KeyValuePair<int,string> pair in connectedHeroIds){if(!string.Equals(pair.Value,recipient,StringComparison.Ordinal))continue;NetPeer candidate;if(!connectedPeers.TryGetValue(pair.Key,out candidate)||candidate==null)continue;if(peer!=null&&!ReferenceEquals(peer,candidate))return "rejected:ambiguous_recipient";peer=candidate;}}}
            Hero targetHero=null;string recipientHeroId=recipient;
            if(peer!=null)
            {
                Player targetPlayer;string resolveFailure;if(!resolver.TryResolve(peer,out targetPlayer,out resolveFailure))return "rejected:player_unresolved";RegisterResolvedPeerHero(peer,targetPlayer.HeroId);MobileParty targetParty;string objectFailure;if(!resolver.TryResolveControlledCampaignObjects(targetPlayer,out targetHero,out targetParty,out objectFailure)||targetHero==null)return "rejected:player_objects_unresolved";recipientHeroId=targetPlayer.HeroId;
            }
            else
            {
                if(int.TryParse(recipient,out peerId))return "rejected:recipient_offline";if(!resolver.TryResolveCampaignHero(recipient,out targetHero)||targetHero==null)return "rejected:recipient_hero_unresolved";if(!IsPlayerControlledDiplomacyTarget(targetHero))return "rejected:recipient_not_player_hero";
            }
            Hero sourceHero;if(!resolver.TryResolveCampaignHero(npcId,out sourceHero)||sourceHero==null)return "rejected:npc_unresolved";
            if(IsPlayerControlledDiplomacyTarget(sourceHero)||FindSinglePeerForHero(sourceHero.StringId)!=null)return "rejected:npc_source_player_controlled";
            IFaction sourceFaction=null,targetFaction=null;try{sourceFaction=sourceHero.MapFaction;targetFaction=targetHero.MapFaction;}catch{}
            if(sourceFaction==null||targetFaction==null)return "rejected:faction_required";
            if(sourceFaction.IsBanditFaction||targetFaction.IsBanditFaction||ReferenceEquals(sourceFaction,targetFaction))return "rejected:faction_ineligible";
            DiplomacyAuthorityContext authority=diplomacyAuthority.Evaluate(sourceHero,targetHero);if(!authority.PairAuthorized)return "rejected:diplomatic_authority_required";
            bool atWar=false;try{atWar=FactionManager.IsAtWarAgainstFaction(sourceFaction,targetFaction);}catch{return "rejected:precondition_unavailable";}
            if(action=="war"&&atWar)return "rejected:already_at_war";if(action=="peace"&&!atWar)return "rejected:not_at_war";
            int expired=ExpireDiplomacyDue(DateTime.UtcNow);string statementId=Guid.NewGuid().ToString("N");PersistentDiplomaticStatementRecord record;string reason;bool fresh;
            if(!diplomaticStatements.TryRecord(statementId,sourceHero.StringId,recipientHeroId,sourceFaction.StringId,targetFaction.StringId,action,DateTime.UtcNow,"server_simulation","manual_server_simulation",0,CurrentCampaignDay(),CurrentCampaignHour(),out record,out reason,out fresh))return "rejected:"+reason;
            if(fresh)memory.AdvanceRevision(1);NotifyRecipientInbox(recipientHeroId,"server_simulated_npc_offer");
            Logger.Information("AIPort server simulated NPC diplomacy offer StatementId={StatementId} SourceHeroId={SourceHeroId} RecipientHeroId={RecipientHeroId} RecipientOnline={RecipientOnline} Action={Action} SourceFactionId={SourceFactionId} TargetFactionId={TargetFactionId} ExpiredTransitions={ExpiredTransitions} Revision={Revision} NativeMutationApplied=false",record.Id,record.PlayerHeroId,record.TargetHeroId,peer!=null,record.Action,record.SourceKingdomId,record.TargetKingdomId,expired,memory.Revision);
            return "ok:statement="+record.Id+";recipient="+record.TargetHeroId+";recipientOnline="+(peer!=null?"true":"false")+";source="+record.PlayerHeroId+";action="+record.Action+";revision="+memory.Revision.ToString(System.Globalization.CultureInfo.InvariantCulture)+";nativeMutation=false";
        }

        private void HandleHourlyDiplomacyMaintenance()
        {
            if(!campaignReady||stateStore.IsSaving)return;
            int expired=ExpireDiplomacyDue(DateTime.UtcNow),reconciled=stateStore.Loaded&&!stateStore.ReadOnly?ReconcileNativeDiplomacyJournal("hourly"):0,initiatives=TryRunNpcDiplomacyInitiative();
            if(expired>0||reconciled>0||initiatives>0)Logger.Information("AIPort hourly diplomacy maintenance Expired={Expired} Reconciled={Reconciled} Initiatives={Initiatives} Revision={Revision} NativeMutationApplied=false",expired,reconciled,initiatives,memory.Revision);
        }
        private int TryRunNpcDiplomacyInitiative()
        {
            if(!settings.EnableNpcDiplomacyInitiative||!campaignReady||stateStore.IsSaving||!stateStore.Enabled||!stateStore.Loaded||stateStore.ReadOnly)return 0;
            long campaignHour=CurrentCampaignHour(),campaignDay=CurrentCampaignDay();if(campaignHour<0||campaignDay<0)return 0;
            long durableLastHour=diplomaticStatements.LatestNpcInitiativeCampaignHour();if(durableLastHour>lastNpcInitiativeCampaignHour)lastNpcInitiativeCampaignHour=durableLastHour;
            if(lastNpcInitiativeCampaignHour!=long.MinValue&&lastNpcInitiativeCampaignHour>=0&&campaignHour-lastNpcInitiativeCampaignHour<settings.NpcDiplomacyMinimumIntervalHours)return 0;
            lastNpcInitiativeCampaignHour=campaignHour;
            int used=diplomaticStatements.CountNpcInitiativesForCampaignDay(campaignDay);if(used>=settings.NpcDiplomacyDailyBudget){Logger.Debug("AIPort NPC diplomacy initiative skipped CampaignDay={CampaignDay} Reason=daily_budget Used={Used} Budget={Budget}",campaignDay,used,settings.NpcDiplomacyDailyBudget);return 0;}
            List<Hero> targets=CollectPlayerDiplomacyTargets(),sources=CollectNpcDiplomacySources();List<NpcDiplomacyInitiativeCandidate> candidates=new List<NpcDiplomacyInitiativeCandidate>();DateTime now=DateTime.UtcNow;
            foreach(Hero targetHero in targets)
            {
                string targetHeroId=AuthoritativeHeroIdForControlledHero(targetHero);
                if(targetHero==null||string.IsNullOrWhiteSpace(targetHeroId)||diplomaticStatements.CountNpcInitiativesForTarget(targetHeroId,campaignDay)>=1)continue;
                foreach(Hero sourceHero in sources)
                {
                    if(sourceHero==null||ReferenceEquals(sourceHero,targetHero))continue;IFaction sourceFaction=null,targetFaction=null;try{sourceFaction=sourceHero.MapFaction;targetFaction=targetHero.MapFaction;}catch{}
                    if(sourceFaction==null||targetFaction==null||sourceFaction.IsBanditFaction||targetFaction.IsBanditFaction||ReferenceEquals(sourceFaction,targetFaction))continue;
                    if(diplomaticStatements.HasPendingPair(sourceFaction.StringId,targetFaction.StringId,now)||diplomaticStatements.HasRecentNpcInitiativeForPair(sourceFaction.StringId,targetFaction.StringId,campaignDay,settings.NpcDiplomacyPairCooldownDays))continue;
                    DiplomacyAuthorityContext authority=diplomacyAuthority.Evaluate(sourceHero,targetHero);if(!authority.PairAuthorized)continue;bool atWar;try{atWar=FactionManager.IsAtWarAgainstFaction(sourceFaction,targetFaction);}catch{continue;}int relation=0;try{relation=sourceHero.GetRelation(targetHero);}catch{}
                    candidates.Add(new NpcDiplomacyInitiativeCandidate{SourceHeroId=sourceHero.StringId,TargetHeroId=targetHeroId,SourceFactionId=sourceFaction.StringId,TargetFactionId=targetFaction.StringId,PairAuthorized=true,AtWar=atWar,Relation=relation});
                }
            }
            NpcDiplomacyInitiativeDecision decision=npcDiplomacyInitiativeScheduler.Select(candidates,stateStore.CampaignGeneration,campaignDay,settings.NpcDiplomacyMinimumScore);
            if(decision==null||!decision.Selected||decision.Candidate==null){Logger.Information("AIPort NPC diplomacy initiative evaluated CampaignDay={CampaignDay} CampaignHour={CampaignHour} Candidates={Candidates} Selected=false Reason={Reason} Used={Used} Budget={Budget} NativeMutationApplied=false",campaignDay,campaignHour,candidates.Count,decision==null?"selector_failed":decision.ReasonCode,used,settings.NpcDiplomacyDailyBudget);return 0;}
            Hero finalSource,finalTarget;if(!resolver.TryResolveCampaignHero(decision.Candidate.SourceHeroId,out finalSource)||!TryResolveAuthoritativeConnectedHero(decision.Candidate.TargetHeroId,out finalTarget)||finalSource==null||finalTarget==null)return 0;
            IFaction finalSourceFaction=null,finalTargetFaction=null;try{finalSourceFaction=finalSource.MapFaction;finalTargetFaction=finalTarget.MapFaction;}catch{}
            DiplomacyAuthorityContext finalAuthority=diplomacyAuthority.Evaluate(finalSource,finalTarget);bool finalAtWar=false;try{finalAtWar=finalSourceFaction!=null&&finalTargetFaction!=null&&FactionManager.IsAtWarAgainstFaction(finalSourceFaction,finalTargetFaction);}catch{return 0;}
            if(finalSourceFaction==null||finalTargetFaction==null||!finalAuthority.PairAuthorized||!string.Equals(finalSourceFaction.StringId,decision.Candidate.SourceFactionId,StringComparison.Ordinal)||!string.Equals(finalTargetFaction.StringId,decision.Candidate.TargetFactionId,StringComparison.Ordinal)||(decision.Action=="war"&&finalAtWar)||(decision.Action=="peace"&&!finalAtWar)||diplomaticStatements.HasPendingPair(finalSourceFaction.StringId,finalTargetFaction.StringId,DateTime.UtcNow)||diplomaticStatements.HasRecentNpcInitiativeForPair(finalSourceFaction.StringId,finalTargetFaction.StringId,campaignDay,settings.NpcDiplomacyPairCooldownDays))
            {Logger.Information("AIPort NPC diplomacy initiative rejected at final revalidation SourceHeroId={SourceHeroId} TargetHeroId={TargetHeroId} Action={Action} Reason=stale_context NativeMutationApplied=false",decision.Candidate.SourceHeroId,decision.Candidate.TargetHeroId,decision.Action);return 0;}
            List<string> authoritativeRecipients=AuthoritativeConnectedHeroIds();
            if(!AuthoritativeDiplomacyRecipientFilter.IsAuthoritativeRecipient(authoritativeRecipients,finalTarget.StringId)||(authoritativeRecipients.Count>0&&FindSinglePeerForHero(finalTarget.StringId)==null))
            {Logger.Warning("AIPort NPC diplomacy initiative rejected before record SourceHeroId={SourceHeroId} TargetHeroId={TargetHeroId} Action={Action} Authoritative={Authoritative} Reason=recipient_not_authoritative_online NativeMutationApplied=false",decision.Candidate.SourceHeroId,decision.Candidate.TargetHeroId,decision.Action,string.Join(",",authoritativeRecipients.ToArray()));return 0;}
            string statementId=Guid.NewGuid().ToString("N"),reason;PersistentDiplomaticStatementRecord record;bool fresh;
            if(!diplomaticStatements.TryRecord(statementId,finalSource.StringId,decision.Candidate.TargetHeroId,finalSourceFaction.StringId,finalTargetFaction.StringId,decision.Action,DateTime.UtcNow,"npc_scheduler",decision.ReasonCode,decision.Score,campaignDay,campaignHour,out record,out reason,out fresh))
            {Logger.Information("AIPort NPC diplomacy initiative record rejected SourceHeroId={SourceHeroId} TargetHeroId={TargetHeroId} Action={Action} Score={Score} Reason={Reason} NativeMutationApplied=false",finalSource.StringId,decision.Candidate.TargetHeroId,decision.Action,decision.Score,reason);return 0;}
            if(fresh)memory.AdvanceRevision(1);NotifyRecipientInbox(finalTarget.StringId,"npc_scheduler_offer");
            Logger.Information("AIPort NPC diplomacy initiative created StatementId={StatementId} SourceHeroId={SourceHeroId} RecipientHeroId={RecipientHeroId} RecipientOnline={RecipientOnline} Action={Action} SourceFactionId={SourceFactionId} TargetFactionId={TargetFactionId} CampaignDay={CampaignDay} CampaignHour={CampaignHour} Score={Score} Reason={Reason} Revision={Revision} NativeMutationApplied=false",record.Id,record.PlayerHeroId,record.TargetHeroId,FindSinglePeerForHero(record.TargetHeroId)!=null,record.Action,record.SourceKingdomId,record.TargetKingdomId,campaignDay,campaignHour,decision.Score,decision.ReasonCode,memory.Revision);return fresh?1:0;
        }

        private List<Hero> CollectPlayerDiplomacyTargets()
        {
            List<string> authoritative=AuthoritativeConnectedHeroIds();Dictionary<string,Hero> discovered=new Dictionary<string,Hero>(StringComparer.Ordinal);List<string> candidateIds=new List<string>();
            try{foreach(Hero hero in Hero.AllAliveHeroes){if(hero==null||!hero.IsAlive||hero.IsDisabled||!IsPlayerControlledDiplomacyTarget(hero)||string.IsNullOrWhiteSpace(hero.StringId)||discovered.ContainsKey(hero.StringId))continue;discovered.Add(hero.StringId,hero);candidateIds.Add(hero.StringId);}}catch{}
            List<string> excluded;List<string> selected=AuthoritativeDiplomacyRecipientFilter.SelectRecipientHeroIds(candidateIds,authoritative,out excluded);
            if(excluded.Count>0)Logger.Information("AIPort NPC diplomacy initiative excluded non-authoritative recipient aliases Authoritative={Authoritative} Candidates={Candidates} Excluded={Excluded} NativeMutationApplied=false",string.Join(",",authoritative.ToArray()),string.Join(",",candidateIds.ToArray()),string.Join(",",excluded.ToArray()));
            List<Hero> result=new List<Hero>();
            foreach(string heroId in selected)
            {
                if(result.Count>=16)break;Hero hero=null;bool valid=false;string resolvedHeroId=string.Empty;
                if(authoritative.Count>0)
                {
                    bool resolved=TryResolveAuthoritativeConnectedHero(heroId,out hero);
                    try{resolvedHeroId=hero==null?string.Empty:hero.StringId;valid=resolved&&hero!=null&&hero.IsAlive&&!hero.IsDisabled&&IsPlayerControlledDiplomacyTarget(hero);}catch{valid=false;}
                    if(!valid){Logger.Warning("AIPort NPC diplomacy authoritative recipient resolution failed AuthoritativeHeroId={AuthoritativeHeroId} ResolvedHeroId={ResolvedHeroId} Reason=authoritative_recipient_resolution_failed NativeMutationApplied=false",heroId,resolvedHeroId);continue;}
                }
                else if(!discovered.TryGetValue(heroId,out hero)||hero==null)continue;
                result.Add(hero);
            }
            result.Sort(delegate(Hero a,Hero b){return string.CompareOrdinal(a==null?string.Empty:a.StringId,b==null?string.Empty:b.StringId);});return result;
        }

        private bool TryResolveAuthoritativeConnectedHero(string authoritativeHeroId,out Hero hero)
        {
            hero=null;NetPeer peer=FindSinglePeerForHero(authoritativeHeroId);if(peer==null)return false;
            Player player;string playerFailure;if(!resolver.TryResolve(peer,out player,out playerFailure)||player==null||!string.Equals(player.HeroId,authoritativeHeroId,StringComparison.Ordinal))return false;
            MobileParty party;string objectFailure;if(!resolver.TryResolveControlledCampaignObjects(player,out hero,out party,out objectFailure)||hero==null)return false;
            try{return hero.IsAlive&&!hero.IsDisabled&&IsPlayerControlledDiplomacyTarget(hero);}catch{return false;}
        }

        private string AuthoritativeHeroIdForControlledHero(Hero hero)
        {
            if(hero==null)return string.Empty;foreach(string authoritativeHeroId in AuthoritativeConnectedHeroIds()){Hero controlled;if(TryResolveAuthoritativeConnectedHero(authoritativeHeroId,out controlled)&&ReferenceEquals(controlled,hero))return authoritativeHeroId;}return string.Empty;
        }

        private List<Hero> CollectNpcDiplomacySources()
        {
            Dictionary<string,Hero> unique=new Dictionary<string,Hero>(StringComparer.Ordinal);try{foreach(Kingdom kingdom in Kingdom.All){Hero leader=kingdom==null?null:kingdom.Leader;AddNpcDiplomacySource(unique,leader);}}catch{}try{foreach(Clan clan in Clan.All){Hero leader=clan==null||clan.Kingdom!=null?null:clan.Leader;AddNpcDiplomacySource(unique,leader);}}catch{}List<Hero> result=new List<Hero>(unique.Values);result.Sort(delegate(Hero a,Hero b){return string.CompareOrdinal(a==null?string.Empty:a.StringId,b==null?string.Empty:b.StringId);});if(result.Count>64)result.RemoveRange(64,result.Count-64);return result;
        }

        private void AddNpcDiplomacySource(Dictionary<string,Hero> unique,Hero hero)
        {
            if(hero==null||!hero.IsAlive||hero.IsDisabled||string.IsNullOrWhiteSpace(hero.StringId)||IsPlayerControlledDiplomacyTarget(hero)||FindSinglePeerForHero(hero.StringId)!=null)return;if(!unique.ContainsKey(hero.StringId))unique.Add(hero.StringId,hero);
        }

        private static long CurrentCampaignHour(){try{return Campaign.Current==null?-1L:(long)Math.Floor(CampaignTime.Now.ToHours);}catch{return -1L;}}
        private static long CurrentCampaignDay(){try{return Campaign.Current==null?-1L:(long)Math.Floor(CampaignTime.Now.ToDays);}catch{return -1L;}}
        private static string FormatUtc(DateTime value){return value==DateTime.MinValue?string.Empty:value.ToUniversalTime().ToString("o",System.Globalization.CultureInfo.InvariantCulture);}
        private static string SafeDisplayName(Hero hero){try{return BoundDisplay(hero==null||hero.Name==null?string.Empty:hero.Name.ToString());}catch{return string.Empty;}}
        private static string SafeFactionDisplayName(IFaction faction){try{return BoundDisplay(faction==null||faction.Name==null?string.Empty:faction.Name.ToString());}catch{return string.Empty;}}
        private static string BoundDisplay(string value){string text=(value??string.Empty).Trim().Replace('\r',' ').Replace('\n',' ');return text.Length<=160?text:text.Substring(0,160);}

        private static DiplomacyStatementDecision LifecycleDecision(string intentId,string status,string reason,long revision,PersistentDiplomaticStatementRecord record,bool mutationApplied)
        {return new DiplomacyStatementDecision{IntentId=intentId??string.Empty,Status=status??"rejected",ReasonCode=reason??string.Empty,StateRevision=revision,Action=record==null?string.Empty:record.Action,TargetHeroId=record==null?string.Empty:record.TargetHeroId,SourceKingdomId=record==null?string.Empty:record.SourceKingdomId,TargetKingdomId=record==null?string.Empty:record.TargetKingdomId,MutationApplied=mutationApplied};}
        private int ExpireDiplomacyDue(DateTime utc)
        {List<PersistentDiplomaticStatementRecord> expiredRecords=new List<PersistentDiplomaticStatementRecord>();int expired=diplomaticStatements.ExpireDue(utc,expiredRecords);if(expired>0)memory.AdvanceRevision(expired);foreach(PersistentDiplomaticStatementRecord record in expiredRecords)NotifyDiplomacyLifecycle(record,"negotiation_expired");return expired;}
        private void NotifyDiplomacyLifecycle(PersistentDiplomaticStatementRecord record,string reason)
        {
            if(record==null)return;NetPeer source=FindSinglePeerForHero(record.PlayerHeroId),target=FindSinglePeerForHero(record.TargetHeroId);string notificationId=Guid.NewGuid().ToString("N");AIDiplomacyLifecycleNotification notice=new AIDiplomacyLifecycleNotification(AIPortProtocol.Version,notificationId,stateStore.CampaignGeneration,memory.Revision,record.Id,record.Status,record.Action,reason??record.LastReasonCode??string.Empty,record.NativeMutationApplied);if(source!=null)network.SendImmediate(source,notice);if(target!=null&&!ReferenceEquals(source,target))network.SendImmediate(target,notice);Logger.Information("AIPort diplomacy lifecycle notification StatementId={StatementId} Status={Status} SourceOnline={SourceOnline} TargetOnline={TargetOnline} Revision={Revision} Reason={Reason} NativeMutationApplied={NativeMutationApplied}",record.Id,record.Status,source!=null,target!=null,memory.Revision,reason,record.NativeMutationApplied);
        }
        private static bool IsPlayerControlledDiplomacyTarget(Hero hero)
        {
            if(hero==null)return false;try{if(hero.CharacterObject!=null&&hero.CharacterObject.IsPlayerCharacter)return true;}catch{}string id=string.Empty;try{id=hero.StringId??string.Empty;}catch{}return id.StartsWith("Hero_Player",StringComparison.Ordinal)||id.StartsWith("Player",StringComparison.Ordinal);
        }
        private NetPeer FindSinglePeerForHero(string heroId)
        {NetPeer match=null;lock(gate){foreach(KeyValuePair<int,string> pair in connectedHeroIds){if(!string.Equals(pair.Value,heroId,StringComparison.Ordinal))continue;NetPeer candidate;if(!connectedPeers.TryGetValue(pair.Key,out candidate)||candidate==null)continue;if(match!=null&&!ReferenceEquals(match,candidate))return null;match=candidate;}}return match;}

        private List<string> AuthoritativeConnectedHeroIds()
        {
            List<KeyValuePair<int,string>> snapshot=new List<KeyValuePair<int,string>>();List<int> peers=new List<int>();lock(gate){foreach(KeyValuePair<int,string> pair in connectedHeroIds)snapshot.Add(pair);foreach(KeyValuePair<int,NetPeer> pair in connectedPeers){if(pair.Value!=null)peers.Add(pair.Key);}}return AuthoritativeDiplomacyRecipientFilter.AuthoritativeHeroIds(snapshot,peers);
        }
        private void RegisterResolvedPeerHero(NetPeer peer,string heroId)
        {
            if(peer==null||string.IsNullOrWhiteSpace(heroId))return;lock(gate){NetPeer current;if(!connectedPeers.TryGetValue(peer.Id,out current)||!ReferenceEquals(current,peer))return;connectedHeroIds[peer.Id]=heroId;}
        }
        private void NotifyRecipientInbox(string recipientHeroId,string reason)
        {
            NetPeer match=null;bool ambiguous=false;lock(gate){foreach(KeyValuePair<int,string> pair in connectedHeroIds){if(!string.Equals(pair.Value,recipientHeroId,StringComparison.Ordinal))continue;NetPeer peer;if(!connectedPeers.TryGetValue(pair.Key,out peer)||peer==null)continue;if(match!=null&&!ReferenceEquals(match,peer)){ambiguous=true;break;}match=peer;}}if(ambiguous){Logger.Warning("AIPort suppressed ambiguous diplomacy recipient notification RecipientHeroId={RecipientHeroId}",recipientHeroId);return;}if(match!=null)NotifyPeerInbox(match,recipientHeroId,reason);
        }
        private void NotifyPeerInbox(NetPeer peer,string recipientHeroId,string reason)
        {
            if(peer==null||string.IsNullOrWhiteSpace(recipientHeroId))return;DateTime now=DateTime.UtcNow;int count=diplomaticStatements.CountPendingIncoming(recipientHeroId,now);string latest=diplomaticStatements.LatestPendingIncomingId(recipientHeroId,now);if(count<=0||string.IsNullOrWhiteSpace(latest))return;
            PersistentDiplomaticStatementRecord record;diplomaticStatements.TryGet(latest,out record);string action=record==null?string.Empty:record.Action,sourceHeroId=record==null?string.Empty:record.PlayerHeroId,sourceFactionId=record==null?string.Empty:record.SourceKingdomId,targetFactionId=record==null?string.Empty:record.TargetKingdomId,expiresUtc=record==null||record.ExpiresUtc==DateTime.MinValue?string.Empty:record.ExpiresUtc.ToUniversalTime().ToString("o",System.Globalization.CultureInfo.InvariantCulture);
            string notificationId=Guid.NewGuid().ToString("N");network.SendImmediate(peer,new AIDiplomacyInboxNotification(AIPortProtocol.Version,notificationId,stateStore.CampaignGeneration,memory.Revision,count,latest,reason??string.Empty,action,sourceHeroId,sourceFactionId,targetFactionId,expiresUtc));Logger.Information("AIPort diplomacy inbox notification sent PeerId={PeerId} RecipientHeroId={RecipientHeroId} NotificationId={NotificationId} PendingCount={PendingCount} LatestStatementId={LatestStatementId} Action={Action} SourceHeroId={SourceHeroId} Revision={Revision} Reason={Reason}",peer.Id,recipientHeroId,notificationId,count,latest,action,sourceHeroId,memory.Revision,reason);
        }

        private string CurrentCampaignId()
        {
            try { return coopSessionProvider == null || coopSessionProvider.CoopSession == null ? string.Empty : (coopSessionProvider.CoopSession.UniqueGameId ?? string.Empty); } catch { return string.Empty; }
        }

        private static string CurrentCampaignTime()
        {
            try { return Campaign.Current == null ? string.Empty : CampaignTime.Now.ToString(); } catch { return string.Empty; }
        }

        private bool TryEnterRateLimit(string controllerId, out int retryAfterMilliseconds)
        {
            lock (gate)
            {
                retryAfterMilliseconds = 0;
                if (inflight >= settings.MaxConcurrentRequests || (settings.Enabled && backendWorkers >= MaximumBackendWorkers))
                {
                    retryAfterMilliseconds = 1000;
                    return false;
                }
                DateTime now = DateTime.UtcNow;
                DateTime cutoff = now.AddMinutes(-1);
                PruneRateLimitBucketsLocked(cutoff);
                Queue<DateTime> queue;
                if (!recent.TryGetValue(controllerId, out queue))
                {
                    queue = new Queue<DateTime>();
                    recent[controllerId] = queue;
                }
                if (queue.Count >= settings.MaxRequestsPerPlayerPerMinute)
                {
                    TimeSpan remaining = queue.Peek().AddMinutes(1) - now;
                    retryAfterMilliseconds = Math.Max(1000, (int)Math.Ceiling(remaining.TotalMilliseconds));
                    return false;
                }
                queue.Enqueue(now);
                inflight++;
                return true;
            }
        }

        private void PruneRateLimitBucketsLocked(DateTime cutoff)
        {
            List<string> empty = null;
            foreach (KeyValuePair<string, Queue<DateTime>> pair in recent)
            {
                while (pair.Value.Count > 0 && pair.Value.Peek() < cutoff) pair.Value.Dequeue();
                if (pair.Value.Count != 0) continue;
                if (empty == null) empty = new List<string>();
                empty.Add(pair.Key);
            }
            if (empty != null) foreach (string controllerId in empty) recent.Remove(controllerId);
        }

        private void ReleaseInflight()
        {
            lock (gate)
            {
                inflight = Math.Max(0, inflight - 1);
            }
        }

        private void Reject(NetPeer peer, string requestId, string errorCode, string safeMessage, bool retryable, int retryAfterMilliseconds = 0)
        {
            Logger.Information("AIPort conversation rejected RequestId={RequestId} ErrorCode={ErrorCode} Retryable={Retryable} RetryAfterMs={RetryAfterMs}", requestId, errorCode, retryable, retryAfterMilliseconds);
            network.SendImmediate(peer, new AIConversationError(requestId, errorCode, safeMessage, retryable, retryAfterMilliseconds));
        }
    }
}
