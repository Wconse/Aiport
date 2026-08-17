using System;
using System.IO;
using System.Globalization;
using System.Net;
using System.Text;
using AIPort.Protocol;

namespace AIPort.Server
{
    public sealed class OpenAiCompatibleBackend
    {
        private const int MaximumBackendResponseCharacters = 1048576;
        public string Complete(AIPortServerSettings settings, string systemPrompt, string userPrompt)
        {
            return Complete(settings, systemPrompt, userPrompt, null);
        }

        public string Complete(AIPortServerSettings settings, string systemPrompt, string userPrompt, Action<HttpWebRequest> requestCreated)
        {
            string body = "{\"model\":\"" + EscapeJsonString(settings.Model)
                + "\",\"messages\":[{\"role\":\"system\",\"content\":\"" + EscapeJsonString(systemPrompt)
                + "\"},{\"role\":\"user\",\"content\":\"" + EscapeJsonString(userPrompt)
                + "\"}],\"temperature\":0.7,\"max_completion_tokens\":" + settings.MaxCompletionTokens.ToString(CultureInfo.InvariantCulture)
                + ",\"stream\":false,\"n\":1}";
            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(settings.Endpoint);
            request.Method = "POST";
            request.Timeout = (int)settings.RequestTimeout.TotalMilliseconds;
            request.ReadWriteTimeout = (int)settings.RequestTimeout.TotalMilliseconds;
            request.ContentType = "application/json; charset=utf-8";
            request.Accept = "application/json";
            request.UserAgent = "AIPort/" + AIPortProtocol.Build;
            request.AllowAutoRedirect = false;
            request.KeepAlive = false;
            request.ConnectionGroupName = "AIPort-" + Guid.NewGuid().ToString("N");
            if (!string.IsNullOrWhiteSpace(settings.ApiKey)) request.Headers["Authorization"] = "Bearer " + settings.ApiKey;
            if (requestCreated != null) requestCreated(request);

            byte[] bytes = Encoding.UTF8.GetBytes(body);
            request.ContentLength = bytes.Length;
            using (Stream stream = request.GetRequestStream()) stream.Write(bytes, 0, bytes.Length);
            using (HttpWebResponse response = (HttpWebResponse)request.GetResponse())
            using (StreamReader reader = new StreamReader(response.GetResponseStream(), Encoding.UTF8))
            {
                if (response.ContentLength > MaximumBackendResponseCharacters) throw new InvalidOperationException("AIPort backend response exceeded the size limit");
                return NormalizeReply(ExtractContent(ReadBounded(reader)));
            }
        }

        private static string ReadBounded(StreamReader reader)
        {
            char[] buffer = new char[4096];
            StringBuilder text = new StringBuilder();
            int read;
            while ((read = reader.Read(buffer, 0, buffer.Length)) > 0)
            {
                if (text.Length + read > MaximumBackendResponseCharacters) throw new InvalidOperationException("AIPort backend response exceeded the size limit");
                text.Append(buffer, 0, read);
            }
            return text.ToString();
        }

        private static string ExtractContent(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) throw new InvalidOperationException("AIPort backend response was empty");
            int choices = FindProperty(json, "choices", 0);
            int message = FindProperty(json, "message", choices + 1);
            int content = FindProperty(json, "content", message + 1);
            int colon = json.IndexOf(':', content);
            if (colon < 0) throw new InvalidOperationException("AIPort backend content property was malformed");
            int cursor = colon + 1;
            while (cursor < json.Length && char.IsWhiteSpace(json[cursor])) cursor++;
            if (cursor >= json.Length || json[cursor] != '"') throw new InvalidOperationException("AIPort backend content was not a string");
            return ParseJsonString(json, cursor);
        }

        private static int FindProperty(string json, string propertyName, int startIndex)
        {
            int index = json.IndexOf("\"" + propertyName + "\"", Math.Max(0, startIndex), StringComparison.Ordinal);
            if (index < 0) throw new InvalidOperationException("AIPort backend response had no " + propertyName + " property");
            return index;
        }

        private static string ParseJsonString(string json, int quoteIndex)
        {
            StringBuilder text = new StringBuilder();
            for (int i = quoteIndex + 1; i < json.Length; i++)
            {
                char c = json[i];
                if (c == '"') return text.ToString();
                if (c != '\\')
                {
                    text.Append(c);
                    continue;
                }
                if (++i >= json.Length) throw new InvalidOperationException("AIPort backend content ended after an escape");
                char escaped = json[i];
                switch (escaped)
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
                        if (i + 4 >= json.Length) throw new InvalidOperationException("AIPort backend unicode escape was truncated");
                        int code = 0;
                        for (int j = 1; j <= 4; j++) code = (code << 4) | HexValue(json[i + j]);
                        text.Append((char)code);
                        i += 4;
                        break;
                    default: throw new InvalidOperationException("AIPort backend content had an unsupported escape");
                }
            }
            throw new InvalidOperationException("AIPort backend content string was not terminated");
        }

        private static int HexValue(char value)
        {
            if (value >= '0' && value <= '9') return value - '0';
            if (value >= 'a' && value <= 'f') return value - 'a' + 10;
            if (value >= 'A' && value <= 'F') return value - 'A' + 10;
            throw new InvalidOperationException("AIPort backend unicode escape was invalid");
        }

        private static string NormalizeReply(string value)
        {
            string safe = (value ?? string.Empty).Replace('\0', ' ').Trim();
            return safe.Length <= AIPortProtocol.MaximumNpcDisplayTextLength
                ? safe
                : safe.Substring(0, AIPortProtocol.MaximumNpcDisplayTextLength);
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
