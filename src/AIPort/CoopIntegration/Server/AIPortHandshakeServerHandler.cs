using System;
using AIPort.Protocol;
using AIPort.Protocol.Messages;
using Common.Logging;
using Common.Messaging;
using Common.Network;
using LiteNetLib;
using Serilog;

namespace Coop.Core.Server.Services.AIPort.Handlers
{
    internal sealed class AIPortHandshakeServerHandler : IHandler, IDisposable
    {
        private static readonly ILogger Logger = LogManager.GetLogger<AIPortHandshakeServerHandler>();
        private readonly IMessageBroker messageBroker;
        private readonly INetwork network;

        public AIPortHandshakeServerHandler(IMessageBroker messageBroker, INetwork network)
        {
            this.messageBroker = messageBroker;
            this.network = network;
            messageBroker.Subscribe<AIPortHandshakeRequest>(Handle);
        }

        public void Dispose()
        {
            messageBroker.Unsubscribe<AIPortHandshakeRequest>(Handle);
        }

        private void Handle(MessagePayload<AIPortHandshakeRequest> payload)
        {
            NetPeer peer = payload.Who as NetPeer;
            if (peer == null)
            {
                Logger.Warning("AIPort handshake ignored because message source was not a NetPeer");
                return;
            }
            AIPortHandshakeRequest request = payload.What;
            bool requestIdValid = !string.IsNullOrWhiteSpace(request.RequestId);
            bool compatible = requestIdValid && request.ProtocolVersion == AIPortProtocol.Version;
            string message = !requestIdValid ? "AIPort request ID missing" : compatible ? "AIPort protocol ready" : "AIPort protocol mismatch";
            Logger.Information("AIPort handshake received PeerId={PeerId} RequestId={RequestId} ClientProtocol={ClientProtocol} ClientBuild={ClientBuild} Compatible={Compatible}", peer.Id, request.RequestId, request.ProtocolVersion, request.ClientBuild, compatible);
            network.SendImmediate(peer, new AIPortHandshakeResponse(AIPortProtocol.Version, request.RequestId, compatible, AIPortProtocol.Build, message));
        }
    }
}
