using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net;
using System.Text;
using AIPort.Protocol;

namespace AIPort.Server
{
    public sealed class Player2Backend
    {
        private static readonly Uri ChatEndpoint = new Uri("https://api.player2.game/v1/chat/completions");
        private static readonly Uri LoginEndpoint = new Uri("https://umkolexvubbenetudtvq.supabase.co/auth/v1/token?grant_type=password");
        private const int MaximumBackendResponseCharacters = 1048576;
        private const int MaximumCredentialAttempts = 64;
        private readonly object gate = new object();
        private readonly HashSet<string> busy = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private List<AccountState> accounts = new List<AccountState>();
        private string loadedTokensPath = string.Empty;
        private string loadedAccountsPath = string.Empty;
        private DateTime loadedTokensWriteUtc = DateTime.MinValue;
        private DateTime loadedAccountsWriteUtc = DateTime.MinValue;
        private int cursor;

        private sealed class AccountState
        {
            public string Email;
            public string Token;
            public string Password;
        }

        private sealed class AccountLease
        {
            public string Email;
            public string Token;
            public string Password;
        }

        private sealed class HttpResult
        {
            public int StatusCode;
            public string Body;
        }

        public string Complete(AIPortServerSettings settings, string systemPrompt, string userPrompt, Action<HttpWebRequest> requestCreated)
        {
            HashSet<string> attempted = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int attempt = 0; attempt < MaximumCredentialAttempts; attempt++)
            {
                AccountLease lease;
                if (!TryAcquire(settings, attempted, out lease)) break;
                attempted.Add(lease.Email);
                try
                {
                    string token = lease.Token;
                    if (string.IsNullOrWhiteSpace(token) && !TryRefresh(settings, lease, requestCreated, out token)) continue;

                    HttpResult result = SendChat(settings, token, systemPrompt, userPrompt, requestCreated);
                    if (result.StatusCode == 401 && TryRefresh(settings, lease, requestCreated, out token))
                    {
                        result = SendChat(settings, token, systemPrompt, userPrompt, requestCreated);
                    }
                    if (result.StatusCode == 200) return NormalizeReply(ExtractContent(result.Body));
                    if (result.StatusCode == 401 || result.StatusCode == 402 || result.StatusCode == 429) continue;
                    throw new InvalidOperationException("AIPort Player2 backend returned HTTP " + result.StatusCode.ToString(CultureInfo.InvariantCulture));
                }
                finally
                {
                    Release(lease.Email);
                }
            }
            throw new InvalidOperationException("AIPort Player2 backend has no usable account credential");
        }

        private bool TryRefresh(AIPortServerSettings settings, AccountLease lease, Action<HttpWebRequest> requestCreated, out string token)
        {
            token = string.Empty;
            if (lease == null || string.IsNullOrWhiteSpace(lease.Email) || string.IsNullOrWhiteSpace(lease.Password) || string.IsNullOrWhiteSpace(settings.Player2SupabaseAnonKey)) return false;
            string body = "{\"email\":\"" + EscapeJsonString(lease.Email) + "\",\"password\":\"" + EscapeJsonString(lease.Password) + "\"}";
            HttpResult result = SendJson(LoginEndpoint, body, string.Empty, settings.Player2SupabaseAnonKey, settings.RequestTimeout, requestCreated);
            if (result.StatusCode != 200) return false;
            token = ExtractStringProperty(result.Body, "access_token");
            if (string.IsNullOrWhiteSpace(token)) return false;
            UpdateToken(lease.Email, token);
            return true;
        }

        private static HttpResult SendChat(AIPortServerSettings settings, string token, string systemPrompt, string userPrompt, Action<HttpWebRequest> requestCreated)
        {
            string body = "{\"messages\":[{\"role\":\"system\",\"content\":\"" + EscapeJsonString(systemPrompt)
                + "\"},{\"role\":\"user\",\"content\":\"" + EscapeJsonString(userPrompt)
                + "\"}],\"stream\":false,\"temperature\":0.7,\"max_tokens\":" + settings.MaxCompletionTokens.ToString(CultureInfo.InvariantCulture) + "}";
            return SendJson(ChatEndpoint, body, token, string.Empty, settings.RequestTimeout, requestCreated);
        }

        private static HttpResult SendJson(Uri endpoint, string body, string bearerToken, string apiKey, TimeSpan timeout, Action<HttpWebRequest> requestCreated)
        {
            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(endpoint);
            request.Method = "POST";
            request.Timeout = (int)timeout.TotalMilliseconds;
            request.ReadWriteTimeout = (int)timeout.TotalMilliseconds;
            request.ContentType = "application/json; charset=utf-8";
            request.Accept = "application/json";
            request.UserAgent = "AIPort/" + AIPortProtocol.Build;
            request.Headers["Origin"] = "https://player2.game";
            request.Referer = "https://player2.game/";
            request.AllowAutoRedirect = false;
            request.KeepAlive = false;
            request.ConnectionGroupName = "AIPort-Player2-" + Guid.NewGuid().ToString("N");
            if (!string.IsNullOrWhiteSpace(bearerToken)) request.Headers["Authorization"] = "Bearer " + bearerToken;
            if (!string.IsNullOrWhiteSpace(apiKey)) request.Headers["apikey"] = apiKey;
            if (requestCreated != null) requestCreated(request);

            byte[] bytes = Encoding.UTF8.GetBytes(body);
            request.ContentLength = bytes.Length;
            try
            {
                using (Stream stream = request.GetRequestStream()) stream.Write(bytes, 0, bytes.Length);
                using (HttpWebResponse response = (HttpWebResponse)request.GetResponse())
                using (StreamReader reader = new StreamReader(response.GetResponseStream(), Encoding.UTF8))
                {
                    return new HttpResult { StatusCode = (int)response.StatusCode, Body = ReadBounded(response, reader) };
                }
            }
            catch (WebException ex)
            {
                HttpWebResponse response = ex.Response as HttpWebResponse;
                if (response == null) throw;
                using (response)
                using (StreamReader reader = new StreamReader(response.GetResponseStream(), Encoding.UTF8))
                {
                    return new HttpResult { StatusCode = (int)response.StatusCode, Body = ReadBounded(response, reader) };
                }
            }
        }

        private bool TryAcquire(AIPortServerSettings settings, HashSet<string> attempted, out AccountLease lease)
        {
            lease = null;
            lock (gate)
            {
                EnsureLoadedLocked(settings);
                if (accounts.Count == 0) return false;
                for (int offset = 0; offset < accounts.Count; offset++)
                {
                    int index = (cursor + offset) % accounts.Count;
                    AccountState account = accounts[index];
                    if (account == null || string.IsNullOrWhiteSpace(account.Email) || busy.Contains(account.Email) || attempted.Contains(account.Email)) continue;
                    bool canRefresh = !string.IsNullOrWhiteSpace(account.Password) && !string.IsNullOrWhiteSpace(settings.Player2SupabaseAnonKey);
                    if (string.IsNullOrWhiteSpace(account.Token) && !canRefresh) continue;
                    busy.Add(account.Email);
                    cursor = (index + 1) % accounts.Count;
                    lease = new AccountLease { Email = account.Email, Token = account.Token, Password = account.Password };
                    return true;
                }
            }
            return false;
        }

        private void EnsureLoadedLocked(AIPortServerSettings settings)
        {
            string tokensPath = settings.Player2TokensPath ?? string.Empty;
            string accountsPath = settings.Player2AccountsPath ?? string.Empty;
            DateTime tokenWrite = LastWriteUtc(tokensPath);
            DateTime accountWrite = LastWriteUtc(accountsPath);
            bool unchanged = string.Equals(tokensPath, loadedTokensPath, StringComparison.OrdinalIgnoreCase)
                && string.Equals(accountsPath, loadedAccountsPath, StringComparison.OrdinalIgnoreCase)
                && tokenWrite == loadedTokensWriteUtc
                && accountWrite == loadedAccountsWriteUtc;
            if (unchanged || busy.Count > 0) return;

            List<Player2CredentialRecord> loaded = Player2CredentialPool.Load(tokensPath, accountsPath);
            List<AccountState> next = new List<AccountState>();
            foreach (Player2CredentialRecord record in loaded)
            {
                next.Add(new AccountState { Email = record.Email, Token = record.AccessToken, Password = record.Password });
            }
            accounts = next;
            loadedTokensPath = tokensPath;
            loadedAccountsPath = accountsPath;
            loadedTokensWriteUtc = tokenWrite;
            loadedAccountsWriteUtc = accountWrite;
            cursor = accounts.Count == 0 ? 0 : cursor % accounts.Count;
        }

        private void UpdateToken(string email, string token)
        {
            lock (gate)
            {
                foreach (AccountState account in accounts)
                {
                    if (string.Equals(account.Email, email, StringComparison.OrdinalIgnoreCase))
                    {
                        account.Token = token;
                        return;
                    }
                }
            }
        }

        private void Release(string email)
        {
            lock (gate) busy.Remove(email ?? string.Empty);
        }

        private static DateTime LastWriteUtc(string path)
        {
            try { return !string.IsNullOrWhiteSpace(path) && File.Exists(path) ? File.GetLastWriteTimeUtc(path) : DateTime.MinValue; }
            catch { return DateTime.MinValue; }
        }

        private static string ReadBounded(HttpWebResponse response, StreamReader reader)
        {
            if (response.ContentLength > MaximumBackendResponseCharacters) throw new InvalidOperationException("AIPort Player2 response exceeded the size limit");
            char[] buffer = new char[4096];
            StringBuilder text = new StringBuilder();
            int read;
            while ((read = reader.Read(buffer, 0, buffer.Length)) > 0)
            {
                if (text.Length + read > MaximumBackendResponseCharacters) throw new InvalidOperationException("AIPort Player2 response exceeded the size limit");
                text.Append(buffer, 0, read);
            }
            return text.ToString();
        }

        private static string ExtractContent(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) throw new InvalidOperationException("AIPort Player2 response was empty");
            int choices = FindProperty(json, "choices", 0);
            int message = FindProperty(json, "message", choices + 1);
            int content = FindProperty(json, "content", message + 1);
            int colon = json.IndexOf(':', content);
            if (colon < 0) throw new InvalidOperationException("AIPort Player2 content property was malformed");
            int cursor = colon + 1;
            while (cursor < json.Length && char.IsWhiteSpace(json[cursor])) cursor++;
            if (cursor >= json.Length || json[cursor] != '"') throw new InvalidOperationException("AIPort Player2 content was not a string");
            return ParseJsonString(json, cursor);
        }

        private static string ExtractStringProperty(string json, string propertyName)
        {
            int property = FindProperty(json, propertyName, 0);
            int colon = json.IndexOf(':', property);
            if (colon < 0) return string.Empty;
            int cursor = colon + 1;
            while (cursor < json.Length && char.IsWhiteSpace(json[cursor])) cursor++;
            return cursor < json.Length && json[cursor] == '"' ? ParseJsonString(json, cursor) : string.Empty;
        }

        private static int FindProperty(string json, string propertyName, int startIndex)
        {
            int index = (json ?? string.Empty).IndexOf("\"" + propertyName + "\"", Math.Max(0, startIndex), StringComparison.Ordinal);
            if (index < 0) throw new InvalidOperationException("AIPort Player2 response had no " + propertyName + " property");
            return index;
        }

        private static string ParseJsonString(string json, int quoteIndex)
        {
            StringBuilder text = new StringBuilder();
            for (int i = quoteIndex + 1; i < json.Length; i++)
            {
                char c = json[i];
                if (c == '"') return text.ToString();
                if (c != '\\') { text.Append(c); continue; }
                if (++i >= json.Length) throw new InvalidOperationException("AIPort Player2 JSON string ended after an escape");
                switch (json[i])
                {
                    case '"': text.Append('"'); break;
                    case '\\': text.Append('\\'); break;
                    case '/': text.Append('/'); break;
                    case 'b': text.Append('\b'); break;
                    case 'f': text.Append('\f'); break;
                    case 'n': text.Append('\n'); break;
                    case 'r': text.Append('\r'); break;
                    case 't': text.Append('\t'); break;
                    case 'u':
                        if (i + 4 >= json.Length) throw new InvalidOperationException("AIPort Player2 unicode escape was truncated");
                        int code = 0;
                        for (int j = 1; j <= 4; j++) code = (code << 4) | HexValue(json[i + j]);
                        text.Append((char)code);
                        i += 4;
                        break;
                    default: throw new InvalidOperationException("AIPort Player2 JSON escape was unsupported");
                }
            }
            throw new InvalidOperationException("AIPort Player2 JSON string was not terminated");
        }

        private static int HexValue(char value)
        {
            if (value >= '0' && value <= '9') return value - '0';
            if (value >= 'a' && value <= 'f') return value - 'a' + 10;
            if (value >= 'A' && value <= 'F') return value - 'A' + 10;
            throw new InvalidOperationException("AIPort Player2 unicode escape was invalid");
        }

        private static string NormalizeReply(string value)
        {
            string safe = (value ?? string.Empty).Replace('\0', ' ').Trim();
            return safe.Length <= AIPortProtocol.MaximumNpcDisplayTextLength ? safe : safe.Substring(0, AIPortProtocol.MaximumNpcDisplayTextLength);
        }

        private static string EscapeJsonString(string value)
        {
            if (string.IsNullOrEmpty(value)) return string.Empty;
            StringBuilder escaped = new StringBuilder(value.Length + 32);
            foreach (char c in value)
            {
                switch (c)
                {
                    case '"': escaped.Append("\\\""); break;
                    case '\\': escaped.Append("\\\\"); break;
                    case '\b': escaped.Append("\\b"); break;
                    case '\f': escaped.Append("\\f"); break;
                    case '\n': escaped.Append("\\n"); break;
                    case '\r': escaped.Append("\\r"); break;
                    case '\t': escaped.Append("\\t"); break;
                    default:
                        if (c < 0x20) escaped.Append("\\u").Append(((int)c).ToString("x4"));
                        else escaped.Append(c);
                        break;
                }
            }
            return escaped.ToString();
        }
    }
}
