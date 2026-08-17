using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace AIPort.Server
{
    public sealed class Player2CredentialRecord
    {
        public string Email { get; private set; }
        public string AccessToken { get; internal set; }
        public string Password { get; internal set; }

        internal Player2CredentialRecord(string email)
        {
            Email = (email ?? string.Empty).Trim();
            AccessToken = string.Empty;
            Password = string.Empty;
        }

        public bool HasToken { get { return !string.IsNullOrWhiteSpace(AccessToken); } }
        public bool HasPassword { get { return !string.IsNullOrWhiteSpace(Password); } }
    }

    public static class Player2CredentialPool
    {
        public static List<Player2CredentialRecord> Load(string tokensPath, string accountsPath)
        {
            Dictionary<string, Player2CredentialRecord> byEmail = new Dictionary<string, Player2CredentialRecord>(StringComparer.OrdinalIgnoreCase);
            List<string> order = new List<string>();

            if (IsReadableFile(tokensPath))
            {
                foreach (string raw in File.ReadAllLines(tokensPath))
                {
                    string email;
                    string token;
                    if (!TryParseTokenLine(raw, out email, out token)) continue;
                    Player2CredentialRecord record = GetOrCreate(byEmail, order, email);
                    record.AccessToken = token;
                }
            }

            if (IsReadableFile(accountsPath))
            {
                foreach (string raw in File.ReadAllLines(accountsPath))
                {
                    string email;
                    string password;
                    if (!TryParseAccountLine(raw, out email, out password)) continue;
                    Player2CredentialRecord record = GetOrCreate(byEmail, order, email);
                    record.Password = password;
                }
            }

            List<Player2CredentialRecord> result = new List<Player2CredentialRecord>();
            foreach (string email in order)
            {
                Player2CredentialRecord record;
                if (byEmail.TryGetValue(email, out record) && (record.HasToken || record.HasPassword)) result.Add(record);
            }
            return result;
        }

        public static bool HasTokenEntries(string path)
        {
            if (!IsReadableFile(path)) return false;
            try
            {
                foreach (string raw in File.ReadLines(path))
                {
                    string email;
                    string token;
                    if (TryParseTokenLine(raw, out email, out token)) return true;
                }
            }
            catch { }
            return false;
        }

        public static bool HasAccountEntries(string path)
        {
            if (!IsReadableFile(path)) return false;
            try
            {
                foreach (string raw in File.ReadLines(path))
                {
                    string email;
                    string password;
                    if (TryParseAccountLine(raw, out email, out password)) return true;
                }
            }
            catch { }
            return false;
        }

        public static bool TryParseTokenLine(string raw, out string email, out string token)
        {
            email = string.Empty;
            token = string.Empty;
            string line = (raw ?? string.Empty).Trim();
            if (line.Length == 0 || line.StartsWith("#", StringComparison.Ordinal)) return false;
            int split = line.IndexOf('\t');
            if (split < 1) split = FirstWhitespace(line);
            if (split < 1) return false;
            email = line.Substring(0, split).Trim();
            token = line.Substring(split + 1).Trim();
            return email.Length > 0 && token.Length > 0;
        }

        public static bool TryParseAccountLine(string raw, out string email, out string password)
        {
            email = string.Empty;
            password = string.Empty;
            string line = (raw ?? string.Empty).Trim();
            if (line.Length == 0 || line.StartsWith("#", StringComparison.Ordinal)) return false;

            if (line.StartsWith("{", StringComparison.Ordinal))
            {
                email = ExtractJsonString(line, "email");
                password = ExtractJsonString(line, "password");
                return email.Length > 0 && password.Length > 0;
            }

            int colon = line.IndexOf(':');
            int equal = line.IndexOf('=');
            int separator = colon >= 0 && equal >= 0 ? Math.Min(colon, equal) : Math.Max(colon, equal);
            if (separator > 0)
            {
                email = line.Substring(0, separator).Trim();
                password = line.Substring(separator + 1).Trim();
                return email.Length > 0 && password.Length > 0;
            }

            int whitespace = FirstWhitespace(line);
            if (whitespace < 1) return false;
            email = line.Substring(0, whitespace).Trim();
            password = line.Substring(whitespace + 1).Trim();
            return email.Length > 0 && password.Length > 0;
        }

        private static Player2CredentialRecord GetOrCreate(Dictionary<string, Player2CredentialRecord> byEmail, List<string> order, string email)
        {
            string key = (email ?? string.Empty).Trim();
            Player2CredentialRecord record;
            if (!byEmail.TryGetValue(key, out record))
            {
                record = new Player2CredentialRecord(key);
                byEmail[key] = record;
                order.Add(key);
            }
            return record;
        }

        private static bool IsReadableFile(string path)
        {
            try { return !string.IsNullOrWhiteSpace(path) && Path.IsPathRooted(path) && File.Exists(path); }
            catch { return false; }
        }

        private static int FirstWhitespace(string value)
        {
            for (int i = 0; i < value.Length; i++) if (char.IsWhiteSpace(value[i])) return i;
            return -1;
        }

        private static string ExtractJsonString(string json, string propertyName)
        {
            int property = json.IndexOf("\"" + propertyName + "\"", StringComparison.OrdinalIgnoreCase);
            if (property < 0) return string.Empty;
            int colon = json.IndexOf(':', property + propertyName.Length + 2);
            if (colon < 0) return string.Empty;
            int quote = colon + 1;
            while (quote < json.Length && char.IsWhiteSpace(json[quote])) quote++;
            if (quote >= json.Length || json[quote] != '"') return string.Empty;
            return ParseJsonString(json, quote);
        }

        private static string ParseJsonString(string json, int quoteIndex)
        {
            StringBuilder value = new StringBuilder();
            for (int i = quoteIndex + 1; i < json.Length; i++)
            {
                char c = json[i];
                if (c == '"') return value.ToString();
                if (c != '\\')
                {
                    value.Append(c);
                    continue;
                }
                if (++i >= json.Length) return string.Empty;
                switch (json[i])
                {
                    case '"': value.Append('"'); break;
                    case '\\': value.Append('\\'); break;
                    case '/': value.Append('/'); break;
                    case 'b': value.Append('\b'); break;
                    case 'f': value.Append('\f'); break;
                    case 'n': value.Append('\n'); break;
                    case 'r': value.Append('\r'); break;
                    case 't': value.Append('\t'); break;
                    case 'u':
                        if (i + 4 >= json.Length) return string.Empty;
                        int code = 0;
                        for (int j = 1; j <= 4; j++)
                        {
                            int hex = HexValue(json[i + j]);
                            if (hex < 0) return string.Empty;
                            code = (code << 4) | hex;
                        }
                        value.Append((char)code);
                        i += 4;
                        break;
                    default: return string.Empty;
                }
            }
            return string.Empty;
        }

        private static int HexValue(char value)
        {
            if (value >= '0' && value <= '9') return value - '0';
            if (value >= 'a' && value <= 'f') return value - 'a' + 10;
            if (value >= 'A' && value <= 'F') return value - 'A' + 10;
            return -1;
        }
    }
}
