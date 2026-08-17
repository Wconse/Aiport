using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using ThreadingTimer = System.Threading.Timer;
using AIPort;
using AIPort.Protocol;
using AIPort.Protocol.Messages;
using AIPort.Server;
using Common.Logging;
using Common.Messaging;
using Common.Network;
using Coop.Core.Client.Messages;
using Coop.Core.Server.Services.Time.Messages;
using GameInterface.Services.Heroes.Enum;
using GameInterface.Services.GameState.Messages;
using Serilog;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Conversation;
using TaleWorlds.Core;
using TaleWorlds.Library;

namespace Coop.Core.Client.Services.AIPort.Handlers
{
    internal sealed class AIPortConversationClientHandler : IHandler, IDisposable
    {
        private const int RetryProbeDelayMilliseconds = 3000;
        private const int ConversationScanIntervalMilliseconds = 250;
        private const int TargetBindingRetryDelayMilliseconds = 1000;
        private const int MaximumTargetBindingRetries = 30;
        private const int ConversationRequestTimeoutMilliseconds = 100000;
        private const int MaximumConversationRateLimitRetries = 5;
        private const int DefaultConversationRateLimitRetryDelayMilliseconds = 3000;
        private const int MinimumConversationRateLimitRetryDelayMilliseconds = 1000;
        private const int MaximumConversationRateLimitRetryDelayMilliseconds = 60000;

        private static readonly ILogger Logger = LogManager.GetLogger<AIPortConversationClientHandler>();
        private readonly IMessageBroker messageBroker;
        private readonly INetwork network;
        private readonly object probeSync = new object();
        private readonly Dictionary<IAgent, string> regularAgentNonces = new Dictionary<IAgent, string>();
        private ThreadingTimer conversationRetryTimer;
        private ThreadingTimer conversationRequestTimeoutTimer;
        private bool disposed;
        private bool campaignReady;
        private long clientSequence;
        private IMbEvent<float> attachedTickEvent;
        private IMbEvent<IAgent> attachedAgentJoinedEvent;
        private IMbEvent<IEnumerable<CharacterObject>> attachedConversationEndedEvent;
        private ConversationManager attachedConversationManager;
        private DateTime nextConversationScanUtc;
        private string activeNpcHeroId = string.Empty;
        private string activeConversationId = string.Empty;
        private string activeClientTargetNonce = string.Empty;
        private string activeTargetLeaseId = string.Empty;
        private string activeTargetInstanceId = string.Empty;
        private string deferredPlayerText = string.Empty;
        private bool targetBindingRetryPending;
        private int targetBindingRetryAttempt;
        private DateTime nextTargetBindingRetryUtc;
        private int activeConversationProbeTurns;
        private string pendingConversationRequestId = string.Empty;
        private string pendingConversationId = string.Empty;
        private string pendingConversationNpcHeroId = string.Empty;
        private string pendingConversationText = string.Empty;
        private int pendingConversationTurn;
        private int pendingConversationRetryAttempt;
        private string retryConversationId = string.Empty;
        private string retryConversationNpcHeroId = string.Empty;
        private string retryConversationText = string.Empty;
        private int retryConversationTurn;
        private int retryConversationAttempt;
        private string timeoutConversationRequestId = string.Empty;
        private string timeoutConversationId = string.Empty;
        private string queuedDisplayRequestId = string.Empty;
        private string queuedDisplayConversationId = string.Empty;
        private string queuedDisplayText = string.Empty;
        private string queuedDisplayKind = string.Empty;
        private int intentCapabilityFlags;
        private string intentCampaignGeneration = string.Empty;
        private long intentStateRevision;
        private string relationShadowRequestId = string.Empty;
        private string relationShadowConversationId = string.Empty;
        private string relationProposalRequestId = string.Empty;
        private string relationProposalConversationId = string.Empty;
        private string relationProposalIntentId = string.Empty;
        private string relationConfirmationRequestId = string.Empty;
        private string diplomacySnapshotRequestId = string.Empty;
        private string diplomacySnapshotConversationId = string.Empty;
        private string diplomacyProposalRequestId = string.Empty;
        private string diplomacyProposalConversationId = string.Empty;
        private string diplomacyProposalIntentId = string.Empty;
        private string diplomacyProposalAction = string.Empty;
        private string diplomacyConfirmationRequestId = string.Empty;
        private string diplomacyRecipientDecisionRequestId = string.Empty;
        private string diplomacyRecipientDecisionConversationId = string.Empty;
        private string diplomacyRecipientDecisionKind = string.Empty;
        private string diplomacyRecipientDecisionStatementId = string.Empty;
        private string validationGateRequestId = string.Empty;
        private string validationGateConversationId = string.Empty;
        private string pendingInboxNotificationText = string.Empty;
        private string pendingInboxNotificationKey = string.Empty;
        private string lastInboxNotificationKey = string.Empty;
        private AIPortDiplomacyMapNotification pendingInboxMapNotification;
        private List<AIPortDiplomacyMapNotification> pendingInboxReconcileNotifications;
        private string pendingMapDismissStatementId = string.Empty;
        private string pendingMapReleaseStatementId = string.Empty;
        private bool dismissAllMapNotifications;
        private string diplomacyInboxPageRequestId = string.Empty;
        private string diplomacyInboxPageCursor = string.Empty;
        private long diplomacyInboxPageRevision;
        private bool diplomacyInboxRefreshRequested;
        private readonly Dictionary<string,AIPortDiplomacyMapNotification> diplomacyInboxAccumulator = new Dictionary<string,AIPortDiplomacyMapNotification>(StringComparer.Ordinal);
        private readonly List<string> diplomacyInboxAccumulatorOrder = new List<string>();
        private string diplomacyLifecycleRequestId = string.Empty;
        private string diplomacyLifecycleConversationId = string.Empty;
        private string diplomacyLifecycleRequestKind = string.Empty;
        private string diplomacyLifecycleStatementId = string.Empty;

        public AIPortConversationClientHandler(IMessageBroker messageBroker, INetwork network)
        {
            this.messageBroker = messageBroker;
            this.network = network;
            // This bridge callback is the sole entry point that may create an AI dialogue request.
            AIPortConversationInputBridge.Attach(HandleFreeFormSubmitted, CanSubmitFreeForm, RequestSharedCampaignPause, HandleReturnToVanilla);
            AIPortRuntimeLifecycleBridge.AttachApplicationTick(HandleApplicationTick);
            AIPortDiplomacyDecisionBridge.Attach(TrySubmitDiplomacyRecipientDecisionFromUi);
            AIPortDiplomacyMapNotificationRegistrar.Initialize();
            messageBroker.Subscribe<CampaignReady>(Handle);
            messageBroker.Subscribe<NetworkDisconnected>(Handle);
            messageBroker.Subscribe<AIConversationTargetBound>(Handle);
            messageBroker.Subscribe<AIConversationAccepted>(Handle);
            messageBroker.Subscribe<AIConversationResult>(Handle);
            messageBroker.Subscribe<AIConversationError>(Handle);
            messageBroker.Subscribe<AIPortCapabilitiesResponse>(Handle);
            messageBroker.Subscribe<AIIntentProposalResult>(Handle);
            messageBroker.Subscribe<AIDiplomacySnapshotResponse>(Handle);
            messageBroker.Subscribe<AIPortValidationGateResponse>(Handle);
            messageBroker.Subscribe<AIDiplomacyInboxNotification>(Handle);
            messageBroker.Subscribe<AIDiplomacyInboxPageResponse>(Handle);
            messageBroker.Subscribe<AIDiplomacyLifecycleNotification>(Handle);
        }

        public bool CampaignReady { get { return campaignReady; } }

        public void Dispose()
        {
            ClearCampaignListeners("dispose");
            lock (probeSync)
            {
                disposed = true;
            }

            AIPortConversationInputBridge.Detach(HandleFreeFormSubmitted, CanSubmitFreeForm, RequestSharedCampaignPause, HandleReturnToVanilla);
            AIPortRuntimeLifecycleBridge.DetachApplicationTick(HandleApplicationTick);
            AIPortDiplomacyDecisionBridge.Detach(TrySubmitDiplomacyRecipientDecisionFromUi);
            messageBroker.Unsubscribe<CampaignReady>(Handle);
            messageBroker.Unsubscribe<NetworkDisconnected>(Handle);
            messageBroker.Unsubscribe<AIConversationTargetBound>(Handle);
            messageBroker.Unsubscribe<AIConversationAccepted>(Handle);
            messageBroker.Unsubscribe<AIConversationResult>(Handle);
            messageBroker.Unsubscribe<AIConversationError>(Handle);
            messageBroker.Unsubscribe<AIPortCapabilitiesResponse>(Handle);
            messageBroker.Unsubscribe<AIIntentProposalResult>(Handle);
            messageBroker.Unsubscribe<AIDiplomacySnapshotResponse>(Handle);
            messageBroker.Unsubscribe<AIPortValidationGateResponse>(Handle);
            messageBroker.Unsubscribe<AIDiplomacyInboxNotification>(Handle);
            messageBroker.Unsubscribe<AIDiplomacyInboxPageResponse>(Handle);
            messageBroker.Unsubscribe<AIDiplomacyLifecycleNotification>(Handle);
        }

        private void Handle(MessagePayload<CampaignReady> payload)
        {
            campaignReady = true;
            EnsureCampaignListeners();
            Logger.Information("AIPort client campaign ready; dialogue requests are allowed");
        }

        private void Handle(MessagePayload<NetworkDisconnected> payload)
        {
            ClearCampaignListeners("network_disconnected");
            lock (probeSync)
            {
                campaignReady = false;
                clientSequence = 0;
                intentCapabilityFlags = 0;
                intentCampaignGeneration = string.Empty;
                intentStateRevision = 0;
                relationShadowRequestId = string.Empty;
                relationShadowConversationId = string.Empty;
                diplomacySnapshotRequestId = string.Empty;
                diplomacySnapshotConversationId = string.Empty;
                diplomacyProposalRequestId = string.Empty;
                diplomacyProposalConversationId = string.Empty;
                diplomacyProposalIntentId = string.Empty;
                diplomacyProposalAction = string.Empty;
                diplomacyConfirmationRequestId = string.Empty;
                diplomacyRecipientDecisionRequestId = string.Empty;
                diplomacyRecipientDecisionConversationId = string.Empty;
                diplomacyRecipientDecisionKind = string.Empty;
                diplomacyRecipientDecisionStatementId = string.Empty;
                validationGateRequestId = string.Empty;
                validationGateConversationId = string.Empty;
                pendingInboxNotificationText = string.Empty;
                pendingInboxNotificationKey = string.Empty;
                lastInboxNotificationKey = string.Empty;
                pendingInboxMapNotification = null;
                pendingInboxReconcileNotifications = null;
                pendingMapDismissStatementId = string.Empty;
                pendingMapReleaseStatementId = string.Empty;
                dismissAllMapNotifications = true;
                diplomacyInboxPageRequestId = string.Empty;
                diplomacyInboxPageCursor = string.Empty;
                diplomacyInboxPageRevision = 0;
                diplomacyInboxRefreshRequested = false;
                diplomacyInboxAccumulator.Clear();
                diplomacyInboxAccumulatorOrder.Clear();
                diplomacyLifecycleRequestId = string.Empty;
                diplomacyLifecycleConversationId = string.Empty;
                diplomacyLifecycleRequestKind = string.Empty;
                diplomacyLifecycleStatementId = string.Empty;
            }

            Logger.Information("AIPort client dialogue state reset after network disconnect");
        }

        private void Handle(MessagePayload<AIConversationTargetBound> payload)
        {
            string deferred = string.Empty;
            bool retryScheduled = false;
            int retryAttempt = 0;
            lock (probeSync)
            {
                if (disposed || !campaignReady || !string.Equals(payload.What.ConversationId, activeConversationId, StringComparison.Ordinal)) return;
                if (!payload.What.Accepted)
                {
                    activeTargetLeaseId = string.Empty;
                    activeTargetInstanceId = string.Empty;
                    bool playerUnresolved = string.Equals(payload.What.ErrorCode, "player_unresolved", StringComparison.OrdinalIgnoreCase);
                    if (playerUnresolved && targetBindingRetryAttempt < MaximumTargetBindingRetries)
                    {
                        targetBindingRetryPending = true;
                        nextTargetBindingRetryUtc = DateTime.UtcNow.AddMilliseconds(TargetBindingRetryDelayMilliseconds);
                        retryAttempt = targetBindingRetryAttempt + 1;
                        retryScheduled = true;
                    }
                    else
                    {
                        targetBindingRetryPending = false;
                        deferredPlayerText = string.Empty;
                        QueueDisplayLocked(string.Empty, activeConversationId, "Сервер не подтвердил текущего собеседника. Закройте диалог и начните его снова.", "target_rejected");
                    }
                }
                else if (string.Equals(payload.What.TargetId, activeNpcHeroId, StringComparison.Ordinal))
                {
                    activeTargetLeaseId = payload.What.TargetLeaseId ?? string.Empty;
                    activeTargetInstanceId = payload.What.TargetInstanceId ?? string.Empty;
                    targetBindingRetryPending = false;
                    targetBindingRetryAttempt = 0;
                    nextTargetBindingRetryUtc = DateTime.MinValue;
                    deferred = deferredPlayerText;
                    deferredPlayerText = string.Empty;
                }
            }
            Logger.Information("AIPort target binding result ConversationId={ConversationId} TargetId={TargetId} TargetInstanceId={TargetInstanceId} Accepted={Accepted} ErrorCode={ErrorCode}", payload.What.ConversationId, payload.What.TargetId, payload.What.TargetInstanceId, payload.What.Accepted, payload.What.ErrorCode);
            if (retryScheduled) Logger.Information("AIPort target binding retry scheduled ConversationId={ConversationId} Attempt={Attempt} DelayMs={DelayMs} Reason=player_unresolved", payload.What.ConversationId, retryAttempt, TargetBindingRetryDelayMilliseconds);
            if (!string.IsNullOrWhiteSpace(deferred)) SubmitPlayerText(deferred, "deferred_after_target_bind");
        }

        private void Handle(MessagePayload<AIConversationAccepted> payload)
        {
            Logger.Information("AIPort conversation accepted RequestId={RequestId} ConversationId={ConversationId} QueuePosition={QueuePosition}", payload.What.RequestId, payload.What.ConversationId, payload.What.QueuePosition);
        }

        private void Handle(MessagePayload<AIConversationResult> payload)
        {
            bool queuedForDisplay = false;
            lock (probeSync)
            {
                bool matchesPending = string.Equals(payload.What.RequestId, pendingConversationRequestId, StringComparison.Ordinal);
                if (matchesPending
                    && !string.IsNullOrWhiteSpace(activeConversationId)
                    && string.Equals(payload.What.ConversationId, activeConversationId, StringComparison.Ordinal)
                    && !string.IsNullOrWhiteSpace(payload.What.DisplayText))
                {
                    QueueDisplayLocked(payload.What.RequestId, payload.What.ConversationId, payload.What.DisplayText, "result");
                    queuedForDisplay = true;
                }
                if (matchesPending && payload.What.StateRevision > 0)
                {
                    intentStateRevision = payload.What.StateRevision;
                }
                if (matchesPending) ClearPendingConversationRequestLocked();
            }

            Logger.Information("AIPort conversation result RequestId={RequestId} ConversationId={ConversationId} ServerSequence={ServerSequence} Completed={Completed} SpeakerHeroId={SpeakerHeroId} DisplayChars={DisplayChars} StateRevision={StateRevision}", payload.What.RequestId, payload.What.ConversationId, payload.What.ServerSequence, payload.What.Completed, payload.What.SpeakerHeroId, payload.What.DisplayText == null ? 0 : payload.What.DisplayText.Length, payload.What.StateRevision);
            if (queuedForDisplay) Logger.Information("AIPort authoritative NPC response queued for dialogue UI RequestId={RequestId} ConversationId={ConversationId}", payload.What.RequestId, payload.What.ConversationId);
        }

        private void Handle(MessagePayload<AIConversationError> payload)
        {
            Logger.Warning("AIPort conversation error RequestId={RequestId} ErrorCode={ErrorCode} Retryable={Retryable} RetryAfterMs={RetryAfterMs} Message={Message}", payload.What.RequestId, payload.What.ErrorCode, payload.What.Retryable, payload.What.RetryAfterMilliseconds, payload.What.SafeMessage);
            bool rateLimited = string.Equals(payload.What.ErrorCode, "rate_limited", StringComparison.OrdinalIgnoreCase);
            bool playerUnresolved = string.Equals(payload.What.ErrorCode, "player_unresolved", StringComparison.OrdinalIgnoreCase);
            bool automaticRetry = payload.What.Retryable && (rateLimited || playerUnresolved);
            bool scheduled = false;
            bool exhausted = false;
            bool surfaced = false;
            int delayMilliseconds = 0;
            int retryAttempt = 0;
            string conversationId = string.Empty;
            lock (probeSync)
            {
                if (disposed || !campaignReady
                    || !string.Equals(payload.What.RequestId, pendingConversationRequestId, StringComparison.Ordinal)
                    || string.IsNullOrWhiteSpace(activeConversationId)
                    || !string.Equals(pendingConversationId, activeConversationId, StringComparison.Ordinal)) return;

                conversationId = pendingConversationId;
                if (automaticRetry && pendingConversationRetryAttempt < MaximumConversationRateLimitRetries)
                {
                    retryAttempt = pendingConversationRetryAttempt + 1;
                    delayMilliseconds = rateLimited
                        ? ResolveRateLimitRetryDelayMilliseconds(payload.What.RetryAfterMilliseconds, pendingConversationRetryAttempt)
                        : RetryProbeDelayMilliseconds;
                    CancelConversationRetryTimerLocked("retry_rescheduled");
                    retryConversationId = pendingConversationId;
                    retryConversationNpcHeroId = pendingConversationNpcHeroId;
                    retryConversationText = pendingConversationText;
                    retryConversationTurn = pendingConversationTurn;
                    retryConversationAttempt = retryAttempt;
                    ClearPendingConversationRequestLocked();
                    conversationRetryTimer = new ThreadingTimer(_ => TryRetryConversationProbe(), null, delayMilliseconds, Timeout.Infinite);
                    string retryText = rateLimited
                        ? "Слишком много запросов. Повторю автоматически через " + Math.Max(1, (int)Math.Ceiling(delayMilliseconds / 1000.0)) + " сек…"
                        : "Сервер ещё готовит персонажа. Повторяю запрос…";
                    QueueDisplayLocked(payload.What.RequestId, conversationId, retryText, "retry_wait");
                    scheduled = true;
                }
                else
                {
                    exhausted = automaticRetry && pendingConversationRetryAttempt >= MaximumConversationRateLimitRetries;
                    ClearPendingConversationRequestLocked();
                    string displayText = exhausted
                        ? "Не удалось получить ответ после нескольких попыток. Попробуйте ещё раз."
                        : ResolveConversationErrorDisplayText(payload.What.ErrorCode);
                    QueueDisplayLocked(payload.What.RequestId, conversationId, displayText, exhausted ? "retry_exhausted" : "error");
                    surfaced = true;
                }
            }

            string reason = rateLimited ? "rate_limited" : playerUnresolved ? "player_unresolved" : payload.What.ErrorCode;
            if (scheduled) Logger.Information("AIPort conversation retry scheduled ConversationId={ConversationId} Attempt={Attempt} DelayMs={DelayMs} Reason={Reason}", conversationId, retryAttempt, delayMilliseconds, reason);
            else if (exhausted) Logger.Warning("AIPort conversation retry limit reached ConversationId={ConversationId} Attempts={Attempts} Reason={Reason}", conversationId, MaximumConversationRateLimitRetries, reason);
            else if (surfaced) Logger.Information("AIPort conversation error queued for dialogue UI ConversationId={ConversationId} Reason={Reason}", conversationId, reason);
        }

        private void EnsureCampaignListeners()
        {
            IMbEvent<float> tickEvent;
            IMbEvent<IAgent> agentJoinedEvent;
            IMbEvent<IEnumerable<CharacterObject>> conversationEndedEvent;
            ConversationManager conversationManager;
            try
            {
                tickEvent = CampaignEvents.TickEvent;
                agentJoinedEvent = CampaignEvents.OnAgentJoinedConversationEvent;
                conversationEndedEvent = CampaignEvents.ConversationEnded;
                conversationManager = Campaign.Current == null ? null : Campaign.Current.ConversationManager;
            }
            catch (Exception ex)
            {
                Logger.Warning(ex, "AIPort could not load the current campaign event set");
                return;
            }

            IMbEvent<float> oldTickEvent;
            IMbEvent<IAgent> oldAgentJoinedEvent;
            IMbEvent<IEnumerable<CharacterObject>> oldConversationEndedEvent;
            ConversationManager oldConversationManager;
            lock (probeSync)
            {
                if (disposed) return;
                if (ReferenceEquals(attachedTickEvent, tickEvent)
                    && ReferenceEquals(attachedAgentJoinedEvent, agentJoinedEvent)
                    && ReferenceEquals(attachedConversationEndedEvent, conversationEndedEvent)
                    && ReferenceEquals(attachedConversationManager, conversationManager)) return;
                oldTickEvent = attachedTickEvent;
                oldAgentJoinedEvent = attachedAgentJoinedEvent;
                oldConversationEndedEvent = attachedConversationEndedEvent;
                oldConversationManager = attachedConversationManager;
                attachedTickEvent = tickEvent;
                attachedAgentJoinedEvent = agentJoinedEvent;
                attachedConversationEndedEvent = conversationEndedEvent;
                attachedConversationManager = conversationManager;
            }

            ClearListenerSet(oldTickEvent, oldAgentJoinedEvent, oldConversationEndedEvent, oldConversationManager);
            try
            {
                tickEvent.AddNonSerializedListener(this, HandleCampaignTick);
                agentJoinedEvent.AddNonSerializedListener(this, HandleAgentJoinedConversation);
                conversationEndedEvent.AddNonSerializedListener(this, HandleConversationEnded);
                Logger.Information("AIPort client conversation listeners attached TickEventHash={TickEventHash} AgentEventHash={AgentEventHash} EndEventHash={EndEventHash} ManagerHash={ManagerHash}", tickEvent.GetHashCode(), agentJoinedEvent.GetHashCode(), conversationEndedEvent.GetHashCode(), conversationManager == null ? 0 : conversationManager.GetHashCode());
            }
            catch (Exception ex)
            {
                ClearListenerSet(tickEvent, agentJoinedEvent, conversationEndedEvent, conversationManager);
                lock (probeSync)
                {
                    if (ReferenceEquals(attachedTickEvent, tickEvent)) attachedTickEvent = null;
                    if (ReferenceEquals(attachedAgentJoinedEvent, agentJoinedEvent)) attachedAgentJoinedEvent = null;
                    if (ReferenceEquals(attachedConversationEndedEvent, conversationEndedEvent)) attachedConversationEndedEvent = null;
                    if (ReferenceEquals(attachedConversationManager, conversationManager)) attachedConversationManager = null;
                }
                Logger.Warning(ex, "AIPort failed to attach client conversation listeners");
            }
        }

        private void ClearCampaignListeners(string reason)
        {
            IMbEvent<float> tickEvent;
            IMbEvent<IAgent> agentJoinedEvent;
            IMbEvent<IEnumerable<CharacterObject>> conversationEndedEvent;
            ConversationManager conversationManager;
            lock (probeSync)
            {
                tickEvent = attachedTickEvent;
                agentJoinedEvent = attachedAgentJoinedEvent;
                conversationEndedEvent = attachedConversationEndedEvent;
                conversationManager = attachedConversationManager;
                attachedTickEvent = null;
                attachedAgentJoinedEvent = null;
                attachedConversationEndedEvent = null;
                attachedConversationManager = null;
                activeNpcHeroId = string.Empty;
                activeConversationId = string.Empty;
                activeClientTargetNonce = string.Empty;
                activeTargetLeaseId = string.Empty;
                activeTargetInstanceId = string.Empty;
                deferredPlayerText = string.Empty;
                relationShadowRequestId = string.Empty;
                relationShadowConversationId = string.Empty;
                ResetTargetBindingRetryLocked();
                regularAgentNonces.Clear();
                activeConversationProbeTurns = 0;
                CancelConversationRetryTimerLocked(reason);
                ClearPendingConversationRequestLocked();
                ClearQueuedDisplayLocked();
            }
            ClearListenerSet(tickEvent, agentJoinedEvent, conversationEndedEvent, conversationManager);
        }

        private void ClearListenerSet(IMbEvent<float> tickEvent, IMbEvent<IAgent> agentJoinedEvent, IMbEvent<IEnumerable<CharacterObject>> conversationEndedEvent, ConversationManager conversationManager)
        {
            try { if (tickEvent != null) tickEvent.ClearListeners(this); }
            catch (Exception ex) { Logger.Warning(ex, "AIPort failed to clear tick listener"); }
            try { if (agentJoinedEvent != null) agentJoinedEvent.ClearListeners(this); }
            catch (Exception ex) { Logger.Warning(ex, "AIPort failed to clear agent conversation listener"); }
            try { if (conversationEndedEvent != null) conversationEndedEvent.ClearListeners(this); }
            catch (Exception ex) { Logger.Warning(ex, "AIPort failed to clear conversation end listener"); }
        }

        private void HandleAgentJoinedConversation(IAgent agent)
        {
            CharacterObject character = agent == null ? null : agent.Character as CharacterObject;
            if (character == null || character.IsPlayerCharacter) return;
            string targetId = character.IsHero && character.HeroObject != null ? character.HeroObject.StringId : character.StringId;
            string targetKind = character.IsHero ? "hero" : "regular_character";
            string targetNonce = character.IsHero ? string.Empty : GetOrCreateRegularAgentNonce(agent);
            string conversationId;
            string oldConversationId;
            string oldLeaseId;
            lock (probeSync)
            {
                if (disposed || !campaignReady || string.IsNullOrWhiteSpace(targetId)) return;
                if (string.Equals(activeNpcHeroId, targetId, StringComparison.Ordinal)
                    && string.Equals(activeClientTargetNonce, targetNonce, StringComparison.Ordinal)
                    && !string.IsNullOrWhiteSpace(activeConversationId)) return;
                oldConversationId = activeConversationId;
                oldLeaseId = activeTargetLeaseId;
                CancelConversationRetryTimerLocked("conversation_replaced");
                ClearPendingConversationRequestLocked();
                ClearQueuedDisplayLocked();
                activeNpcHeroId = targetId;
                activeConversationId = Guid.NewGuid().ToString("N");
                activeClientTargetNonce = targetNonce;
                activeTargetLeaseId = string.Empty;
                activeTargetInstanceId = string.Empty;
                deferredPlayerText = string.Empty;
                ResetTargetBindingRetryLocked();
                activeConversationProbeTurns = 0;
                conversationId = activeConversationId;
            }
            TryCloseTargetBinding(oldConversationId, oldLeaseId, "conversation_replaced");
            TryOpenTargetBinding(targetId, conversationId, targetNonce, "agent_joined");
            Logger.Information("AIPort client observed NPC conversation start NpcTargetId={NpcTargetId} TargetKind={TargetKind} ClientTargetNonce={ClientTargetNonce} ConversationId={ConversationId} Source=agent_joined", targetId, targetKind, targetNonce, conversationId);
        }

        private void HandleFreeFormSubmitted(string playerText)
        {
            int delta;string statementId,recipientDecision,lifecycleKind,commitToken;
            if (TryParseRelationShadowCommand(playerText, out delta)) SubmitRelationShadowProbe(delta);
            else if (TryParseRelationProposalCommand(playerText, out delta)) SubmitRelationProposal(delta);
            else if (string.Equals((playerText ?? string.Empty).Trim(), "/relation-confirm", StringComparison.OrdinalIgnoreCase)) SubmitRelationConfirmation();
            else if (string.Equals((playerText ?? string.Empty).Trim(), "/diplomacy-snapshot", StringComparison.OrdinalIgnoreCase) || string.Equals((playerText ?? string.Empty).Trim(), "/diplomacy-authority", StringComparison.OrdinalIgnoreCase) || string.Equals((playerText ?? string.Empty).Trim(), "/diplomacy-inbox", StringComparison.OrdinalIgnoreCase) || string.Equals((playerText ?? string.Empty).Trim(), "/diplomacy-history", StringComparison.OrdinalIgnoreCase)) SubmitDiplomacySnapshot();
            else if (TryParseDiplomacyRecipientCommand(playerText,out statementId,out recipientDecision)) SubmitDiplomacyRecipientDecision(statementId,recipientDecision);
            else if (TryParseDiplomacyLifecycleCommand(playerText,out statementId,out lifecycleKind,out commitToken)) SubmitDiplomacyLifecycleCommand(statementId,lifecycleKind,commitToken);
            else if (string.Equals((playerText ?? string.Empty).Trim(), "/diplomacy-propose war", StringComparison.OrdinalIgnoreCase)) SubmitDiplomacyProposal("war");
            else if (string.Equals((playerText ?? string.Empty).Trim(), "/diplomacy-propose peace", StringComparison.OrdinalIgnoreCase)) SubmitDiplomacyProposal("peace");
            else if (string.Equals((playerText ?? string.Empty).Trim(), "/diplomacy-confirm", StringComparison.OrdinalIgnoreCase)) SubmitDiplomacyConfirmation();
            else if (string.Equals((playerText ?? string.Empty).Trim(), "/aiport-gate baseline", StringComparison.OrdinalIgnoreCase)) SubmitValidationGate("baseline");
            else if (string.Equals((playerText ?? string.Empty).Trim(), "/aiport-gate report", StringComparison.OrdinalIgnoreCase) || string.Equals((playerText ?? string.Empty).Trim(), "/aiport-status", StringComparison.OrdinalIgnoreCase)) SubmitValidationGate("report");
            else SubmitPlayerText(playerText, "free_form");
        }

        private static bool TryParseRelationProposalCommand(string text, out int delta)
        {
            delta = 0; text = (text ?? string.Empty).Trim();
            if (string.Equals(text, "/relation-propose +1", StringComparison.OrdinalIgnoreCase)) { delta = 1; return true; }
            if (string.Equals(text, "/relation-propose -1", StringComparison.OrdinalIgnoreCase)) { delta = -1; return true; }
            return false;
        }

        private static bool TryParseRelationShadowCommand(string text, out int delta)
        {
            delta = 0; text = (text ?? string.Empty).Trim();
            if (string.Equals(text, "/relation-shadow +1", StringComparison.OrdinalIgnoreCase)) { delta = 1; return true; }
            if (string.Equals(text, "/relation-shadow -1", StringComparison.OrdinalIgnoreCase)) { delta = -1; return true; }
            return false;
        }

        private void SubmitRelationShadowProbe(int delta)
        {
            string requestId, generation, conversationId, leaseId, targetInstanceId, payloadJson; long revision;
            lock (probeSync)
            {
                if (disposed || !campaignReady || (intentCapabilityFlags & AIPortProtocol.CapabilityRelationShadowIntent) == 0 || !string.IsNullOrWhiteSpace(relationShadowRequestId) || string.IsNullOrWhiteSpace(activeConversationId) || string.IsNullOrWhiteSpace(activeTargetLeaseId) || string.IsNullOrWhiteSpace(activeTargetInstanceId))
                {
                    QueueDisplayLocked(string.Empty, activeConversationId, "Relation shadow probe недоступен: capability или активная привязка цели ещё не готовы.", "relation_shadow_unavailable");
                    return;
                }
                requestId = Guid.NewGuid().ToString("N"); generation = intentCampaignGeneration; revision = intentStateRevision;
                conversationId = activeConversationId; leaseId = activeTargetLeaseId; targetInstanceId = activeTargetInstanceId;
                relationShadowRequestId = requestId; relationShadowConversationId = conversationId;
                payloadJson = "{\"conversationId\":\"" + conversationId + "\",\"targetLeaseId\":\"" + leaseId + "\",\"targetInstanceId\":\"" + targetInstanceId + "\",\"delta\":" + delta.ToString(System.Globalization.CultureInfo.InvariantCulture) + ",\"reason\":\"manual_dialogue_probe\"}";
            }
            network.SendAll(new AIIntentProposalRequest(AIPortProtocol.Version, requestId, generation, revision, "relation_change_shadow", payloadJson));
            Logger.Information("AIPort relation shadow intent requested RequestId={RequestId} ConversationId={ConversationId} TargetInstanceId={TargetInstanceId} Delta={Delta} CampaignGeneration={CampaignGeneration} Revision={Revision}", requestId, conversationId, targetInstanceId, delta, generation, revision);
        }

        private void SubmitRelationProposal(int delta)
        {
            string requestId, generation, conversationId, leaseId, targetInstanceId, payloadJson; long revision;
            lock (probeSync)
            {
                if (disposed || !campaignReady || (intentCapabilityFlags & AIPortProtocol.CapabilityRelationConfirmation) == 0 || !string.IsNullOrWhiteSpace(relationProposalRequestId) || !string.IsNullOrWhiteSpace(relationConfirmationRequestId) || string.IsNullOrWhiteSpace(activeConversationId) || string.IsNullOrWhiteSpace(activeTargetLeaseId) || string.IsNullOrWhiteSpace(activeTargetInstanceId))
                {
                    QueueDisplayLocked(string.Empty, activeConversationId, "Relation proposal недоступен: capability или активная привязка цели ещё не готовы.", "relation_proposal_unavailable"); return;
                }
                requestId=Guid.NewGuid().ToString("N"); generation=intentCampaignGeneration; revision=intentStateRevision; conversationId=activeConversationId; leaseId=activeTargetLeaseId; targetInstanceId=activeTargetInstanceId;
                relationProposalRequestId=requestId; relationProposalConversationId=conversationId; relationProposalIntentId=string.Empty;
                payloadJson="{\"conversationId\":\""+conversationId+"\",\"targetLeaseId\":\""+leaseId+"\",\"targetInstanceId\":\""+targetInstanceId+"\",\"delta\":"+delta.ToString(System.Globalization.CultureInfo.InvariantCulture)+",\"reason\":\"manual_dialogue_proposal\"}";
            }
            network.SendAll(new AIIntentProposalRequest(AIPortProtocol.Version,requestId,generation,revision,"relation_change_proposal",payloadJson));
            Logger.Information("AIPort relation proposal requested RequestId={RequestId} ConversationId={ConversationId} TargetInstanceId={TargetInstanceId} Delta={Delta} Revision={Revision}",requestId,conversationId,targetInstanceId,delta,revision);
        }

        private void SubmitRelationConfirmation()
        {
            string requestId,generation,conversationId,leaseId,targetInstanceId,intentId,payloadJson;long revision;
            lock(probeSync)
            {
                if(disposed || !campaignReady || (intentCapabilityFlags & AIPortProtocol.CapabilityRelationConfirmation)==0 || string.IsNullOrWhiteSpace(relationProposalIntentId) || !string.IsNullOrWhiteSpace(relationConfirmationRequestId) || string.IsNullOrWhiteSpace(activeConversationId) || !string.Equals(activeConversationId,relationProposalConversationId,StringComparison.Ordinal))
                { QueueDisplayLocked(string.Empty,activeConversationId,"Нет активного relation proposal для подтверждения.","relation_confirmation_unavailable");return; }
                requestId=Guid.NewGuid().ToString("N");generation=intentCampaignGeneration;revision=intentStateRevision;conversationId=activeConversationId;leaseId=activeTargetLeaseId;targetInstanceId=activeTargetInstanceId;intentId=relationProposalIntentId;relationConfirmationRequestId=requestId;
                payloadJson="{\"intentId\":\""+intentId+"\",\"conversationId\":\""+conversationId+"\",\"targetLeaseId\":\""+leaseId+"\",\"targetInstanceId\":\""+targetInstanceId+"\",\"reason\":\"manual_dialogue_confirm\"}";
            }
            network.SendAll(new AIIntentProposalRequest(AIPortProtocol.Version,requestId,generation,revision,"relation_change_confirm",payloadJson));
            Logger.Information("AIPort relation confirmation requested RequestId={RequestId} ProposalIntentId={ProposalIntentId} ConversationId={ConversationId} Revision={Revision}",requestId,intentId,conversationId,revision);
        }


        private void Handle(MessagePayload<AIDiplomacyInboxNotification> payload)
        {
            AIDiplomacyInboxNotification notice=payload.What;if(notice==null)return;string key=string.Empty;bool decisionUi=false,typedInbox=false;
            lock(probeSync)
            {
                if(disposed||!campaignReady||notice.ProtocolVersion!=AIPortProtocol.Version||(intentCapabilityFlags&AIPortProtocol.CapabilityDiplomacyInboxNotification)==0||notice.PendingCount<1||notice.PendingCount>AIPortProtocol.MaximumDiplomacyInboxItems||!IsHexId(notice.NotificationId)||!IsHexId(notice.LatestStatementId)||!string.Equals(notice.CampaignGeneration,intentCampaignGeneration,StringComparison.Ordinal)||notice.StateRevision<intentStateRevision)return;
                key=notice.CampaignGeneration+":"+notice.StateRevision.ToString(System.Globalization.CultureInfo.InvariantCulture)+":"+notice.PendingCount.ToString(System.Globalization.CultureInfo.InvariantCulture)+":"+notice.LatestStatementId;if(string.Equals(key,lastInboxNotificationKey,StringComparison.Ordinal)||string.Equals(key,pendingInboxNotificationKey,StringComparison.Ordinal))return;
                intentStateRevision=notice.StateRevision;typedInbox=(intentCapabilityFlags&AIPortProtocol.CapabilityDiplomacyInboxList)!=0;
                if(typedInbox){diplomacyInboxRefreshRequested=true;}
                else
                {
                    decisionUi=(intentCapabilityFlags&AIPortProtocol.CapabilityDiplomacyDecisionUi)!=0&&(string.Equals(notice.Action,"war",StringComparison.Ordinal)||string.Equals(notice.Action,"peace",StringComparison.Ordinal))&&!string.IsNullOrWhiteSpace(notice.SourceHeroId)&&notice.SourceHeroId.Length<=AIPortProtocol.MaximumTargetIdLength&&!string.IsNullOrWhiteSpace(notice.SourceFactionId)&&notice.SourceFactionId.Length<=AIPortProtocol.MaximumTargetIdLength&&!string.IsNullOrWhiteSpace(notice.TargetFactionId)&&notice.TargetFactionId.Length<=AIPortProtocol.MaximumTargetIdLength&&!string.IsNullOrWhiteSpace(notice.ExpiresUtc);
                    pendingInboxNotificationKey=key;pendingInboxNotificationText="Incoming diplomatic proposals: "+notice.PendingCount.ToString(System.Globalization.CultureInfo.InvariantCulture)+". Use /diplomacy-inbox for the full list.";pendingInboxMapNotification=decisionUi?new AIPortDiplomacyMapNotification(notice.LatestStatementId,notice.Action,notice.SourceHeroId,notice.SourceFactionId,notice.TargetFactionId,notice.ExpiresUtc,notice.PendingCount):null;
                }
            }
            Logger.Information("AIPort diplomacy inbox notification queued NotificationId={NotificationId} PendingCount={PendingCount} LatestStatementId={LatestStatementId} TypedInbox={TypedInbox} DecisionUi={DecisionUi} Revision={Revision} Reason={Reason}",notice.NotificationId,notice.PendingCount,notice.LatestStatementId,typedInbox,decisionUi,notice.StateRevision,notice.ReasonCode);
        }

        private void Handle(MessagePayload<AIDiplomacyInboxPageResponse> payload)
        {
            AIDiplomacyInboxPageResponse response=payload.What;if(response==null)return;string nextCursor=string.Empty,currentCursor=string.Empty;bool requestNext=false;int displayed=0;
            lock(probeSync)
            {
                if(disposed||string.IsNullOrWhiteSpace(diplomacyInboxPageRequestId)||!string.Equals(response.RequestId,diplomacyInboxPageRequestId,StringComparison.Ordinal))return;
                currentCursor=diplomacyInboxPageCursor;diplomacyInboxPageRequestId=string.Empty;diplomacyInboxPageCursor=string.Empty;
                if(response.ProtocolVersion!=AIPortProtocol.Version||!string.Equals(response.CampaignGeneration,intentCampaignGeneration,StringComparison.Ordinal)){ResetDiplomacyInboxAccumulatorLocked();return;}
                if(!response.Accepted)
                {
                    if(response.StateRevision>=intentStateRevision)intentStateRevision=response.StateRevision;if(string.Equals(response.ReasonCode,"stale_revision",StringComparison.Ordinal)||string.Equals(response.ReasonCode,"cursor_not_found",StringComparison.Ordinal))diplomacyInboxRefreshRequested=true;ResetDiplomacyInboxAccumulatorLocked();Logger.Information("AIPort typed diplomacy inbox page rejected RequestId={RequestId} Reason={Reason} Revision={Revision}",response.RequestId,response.ReasonCode,response.StateRevision);return;
                }
                AIDiplomacyInboxEntry[] entries=response.Entries??new AIDiplomacyInboxEntry[0];bool first=string.IsNullOrWhiteSpace(currentCursor);
                if(response.StateRevision<intentStateRevision||(!first&&response.StateRevision!=diplomacyInboxPageRevision)||response.TotalCount<0||response.TotalCount>AIPortProtocol.MaximumDiplomacyInboxItems||entries.Length>AIPortProtocol.MaximumDiplomacyInboxPageSize){diplomacyInboxRefreshRequested=true;ResetDiplomacyInboxAccumulatorLocked();return;}
                if(first){ResetDiplomacyInboxAccumulatorLocked();diplomacyInboxPageRevision=response.StateRevision;}intentStateRevision=response.StateRevision;
                bool decisionUi=(intentCapabilityFlags&AIPortProtocol.CapabilityDiplomacyDecisionUi)!=0;
                foreach(AIDiplomacyInboxEntry entry in entries)
                {
                    if(entry==null||!IsHexId(entry.StatementId)||(entry.Action!="war"&&entry.Action!="peace")||string.IsNullOrWhiteSpace(entry.SourceHeroId)||entry.SourceHeroId.Length>AIPortProtocol.MaximumTargetIdLength||string.IsNullOrWhiteSpace(entry.SourceFactionId)||entry.SourceFactionId.Length>AIPortProtocol.MaximumTargetIdLength||string.IsNullOrWhiteSpace(entry.TargetFactionId)||entry.TargetFactionId.Length>AIPortProtocol.MaximumTargetIdLength||string.IsNullOrWhiteSpace(entry.ExpiresUtc)){diplomacyInboxRefreshRequested=true;ResetDiplomacyInboxAccumulatorLocked();return;}
                    if(!diplomacyInboxAccumulator.ContainsKey(entry.StatementId)){diplomacyInboxAccumulatorOrder.Add(entry.StatementId);if(decisionUi)diplomacyInboxAccumulator[entry.StatementId]=new AIPortDiplomacyMapNotification(entry.StatementId,entry.Action,entry.SourceHeroId,entry.SourceHeroName,entry.SourceFactionId,entry.SourceFactionName,entry.TargetFactionId,entry.TargetFactionName,entry.ExpiresUtc,entry.Origin,entry.ReasonCode,entry.Score,response.TotalCount);else diplomacyInboxAccumulator[entry.StatementId]=null;}
                }
                if(diplomacyInboxAccumulatorOrder.Count>AIPortProtocol.MaximumDiplomacyInboxItems){diplomacyInboxRefreshRequested=true;ResetDiplomacyInboxAccumulatorLocked();return;}
                if(response.HasMore)
                {
                    if(!IsHexId(response.NextCursor)||string.Equals(response.NextCursor,currentCursor,StringComparison.Ordinal)){diplomacyInboxRefreshRequested=true;ResetDiplomacyInboxAccumulatorLocked();return;}nextCursor=response.NextCursor;requestNext=true;
                }
                else
                {
                    List<AIPortDiplomacyMapNotification> notices=new List<AIPortDiplomacyMapNotification>();foreach(string id in diplomacyInboxAccumulatorOrder){AIPortDiplomacyMapNotification item;if(diplomacyInboxAccumulator.TryGetValue(id,out item)&&item!=null)notices.Add(item);}displayed=notices.Count;pendingInboxReconcileNotifications=notices;pendingInboxMapNotification=null;pendingInboxNotificationKey="typed-inbox:"+response.CampaignGeneration+":"+response.StateRevision.ToString(System.Globalization.CultureInfo.InvariantCulture)+":"+response.TotalCount.ToString(System.Globalization.CultureInfo.InvariantCulture);pendingInboxNotificationText=response.TotalCount>0?"Incoming diplomatic proposals: "+response.TotalCount.ToString(System.Globalization.CultureInfo.InvariantCulture)+". Open the map notifications to accept or reject them.":string.Empty;ResetDiplomacyInboxAccumulatorLocked();
                }
            }
            Logger.Information("AIPort typed diplomacy inbox page completed RequestId={RequestId} Entries={Entries} TotalCount={TotalCount} HasMore={HasMore} Displayed={Displayed} Revision={Revision}",response.RequestId,response.Entries==null?0:response.Entries.Length,response.TotalCount,response.HasMore,displayed,response.StateRevision);
            if(requestNext)RequestDiplomacyInboxPage(nextCursor);
        }

        private void Handle(MessagePayload<AIDiplomacyLifecycleNotification> payload)
        {
            AIDiplomacyLifecycleNotification notice=payload.What;if(notice==null)return;string text,key;bool validStatus=string.Equals(notice.Status,"accepted_shadow",StringComparison.Ordinal)||string.Equals(notice.Status,"rejected_shadow",StringComparison.Ordinal)||string.Equals(notice.Status,"withdrawn_shadow",StringComparison.Ordinal)||string.Equals(notice.Status,"expired",StringComparison.Ordinal)||string.Equals(notice.Status,"committed_native_war",StringComparison.Ordinal)||string.Equals(notice.Status,"committed_native_peace",StringComparison.Ordinal);if(!validStatus||(!string.Equals(notice.Action,"war",StringComparison.Ordinal)&&!string.Equals(notice.Action,"peace",StringComparison.Ordinal))||(notice.NativeMutationApplied&&!string.Equals(notice.Status,"committed_native_war",StringComparison.Ordinal)&&!string.Equals(notice.Status,"committed_native_peace",StringComparison.Ordinal)))return;
            if(string.Equals(notice.Status,"accepted_shadow",StringComparison.Ordinal))text="Дипломатическое предложение принято обеими сторонами в shadow-режиме.";else if(string.Equals(notice.Status,"rejected_shadow",StringComparison.Ordinal))text="Дипломатическое предложение отклонено получателем.";else if(string.Equals(notice.Status,"withdrawn_shadow",StringComparison.Ordinal))text="Инициатор отозвал дипломатическое предложение.";else if(string.Equals(notice.Status,"expired",StringComparison.Ordinal))text="Срок дипломатического предложения истёк.";else if(string.Equals(notice.Status,"committed_native_war",StringComparison.Ordinal))text=notice.NativeMutationApplied?"Сервер применил нативное объявление войны.":"Нативное объявление войны не подтверждено.";else text=notice.NativeMutationApplied?"Сервер применил нативное заключение мира.":"Нативное заключение мира не подтверждено.";
            lock(probeSync){if(disposed||!campaignReady||notice.ProtocolVersion!=AIPortProtocol.Version||(intentCapabilityFlags&AIPortProtocol.CapabilityDiplomacyLifecycleBundle)==0||!IsHexId(notice.NotificationId)||!IsHexId(notice.StatementId)||!string.Equals(notice.CampaignGeneration,intentCampaignGeneration,StringComparison.Ordinal)||notice.StateRevision<intentStateRevision)return;key=notice.CampaignGeneration+":"+notice.StateRevision.ToString(System.Globalization.CultureInfo.InvariantCulture)+":"+notice.StatementId+":"+notice.Status;if(string.Equals(key,lastInboxNotificationKey,StringComparison.Ordinal)||string.Equals(key,pendingInboxNotificationKey,StringComparison.Ordinal))return;intentStateRevision=notice.StateRevision;pendingMapDismissStatementId=notice.StatementId;pendingInboxMapNotification=null;pendingInboxNotificationKey=key;pendingInboxNotificationText=text+" ID: "+notice.StatementId+".";if((intentCapabilityFlags&AIPortProtocol.CapabilityDiplomacyInboxList)!=0)diplomacyInboxRefreshRequested=true;}
            Logger.Information("AIPort diplomacy lifecycle notification queued NotificationId={NotificationId} StatementId={StatementId} Status={Status} Revision={Revision} NativeMutationApplied={NativeMutationApplied}",notice.NotificationId,notice.StatementId,notice.Status,notice.StateRevision,notice.NativeMutationApplied);
        }

        private void TryStartQueuedDiplomacyInboxRefresh()
        {
            bool start=false;lock(probeSync){if(!disposed&&campaignReady&&diplomacyInboxRefreshRequested&&string.IsNullOrWhiteSpace(diplomacyInboxPageRequestId)&&(intentCapabilityFlags&AIPortProtocol.CapabilityDiplomacyInboxList)!=0){diplomacyInboxRefreshRequested=false;start=true;}}if(start)RequestDiplomacyInboxPage(string.Empty);
        }

        private void RequestDiplomacyInboxPage(string cursor)
        {
            string requestId,generation;long revision;int pageSize=AIPortProtocol.MaximumDiplomacyInboxPageSize;
            lock(probeSync)
            {
                if(disposed||!campaignReady||(intentCapabilityFlags&AIPortProtocol.CapabilityDiplomacyInboxList)==0||!string.IsNullOrWhiteSpace(diplomacyInboxPageRequestId))return;
                bool first=string.IsNullOrWhiteSpace(cursor);if(first){ResetDiplomacyInboxAccumulatorLocked();diplomacyInboxPageRevision=intentStateRevision;}
                requestId=Guid.NewGuid().ToString("N");generation=intentCampaignGeneration;revision=diplomacyInboxPageRevision;diplomacyInboxPageRequestId=requestId;diplomacyInboxPageCursor=cursor??string.Empty;
            }
            network.SendAll(new AIDiplomacyInboxPageRequest(AIPortProtocol.Version,requestId,generation,revision,cursor??string.Empty,pageSize));Logger.Information("AIPort typed diplomacy inbox page requested RequestId={RequestId} Cursor={Cursor} CampaignGeneration={CampaignGeneration} Revision={Revision} PageSize={PageSize}",requestId,cursor,generation,revision,pageSize);
        }

        private void ResetDiplomacyInboxAccumulatorLocked(){diplomacyInboxAccumulator.Clear();diplomacyInboxAccumulatorOrder.Clear();}

        private static bool IsHexId(string value){if(string.IsNullOrWhiteSpace(value)||value.Length!=32)return false;foreach(char c in value)if(!char.IsDigit(c)&&!(c>='a'&&c<='f')&&!(c>='A'&&c<='F'))return false;return true;}

        private void TryDisplayInboxNotification()
        {
            string text,key,dismissStatement,releaseStatement;bool dismissAll;AIPortDiplomacyMapNotification mapNotice;List<AIPortDiplomacyMapNotification> reconcile;
            lock(probeSync){text=pendingInboxNotificationText;key=pendingInboxNotificationKey;mapNotice=pendingInboxMapNotification;reconcile=pendingInboxReconcileNotifications;dismissStatement=pendingMapDismissStatementId;releaseStatement=pendingMapReleaseStatementId;dismissAll=dismissAllMapNotifications;pendingInboxNotificationText=string.Empty;pendingInboxNotificationKey=string.Empty;pendingInboxMapNotification=null;pendingInboxReconcileNotifications=null;pendingMapDismissStatementId=string.Empty;pendingMapReleaseStatementId=string.Empty;dismissAllMapNotifications=false;if(!string.IsNullOrWhiteSpace(key))lastInboxNotificationKey=key;}
            if(dismissAll)AIPortDiplomacyMapNotificationRegistrar.DismissAll();else if(!string.IsNullOrWhiteSpace(dismissStatement))AIPortDiplomacyMapNotificationRegistrar.Dismiss(dismissStatement);if(!string.IsNullOrWhiteSpace(releaseStatement))AIPortDiplomacyMapNotificationRegistrar.ReleaseDecision(releaseStatement);
            if(string.IsNullOrWhiteSpace(text)&&mapNotice==null&&reconcile==null)return;
            try
            {
                bool mapDisplayed=reconcile!=null?AIPortDiplomacyMapNotificationRegistrar.Reconcile(reconcile):mapNotice!=null&&AIPortDiplomacyMapNotificationRegistrar.Publish(mapNotice);
                if(!mapDisplayed&&!string.IsNullOrWhiteSpace(text))InformationManager.DisplayMessage(new InformationMessage(text));
                Logger.Information("AIPort diplomacy inbox notification displayed Key={NotificationKey} MapNotification={MapNotification} ReconciledCount={ReconciledCount}",key,mapDisplayed,reconcile==null?0:reconcile.Count);
            }
            catch(Exception ex){Logger.Warning(ex,"AIPort failed to display diplomacy inbox notification");}
        }

        private static bool TryParseDiplomacyLifecycleCommand(string text,out string statementId,out string kind,out string commitToken)
        {
            statementId=string.Empty;kind=string.Empty;commitToken=string.Empty;string value=(text??string.Empty).Trim(),rest;
            if(value.StartsWith("/diplomacy-withdraw ",StringComparison.OrdinalIgnoreCase)){kind="withdraw";rest=value.Substring("/diplomacy-withdraw ".Length).Trim();if(!IsHexId(rest))return false;statementId=rest.ToLowerInvariant();return true;}
            if(value.StartsWith("/diplomacy-ready ",StringComparison.OrdinalIgnoreCase)){kind="preflight";rest=value.Substring("/diplomacy-ready ".Length).Trim();if(!IsHexId(rest))return false;statementId=rest.ToLowerInvariant();return true;}
            if(value.StartsWith("/diplomacy-native-war ",StringComparison.OrdinalIgnoreCase)){kind="commit_war";rest=value.Substring("/diplomacy-native-war ".Length).Trim();string[] parts=rest.Split(new[]{' '},StringSplitOptions.RemoveEmptyEntries);if(parts.Length!=2||!IsHexId(parts[0])||!IsHexId(parts[1]))return false;statementId=parts[0].ToLowerInvariant();commitToken=parts[1].ToLowerInvariant();return true;}
            if(value.StartsWith("/diplomacy-native-peace ",StringComparison.OrdinalIgnoreCase)){kind="commit_peace";rest=value.Substring("/diplomacy-native-peace ".Length).Trim();string[] parts=rest.Split(new[]{' '},StringSplitOptions.RemoveEmptyEntries);if(parts.Length!=2||!IsHexId(parts[0])||!IsHexId(parts[1]))return false;statementId=parts[0].ToLowerInvariant();commitToken=parts[1].ToLowerInvariant();return true;}
            return false;
        }
        private void SubmitDiplomacyLifecycleCommand(string statementId,string kind,string commitToken)
        {
            string requestId,generation,conversationId,payloadJson,intentType;long revision;
            lock(probeSync)
            {
                int required=AIPortProtocol.CapabilityDiplomacyLifecycleBundle;if(kind!="withdraw")required|=AIPortProtocol.CapabilityNativeDiplomacyJournal|(kind=="commit_peace"?AIPortProtocol.CapabilityNativePeaceAdapter:kind=="commit_war"?AIPortProtocol.CapabilityNativeWarAdapter:AIPortProtocol.CapabilityNativeWarAdapter|AIPortProtocol.CapabilityNativePeaceAdapter);
                if(disposed||!campaignReady||(intentCapabilityFlags&required)!=required||!string.IsNullOrWhiteSpace(diplomacyLifecycleRequestId)||string.IsNullOrWhiteSpace(activeConversationId)){QueueDisplayLocked(string.Empty,activeConversationId,"Команда дипломатического lifecycle сейчас недоступна.","diplomacy_lifecycle_unavailable");return;}
                requestId=Guid.NewGuid().ToString("N");generation=intentCampaignGeneration;revision=intentStateRevision;conversationId=activeConversationId;diplomacyLifecycleRequestId=requestId;diplomacyLifecycleConversationId=conversationId;diplomacyLifecycleRequestKind=kind;diplomacyLifecycleStatementId=statementId;
                if(kind=="withdraw"){intentType="diplomacy_source_withdraw";payloadJson="{\"statementId\":\""+statementId+"\",\"reason\":\"manual_diplomacy_withdraw\"}";}
                else if(kind=="preflight"){intentType="diplomacy_native_preflight";payloadJson="{\"statementId\":\""+statementId+"\",\"reason\":\"manual_native_diplomacy_preflight\"}";}
                else if(kind=="commit_peace"){intentType="diplomacy_native_peace_commit";payloadJson="{\"statementId\":\""+statementId+"\",\"commitToken\":\""+commitToken+"\",\"reason\":\"manual_native_peace_commit\"}";}
                else{intentType="diplomacy_native_war_commit";payloadJson="{\"statementId\":\""+statementId+"\",\"commitToken\":\""+commitToken+"\",\"reason\":\"manual_native_war_commit\"}";}
            }
            network.SendAll(new AIIntentProposalRequest(AIPortProtocol.Version,requestId,generation,revision,intentType,payloadJson));Logger.Information("AIPort diplomacy lifecycle command requested RequestId={RequestId} StatementId={StatementId} Kind={Kind} Revision={Revision}",requestId,statementId,kind,revision);
        }

        private static bool TryParseDiplomacyRecipientCommand(string text,out string statementId,out string decision)
        {
            statementId=string.Empty;decision=string.Empty;string value=(text??string.Empty).Trim();string prefix;
            if(value.StartsWith("/diplomacy-accept ",StringComparison.OrdinalIgnoreCase)){decision="accept";prefix="/diplomacy-accept ";}
            else if(value.StartsWith("/diplomacy-reject ",StringComparison.OrdinalIgnoreCase)){decision="reject";prefix="/diplomacy-reject ";}
            else return false;statementId=value.Substring(prefix.Length).Trim().ToLowerInvariant();if(statementId.Length!=32)return false;foreach(char c in statementId)if(!char.IsDigit(c)&&(c<'a'||c>'f'))return false;return true;
        }

        private void SubmitDiplomacyRecipientDecision(string statementId,string decision)
        {
            TrySubmitDiplomacyRecipientDecision(statementId,decision,false);
        }

        private bool TrySubmitDiplomacyRecipientDecisionFromUi(string statementId,string decision)
        {
            return TrySubmitDiplomacyRecipientDecision(statementId,decision,true);
        }

        private bool TrySubmitDiplomacyRecipientDecision(string statementId,string decision,bool fromMapNotification)
        {
            if(!IsHexId(statementId)||(decision!="accept"&&decision!="reject"))return false;
            string requestId,generation,conversationId,payloadJson;long revision;
            lock(probeSync)
            {
                int required=AIPortProtocol.CapabilityDiplomacyRecipientConsent|AIPortProtocol.CapabilityDiplomacyConflictGuard|AIPortProtocol.CapabilityDiplomacyInboxNotification;
                if(fromMapNotification)required|=AIPortProtocol.CapabilityDiplomacyDecisionUi;
                if(disposed||!campaignReady||(intentCapabilityFlags&required)!=required||!string.IsNullOrWhiteSpace(diplomacyRecipientDecisionRequestId)||(!fromMapNotification&&string.IsNullOrWhiteSpace(activeConversationId)))
                {
                    if(!fromMapNotification)QueueDisplayLocked(string.Empty,activeConversationId,"Diplomatic decision is unavailable right now.","diplomacy_recipient_decision_unavailable");
                    return false;
                }
                requestId=Guid.NewGuid().ToString("N");generation=intentCampaignGeneration;revision=intentStateRevision;conversationId=fromMapNotification?string.Empty:activeConversationId;diplomacyRecipientDecisionRequestId=requestId;diplomacyRecipientDecisionConversationId=conversationId;diplomacyRecipientDecisionKind=decision;diplomacyRecipientDecisionStatementId=statementId.ToLowerInvariant();payloadJson="{\"statementId\":\""+statementId.ToLowerInvariant()+"\",\"decision\":\""+decision+"\",\"reason\":\""+(fromMapNotification?"map_notification_decision":"manual_diplomacy_recipient_decision")+"\"}";
            }
            network.SendAll(new AIIntentProposalRequest(AIPortProtocol.Version,requestId,generation,revision,decision=="accept"?"diplomacy_recipient_accept":"diplomacy_recipient_reject",payloadJson));Logger.Information("AIPort diplomacy recipient decision requested RequestId={RequestId} StatementId={StatementId} Decision={Decision} Source={Source} Revision={Revision}",requestId,statementId,decision,fromMapNotification?"map_notification":"dialogue_command",revision);return true;
        }

        private void SubmitDiplomacyProposal(string action)
        {
            string requestId,generation,conversationId,leaseId,targetInstanceId,payloadJson;long revision;
            lock(probeSync)
            {
                if(disposed||!campaignReady||(intentCapabilityFlags&(AIPortProtocol.CapabilityDiplomacyStatements|AIPortProtocol.CapabilityDiplomacyAuthority))!=(AIPortProtocol.CapabilityDiplomacyStatements|AIPortProtocol.CapabilityDiplomacyAuthority)||!string.IsNullOrWhiteSpace(diplomacyProposalRequestId)||!string.IsNullOrWhiteSpace(diplomacyConfirmationRequestId)||string.IsNullOrWhiteSpace(activeConversationId)||string.IsNullOrWhiteSpace(activeTargetLeaseId)||string.IsNullOrWhiteSpace(activeTargetInstanceId)){QueueDisplayLocked(string.Empty,activeConversationId,"Дипломатическое предложение сейчас недоступно.","diplomacy_proposal_unavailable");return;}
                requestId=Guid.NewGuid().ToString("N");generation=intentCampaignGeneration;revision=intentStateRevision;conversationId=activeConversationId;leaseId=activeTargetLeaseId;targetInstanceId=activeTargetInstanceId;diplomacyProposalRequestId=requestId;diplomacyProposalConversationId=conversationId;diplomacyProposalIntentId=string.Empty;diplomacyProposalAction=action;payloadJson="{\"conversationId\":\""+conversationId+"\",\"targetLeaseId\":\""+leaseId+"\",\"targetInstanceId\":\""+targetInstanceId+"\",\"action\":\""+action+"\",\"reason\":\"manual_diplomacy_proposal\"}";
            }
            network.SendAll(new AIIntentProposalRequest(AIPortProtocol.Version,requestId,generation,revision,"diplomacy_statement_proposal",payloadJson));
            Logger.Information("AIPort diplomacy statement proposal requested RequestId={RequestId} Action={Action} ConversationId={ConversationId} Revision={Revision}",requestId,action,conversationId,revision);
        }

        private void SubmitDiplomacyConfirmation()
        {
            string requestId,generation,conversationId,leaseId,targetInstanceId,intentId,payloadJson;long revision;
            lock(probeSync)
            {
                if(disposed||!campaignReady||(intentCapabilityFlags&(AIPortProtocol.CapabilityDiplomacyStatements|AIPortProtocol.CapabilityDiplomacyAuthority))!=(AIPortProtocol.CapabilityDiplomacyStatements|AIPortProtocol.CapabilityDiplomacyAuthority)||string.IsNullOrWhiteSpace(diplomacyProposalIntentId)||!string.IsNullOrWhiteSpace(diplomacyConfirmationRequestId)||!string.Equals(activeConversationId,diplomacyProposalConversationId,StringComparison.Ordinal)){QueueDisplayLocked(string.Empty,activeConversationId,"Нет дипломатического предложения для подтверждения.","diplomacy_confirmation_unavailable");return;}
                requestId=Guid.NewGuid().ToString("N");generation=intentCampaignGeneration;revision=intentStateRevision;conversationId=activeConversationId;leaseId=activeTargetLeaseId;targetInstanceId=activeTargetInstanceId;intentId=diplomacyProposalIntentId;diplomacyConfirmationRequestId=requestId;payloadJson="{\"intentId\":\""+intentId+"\",\"conversationId\":\""+conversationId+"\",\"targetLeaseId\":\""+leaseId+"\",\"targetInstanceId\":\""+targetInstanceId+"\",\"reason\":\"manual_diplomacy_confirm\"}";
            }
            network.SendAll(new AIIntentProposalRequest(AIPortProtocol.Version,requestId,generation,revision,"diplomacy_statement_confirm",payloadJson));
            Logger.Information("AIPort diplomacy statement confirmation requested RequestId={RequestId} ProposalIntentId={ProposalIntentId} Revision={Revision}",requestId,intentId,revision);
        }


        private static string DiplomacyRejectionText(string prefix,string reason)
        {
            string code=reason??"unknown";
            if(string.Equals(code,"player_faction_authority_required",StringComparison.Ordinal))return "Дипломатическое полномочие игрока не подтверждено: требуется правитель королевства или лидер независимого клана.";
            if(string.Equals(code,"target_faction_authority_required",StringComparison.Ordinal))return "Собеседник не уполномочен на межфракционное заявление: поговорите с правителем или лидером независимой фракции.";
            if(string.Equals(code,"stale_diplomatic_authority",StringComparison.Ordinal))return "Дипломатические полномочия изменились после предложения; создайте новое предложение.";
            if(string.Equals(code,"recipient_not_authorized",StringComparison.Ordinal))return "Только указанный целевой лидер может принять или отклонить это предложение.";
            if(string.Equals(code,"negotiation_expired",StringComparison.Ordinal))return "Срок дипломатического предложения истёк.";
            if(string.Equals(code,"negotiation_already_resolved",StringComparison.Ordinal))return "Дипломатическое предложение уже обработано.";
            if(string.Equals(code,"diplomacy_pair_pending",StringComparison.Ordinal))return "Для этой пары фракций уже существует активное дипломатическое предложение.";
            if(string.Equals(code,"diplomacy_pending_limit",StringComparison.Ordinal))return "Достигнут лимит активных дипломатических предложений.";
            if(string.Equals(code,"stale_diplomatic_context",StringComparison.Ordinal))return "Дипломатический контекст изменился; предложение больше нельзя принять.";
            if(string.Equals(code,"source_not_authorized",StringComparison.Ordinal))return "Только инициатор может отозвать предложение или запустить native preflight.";
            if(string.Equals(code,"native_war_adapter_disabled",StringComparison.Ordinal))return "Native war adapter установлен, но выключен независимыми предохранителями.";
            if(string.Equals(code,"native_war_status_not_eligible",StringComparison.Ordinal))return "Для native war требуется принятое обеими сторонами shadow-предложение войны.";
            if(string.Equals(code,"native_war_commit_expired",StringComparison.Ordinal)||string.Equals(code,"native_war_commit_consumed",StringComparison.Ordinal))return "Native war commit-token истёк или уже использован; повторите /diplomacy-ready.";
            if(string.Equals(code,"native_war_commit_not_authorized",StringComparison.Ordinal)||string.Equals(code,"native_diplomacy_commit_not_authorized",StringComparison.Ordinal))return "Native diplomacy commit-token не принадлежит текущему peer или герою.";
            if(string.Equals(code,"native_peace_adapter_disabled",StringComparison.Ordinal))return "Native peace adapter установлен, но выключен независимыми предохранителями.";
            if(string.Equals(code,"native_peace_status_not_eligible",StringComparison.Ordinal))return "Для native peace требуется принятое обеими сторонами shadow-предложение мира.";
            if(string.Equals(code,"native_diplomacy_generation_not_pinned",StringComparison.Ordinal))return "Native adapter не привязан к текущей campaign generation.";
            if(string.Equals(code,"native_journal_pair_active",StringComparison.Ordinal)||string.Equals(code,"native_journal_pair_rate_limit",StringComparison.Ordinal))return "Commit journal заблокировал конфликтующую или слишком частую native-операцию.";
            return prefix+" отклонено: "+code+".";
        }

        private void SubmitValidationGate(string mode)
        {
            string requestId,generation,conversationId;long revision;
            lock(probeSync)
            {
                if(disposed||!campaignReady||(intentCapabilityFlags&AIPortProtocol.CapabilityValidationGate)==0||!string.IsNullOrWhiteSpace(validationGateRequestId)||string.IsNullOrWhiteSpace(activeConversationId)){QueueDisplayLocked(string.Empty,activeConversationId,"Runtime gate сейчас недоступен.","validation_gate_unavailable");return;}
                requestId=Guid.NewGuid().ToString("N");generation=intentCampaignGeneration;revision=intentStateRevision;conversationId=activeConversationId;validationGateRequestId=requestId;validationGateConversationId=conversationId;
            }
            network.SendAll(new AIPortValidationGateRequest(AIPortProtocol.Version,requestId,generation,revision,mode));
            Logger.Information("AIPort validation gate requested Mode={Mode} RequestId={RequestId} ConversationId={ConversationId} Generation={Generation} Revision={Revision}",mode,requestId,conversationId,generation,revision);
        }

        private void Handle(MessagePayload<AIPortValidationGateResponse> payload)
        {
            bool accepted;string conversationId;
            lock(probeSync)
            {
                if(string.IsNullOrWhiteSpace(validationGateRequestId)||!string.Equals(payload.What.RequestId,validationGateRequestId,StringComparison.Ordinal))return;conversationId=validationGateConversationId;validationGateRequestId=string.Empty;validationGateConversationId=string.Empty;accepted=payload.What.Accepted&&!string.IsNullOrWhiteSpace(payload.What.DisplayText)&&payload.What.StateRevision>=intentStateRevision;if(accepted)intentStateRevision=payload.What.StateRevision;QueueDisplayLocked(payload.What.RequestId,conversationId,accepted?payload.What.DisplayText:"Runtime gate отклонён: "+(payload.What.ReasonCode??"unknown")+".",accepted?"validation_gate_report":"validation_gate_rejected");
            }
            Logger.Information("AIPort validation gate result RequestId={RequestId} Accepted={Accepted} Reason={Reason} Revision={Revision} MutationApplied=false",payload.What.RequestId,accepted,payload.What.ReasonCode,payload.What.StateRevision);
        }

        private void SubmitDiplomacySnapshot()
        {
            string requestId,generation,conversationId;long revision;
            lock(probeSync)
            {
                if(disposed||!campaignReady||(intentCapabilityFlags&AIPortProtocol.CapabilityDiplomacySnapshot)==0||!string.IsNullOrWhiteSpace(diplomacySnapshotRequestId)||string.IsNullOrWhiteSpace(activeConversationId))
                {QueueDisplayLocked(string.Empty,activeConversationId,"Дипломатическая сводка сейчас недоступна.","diplomacy_snapshot_unavailable");return;}
                requestId=Guid.NewGuid().ToString("N");generation=intentCampaignGeneration;revision=intentStateRevision;conversationId=activeConversationId;diplomacySnapshotRequestId=requestId;diplomacySnapshotConversationId=conversationId;
            }
            network.SendAll(new AIDiplomacySnapshotRequest(AIPortProtocol.Version,requestId,generation,revision));
            Logger.Information("AIPort diplomacy snapshot requested RequestId={RequestId} ConversationId={ConversationId} CampaignGeneration={CampaignGeneration} Revision={Revision}",requestId,conversationId,generation,revision);
        }

        private void Handle(MessagePayload<AIDiplomacySnapshotResponse> payload)
        {
            bool accepted;string conversationId;
            lock(probeSync)
            {
                if(string.IsNullOrWhiteSpace(diplomacySnapshotRequestId)||!string.Equals(payload.What.RequestId,diplomacySnapshotRequestId,StringComparison.Ordinal))return;
                conversationId=diplomacySnapshotConversationId;diplomacySnapshotRequestId=string.Empty;diplomacySnapshotConversationId=string.Empty;accepted=payload.What.Accepted&&!string.IsNullOrWhiteSpace(payload.What.DisplayText)&&payload.What.StateRevision>=intentStateRevision;
                if(accepted)intentStateRevision=payload.What.StateRevision;
                QueueDisplayLocked(payload.What.RequestId,conversationId,accepted?payload.What.DisplayText:"Дипломатическая сводка отклонена: "+(payload.What.ReasonCode??"unknown")+".",accepted?"diplomacy_snapshot":"diplomacy_snapshot_rejected");
            }
            Logger.Information("AIPort diplomacy snapshot result RequestId={RequestId} Accepted={Accepted} Reason={Reason} Revision={Revision} MutationApplied=false",payload.What.RequestId,accepted,payload.What.ReasonCode,payload.What.StateRevision);
        }

        private void Handle(MessagePayload<AIPortCapabilitiesResponse> payload)
        {
            lock (probeSync)
            {
                intentCapabilityFlags = payload.What.Accepted ? payload.What.ServerCapabilityFlags : 0;
                intentCampaignGeneration = payload.What.CampaignGeneration ?? string.Empty;
                intentStateRevision = payload.What.StateRevision;
            }
        }

        private void Handle(MessagePayload<AIIntentProposalResult> payload)
        {
            string conversationId; bool validated;
            lock (probeSync)
            {
                if(!string.IsNullOrWhiteSpace(diplomacyLifecycleRequestId)&&string.Equals(payload.What.RequestId,diplomacyLifecycleRequestId,StringComparison.Ordinal))
                {
                    conversationId=diplomacyLifecycleConversationId;string kind=diplomacyLifecycleRequestKind,statementId=diplomacyLifecycleStatementId;diplomacyLifecycleRequestId=string.Empty;diplomacyLifecycleConversationId=string.Empty;diplomacyLifecycleRequestKind=string.Empty;diplomacyLifecycleStatementId=string.Empty;bool withdrawal=kind=="withdraw"&&string.Equals(payload.What.Status,"source_withdrawn_shadow",StringComparison.Ordinal)&&string.Equals(payload.What.ReasonCode,"mutation_suppressed",StringComparison.Ordinal);bool dryWar=kind=="preflight"&&string.Equals(payload.What.Status,"native_war_dry_run_ready",StringComparison.Ordinal);bool dryPeace=kind=="preflight"&&string.Equals(payload.What.Status,"native_peace_dry_run_ready",StringComparison.Ordinal);bool armedWar=kind=="preflight"&&string.Equals(payload.What.Status,"native_war_commit_ready",StringComparison.Ordinal)&&string.Equals(payload.What.ReasonCode,"explicit_native_diplomacy_commit_required",StringComparison.Ordinal)&&IsHexId(payload.What.IntentId);bool armedPeace=kind=="preflight"&&string.Equals(payload.What.Status,"native_peace_commit_ready",StringComparison.Ordinal)&&string.Equals(payload.What.ReasonCode,"explicit_native_diplomacy_commit_required",StringComparison.Ordinal)&&IsHexId(payload.What.IntentId);bool committedWar=kind=="commit_war"&&string.Equals(payload.What.Status,"native_war_committed",StringComparison.Ordinal)&&string.Equals(payload.What.ReasonCode,"native_war_applied",StringComparison.Ordinal);bool committedPeace=kind=="commit_peace"&&string.Equals(payload.What.Status,"native_peace_committed",StringComparison.Ordinal)&&string.Equals(payload.What.ReasonCode,"native_peace_applied",StringComparison.Ordinal);validated=(withdrawal||dryWar||dryPeace||armedWar||armedPeace||committedWar||committedPeace)&&payload.What.StateRevision>=intentStateRevision;if(validated)intentStateRevision=payload.What.StateRevision;string message;if(withdrawal)message="Дипломатическое предложение отозвано; pair lock освобождён.";else if(dryWar||dryPeace)message="Native "+(dryWar?"war":"peace")+" dry-run прошёл, но commit остаётся выключен: "+payload.What.ReasonCode+".";else if(armedWar)message="Native war preflight прошёл. Для явного commit введите /diplomacy-native-war "+statementId+" "+payload.What.IntentId;else if(armedPeace)message="Native peace preflight прошёл. Для явного commit введите /diplomacy-native-peace "+statementId+" "+payload.What.IntentId;else if(committedWar)message="Нативное объявление войны применено и подтверждено journal/postcondition-проверкой.";else if(committedPeace)message="Нативное заключение мира применено и подтверждено journal/postcondition-проверкой.";else message=DiplomacyRejectionText("Команда дипломатического lifecycle",payload.What.ReasonCode);QueueDisplayLocked(payload.What.RequestId,conversationId,message,validated?"diplomacy_lifecycle_validated":"diplomacy_lifecycle_rejected");Logger.Information("AIPort diplomacy lifecycle command result RequestId={RequestId} Kind={Kind} Status={Status} Reason={Reason} Revision={Revision} Validated={Validated}",payload.What.RequestId,kind,payload.What.Status,payload.What.ReasonCode,payload.What.StateRevision,validated);return;
                }
                if(!string.IsNullOrWhiteSpace(diplomacyRecipientDecisionRequestId)&&string.Equals(payload.What.RequestId,diplomacyRecipientDecisionRequestId,StringComparison.Ordinal))
                {
                    conversationId=diplomacyRecipientDecisionConversationId;string statementId=diplomacyRecipientDecisionStatementId;diplomacyRecipientDecisionRequestId=string.Empty;diplomacyRecipientDecisionConversationId=string.Empty;diplomacyRecipientDecisionStatementId=string.Empty;validated=(string.Equals(payload.What.Status,"recipient_accepted_shadow",StringComparison.Ordinal)||string.Equals(payload.What.Status,"recipient_rejected_shadow",StringComparison.Ordinal))&&string.Equals(payload.What.ReasonCode,"mutation_suppressed",StringComparison.Ordinal)&&payload.What.StateRevision>=intentStateRevision;if(payload.What.StateRevision>=intentStateRevision)intentStateRevision=payload.What.StateRevision;string message=validated?(diplomacyRecipientDecisionKind=="accept"?"Diplomatic proposal accepted in shadow state. Native war or peace did not change.":"Diplomatic proposal rejected in shadow state. Native war or peace did not change."):DiplomacyRejectionText("Diplomatic decision",payload.What.ReasonCode);if(string.IsNullOrWhiteSpace(conversationId)){pendingInboxMapNotification=null;if(validated)pendingMapDismissStatementId=statementId;else pendingMapReleaseStatementId=statementId;pendingInboxNotificationKey="decision:"+payload.What.RequestId;pendingInboxNotificationText=message;if((intentCapabilityFlags&AIPortProtocol.CapabilityDiplomacyInboxList)!=0)diplomacyInboxRefreshRequested=true;}else QueueDisplayLocked(payload.What.RequestId,conversationId,message,validated?"diplomacy_recipient_decision_recorded":"diplomacy_recipient_decision_rejected");Logger.Information("AIPort diplomacy recipient decision result RequestId={RequestId} StatementId={StatementId} Status={Status} Reason={Reason} Revision={Revision} Validated={Validated} MutationApplied=false",payload.What.RequestId,statementId,payload.What.Status,payload.What.ReasonCode,payload.What.StateRevision,validated);diplomacyRecipientDecisionKind=string.Empty;return;
                }
                if(!string.IsNullOrWhiteSpace(diplomacyProposalRequestId)&&string.Equals(payload.What.RequestId,diplomacyProposalRequestId,StringComparison.Ordinal))
                {
                    conversationId=diplomacyProposalConversationId;diplomacyProposalRequestId=string.Empty;validated=string.Equals(payload.What.Status,"confirmation_required",StringComparison.Ordinal)&&string.Equals(payload.What.ReasonCode,"player_confirmation_required",StringComparison.Ordinal)&&payload.What.StateRevision==intentStateRevision;diplomacyProposalIntentId=validated?(payload.What.IntentId??string.Empty):string.Empty;QueueDisplayLocked(payload.What.RequestId,conversationId,validated?"Дипломатическое shadow-предложение подготовлено. Введите /diplomacy-confirm.":DiplomacyRejectionText("Дипломатическое предложение",payload.What.ReasonCode),validated?"diplomacy_confirmation_required":"diplomacy_proposal_rejected");Logger.Information("AIPort diplomacy proposal result RequestId={RequestId} IntentId={IntentId} Action={Action} Status={Status} Reason={Reason} Revision={Revision}",payload.What.RequestId,payload.What.IntentId,diplomacyProposalAction,payload.What.Status,payload.What.ReasonCode,payload.What.StateRevision);return;
                }
                if(!string.IsNullOrWhiteSpace(diplomacyConfirmationRequestId)&&string.Equals(payload.What.RequestId,diplomacyConfirmationRequestId,StringComparison.Ordinal))
                {
                    conversationId=diplomacyProposalConversationId;
                    diplomacyConfirmationRequestId=string.Empty;
                    bool pendingRecipient=string.Equals(payload.What.Status,"pending_recipient",StringComparison.Ordinal)&&string.Equals(payload.What.ReasonCode,"recipient_consent_required",StringComparison.Ordinal);
                    bool npcAccepted=string.Equals(payload.What.Status,"npc_recipient_accepted_shadow",StringComparison.Ordinal)&&string.Equals(payload.What.ReasonCode,"npc_policy_war_acknowledged",StringComparison.Ordinal)||string.Equals(payload.What.Status,"npc_recipient_accepted_shadow",StringComparison.Ordinal)&&string.Equals(payload.What.ReasonCode,"npc_policy_peace_accepted",StringComparison.Ordinal);
                    bool npcRejected=string.Equals(payload.What.Status,"npc_recipient_rejected_shadow",StringComparison.Ordinal)&&string.Equals(payload.What.ReasonCode,"npc_policy_peace_rejected",StringComparison.Ordinal);
                    validated=(pendingRecipient||npcAccepted||npcRejected)&&payload.What.StateRevision>=intentStateRevision;
                    if(validated)intentStateRevision=payload.What.StateRevision;
                    diplomacyProposalIntentId=string.Empty;
                    string diplomacyRejected=string.Equals(payload.What.ReasonCode,"diplomacy_cooldown",StringComparison.Ordinal)?"Diplomacy cooldown is active; wait 30 seconds.":DiplomacyRejectionText("Diplomacy confirmation",payload.What.ReasonCode);
                    string diplomacyMessage=pendingRecipient?"Diplomatic proposal recorded and awaiting the target player ruler's decision.":npcAccepted?"The NPC ruler's deterministic policy accepted the shadow diplomatic outcome. Native state has not changed.":npcRejected?"The NPC ruler's deterministic policy rejected the diplomatic proposal. Native state has not changed.":diplomacyRejected;
                    QueueDisplayLocked(payload.What.RequestId,conversationId,diplomacyMessage,validated?"diplomacy_confirmation_validated":"diplomacy_confirmation_rejected");
                    Logger.Information("AIPort DiplomacyConfirmationValidated={Validated} RequestId={RequestId} Action={Action} Status={Status} Reason={Reason} Revision={Revision} MutationApplied=false",validated,payload.What.RequestId,diplomacyProposalAction,payload.What.Status,payload.What.ReasonCode,payload.What.StateRevision);
                    diplomacyProposalAction=string.Empty;return;
                }
                if (!string.IsNullOrWhiteSpace(relationProposalRequestId) && string.Equals(payload.What.RequestId, relationProposalRequestId, StringComparison.Ordinal))
                {
                    conversationId=relationProposalConversationId; relationProposalRequestId=string.Empty;
                    validated=string.Equals(payload.What.Status,"confirmation_required",StringComparison.Ordinal) && string.Equals(payload.What.ReasonCode,"player_confirmation_required",StringComparison.Ordinal) && payload.What.StateRevision==intentStateRevision;
                    relationProposalIntentId=validated?(payload.What.IntentId??string.Empty):string.Empty;
                    QueueDisplayLocked(payload.What.RequestId,conversationId,validated?"Предложение подготовлено. Введите /relation-confirm для безопасного shadow-подтверждения.":"Предложение отклонено: "+(payload.What.ReasonCode??"unknown")+".",validated?"relation_confirmation_required":"relation_proposal_rejected");
                    Logger.Information("AIPort relation proposal result RequestId={RequestId} IntentId={IntentId} Status={Status} Reason={Reason} Revision={Revision}",payload.What.RequestId,payload.What.IntentId,payload.What.Status,payload.What.ReasonCode,payload.What.StateRevision); return;
                }
                if (!string.IsNullOrWhiteSpace(relationConfirmationRequestId) && string.Equals(payload.What.RequestId, relationConfirmationRequestId, StringComparison.Ordinal))
                {
                    conversationId=relationProposalConversationId; relationConfirmationRequestId=string.Empty;
                    validated=string.Equals(payload.What.Status,"confirmed_shadow",StringComparison.Ordinal) && string.Equals(payload.What.ReasonCode,"mutation_suppressed",StringComparison.Ordinal) && payload.What.StateRevision>=intentStateRevision;
                    if(validated)intentStateRevision=payload.What.StateRevision;
                    relationProposalIntentId=string.Empty;
                    string relationRejected=string.Equals(payload.What.ReasonCode,"social_cooldown",StringComparison.Ordinal)?"Cooldown отношения ещё активен: подождите 15 секунд.":"Подтверждение отклонено: "+(payload.What.ReasonCode??"unknown")+".";
                    QueueDisplayLocked(payload.What.RequestId,conversationId,validated?"Отношение записано в shadow-режиме. Нативное отношение не изменено.":relationRejected,validated?"relation_confirmation_validated":"relation_confirmation_rejected");
                    Logger.Information("AIPort RelationConfirmationValidated={Validated} RequestId={RequestId} Status={Status} Reason={Reason} Revision={Revision} MutationApplied=false",validated,payload.What.RequestId,payload.What.Status,payload.What.ReasonCode,payload.What.StateRevision); return;
                }
                if (string.IsNullOrWhiteSpace(relationShadowRequestId) || !string.Equals(payload.What.RequestId, relationShadowRequestId, StringComparison.Ordinal)) return;
                conversationId=relationShadowConversationId; relationShadowRequestId=string.Empty; relationShadowConversationId=string.Empty;
                validated=string.Equals(payload.What.Status,"shadow_validated",StringComparison.Ordinal) && string.Equals(payload.What.ReasonCode,"mutation_suppressed",StringComparison.Ordinal) && payload.What.StateRevision==intentStateRevision;
                QueueDisplayLocked(payload.What.RequestId,conversationId,validated?"Shadow-проверка принята. Изменение отношений не применялось.":"Shadow-проверка отклонена: "+(payload.What.ReasonCode??"unknown")+".",validated?"relation_shadow_validated":"relation_shadow_rejected");
            }
            Logger.Information("AIPort relation shadow intent result RequestId={RequestId} IntentId={IntentId} Status={Status} Reason={Reason} Revision={Revision} MutationExpected=false",payload.What.RequestId,payload.What.IntentId,payload.What.Status,payload.What.ReasonCode,payload.What.StateRevision);
            if(validated)Logger.Information("AIPort RelationShadowValidated IntentId={IntentId} Revision={Revision} MutationApplied=false",payload.What.IntentId,payload.What.StateRevision);
        }

        private void HandleReturnToVanilla()
        {
            string requestId;
            string conversationId;
            bool hadQueuedDisplay;
            lock (probeSync)
            {
                requestId = pendingConversationRequestId;
                conversationId = string.IsNullOrWhiteSpace(pendingConversationId) ? activeConversationId : pendingConversationId;
                hadQueuedDisplay = !string.IsNullOrWhiteSpace(queuedDisplayText);
                deferredPlayerText = string.Empty;
                CancelConversationRetryTimerLocked("return_to_vanilla");
                ClearPendingConversationRequestLocked();
                ClearQueuedDisplayLocked();
            }
            TrySendConversationCancel(requestId, conversationId, "return_to_vanilla");
            Logger.Information("AIPort AI dialogue branch closed for vanilla conversation ConversationId={ConversationId} PendingRequestCanceled={PendingRequestCanceled} QueuedDisplayDropped={QueuedDisplayDropped}", conversationId, !string.IsNullOrWhiteSpace(requestId), hadQueuedDisplay);
        }

        private void RequestSharedCampaignPause()
        {
            string conversationId;
            lock (probeSync)
            {
                if (disposed || !campaignReady || string.IsNullOrWhiteSpace(activeConversationId)) return;
                conversationId = activeConversationId;
            }
            try
            {
                network.SendAll(new NetworkRequestTimeSpeedChange(TimeControlEnum.Pause));
                Logger.Information("AIPort requested shared campaign pause for AI dialogue ConversationId={ConversationId}", conversationId);
            }
            catch (Exception ex)
            {
                Logger.Warning(ex, "AIPort failed to request shared campaign pause for AI dialogue ConversationId={ConversationId}", conversationId);
            }
        }

        private bool CanSubmitFreeForm()
        {
            lock (probeSync)
            {
                return !disposed
                    && campaignReady
                    && !string.IsNullOrWhiteSpace(activeNpcHeroId)
                    && !string.IsNullOrWhiteSpace(activeConversationId)
                    && string.IsNullOrWhiteSpace(pendingConversationRequestId)
                    && string.IsNullOrWhiteSpace(retryConversationId)
                    && conversationRetryTimer == null;
            }
        }

        private void SubmitPlayerText(string playerText, string source)
        {
            playerText = playerText == null ? string.Empty : playerText.Trim();
            if (string.IsNullOrWhiteSpace(playerText)) return;
            if (playerText.Length > AIPortProtocol.MaximumPlayerTextLength) playerText = playerText.Substring(0, AIPortProtocol.MaximumPlayerTextLength);

            string npcHeroId = ConversationTargetResolver.CurrentNpcTargetId();
            string conversationId;
            int turn;
            lock (probeSync)
            {
                if (disposed || !campaignReady || string.IsNullOrWhiteSpace(npcHeroId)) return;
                if (!string.Equals(activeNpcHeroId, npcHeroId, StringComparison.Ordinal) || string.IsNullOrWhiteSpace(activeConversationId))
                {
                    activeNpcHeroId = npcHeroId;
                    activeConversationId = Guid.NewGuid().ToString("N");
                    activeClientTargetNonce = Guid.NewGuid().ToString("N");
                    activeTargetLeaseId = string.Empty;
                    activeTargetInstanceId = string.Empty;
                    deferredPlayerText = playerText;
                    ResetTargetBindingRetryLocked();
                    activeConversationProbeTurns = 0;
                    CancelConversationRetryTimerLocked("conversation_replaced");
                    ClearPendingConversationRequestLocked();
                    ClearQueuedDisplayLocked();
                }
                if (string.IsNullOrWhiteSpace(activeTargetLeaseId) || string.IsNullOrWhiteSpace(activeTargetInstanceId))
                {
                    deferredPlayerText = playerText;
                    Logger.Information("AIPort player dialogue deferred until server target binding ConversationId={ConversationId} NpcTargetId={NpcTargetId} Source={Source}", activeConversationId, activeNpcHeroId, source);
                    return;
                }
                if (!string.IsNullOrWhiteSpace(pendingConversationRequestId)
                    || !string.IsNullOrWhiteSpace(retryConversationId)
                    || conversationRetryTimer != null)
                {
                    Logger.Warning("AIPort player dialogue ignored while an authoritative request or retry is pending PendingRequestId={RequestId} ConversationId={ConversationId} Source={Source}", pendingConversationRequestId, activeConversationId, source);
                    return;
                }
                conversationId = activeConversationId;
                turn = ++activeConversationProbeTurns;
            }
            Logger.Information("AIPort captured player dialogue ConversationId={ConversationId} NpcHeroId={NpcHeroId} Turn={Turn} Chars={Chars} Source={Source}", conversationId, npcHeroId, turn, playerText.Length, source);
            SendConversationProbe(npcHeroId, conversationId, playerText, turn);
        }

        private void HandleApplicationTick(float deltaTime)
        {
            if (disposed) return;
            TryDisplayInboxNotification();
            TryStartQueuedDiplomacyInboxRefresh();
            if (!campaignReady) return;
            TryRetryTargetBindingOnApplicationTick();
            TryApplyQueuedConversationResult();
        }

        private void TryRetryTargetBindingOnApplicationTick()
        {
            string targetId = string.Empty;
            string conversationId = string.Empty;
            string targetNonce = string.Empty;
            DateTime now = DateTime.UtcNow;
            lock (probeSync)
            {
                if (disposed || !campaignReady
                    || !targetBindingRetryPending
                    || !string.IsNullOrWhiteSpace(activeTargetLeaseId)
                    || targetBindingRetryAttempt >= MaximumTargetBindingRetries
                    || now < nextTargetBindingRetryUtc)
                {
                    return;
                }
                targetBindingRetryPending = false;
                targetId = activeNpcHeroId;
                conversationId = activeConversationId;
                targetNonce = activeClientTargetNonce;
            }
            if (!string.IsNullOrWhiteSpace(targetId) && !string.IsNullOrWhiteSpace(conversationId))
            {
                TryOpenTargetBinding(targetId, conversationId, targetNonce, "player_resolution_application_retry");
            }
        }

        private void HandleCampaignTick(float deltaTime)
        {
            if (!campaignReady) return;
            DateTime now = DateTime.UtcNow;
            lock (probeSync)
            {
                if (now < nextConversationScanUtc) return;
                nextConversationScanUtc = now.AddMilliseconds(ConversationScanIntervalMilliseconds);
            }
            string targetId = ConversationTargetResolver.CurrentNpcTargetId();
            string openConversationId = string.Empty;
            string openNonce = string.Empty;
            string closeConversationId = string.Empty;
            string closeLeaseId = string.Empty;
            lock (probeSync)
            {
                if (disposed || !campaignReady) return;
                if (string.IsNullOrWhiteSpace(targetId))
                {
                    closeConversationId = activeConversationId;
                    closeLeaseId = activeTargetLeaseId;
                    activeNpcHeroId = string.Empty;
                    activeConversationId = string.Empty;
                    activeClientTargetNonce = string.Empty;
                    activeTargetLeaseId = string.Empty;
                    activeTargetInstanceId = string.Empty;
                    deferredPlayerText = string.Empty;
                    ResetTargetBindingRetryLocked();
                    activeConversationProbeTurns = 0;
                    CancelConversationRetryTimerLocked("target_lost");
                    ClearPendingConversationRequestLocked();
                    ClearQueuedDisplayLocked();
                }
                else if (!string.Equals(activeNpcHeroId, targetId, StringComparison.Ordinal))
                {
                    closeConversationId = activeConversationId;
                    closeLeaseId = activeTargetLeaseId;
                    activeNpcHeroId = targetId;
                    activeConversationId = Guid.NewGuid().ToString("N");
                    activeClientTargetNonce = Guid.NewGuid().ToString("N");
                    activeTargetLeaseId = string.Empty;
                    activeTargetInstanceId = string.Empty;
                    deferredPlayerText = string.Empty;
                    ResetTargetBindingRetryLocked();
                    activeConversationProbeTurns = 0;
                    CancelConversationRetryTimerLocked("conversation_replaced");
                    ClearPendingConversationRequestLocked();
                    ClearQueuedDisplayLocked();
                    openConversationId = activeConversationId;
                    openNonce = activeClientTargetNonce;
                }
            }
            TryCloseTargetBinding(closeConversationId, closeLeaseId, "target_scan_end_or_replace");
            if (!string.IsNullOrWhiteSpace(openConversationId)) TryOpenTargetBinding(targetId, openConversationId, openNonce, "target_scan");
        }

        private void HandleConversationEnded(IEnumerable<CharacterObject> characters)
        {
            string requestId;
            string conversationId;
            string leaseConversationId;
            string leaseId;
            lock (probeSync)
            {
                requestId = pendingConversationRequestId;
                conversationId = pendingConversationId;
                leaseConversationId = activeConversationId;
                leaseId = activeTargetLeaseId;
                activeNpcHeroId = string.Empty;
                activeConversationId = string.Empty;
                activeClientTargetNonce = string.Empty;
                activeTargetLeaseId = string.Empty;
                activeTargetInstanceId = string.Empty;
                deferredPlayerText = string.Empty;
                ResetTargetBindingRetryLocked();
                activeConversationProbeTurns = 0;
                CancelConversationRetryTimerLocked("conversation_ended");
                ClearPendingConversationRequestLocked();
                ClearQueuedDisplayLocked();
            }
            TrySendConversationCancel(requestId, conversationId, "conversation_ended");
            TryCloseTargetBinding(leaseConversationId, leaseId, "conversation_ended");
            Logger.Information("AIPort client conversation ended event received");
        }

        private void TryApplyQueuedConversationResult()
        {
            string requestId;
            string conversationId;
            string displayText;
            string displayKind;
            lock (probeSync)
            {
                if (string.IsNullOrWhiteSpace(queuedDisplayText)) return;
                if (string.IsNullOrWhiteSpace(activeConversationId) || !string.Equals(queuedDisplayConversationId, activeConversationId, StringComparison.Ordinal))
                {
                    ClearQueuedDisplayLocked();
                    return;
                }
                requestId = queuedDisplayRequestId;
                conversationId = queuedDisplayConversationId;
                displayText = queuedDisplayText;
                displayKind = queuedDisplayKind;
                ClearQueuedDisplayLocked();
            }

            try
            {
                ConversationManager manager = Campaign.Current == null ? null : Campaign.Current.ConversationManager;
                if (manager == null || !manager.IsConversationInProgress)
                {
                    Logger.Information("AIPort authoritative NPC response dropped because conversation is no longer active RequestId={RequestId} ConversationId={ConversationId}", requestId, conversationId);
                    return;
                }
                object dataSource = GetConversationDataSource(manager.Handler);
                bool updatedDialogText = false;
                if (dataSource != null)
                {
                    PropertyInfo dialogText = dataSource.GetType().GetProperty("DialogText", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    if (dialogText != null && dialogText.CanWrite)
                    {
                        dialogText.SetValue(dataSource, displayText, null);
                        updatedDialogText = true;
                    }
                }
                Logger.Information("AIPort dialogue display-only override applied RequestId={RequestId} ConversationId={ConversationId} Kind={Kind} DialogTextUpdated={DialogTextUpdated} Chars={Chars}", requestId, conversationId, displayKind, updatedDialogText, displayText.Length);
            }
            catch (Exception ex)
            {
                Logger.Warning(ex, "AIPort failed to apply authoritative NPC response to dialogue UI RequestId={RequestId} ConversationId={ConversationId}", requestId, conversationId);
            }
        }

        private static object GetConversationDataSource(object handler)
        {
            if (handler == null) return null;
            FieldInfo dataSourceField = FindInstanceField(handler.GetType(), "_dataSource");
            object dataSource = dataSourceField == null ? null : dataSourceField.GetValue(handler);
            if (dataSource == null) return null;
            PropertyInfo dialogController = dataSource.GetType().GetProperty("DialogController", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (dialogController != null)
            {
                object controller = dialogController.GetValue(dataSource, null);
                if (controller != null) dataSource = controller;
            }
            return dataSource;
        }

        private static FieldInfo FindInstanceField(Type type, string name)
        {
            while (type != null)
            {
                FieldInfo field = type.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
                if (field != null) return field;
                type = type.BaseType;
            }
            return null;
        }

        private void QueueDisplayLocked(string requestId, string conversationId, string displayText, string kind)
        {
            if (string.IsNullOrWhiteSpace(conversationId)
                || !string.Equals(conversationId, activeConversationId, StringComparison.Ordinal)
                || string.IsNullOrWhiteSpace(displayText)) return;
            queuedDisplayRequestId = requestId ?? string.Empty;
            queuedDisplayConversationId = conversationId;
            queuedDisplayText = displayText;
            queuedDisplayKind = kind ?? string.Empty;
        }

        private static string ResolveConversationErrorDisplayText(string errorCode)
        {
            if (string.Equals(errorCode, "backend_timeout", StringComparison.OrdinalIgnoreCase))
                return "Ответ не успел прийти вовремя. Попробуйте ещё раз.";
            if (string.Equals(errorCode, "backend_failed", StringComparison.OrdinalIgnoreCase))
                return "Персонаж сейчас не может ответить. Попробуйте ещё раз позже.";
            if (string.Equals(errorCode, "rate_limited", StringComparison.OrdinalIgnoreCase))
                return "Слишком много запросов. Подождите немного и попробуйте снова.";
            if (string.Equals(errorCode, "player_unresolved", StringComparison.OrdinalIgnoreCase))
                return "Сервер не смог определить персонажа игрока. Попробуйте ещё раз.";
            return "Не удалось получить ответ. Попробуйте ещё раз.";
        }

        private void ClearQueuedDisplayLocked()
        {
            queuedDisplayRequestId = string.Empty;
            queuedDisplayConversationId = string.Empty;
            queuedDisplayText = string.Empty;
            queuedDisplayKind = string.Empty;
        }

        private void TryRetryConversationProbe()
        {
            string npcHeroId;
            string conversationId;
            string playerText;
            int turn;
            int retryAttempt;
            lock (probeSync)
            {
                ThreadingTimer timer = conversationRetryTimer;
                conversationRetryTimer = null;
                if (timer != null)
                {
                    timer.Dispose();
                }
                if (disposed
                    || !campaignReady
                    || string.IsNullOrWhiteSpace(activeConversationId)
                    || !string.Equals(retryConversationId, activeConversationId, StringComparison.Ordinal))
                {
                    ClearConversationRetryPayloadLocked();
                    return;
                }

                npcHeroId = retryConversationNpcHeroId;
                conversationId = retryConversationId;
                playerText = retryConversationText;
                turn = retryConversationTurn;
                retryAttempt = retryConversationAttempt;
                ClearConversationRetryPayloadLocked();
            }

            SendConversationProbe(npcHeroId, conversationId, playerText, turn, retryAttempt);
        }

        private static int ResolveRateLimitRetryDelayMilliseconds(int serverHintMilliseconds, int completedRetryAttempts)
        {
            long delay = serverHintMilliseconds > 0
                ? (long)serverHintMilliseconds + 250L
                : (long)DefaultConversationRateLimitRetryDelayMilliseconds << Math.Min(completedRetryAttempts, 4);
            return (int)Math.Max(MinimumConversationRateLimitRetryDelayMilliseconds, Math.Min(MaximumConversationRateLimitRetryDelayMilliseconds, delay));
        }

        private void SendConversationProbe(string npcHeroId, string conversationId, string playerText, int turn)
        {
            SendConversationProbe(npcHeroId, conversationId, playerText, turn, 0);
        }

        private void SendConversationProbe(string npcHeroId, string conversationId, string playerText, int turn, int retryAttempt)
        {
            string requestId;
            string targetLeaseId;
            string targetInstanceId;
            long sequence;
            lock (probeSync)
            {
                if (disposed
                    || !campaignReady
                    || string.IsNullOrWhiteSpace(activeConversationId)
                    || !string.Equals(conversationId, activeConversationId, StringComparison.Ordinal))
                {
                    return;
                }

                requestId = Guid.NewGuid().ToString("N");
                targetLeaseId = activeTargetLeaseId;
                targetInstanceId = activeTargetInstanceId;
                if (string.IsNullOrWhiteSpace(targetLeaseId) || string.IsNullOrWhiteSpace(targetInstanceId)) return;
                sequence = Interlocked.Increment(ref clientSequence);
                pendingConversationRequestId = requestId;
                pendingConversationId = conversationId;
                pendingConversationNpcHeroId = npcHeroId;
                pendingConversationText = playerText;
                pendingConversationTurn = turn;
                pendingConversationRetryAttempt = retryAttempt;
                CancelConversationRequestTimeoutLocked("new_request");
                timeoutConversationRequestId = requestId;
                timeoutConversationId = conversationId;
                conversationRequestTimeoutTimer = new ThreadingTimer(_ => HandleConversationRequestTimeout(), null, ConversationRequestTimeoutMilliseconds, Timeout.Infinite);
            }

            Logger.Information("AIPort sending selected player dialogue RequestId={RequestId} ConversationId={ConversationId} NpcHeroId={NpcHeroId} Turn={Turn} RetryAttempt={RetryAttempt}", requestId, conversationId, npcHeroId, turn, retryAttempt);
            network.SendAll(new AIConversationRequest(AIPortProtocol.Version, requestId, conversationId, string.Empty, npcHeroId, playerText, sequence, targetLeaseId, targetInstanceId));
        }

        private string GetOrCreateRegularAgentNonce(IAgent agent)
        {
            lock (probeSync)
            {
                string nonce;
                if (!regularAgentNonces.TryGetValue(agent, out nonce))
                {
                    nonce = Guid.NewGuid().ToString("N");
                    regularAgentNonces[agent] = nonce;
                }
                return nonce;
            }
        }

        private void ResetTargetBindingRetryLocked()
        {
            targetBindingRetryPending = false;
            targetBindingRetryAttempt = 0;
            nextTargetBindingRetryUtc = DateTime.MinValue;
        }

        private void TryOpenTargetBinding(string targetId, string conversationId, string clientTargetNonce, string source)
        {
            if (string.IsNullOrWhiteSpace(targetId) || string.IsNullOrWhiteSpace(conversationId)) return;
            try
            {
                network.SendAll(new AIConversationTargetOpen(AIPortProtocol.Version, conversationId, targetId, clientTargetNonce ?? string.Empty));
                lock (probeSync)
                {
                    if (string.Equals(conversationId, activeConversationId, StringComparison.Ordinal))
                    {
                        targetBindingRetryPending = false;
                        targetBindingRetryAttempt++;
                    }
                }
                Logger.Information("AIPort target bind requested ConversationId={ConversationId} TargetId={TargetId} ClientTargetNonce={ClientTargetNonce} Source={Source} Attempt={Attempt}", conversationId, targetId, clientTargetNonce, source, targetBindingRetryAttempt);
            }
            catch (Exception ex) { Logger.Warning(ex, "AIPort failed to request target bind ConversationId={ConversationId}", conversationId); }
        }

        private void TryCloseTargetBinding(string conversationId, string targetLeaseId, string reason)
        {
            if (string.IsNullOrWhiteSpace(conversationId) || string.IsNullOrWhiteSpace(targetLeaseId)) return;
            try
            {
                network.SendAll(new AIConversationTargetClose(conversationId, targetLeaseId));
                Logger.Information("AIPort target close sent ConversationId={ConversationId} TargetLeaseId={TargetLeaseId} Reason={Reason}", conversationId, targetLeaseId, reason);
            }
            catch (Exception ex) { Logger.Warning(ex, "AIPort failed to close target lease ConversationId={ConversationId}", conversationId); }
        }

        private void HandleConversationRequestTimeout()
        {
            string requestId;
            string conversationId;
            lock (probeSync)
            {
                ThreadingTimer timer = conversationRequestTimeoutTimer;
                conversationRequestTimeoutTimer = null;
                if (timer != null) timer.Dispose();
                requestId = timeoutConversationRequestId;
                conversationId = timeoutConversationId;
                timeoutConversationRequestId = string.Empty;
                timeoutConversationId = string.Empty;
                if (disposed || !campaignReady
                    || string.IsNullOrWhiteSpace(requestId)
                    || !string.Equals(requestId, pendingConversationRequestId, StringComparison.Ordinal)
                    || !string.Equals(conversationId, activeConversationId, StringComparison.Ordinal)) return;
                ClearPendingConversationRequestLocked();
                QueueDisplayLocked(requestId, conversationId, "Ответ не пришёл вовремя. Можно попробовать ещё раз.", "client_timeout");
            }
            TrySendConversationCancel(requestId, conversationId, "client_timeout");
            Logger.Warning("AIPort conversation request timed out RequestId={RequestId} ConversationId={ConversationId} TimeoutMs={TimeoutMs}", requestId, conversationId, ConversationRequestTimeoutMilliseconds);
        }

        private void TrySendConversationCancel(string requestId, string conversationId, string reason)
        {
            if (string.IsNullOrWhiteSpace(requestId) || string.IsNullOrWhiteSpace(conversationId)) return;
            try
            {
                network.SendAll(new AIConversationCancel(requestId, conversationId));
                Logger.Information("AIPort conversation cancel sent RequestId={RequestId} ConversationId={ConversationId} Reason={Reason}", requestId, conversationId, reason);
            }
            catch (Exception ex)
            {
                Logger.Warning(ex, "AIPort failed to send conversation cancel RequestId={RequestId} ConversationId={ConversationId} Reason={Reason}", requestId, conversationId, reason);
            }
        }

        private void CancelConversationRequestTimeoutLocked(string reason)
        {
            ThreadingTimer timer = conversationRequestTimeoutTimer;
            string requestId = timeoutConversationRequestId;
            conversationRequestTimeoutTimer = null;
            timeoutConversationRequestId = string.Empty;
            timeoutConversationId = string.Empty;
            if (timer != null) timer.Dispose();
            if (timer != null) Logger.Debug("AIPort conversation request timeout canceled RequestId={RequestId} Reason={Reason}", requestId, reason ?? string.Empty);
        }

        private void CancelConversationRetryTimerLocked(string reason)
        {
            ThreadingTimer timer = conversationRetryTimer;
            string conversationId = retryConversationId;
            int retryAttempt = retryConversationAttempt;
            bool hadPendingRetry = timer != null || !string.IsNullOrWhiteSpace(conversationId);
            conversationRetryTimer = null;
            if (timer != null)
            {
                timer.Dispose();
            }
            ClearConversationRetryPayloadLocked();
            if (hadPendingRetry)
            {
                Logger.Information("AIPort conversation retry canceled ConversationId={ConversationId} Attempt={Attempt} Reason={Reason}", conversationId, retryAttempt, reason ?? string.Empty);
            }
        }

        private void ClearConversationRetryPayloadLocked()
        {
            retryConversationId = string.Empty;
            retryConversationNpcHeroId = string.Empty;
            retryConversationText = string.Empty;
            retryConversationTurn = 0;
            retryConversationAttempt = 0;
        }

        private void ClearPendingConversationRequestLocked()
        {
            CancelConversationRequestTimeoutLocked("pending_cleared");
            pendingConversationRequestId = string.Empty;
            pendingConversationId = string.Empty;
            pendingConversationNpcHeroId = string.Empty;
            pendingConversationText = string.Empty;
            pendingConversationTurn = 0;
            pendingConversationRetryAttempt = 0;
        }

    }
}
