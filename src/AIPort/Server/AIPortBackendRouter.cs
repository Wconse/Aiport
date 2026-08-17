using System;
using System.Net;

namespace AIPort.Server
{
    public sealed class AIPortBackendRouter
    {
        private readonly OpenAiCompatibleBackend openAiCompatible = new OpenAiCompatibleBackend();
        private readonly Player2Backend player2 = new Player2Backend();

        public string Complete(AIPortServerSettings settings, string systemPrompt, string userPrompt, Action<HttpWebRequest> requestCreated)
        {
            if (settings == null) throw new ArgumentNullException("settings");
            return settings.Player2BackendSelected
                ? player2.Complete(settings, systemPrompt, userPrompt, requestCreated)
                : openAiCompatible.Complete(settings, systemPrompt, userPrompt, requestCreated);
        }
    }
}
