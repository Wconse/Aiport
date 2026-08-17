using System;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using AIPort.Protocol;
using AIPort.Protocol.Messages;
using Common.Logging;
using Common.Messaging;
using Common.Network;
using Coop.Core.Client.Messages;
using Coop.Core.Server.Connections.Messages;
using GameInterface.Services.GameState.Messages;
using Serilog;

namespace Coop.Core.Client.Services.AIPort.Handlers
{
    internal sealed class AIPortHandshakeClientHandler : IHandler, IDisposable
    {
        private const int MaximumSnapshotAttempts = 30;
        private const int MinimumSnapshotRetryMilliseconds = 500;
        private const int MaximumSnapshotRetryMilliseconds = 5000;
        private static readonly ILogger Logger = LogManager.GetLogger<AIPortHandshakeClientHandler>();
        private readonly IMessageBroker messageBroker;
        private readonly INetwork network;
        private readonly object sync = new object();
        private Timer snapshotRetryTimer;
        private long connectionGeneration;
        private string pendingRequestId;
        private bool sentForConnection;
        private bool campaignReady;
        private string capabilityRequestId;
        private string snapshotRequestId;
        private string snapshotRequestGeneration = string.Empty;
        private long snapshotRequestRevision;
        private int snapshotAttempt;
        private string campaignGeneration = string.Empty;
        private long stateRevision;
        private int serverCapabilityFlags;
        private string appliedSnapshotKey = string.Empty;
        private string noOpRequestId;
        private string noOpGeneration = string.Empty;
        private long noOpRevision;
        private bool noOpValidated;
        private bool disposed;

        public AIPortHandshakeClientHandler(IMessageBroker messageBroker, INetwork network)
        {
            this.messageBroker = messageBroker;
            this.network = network;
            messageBroker.Subscribe<NetworkClientValidated>(Handle);
            messageBroker.Subscribe<NetworkDisconnected>(Handle);
            messageBroker.Subscribe<CampaignReady>(Handle);
            messageBroker.Subscribe<AIPortHandshakeResponse>(Handle);
            messageBroker.Subscribe<AIPortCapabilitiesResponse>(Handle);
            messageBroker.Subscribe<AIPortStateSnapshotResponse>(Handle);
            messageBroker.Subscribe<AIIntentProposalResult>(Handle);
        }

        public void Dispose()
        {
            lock (sync)
            {
                disposed = true;
                CancelSnapshotRetryLocked("dispose");
            }
            messageBroker.Unsubscribe<NetworkClientValidated>(Handle);
            messageBroker.Unsubscribe<NetworkDisconnected>(Handle);
            messageBroker.Unsubscribe<CampaignReady>(Handle);
            messageBroker.Unsubscribe<AIPortHandshakeResponse>(Handle);
            messageBroker.Unsubscribe<AIPortCapabilitiesResponse>(Handle);
            messageBroker.Unsubscribe<AIPortStateSnapshotResponse>(Handle);
            messageBroker.Unsubscribe<AIIntentProposalResult>(Handle);
        }

        private void Handle(MessagePayload<NetworkClientValidated> payload)
        {
            string requestId;
            lock (sync)
            {
                if (disposed || sentForConnection) return;
                connectionGeneration++;
                sentForConnection = true;
                pendingRequestId = Guid.NewGuid().ToString("N");
                requestId = pendingRequestId;
            }
            Logger.Information("AIPort handshake sending RequestId={RequestId} Protocol={ProtocolVersion} Build={ClientBuild} HeroExists={HeroExists}", requestId, AIPortProtocol.Version, AIPortProtocol.Build, payload.What.HeroExists);
            network.SendAll(new AIPortHandshakeRequest(AIPortProtocol.Version, requestId, AIPortProtocol.Build));
        }

        private void Handle(MessagePayload<NetworkDisconnected> payload)
        {
            lock (sync)
            {
                connectionGeneration++;
                CancelSnapshotRetryLocked("network_disconnected");
                pendingRequestId = null;
                capabilityRequestId = null;
                snapshotRequestId = null;
                snapshotRequestGeneration = string.Empty;
                snapshotRequestRevision = 0;
                snapshotAttempt = 0;
                campaignGeneration = string.Empty;
                stateRevision = 0;
                serverCapabilityFlags = 0;
                appliedSnapshotKey = string.Empty;
                noOpRequestId = null;
                noOpGeneration = string.Empty;
                noOpRevision = 0;
                noOpValidated = false;
                campaignReady = false;
                sentForConnection = false;
            }
            Logger.Information("AIPort handshake state reset after network disconnect");
        }

        private void Handle(MessagePayload<AIPortHandshakeResponse> payload)
        {
            AIPortHandshakeResponse response = payload.What;
            string requestId = null;
            lock (sync)
            {
                bool correlated = pendingRequestId != null && string.Equals(response.RequestId, pendingRequestId, StringComparison.Ordinal);
                if (!correlated)
                {
                    Logger.Warning("AIPort handshake ignored uncorrelated response RequestId={RequestId} ExpectedRequestId={ExpectedRequestId}", response.RequestId, pendingRequestId);
                    return;
                }
                if (response.Compatible)
                {
                    capabilityRequestId = Guid.NewGuid().ToString("N");
                    requestId = capabilityRequestId;
                }
            }
            Logger.Information("AIPort handshake completed RequestId={RequestId} Compatible={Compatible} ServerProtocol={ServerProtocol} ServerBuild={ServerBuild} Message={Message}", response.RequestId, response.Compatible, response.ProtocolVersion, response.ServerBuild, response.Message);
            if (requestId != null) SendCapabilitiesRequest(requestId);
        }

        private void Handle(MessagePayload<CampaignReady> payload)
        {
            lock (sync) campaignReady = true;
            TryRequestSnapshot();
        }

        private void Handle(MessagePayload<AIPortCapabilitiesResponse> payload)
        {
            AIPortCapabilitiesResponse response = payload.What;
            lock (sync)
            {
                if (capabilityRequestId == null || !string.Equals(response.RequestId, capabilityRequestId, StringComparison.Ordinal)) return;
                capabilityRequestId = null;
                serverCapabilityFlags = response.Accepted ? response.ServerCapabilityFlags : 0;
                campaignGeneration = response.CampaignGeneration ?? string.Empty;
                stateRevision = response.StateRevision;
                snapshotRequestId = null;
                snapshotRequestGeneration = string.Empty;
                snapshotRequestRevision = 0;
                snapshotAttempt = 0;
                appliedSnapshotKey = string.Empty;
                noOpRequestId = null;
                noOpGeneration = string.Empty;
                noOpRevision = 0;
                noOpValidated = false;
                CancelSnapshotRetryLocked("capabilities_refreshed");
            }
            Logger.Information("AIPort capabilities completed Accepted={Accepted} Flags={Flags} IntentSchema={IntentSchema} StateSchema={StateSchema} CampaignGeneration={CampaignGeneration} Revision={Revision}", response.Accepted, response.ServerCapabilityFlags, response.IntentSchemaVersion, response.StateSchemaVersion, response.CampaignGeneration, response.StateRevision);
            TryRequestSnapshot();
        }

        private void SendCapabilitiesRequest(string requestId)
        {
            int flags = AIPortProtocol.CapabilityNarrative | AIPortProtocol.CapabilityNoOpIntent | AIPortProtocol.CapabilityStateSnapshot | AIPortProtocol.CapabilityPersistentMemory | AIPortProtocol.CapabilityRelationShadowIntent | AIPortProtocol.CapabilityRelationConfirmation | AIPortProtocol.CapabilityDiplomacySnapshot | AIPortProtocol.CapabilityDiplomacyStatements | AIPortProtocol.CapabilityValidationGate | AIPortProtocol.CapabilityDiplomacyAuthority | AIPortProtocol.CapabilityDiplomacyRecipientConsent | AIPortProtocol.CapabilityDiplomacyConflictGuard | AIPortProtocol.CapabilityDiplomacyInboxNotification | AIPortProtocol.CapabilityDiplomacyLifecycleBundle | AIPortProtocol.CapabilityNativeWarAdapter | AIPortProtocol.CapabilityNativeDiplomacyJournal | AIPortProtocol.CapabilityNativePeaceAdapter | AIPortProtocol.CapabilityNpcDiplomacyPolicy | AIPortProtocol.CapabilityDiplomacyDecisionUi | AIPortProtocol.CapabilityDiplomacyInboxList | AIPortProtocol.CapabilityNpcDiplomacyInitiativeScheduler;
            network.SendAll(new AIPortCapabilitiesRequest(AIPortProtocol.Version, requestId, flags, AIPortProtocol.StateSchemaVersion));
        }

        private void TryRequestSnapshot()
        {
            string requestId;
            string generation;
            long revision;
            int attempt;
            lock (sync)
            {
                if (disposed || !campaignReady || (serverCapabilityFlags & AIPortProtocol.CapabilityStateSnapshot) == 0 || snapshotRequestId != null || snapshotRetryTimer != null || noOpValidated) return;
                if (snapshotAttempt >= MaximumSnapshotAttempts)
                {
                    Logger.Warning("AIPort private state snapshot retry limit reached CampaignGeneration={CampaignGeneration} Revision={Revision} Attempts={Attempts}", campaignGeneration, stateRevision, snapshotAttempt);
                    return;
                }
                snapshotRequestId = Guid.NewGuid().ToString("N");
                snapshotRequestGeneration = campaignGeneration;
                snapshotRequestRevision = stateRevision;
                snapshotAttempt++;
                requestId = snapshotRequestId;
                generation = snapshotRequestGeneration;
                revision = snapshotRequestRevision;
                attempt = snapshotAttempt;
            }
            Logger.Information("AIPort private state snapshot requested RequestId={RequestId} CampaignGeneration={CampaignGeneration} KnownRevision={Revision} Attempt={Attempt}", requestId, generation, revision, attempt);
            network.SendAll(new AIPortStateSnapshotRequest(AIPortProtocol.Version, requestId, generation, revision));
        }

        private void Handle(MessagePayload<AIPortStateSnapshotResponse> payload)
        {
            AIPortStateSnapshotResponse response = payload.What;
            string noOpId = null;
            string noOpCampaignGeneration = string.Empty;
            long noOpStateRevision = 0;
            bool retryScheduled = false;
            bool refreshCapabilities = false;
            string rejection = string.Empty;
            lock (sync)
            {
                if (snapshotRequestId == null || !string.Equals(response.RequestId, snapshotRequestId, StringComparison.Ordinal)) return;
                string expectedGeneration = snapshotRequestGeneration;
                long expectedRevision = snapshotRequestRevision;
                snapshotRequestId = null;
                snapshotRequestGeneration = string.Empty;
                snapshotRequestRevision = 0;

                if (!response.Ready)
                {
                    if (string.Equals(response.ReasonCode, "player_unresolved", StringComparison.OrdinalIgnoreCase) && snapshotAttempt < MaximumSnapshotAttempts)
                    {
                        ScheduleSnapshotRetryLocked(response.RetryAfterMilliseconds, "player_unresolved");
                        retryScheduled = true;
                    }
                    else if (string.Equals(response.ReasonCode, "generation_mismatch", StringComparison.OrdinalIgnoreCase))
                    {
                        CancelSnapshotRetryLocked("generation_mismatch");
                        capabilityRequestId = Guid.NewGuid().ToString("N");
                        refreshCapabilities = true;
                    }
                }
                else if (string.IsNullOrWhiteSpace(expectedGeneration) || !string.Equals(response.CampaignGeneration, expectedGeneration, StringComparison.Ordinal))
                {
                    rejection = "generation_changed";
                }
                else if (response.StateRevision < expectedRevision || response.StateRevision < stateRevision)
                {
                    rejection = "stale_revision";
                }
                else if (!IsValidSnapshotHash(response.SnapshotJson, response.ContentSha256))
                {
                    rejection = "hash_mismatch";
                }
                else
                {
                    string snapshotKey = response.CampaignGeneration + ":" + response.StateRevision + ":" + response.ContentSha256;
                    if (string.Equals(snapshotKey, appliedSnapshotKey, StringComparison.Ordinal))
                    {
                        rejection = "duplicate_snapshot";
                    }
                    else
                    {
                        appliedSnapshotKey = snapshotKey;
                        campaignGeneration = response.CampaignGeneration;
                        stateRevision = response.StateRevision;
                        CancelSnapshotRetryLocked("snapshot_ready");
                        if (!noOpValidated && noOpRequestId == null && (serverCapabilityFlags & AIPortProtocol.CapabilityNoOpIntent) != 0)
                        {
                            noOpRequestId = Guid.NewGuid().ToString("N");
                            noOpGeneration = campaignGeneration;
                            noOpRevision = stateRevision;
                            noOpId = noOpRequestId;
                            noOpCampaignGeneration = noOpGeneration;
                            noOpStateRevision = noOpRevision;
                        }
                    }
                }
            }
            Logger.Information("AIPort private state snapshot completed Ready={Ready} CampaignGeneration={CampaignGeneration} Revision={Revision} Chars={Chars} Hash={Hash} Reason={Reason} RetryScheduled={RetryScheduled}", response.Ready, response.CampaignGeneration, response.StateRevision, response.SnapshotJson == null ? 0 : response.SnapshotJson.Length, response.ContentSha256, string.IsNullOrWhiteSpace(rejection) ? response.ReasonCode : rejection, retryScheduled);
            if (refreshCapabilities)
            {
                string requestId;
                lock (sync) requestId = capabilityRequestId;
                Logger.Information("AIPort capabilities refresh requested after snapshot generation mismatch RequestId={RequestId}", requestId);
                SendCapabilitiesRequest(requestId);
                return;
            }
            if (!string.IsNullOrWhiteSpace(rejection) || !response.Ready) return;
            Logger.Information("AIPort SnapshotReady CampaignGeneration={CampaignGeneration} Revision={Revision} Hash={Hash}", response.CampaignGeneration, response.StateRevision, response.ContentSha256);
            if (noOpId != null)
            {
                network.SendAll(new AIIntentProposalRequest(AIPortProtocol.Version, noOpId, noOpCampaignGeneration, noOpStateRevision, "no_op", "{}"));
                Logger.Information("AIPort no-op intent requested RequestId={RequestId} CampaignGeneration={CampaignGeneration} Revision={Revision}", noOpId, noOpCampaignGeneration, noOpStateRevision);
            }
        }

        private void Handle(MessagePayload<AIIntentProposalResult> payload)
        {
            bool validated;
            lock (sync)
            {
                if (noOpRequestId == null || !string.Equals(payload.What.RequestId, noOpRequestId, StringComparison.Ordinal)) return;
                validated = string.Equals(payload.What.Status, "validated", StringComparison.OrdinalIgnoreCase)
                    && payload.What.StateRevision >= noOpRevision
                    && string.Equals(noOpGeneration, campaignGeneration, StringComparison.Ordinal);
                if (validated) noOpValidated = true;
                noOpRequestId = null;
            }
            Logger.Information("AIPort no-op intent result RequestId={RequestId} IntentId={IntentId} Status={Status} Reason={Reason} Revision={Revision}", payload.What.RequestId, payload.What.IntentId, payload.What.Status, payload.What.ReasonCode, payload.What.StateRevision);
            if (validated) Logger.Information("AIPort NoOpValidated CampaignGeneration={CampaignGeneration} Revision={Revision} IntentId={IntentId}", noOpGeneration, payload.What.StateRevision, payload.What.IntentId);
        }

        private void ScheduleSnapshotRetryLocked(int serverDelayMilliseconds, string reason)
        {
            CancelSnapshotRetryLocked("retry_rescheduled");
            int delay = Math.Max(MinimumSnapshotRetryMilliseconds, Math.Min(MaximumSnapshotRetryMilliseconds, serverDelayMilliseconds <= 0 ? 1000 : serverDelayMilliseconds));
            long token = connectionGeneration;
            snapshotRetryTimer = new Timer(_ => RetrySnapshot(token), null, delay, Timeout.Infinite);
            Logger.Information("AIPort private state snapshot retry scheduled CampaignGeneration={CampaignGeneration} Revision={Revision} NextAttempt={Attempt} DelayMs={DelayMs} Reason={Reason}", campaignGeneration, stateRevision, snapshotAttempt + 1, delay, reason);
        }

        private void RetrySnapshot(long token)
        {
            lock (sync)
            {
                Timer timer = snapshotRetryTimer;
                snapshotRetryTimer = null;
                if (timer != null) timer.Dispose();
                if (disposed || token != connectionGeneration || !sentForConnection || !campaignReady) return;
            }
            TryRequestSnapshot();
        }

        private void CancelSnapshotRetryLocked(string reason)
        {
            Timer timer = snapshotRetryTimer;
            snapshotRetryTimer = null;
            if (timer != null)
            {
                timer.Dispose();
                Logger.Debug("AIPort private state snapshot retry canceled Reason={Reason}", reason);
            }
        }

        private static bool IsValidSnapshotHash(string json, string expectedHash)
        {
            if (json == null || string.IsNullOrWhiteSpace(expectedHash) || expectedHash.Length != 64) return false;
            using (SHA256 sha = SHA256.Create())
            {
                byte[] bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(json));
                StringBuilder actual = new StringBuilder(64);
                for (int i = 0; i < bytes.Length; i++) actual.Append(bytes[i].ToString("x2"));
                return string.Equals(actual.ToString(), expectedHash, StringComparison.OrdinalIgnoreCase);
            }
        }
    }
}
