using System;

namespace AIPort.Server
{
    public interface IAIPortServerSettings
    {
        string Backend { get; }
        string Model { get; }
        string ApiKey { get; }
        Uri Endpoint { get; }
        TimeSpan RequestTimeout { get; }
        int MaxConcurrentRequests { get; }
        int MaxCompletionTokens { get; }
        bool CredentialsPresent { get; }
        bool Player2BackendSelected { get; }
        string Player2TokensPath { get; }
        string Player2AccountsPath { get; }
        bool EnableDiplomacy { get; }
        bool EnableDynamicEvents { get; }
    }
}
