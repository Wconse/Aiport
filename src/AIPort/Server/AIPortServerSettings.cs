using System;
using System.IO;

namespace AIPort.Server
{
    public sealed class AIPortServerSettings : IAIPortServerSettings
    {
        public bool Enabled { get; private set; }
        public bool ExplicitlyEnabled { get; private set; }
        public bool EndpointAllowed { get; private set; }
        public bool CredentialsPresent { get; private set; }
        public string ConfigPath { get; private set; }
        public string Backend { get; private set; }
        public string Model { get; private set; }
        public string ApiKey { get; private set; }
        public Uri Endpoint { get; private set; }
        public TimeSpan RequestTimeout { get; private set; }
        public int MaxConcurrentRequests { get; private set; }
        public int MaxCompletionTokens { get; private set; }
        public int MaxRequestsPerPlayerPerMinute { get; private set; }
        public bool Player2BackendSelected { get; private set; }
        public string Player2TokensPath { get; private set; }
        public string Player2AccountsPath { get; private set; }
        public string Player2SupabaseAnonKey { get; private set; }
        public bool Player2TokenFilePresent { get; private set; }
        public bool Player2AccountFilePresent { get; private set; }
        public bool Player2RefreshAvailable { get; private set; }
        public bool EnableDiplomacy { get; private set; }
        public bool EnableDynamicEvents { get; private set; }
        public bool EnableIntentFoundation { get; private set; }
        public bool EnableStateSnapshots { get; private set; }
        public bool EnablePersistentMemory { get; private set; }
        public bool EnableRelationShadowIntents { get; private set; }
        public bool NativeWarAdapterConfigured { get; private set; }
        public bool NativeWarAdapterEnvironmentArmed { get; private set; }
        public bool EnableNativeWarAdapter { get; private set; }
        public bool NativePeaceAdapterConfigured { get; private set; }
        public bool NativePeaceAdapterEnvironmentArmed { get; private set; }
        public bool EnableNativePeaceAdapter { get; private set; }
        public string NativeDiplomacyGenerationPin { get; private set; }
        public bool EnableNpcDiplomacyInitiative { get; private set; }
        public int NpcDiplomacyDailyBudget { get; private set; }
        public int NpcDiplomacyMinimumIntervalHours { get; private set; }
        public int NpcDiplomacyPairCooldownDays { get; private set; }
        public int NpcDiplomacyMinimumScore { get; private set; }
        public string StatePath { get; private set; }

        public static AIPortServerSettings Load()
        {
            AIPortServerSettings settings = new AIPortServerSettings();
            settings.Enabled = false;
            settings.Backend = "OpenRouter";
            settings.Model = "openai/gpt-4o-mini";
            settings.Endpoint = new Uri("https://openrouter.ai/api/v1/chat/completions");
            settings.RequestTimeout = TimeSpan.FromSeconds(90);
            settings.MaxConcurrentRequests = 1;
            settings.MaxCompletionTokens = 384;
            settings.MaxRequestsPerPlayerPerMinute = 4;
            settings.Player2TokensPath = string.Empty;
            settings.Player2AccountsPath = string.Empty;
            settings.Player2SupabaseAnonKey = string.Empty;
            settings.EnableDiplomacy = false;
            settings.EnableDynamicEvents = false;
            settings.EnableIntentFoundation = true;
            settings.EnableStateSnapshots = true;
            settings.EnablePersistentMemory = false;
            settings.EnableRelationShadowIntents = false;
            settings.NativeWarAdapterConfigured = false;
            settings.NativeWarAdapterEnvironmentArmed = false;
            settings.EnableNativeWarAdapter = false;
            settings.NativePeaceAdapterConfigured = false;
            settings.NativePeaceAdapterEnvironmentArmed = false;
            settings.EnableNativePeaceAdapter = false;
            settings.NativeDiplomacyGenerationPin = string.Empty;
            settings.EnableNpcDiplomacyInitiative = false;
            settings.NpcDiplomacyDailyBudget = 2;
            settings.NpcDiplomacyMinimumIntervalHours = 6;
            settings.NpcDiplomacyPairCooldownDays = 7;
            settings.NpcDiplomacyMinimumScore = 82;
            settings.StatePath = string.Empty;
            bool endpointConfigurationValid = true;
            string player2TokensText = string.Empty;
            string player2AccountsText = string.Empty;

            string configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", "..", "Modules", "aiport", "config", "server.json");
            string localPath = Path.Combine(Directory.GetCurrentDirectory(), "aiport-server.json");
            string configuredPath = Environment.GetEnvironmentVariable("AIPORT_CONFIG_PATH") ?? string.Empty;
            string chosen = !string.IsNullOrWhiteSpace(configuredPath) && Path.IsPathRooted(configuredPath)
                ? Path.GetFullPath(configuredPath)
                : (File.Exists(localPath) ? localPath : Path.GetFullPath(configPath));
            settings.ConfigPath = chosen;
            if (File.Exists(chosen))
            {
                string json = File.ReadAllText(chosen);
                settings.Enabled = ReadBool(json, "enabled", false);
                settings.Backend = ReadString(json, "backend", settings.Backend).Trim();
                settings.Model = ReadString(json, "model", settings.Model).Trim();
                string endpointText = ReadString(json, "endpoint", settings.Endpoint.ToString()).Trim();
                Uri endpoint;
                if (Uri.TryCreate(endpointText, UriKind.Absolute, out endpoint)) settings.Endpoint = endpoint;
                else endpointConfigurationValid = false;
                player2TokensText = ReadString(json, "player2TokensPath", string.Empty).Trim();
                player2AccountsText = ReadString(json, "player2AccountsPath", string.Empty).Trim();
                settings.RequestTimeout = TimeSpan.FromSeconds(Clamp(ReadInt(json, "requestTimeoutSeconds", 90), 5, 120));
                settings.MaxConcurrentRequests = Clamp(ReadInt(json, "maxConcurrentRequests", 1), 1, 4);
                settings.MaxCompletionTokens = Clamp(ReadInt(json, "maxCompletionTokens", 384), 64, 1024);
                settings.MaxRequestsPerPlayerPerMinute = Clamp(ReadInt(json, "maxRequestsPerPlayerPerMinute", 4), 1, 60);
                settings.EnablePersistentMemory = ReadBool(json, "enablePersistentMemory", false);
                settings.EnableRelationShadowIntents = ReadBool(json, "enableRelationShadowIntents", false);
                settings.NativeWarAdapterConfigured = ReadBool(json, "enableNativeWarAdapter", false);
                settings.NativePeaceAdapterConfigured = ReadBool(json, "enableNativePeaceAdapter", false);
                settings.EnableNpcDiplomacyInitiative = ReadBool(json, "enableNpcDiplomacyInitiative", false);
                settings.NpcDiplomacyDailyBudget = Clamp(ReadInt(json, "npcDiplomacyDailyBudget", 2), 1, 8);
                settings.NpcDiplomacyMinimumIntervalHours = Clamp(ReadInt(json, "npcDiplomacyMinimumIntervalHours", 6), 1, 24);
                settings.NpcDiplomacyPairCooldownDays = Clamp(ReadInt(json, "npcDiplomacyPairCooldownDays", 7), 1, 30);
                settings.NpcDiplomacyMinimumScore = Clamp(ReadInt(json, "npcDiplomacyMinimumScore", 82), 0, 200);
            }

            if (string.IsNullOrWhiteSpace(settings.Backend)) settings.Backend = "OpenRouter";
            settings.Player2BackendSelected = string.Equals(settings.Backend, "Player2", StringComparison.OrdinalIgnoreCase);
            if (settings.Player2BackendSelected)
            {
                settings.Backend = "Player2";
                if (string.IsNullOrWhiteSpace(settings.Model)) settings.Model = "account-configured";
                settings.Endpoint = new Uri("https://api.player2.game/v1/chat/completions");
                endpointConfigurationValid = true;
            }
            else if (string.IsNullOrWhiteSpace(settings.Model)) settings.Model = "openai/gpt-4o-mini";

            string tokensEnvironment = Environment.GetEnvironmentVariable("AIPORT_PLAYER2_TOKENS_PATH") ?? string.Empty;
            string accountsEnvironment = Environment.GetEnvironmentVariable("AIPORT_PLAYER2_ACCOUNTS_PATH") ?? string.Empty;
            settings.Player2TokensPath = ResolveOptionalPath(string.IsNullOrWhiteSpace(tokensEnvironment) ? player2TokensText : tokensEnvironment, chosen);
            settings.Player2AccountsPath = ResolveOptionalPath(string.IsNullOrWhiteSpace(accountsEnvironment) ? player2AccountsText : accountsEnvironment, chosen);
            settings.Player2SupabaseAnonKey = Environment.GetEnvironmentVariable("AIPORT_PLAYER2_SUPABASE_ANON_KEY") ?? string.Empty;
            settings.Player2TokenFilePresent = Player2CredentialPool.HasTokenEntries(settings.Player2TokensPath);
            settings.Player2AccountFilePresent = Player2CredentialPool.HasAccountEntries(settings.Player2AccountsPath);
            settings.Player2RefreshAvailable = settings.Player2AccountFilePresent && !string.IsNullOrWhiteSpace(settings.Player2SupabaseAnonKey);

            // Config cannot redirect either provider's Authorization header to an unrelated process environment variable.
            settings.ApiKey = Environment.GetEnvironmentVariable("AIPORT_API_KEY") ?? string.Empty;
            settings.CredentialsPresent = settings.Player2BackendSelected
                ? settings.Player2TokenFilePresent || settings.Player2RefreshAvailable
                : !string.IsNullOrWhiteSpace(settings.ApiKey);
            settings.ExplicitlyEnabled = settings.Enabled;
            bool isHttps = settings.Endpoint != null
                && string.Equals(settings.Endpoint.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase);
            bool isLoopbackHttp = settings.Endpoint != null
                && settings.Endpoint.IsLoopback
                && string.Equals(settings.Endpoint.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase);
            settings.EndpointAllowed = endpointConfigurationValid
                && settings.Endpoint != null
                && string.IsNullOrEmpty(settings.Endpoint.UserInfo)
                && string.IsNullOrEmpty(settings.Endpoint.Fragment)
                && (isHttps || isLoopbackHttp);
            // Explicit config + provider credentials + secure/loopback endpoint are all required.
            settings.Enabled = settings.ExplicitlyEnabled && settings.CredentialsPresent && settings.EndpointAllowed;
            settings.EnableDiplomacy = false;
            settings.NativeWarAdapterEnvironmentArmed = string.Equals(Environment.GetEnvironmentVariable("AIPORT_ENABLE_NATIVE_WAR"), "I_UNDERSTAND_NATIVE_WAR", StringComparison.Ordinal);
            settings.EnableNativeWarAdapter = settings.NativeWarAdapterConfigured && settings.NativeWarAdapterEnvironmentArmed;
            settings.NativePeaceAdapterEnvironmentArmed = string.Equals(Environment.GetEnvironmentVariable("AIPORT_ENABLE_NATIVE_PEACE"), "I_UNDERSTAND_NATIVE_PEACE", StringComparison.Ordinal);
            settings.EnableNativePeaceAdapter = settings.NativePeaceAdapterConfigured && settings.NativePeaceAdapterEnvironmentArmed;
            settings.NativeDiplomacyGenerationPin = (Environment.GetEnvironmentVariable("AIPORT_NATIVE_DIPLOMACY_GENERATION") ?? string.Empty).Trim();
            settings.EnableDynamicEvents = false;
            string statePath = Environment.GetEnvironmentVariable("AIPORT_STATE_PATH") ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(statePath) && Path.IsPathRooted(statePath)) settings.StatePath = Path.GetFullPath(statePath);
            settings.EnablePersistentMemory = settings.EnablePersistentMemory && !string.IsNullOrWhiteSpace(settings.StatePath);
            settings.EnableNpcDiplomacyInitiative = settings.EnableNpcDiplomacyInitiative && settings.EnablePersistentMemory;
            return settings;
        }

        private static string ResolveOptionalPath(string value, string configPath)
        {
            if (string.IsNullOrWhiteSpace(value)) return string.Empty;
            try
            {
                if (Path.IsPathRooted(value)) return Path.GetFullPath(value);
                string directory = Path.GetDirectoryName(configPath) ?? Directory.GetCurrentDirectory();
                return Path.GetFullPath(Path.Combine(directory, value));
            }
            catch { return string.Empty; }
        }

        private static int Clamp(int value, int minimum, int maximum)
        {
            return Math.Max(minimum, Math.Min(maximum, value));
        }

        private static string ReadString(string json, string key, string fallback)
        {
            string needle = "\"" + key + "\":";
            int index = json.IndexOf(needle, StringComparison.OrdinalIgnoreCase);
            if (index < 0) return fallback;
            int start = json.IndexOf('"', index + needle.Length);
            if (start < 0) return fallback;
            int end = json.IndexOf('"', start + 1);
            if (end < 0) return fallback;
            return json.Substring(start + 1, end - start - 1);
        }

        private static int ReadInt(string json, string key, int fallback)
        {
            string needle = "\"" + key + "\":";
            int index = json.IndexOf(needle, StringComparison.OrdinalIgnoreCase);
            if (index < 0) return fallback;
            int start = index + needle.Length;
            while (start < json.Length && (json[start] == ' ' || json[start] == '\t')) start++;
            int end = start;
            while (end < json.Length && char.IsDigit(json[end])) end++;
            int parsed;
            return end > start && int.TryParse(json.Substring(start, end - start), out parsed) ? parsed : fallback;
        }

        private static bool ReadBool(string json, string key, bool fallback)
        {
            string needle = "\"" + key + "\":";
            int index = json.IndexOf(needle, StringComparison.OrdinalIgnoreCase);
            if (index < 0) return fallback;
            int start = index + needle.Length;
            while (start < json.Length && (json[start] == ' ' || json[start] == '\t')) start++;
            if (json.IndexOf("true", start, StringComparison.OrdinalIgnoreCase) == start) return true;
            if (json.IndexOf("false", start, StringComparison.OrdinalIgnoreCase) == start) return false;
            return fallback;
        }
    }
}
