using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace GetACSEvent
{
    public class SimpleWebServer
    {
        private readonly EventStore _store;
        private readonly int _port;
        private TcpListener _listener;
        private Thread _thread;
        private bool _running;

        private sealed class SimpleHttpRequest
        {
            public string HttpMethod { get; set; }
            public Uri Url { get; set; }
            public NameValueCollection QueryString { get; set; }
            public string ContentType { get; set; }
            public Encoding ContentEncoding { get; set; }
            public Stream InputStream { get; set; }
        }

        private sealed class SimpleHttpResponse
        {
            public int StatusCode { get; set; }
            public string ContentType { get; set; }
            public Encoding ContentEncoding { get; set; }
            public Dictionary<string, string> Headers { get; private set; }
            public string RedirectLocation { get; set; }
            public MemoryStream OutputStream { get; private set; }

            public SimpleHttpResponse()
            {
                StatusCode = 200;
                ContentType = "text/plain; charset=utf-8";
                ContentEncoding = Encoding.UTF8;
                Headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                OutputStream = new MemoryStream();
            }
        }

        private sealed class SimpleHttpContext
        {
            public SimpleHttpRequest Request { get; set; }
            public SimpleHttpResponse Response { get; set; }
        }

        public SimpleWebServer(int port, EventStore store)
        {
            if (port <= 0 || port > 65535)
            {
                throw new ArgumentOutOfRangeException(nameof(port), "Web port must be between 1 and 65535.");
            }

            _store = store;
            _port = port;
        }

        public void Start()
        {
            if (_running) return;
            _running = true;
            _listener = new TcpListener(IPAddress.Any, _port);
            _listener.Start();
            _thread = new Thread(Loop) { IsBackground = true };
            _thread.Start();
        }

        public void Stop()
        {
            if (!_running) return;
            _running = false;
            try { _listener.Stop(); } catch { }
        }

        private void Loop()
        {
            while (_running)
            {
                try
                {
                    var client = _listener.AcceptTcpClient();
                    ThreadPool.QueueUserWorkItem(delegate { ProcessClient(client); });
                }
                catch
                {
                    if (!_running) break;
                }
            }
        }

        private void ProcessClient(TcpClient client)
        {
            using (client)
            using (var stream = client.GetStream())
            {
                try
                {
                    var ctx = ReadContext(stream);
                    if (ctx == null)
                    {
                        return;
                    }

                    Handle(ctx);
                    WriteResponse(stream, ctx.Response);
                }
                catch (Exception ex)
                {
                    try
                    {
                        var response = new SimpleHttpResponse();
                        ApplyCommonHeaders(response);
                        response.StatusCode = 500;
                        response.ContentType = "text/plain; charset=utf-8";
                        byte[] bytes = Encoding.UTF8.GetBytes("Internal Server Error: " + ex.Message);
                        response.OutputStream.Write(bytes, 0, bytes.Length);
                        WriteResponse(stream, response);
                    }
                    catch
                    {
                    }
                }
            }
        }

        private static SimpleHttpContext ReadContext(NetworkStream stream)
        {
            stream.ReadTimeout = 5000;
            var headerBytes = new List<byte>();
            int matched = 0;
            while (true)
            {
                int value = stream.ReadByte();
                if (value < 0)
                {
                    break;
                }

                byte current = (byte)value;
                headerBytes.Add(current);
                switch (matched)
                {
                    case 0: matched = current == 13 ? 1 : 0; break;
                    case 1: matched = current == 10 ? 2 : 0; break;
                    case 2: matched = current == 13 ? 3 : 0; break;
                    case 3: matched = current == 10 ? 4 : 0; break;
                }

                if (matched == 4 || headerBytes.Count > 65536)
                {
                    break;
                }
            }

            if (headerBytes.Count == 0)
            {
                return null;
            }

            string headerText = Encoding.UTF8.GetString(headerBytes.ToArray());
            int splitIndex = headerText.IndexOf("\r\n\r\n", StringComparison.Ordinal);
            if (splitIndex < 0)
            {
                return null;
            }

            string[] lines = headerText.Substring(0, splitIndex).Split(new[] { "\r\n" }, StringSplitOptions.None);
            if (lines.Length == 0)
            {
                return null;
            }

            string[] requestLine = lines[0].Split(' ');
            if (requestLine.Length < 2)
            {
                return null;
            }

            string method = requestLine[0].Trim().ToUpperInvariant();
            string rawTarget = requestLine[1].Trim();
            string contentType = string.Empty;
            int contentLength = 0;

            for (int i = 1; i < lines.Length; i++)
            {
                string line = lines[i];
                int colonIndex = line.IndexOf(':');
                if (colonIndex <= 0)
                {
                    continue;
                }

                string key = line.Substring(0, colonIndex).Trim();
                string value = line.Substring(colonIndex + 1).Trim();
                if (key.Equals("Content-Type", StringComparison.OrdinalIgnoreCase))
                {
                    contentType = value;
                }
                else if (key.Equals("Content-Length", StringComparison.OrdinalIgnoreCase))
                {
                    int.TryParse(value, out contentLength);
                }
            }

            byte[] bodyBytes = new byte[Math.Max(contentLength, 0)];
            int offset = 0;
            while (offset < bodyBytes.Length)
            {
                int read = stream.Read(bodyBytes, offset, bodyBytes.Length - offset);
                if (read <= 0)
                {
                    break;
                }
                offset += read;
            }

            Uri url = new Uri("http://" + LocalNetworkHelper.GetLocalIPAddress() + rawTarget);
            return new SimpleHttpContext
            {
                Request = new SimpleHttpRequest
                {
                    HttpMethod = method,
                    Url = url,
                    QueryString = ParseQueryString(url.Query),
                    ContentType = contentType,
                    ContentEncoding = GetEncoding(contentType),
                    InputStream = new MemoryStream(bodyBytes, 0, offset, false, true)
                },
                Response = new SimpleHttpResponse()
            };
        }

        private static NameValueCollection ParseQueryString(string query)
        {
            var collection = new NameValueCollection(StringComparer.OrdinalIgnoreCase);
            if (string.IsNullOrEmpty(query))
            {
                return collection;
            }

            string raw = query.StartsWith("?") ? query.Substring(1) : query;
            string[] pairs = raw.Split('&');
            foreach (string pair in pairs)
            {
                if (string.IsNullOrEmpty(pair))
                {
                    continue;
                }

                string[] kv = pair.Split(new[] { '=' }, 2);
                string key = UrlDecodeFormComponent(kv[0]);
                string value = kv.Length > 1 ? UrlDecodeFormComponent(kv[1]) : string.Empty;
                collection[key] = value;
            }

            return collection;
        }

        private static Encoding GetEncoding(string contentType)
        {
            if (!string.IsNullOrEmpty(contentType))
            {
                int charsetIndex = contentType.IndexOf("charset=", StringComparison.OrdinalIgnoreCase);
                if (charsetIndex >= 0)
                {
                    string charset = contentType.Substring(charsetIndex + 8).Trim().TrimEnd(';');
                    try { return Encoding.GetEncoding(charset); } catch { }
                }
            }

            return Encoding.UTF8;
        }

        private static void WriteResponse(NetworkStream stream, SimpleHttpResponse response)
        {
            if (!string.IsNullOrEmpty(response.RedirectLocation))
            {
                response.Headers["Location"] = response.RedirectLocation;
            }

            byte[] body = response.OutputStream.ToArray();
            if (!response.Headers.ContainsKey("Content-Length"))
            {
                response.Headers["Content-Length"] = body.Length.ToString();
            }
            if (!string.IsNullOrEmpty(response.ContentType) && !response.Headers.ContainsKey("Content-Type"))
            {
                response.Headers["Content-Type"] = response.ContentType;
            }
            response.Headers["Connection"] = "close";

            var headerBuilder = new StringBuilder();
            headerBuilder.Append("HTTP/1.1 ").Append(response.StatusCode).Append(' ').Append(GetStatusDescription(response.StatusCode)).Append("\r\n");
            foreach (var header in response.Headers)
            {
                headerBuilder.Append(header.Key).Append(": ").Append(header.Value).Append("\r\n");
            }
            headerBuilder.Append("\r\n");

            byte[] headerBytes = Encoding.UTF8.GetBytes(headerBuilder.ToString());
            stream.Write(headerBytes, 0, headerBytes.Length);
            if (body.Length > 0)
            {
                stream.Write(body, 0, body.Length);
            }
        }

        private static string GetStatusDescription(int statusCode)
        {
            switch (statusCode)
            {
                case 200: return "OK";
                case 204: return "No Content";
                case 302: return "Found";
                case 400: return "Bad Request";
                case 403: return "Forbidden";
                case 404: return "Not Found";
                case 500: return "Internal Server Error";
                default: return "OK";
            }
        }

        private void Handle(SimpleHttpContext ctx)
        {
            ApplyCommonHeaders(ctx.Response);

            if (ctx.Request.HttpMethod == "OPTIONS")
            {
                RespondEmpty(ctx, 204);
                return;
            }

            string path = ctx.Request.Url.AbsolutePath.ToLowerInvariant();
            if (path == "/api/dashboard" || path == "/dashboard-data")
            {
                var payload = new DashboardDataBuilder(_store.Snapshot()).Build();
                Respond(ctx, 200, "application/json", SerializeDashboard(payload));
                return;
            }
            if (path == "/api/limit-count" && ctx.Request.HttpMethod == "GET")
            {
                RuntimeConfig config = RuntimeConfig.LoadDefault();
                Respond(ctx, 200, "application/json", SerializeLimitCount(config.LimitCount));
                return;
            }
            if (path == "/api/limit-count" && ctx.Request.HttpMethod == "POST")
            {
                using (var reader = new StreamReader(ctx.Request.InputStream, ctx.Request.ContentEncoding))
                {
                    string body = reader.ReadToEnd();
                    string contentType = ctx.Request.ContentType ?? string.Empty;
                    int limitCount = ReadLimitCountFromRequest(body, contentType);
                    if (limitCount <= 0)
                    {
                        Respond(ctx, 400, "application/json", "{\"message\":\"limitCount must be greater than 0\"}");
                        return;
                    }

                    string errorMessage;
                    if (!TrySaveLimitCount(limitCount, out errorMessage))
                    {
                        Respond(ctx, 500, "application/json", "{\"message\":\"" + Escape(errorMessage) + "\"}");
                        return;
                    }

                    Respond(ctx, 200, "application/json", SerializeLimitCount(limitCount));
                    return;
                }
            }
            if (path == "/api/abnormal/close" && ctx.Request.HttpMethod == "POST")
            {
                using (var reader = new StreamReader(ctx.Request.InputStream, ctx.Request.ContentEncoding))
                {
                    string body = reader.ReadToEnd();
                    string messageId = ExtractStringValue(body, "id");
                    if (string.IsNullOrEmpty(messageId))
                    {
                        Respond(ctx, 400, "application/json", "{\"message\":\"id is required\"}");
                        return;
                    }

                    bool handled = AbnormalMessageState.MarkHandled(messageId);
                    if (!handled)
                    {
                        Respond(ctx, 404, "application/json", "{\"message\":\"message not found\"}");
                        return;
                    }

                    Respond(ctx, 200, "application/json", SerializeAbnormalCloseResult(messageId, true));
                    return;
                }
            }
            if (path == "/api/devices")
            {
                Respond(ctx, 200, "application/json", SerializeDevices());
                return;
            }
            if (path == "/api/acs-events/history" && ctx.Request.HttpMethod == "POST")
            {
                using (var reader = new StreamReader(ctx.Request.InputStream, ctx.Request.ContentEncoding))
                {
                    string body = reader.ReadToEnd();
                    string deviceIP = ExtractStringValue(body, "deviceIP");
                    string startTime = ExtractStringValue(body, "startTime");
                    string endTime = ExtractStringValue(body, "endTime");
                    if (string.IsNullOrEmpty(deviceIP) || string.IsNullOrEmpty(startTime) || string.IsNullOrEmpty(endTime))
                    {
                        Respond(ctx, 400, "application/json", "{\"message\":\"deviceIP, startTime and endTime are required\"}");
                        return;
                    }

                    int major = body.IndexOf("\"major\"", StringComparison.OrdinalIgnoreCase) >= 0
                        ? ExtractIntValue(body, "major")
                        : 5;
                    int minor = body.IndexOf("\"minor\"", StringComparison.OrdinalIgnoreCase) >= 0
                        ? ExtractIntValue(body, "minor")
                        : 0;

                    var query = new AcsEventHistoryQuery
                    {
                        DeviceIP = deviceIP,
                        StartTime = startTime,
                        EndTime = endTime,
                        Major = major,
                        Minor = minor,
                        MaxResults = ExtractIntValue(body, "maxResults") > 0 ? ExtractIntValue(body, "maxResults") : 30,
                        SearchResultPosition = ExtractIntValue(body, "searchResultPosition"),
                        FetchAll = !string.Equals(ExtractStringValue(body, "fetchAll"), "false", StringComparison.OrdinalIgnoreCase),
                        MaxTotal = ExtractIntValue(body, "maxTotal") > 0 ? ExtractIntValue(body, "maxTotal") : 500
                    };

                    AcsEventHistoryResult history = AcsEventIsapiClient.QueryHistory(query);
                    if (!string.IsNullOrEmpty(history.ErrorMessage))
                    {
                        Respond(ctx, 502, "application/json", SerializeHistoryError(history.ErrorMessage));
                        return;
                    }

                    Respond(ctx, 200, "application/json", SerializeHistoryResult(history));
                    return;
                }
            }
            if (path == "/api/employee")
            {
                string keyword = (ctx.Request.QueryString["q"] ?? string.Empty).Trim();
                Respond(ctx, 200, "application/json", SerializeEmployees(FilterEmployees(keyword)));
                return;
            }
            if (path == "/api/employee/search")
            {
                string keyword = (ctx.Request.QueryString["q"] ?? string.Empty).Trim();
                Respond(ctx, 200, "application/json", SerializeEmployees(FilterEmployees(keyword)));
                return;
            }
            if (path == "/health")
            {
                Respond(ctx, 200, "application/json", "{\"status\":\"ok\"}");
                return;
            }
            if (path == "/config/edit" && ctx.Request.HttpMethod == "GET")
            {
                string configPath = ConfigPaths.DeviceConfigPath;
                string json = string.Empty;
                try
                {
                    if (File.Exists(configPath))
                    {
                        json = File.ReadAllText(configPath, Encoding.UTF8);
                    }
                }
                catch
                {
                }

                bool saved = string.Equals(ctx.Request.QueryString["saved"], "1", StringComparison.OrdinalIgnoreCase);
                string page = BuildEditorPage(json, saved);
                Respond(ctx, 200, "text/html; charset=utf-8", page);
                return;
            }
            if (path == "/events")
            {
                Respond(ctx, 200, "application/json", SerializeEvents(_store.Snapshot()));
                return;
            }
            if (path == "/config" && ctx.Request.HttpMethod == "GET")
            {
                string configPath = ConfigPaths.DeviceConfigPath;
                if (!File.Exists(configPath))
                {
                    Respond(ctx, 404, "text/plain", "DeviceConfig.json not found");
                    return;
                }

                Respond(ctx, 200, "application/json", File.ReadAllText(configPath, Encoding.UTF8));
                return;
            }
            if (path == "/config" && ctx.Request.HttpMethod == "POST")
            {
                using (var reader = new StreamReader(ctx.Request.InputStream, ctx.Request.ContentEncoding))
                {
                    string body = reader.ReadToEnd();
                    string contentType = ctx.Request.ContentType ?? string.Empty;
                    string jsonPayload = body;
                    if (contentType.StartsWith("application/x-www-form-urlencoded", StringComparison.OrdinalIgnoreCase))
                    {
                        var form = ParseForm(body);
                        form.TryGetValue("json", out jsonPayload);
                    }

                    try
                    {
                        var document = System.Text.Json.JsonSerializer.Deserialize<DeviceConfigDocument>(
                            jsonPayload ?? string.Empty,
                            new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                        if (document == null)
                        {
                            throw new InvalidOperationException("配置内容为空");
                        }

                        DeviceConfigStore.Save(ConfigPaths.DeviceConfigPath, document);
                        if (contentType.StartsWith("application/x-www-form-urlencoded", StringComparison.OrdinalIgnoreCase))
                        {
                            ctx.Response.StatusCode = 302;
                            ctx.Response.RedirectLocation = "/config/edit?saved=1";
                            return;
                        }

                        Respond(ctx, 200, "text/plain", "OK");
                    }
                    catch (Exception ex)
                    {
                        Respond(ctx, 400, "text/plain", "Invalid JSON: " + ex.Message);
                    }
                }
                return;
            }
            if (path == "/api/acs-events/picture" && ctx.Request.HttpMethod == "GET")
            {
                HandleAcsEventPictureRequest(ctx);
                return;
            }
            if (path.StartsWith("/images/"))
            {
                HandleImageRequest(ctx, path);
                return;
            }
            if (path == "/images")
            {
                Respond(ctx, 200, "text/html; charset=utf-8", BuildImageListPage());
                return;
            }

            Respond(ctx, 200, "text/html; charset=utf-8", BuildHomePage());
        }

        private static void ApplyCommonHeaders(SimpleHttpResponse response)
        {
            response.Headers["Access-Control-Allow-Origin"] = "*";
            response.Headers["Access-Control-Allow-Methods"] = "GET,POST,OPTIONS";
            response.Headers["Access-Control-Allow-Headers"] = "Content-Type";
            response.Headers["Cache-Control"] = "no-cache";
        }

        private void Respond(SimpleHttpContext ctx, int status, string contentType, string content)
        {
            ctx.Response.StatusCode = status;
            ctx.Response.ContentType = contentType;
            ctx.Response.ContentEncoding = Encoding.UTF8;
            ctx.Response.OutputStream.SetLength(0);
            byte[] bytes = Encoding.UTF8.GetBytes(content ?? string.Empty);
            ctx.Response.OutputStream.Write(bytes, 0, bytes.Length);
        }

        private void RespondEmpty(SimpleHttpContext ctx, int status)
        {
            ctx.Response.StatusCode = status;
            ctx.Response.OutputStream.SetLength(0);
        }
        private string BuildHomePage()
        {
            var sb = new StringBuilder();
            sb.Append("<html><head><meta charset=\"utf-8\"><title>GetACSEvent Web</title>");
            sb.Append("<style>body{font-family:Segoe UI,Arial,sans-serif;max-width:900px;margin:24px auto;padding:0 16px;color:#1f2937;} h1{margin-bottom:8px;} ul{line-height:1.9;} code{background:#f3f4f6;padding:2px 6px;border-radius:6px;}</style>");
            sb.Append("</head><body>");
            sb.Append("<h1>GetACSEvent 独立服务</h1>");
            sb.Append("<p>当前服务已同时支持门禁事件查看、配置编辑，以及供前端看板使用的数据接口。</p>");
            sb.Append("<ul>");
            sb.Append("<li><a href=\"/api/dashboard\">/api/dashboard</a> - newdemo 看板聚合接口</li>");
            sb.Append("<li><a href=\"/api/limit-count\">/api/limit-count</a> - 限制人数读取/更新接口</li>");
            sb.Append("<li><a href=\"/api/employee\">/api/employee</a> - 本地人员表 JSON 接口</li>");
            sb.Append("<li><a href=\"/api/employee/search?q=yq\">/api/employee/search?q=关键字</a> - 人员检索接口</li>");
            sb.Append("<li><a href=\"/events\">/events</a> - 最近事件原始 JSON</li>");
            sb.Append("<li><a href=\"/api/devices\">/api/devices</a> - 已启用门禁设备列表</li>");
            sb.Append("<li>POST /api/acs-events/history - 按时间范围查询设备历史事件</li>");
            sb.Append("<li><a href=\"/config\">/config</a> - 当前设备配置 JSON</li>");
            sb.Append("<li><a href=\"/config/edit\">/config/edit</a> - 在线编辑配置</li>");
            sb.Append("<li><a href=\"/images\">/images</a> - 事件图片列表</li>");
            sb.Append("<li><a href=\"/health\">/health</a> - 健康检查</li>");
            sb.Append("</ul>");
            sb.Append("<p>前端默认读取 <code>http://" + LocalNetworkHelper.GetLocalIPAddress() + ":" + _port + "/api/dashboard</code>，也可以通过 <code>VITE_API_BASE_URL</code> 指向其他地址。</p>");
            sb.Append("</body></html>");
            return sb.ToString();
        }

        private string BuildEditorPage(string json, bool saved)
        {
            string escaped = System.Security.SecurityElement.Escape(json ?? string.Empty) ?? string.Empty;
            var sb = new StringBuilder();
            sb.Append("<html><head><meta charset=\"utf-8\"><title>编辑配置</title>");
            sb.Append("<style>textarea{width:100%;height:70vh;font-family:Consolas,monospace;font-size:12px;} body{max-width:1000px;margin:20px auto;padding:0 10px;} .bar{margin:10px 0}</style>");
            sb.Append("</head><body><h3>编辑 DeviceConfig.json</h3>");
            if (saved)
            {
                sb.Append("<div style=\"padding:8px 12px;background:#e6ffed;border:1px solid #b7eb8f;color:#389e0d;margin-bottom:10px;\">保存成功</div>");
            }
            sb.Append("<div class=\"bar\"><a href=\"/config\" target=\"_blank\">查看JSON</a> | <a href=\"/\">首页</a></div>");
            sb.Append("<form method=\"post\" action=\"/config\">\n");
            sb.Append("<textarea name=\"json\">").Append(escaped).Append("</textarea>\n");
            sb.Append("<div class=\"bar\"><button type=\"submit\">保存</button></div>\n");
            sb.Append("</form></body></html>");
            return sb.ToString();
        }

        private static Dictionary<string, string> ParseForm(string body)
        {
            var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (string.IsNullOrEmpty(body)) return dict;
            string[] parts = body.Split('&');
            foreach (string part in parts)
            {
                string[] kv = part.Split(new char[] { '=' }, 2);
                string rawKey = kv[0] ?? string.Empty;
                string rawValue = kv.Length > 1 ? kv[1] : string.Empty;
                dict[UrlDecodeFormComponent(rawKey)] = UrlDecodeFormComponent(rawValue);
            }
            return dict;
        }

        private static string UrlDecodeFormComponent(string value)
        {
            if (string.IsNullOrEmpty(value)) return string.Empty;
            value = value.Replace('+', ' ');
            try { return Uri.UnescapeDataString(value); } catch { return value; }
        }

        private static string SerializeDashboard(DashboardPayload payload)
        {
            var sb = new StringBuilder();
            sb.Append('{');
            AppendJson(sb, "generatedAt", payload.GeneratedAt); sb.Append(',');
            AppendMetrics(sb, payload.Metrics); sb.Append(',');
            AppendAlarms(sb, payload.Alarms); sb.Append(',');
            AppendRecords(sb, "recentRecords", payload.RecentRecords); sb.Append(',');
            AppendRecords(sb, "stayPeople", payload.StayPeople); sb.Append(',');
            AppendJson(sb, "selectedRecordId", payload.SelectedRecordId); sb.Append(',');
            AppendAreaAlert(sb, payload.AreaAlert); sb.Append(',');
            AppendAbnormalMessages(sb, payload.AbnormalMessages);
            sb.Append('}');
            return sb.ToString();
        }

        private static string SerializeLimitCount(int limitCount)
        {
            var sb = new StringBuilder();
            sb.Append('{');
            AppendJsonNumber(sb, "limitCount", limitCount);
            sb.Append('}');
            return sb.ToString();
        }

        private static string SerializeAbnormalCloseResult(string id, bool success)
        {
            var sb = new StringBuilder();
            sb.Append('{');
            AppendJson(sb, "id", id); sb.Append(',');
            AppendJsonBool(sb, "success", success);
            sb.Append('}');
            return sb.ToString();
        }

        private static int ReadLimitCountFromRequest(string body, string contentType)
        {
            if (string.IsNullOrEmpty(body))
            {
                return 0;
            }

            if (!string.IsNullOrEmpty(contentType) &&
                contentType.StartsWith("application/x-www-form-urlencoded", StringComparison.OrdinalIgnoreCase))
            {
                Dictionary<string, string> form = ParseForm(body);
                string formValue;
                if (form.TryGetValue("limitCount", out formValue))
                {
                    int formLimit;
                    if (int.TryParse((formValue ?? string.Empty).Trim(), out formLimit))
                    {
                        return formLimit;
                    }
                }
            }

            return ExtractIntValue(body, "limitCount");
        }

        private static int ExtractIntValue(string body, string key)
        {
            if (string.IsNullOrEmpty(body) || string.IsNullOrEmpty(key))
            {
                return 0;
            }

            string pattern = "\"" + key + "\"";
            int keyIndex = body.IndexOf(pattern, StringComparison.OrdinalIgnoreCase);
            if (keyIndex < 0)
            {
                return 0;
            }

            int colonIndex = body.IndexOf(':', keyIndex + pattern.Length);
            if (colonIndex < 0)
            {
                return 0;
            }

            int index = colonIndex + 1;
            while (index < body.Length && char.IsWhiteSpace(body[index]))
            {
                index++;
            }

            int end = index;
            while (end < body.Length && (char.IsDigit(body[end]) || body[end] == '-'))
            {
                end++;
            }

            int value;
            return int.TryParse(body.Substring(index, end - index), out value) ? value : 0;
        }

        private static string ExtractStringValue(string body, string key)
        {
            string raw = ExtractRawValue(body, key);
            if (string.IsNullOrEmpty(raw))
            {
                return string.Empty;
            }

            raw = raw.Trim();
            if (raw.StartsWith("\"") && raw.EndsWith("\"") && raw.Length >= 2)
            {
                raw = raw.Substring(1, raw.Length - 2);
            }
            return UnescapeJson(raw);
        }

        private static string ExtractRawValue(string body, string key)
        {
            if (string.IsNullOrEmpty(body) || string.IsNullOrEmpty(key))
            {
                return string.Empty;
            }

            string pattern = "\"" + key + "\"";
            int keyIndex = body.IndexOf(pattern, StringComparison.OrdinalIgnoreCase);
            if (keyIndex < 0)
            {
                return string.Empty;
            }

            int colonIndex = body.IndexOf(':', keyIndex + pattern.Length);
            if (colonIndex < 0)
            {
                return string.Empty;
            }

            int index = colonIndex + 1;
            while (index < body.Length && char.IsWhiteSpace(body[index]))
            {
                index++;
            }

            if (index >= body.Length)
            {
                return string.Empty;
            }

            if (body[index] == '"')
            {
                int end = index + 1;
                bool escaped = false;
                while (end < body.Length)
                {
                    char ch = body[end];
                    if (ch == '"' && !escaped)
                    {
                        return body.Substring(index, end - index + 1);
                    }
                    escaped = ch == '\\' && !escaped;
                    if (ch != '\\')
                    {
                        escaped = false;
                    }
                    end++;
                }
                return body.Substring(index);
            }

            int tail = index;
            while (tail < body.Length && body[tail] != ',' && body[tail] != '}' && body[tail] != ']')
            {
                tail++;
            }

            return body.Substring(index, tail - index).Trim();
        }

        private static bool TrySaveLimitCount(int limitCount, out string errorMessage)
        {
            return DeviceConfigStore.UpdateLimitCount(limitCount, out errorMessage);
        }
        private static void AppendAreaAlert(StringBuilder sb, AreaAlertInfo areaAlert)
        {
            sb.Append("\"areaAlert\":");
            if (areaAlert == null)
            {
                sb.Append("null");
                return;
            }

            sb.Append('{');
            AppendJsonBool(sb, "isActive", areaAlert.IsActive); sb.Append(',');
            AppendJsonNumber(sb, "hasPeople", areaAlert.HasPeople); sb.Append(',');
            AppendJson(sb, "zoneName", areaAlert.ZoneName); sb.Append(',');
            AppendJson(sb, "alertId", areaAlert.AlertId); sb.Append(',');
            AppendJson(sb, "triggeredAt", areaAlert.TriggeredAt); sb.Append(',');
            AppendJson(sb, "updatedAt", areaAlert.UpdatedAt); sb.Append(',');
            AppendJson(sb, "sourceTopic", areaAlert.SourceTopic); sb.Append(',');
            AppendJson(sb, "rawPayload", areaAlert.RawPayload);
            sb.Append('}');
        }

        private static void AppendAbnormalMessages(StringBuilder sb, List<AbnormalMessageInfo> messages)
        {
            sb.Append("\"abnormalMessages\":[");
            if (messages != null)
            {
                for (int i = 0; i < messages.Count; i++)
                {
                    if (i > 0)
                    {
                        sb.Append(',');
                    }

                    sb.Append('{');
                    AppendJson(sb, "id", messages[i].Id); sb.Append(',');
                    AppendJson(sb, "type", messages[i].Type); sb.Append(',');
                    AppendJson(sb, "time", messages[i].Time); sb.Append(',');
                    AppendJson(sb, "topic", messages[i].Topic); sb.Append(',');
                    AppendJson(sb, "receivedAt", messages[i].ReceivedAt); sb.Append(',');
                    AppendJson(sb, "updatedAt", messages[i].UpdatedAt); sb.Append(',');
                    AppendJson(sb, "rawPayload", messages[i].RawPayload); sb.Append(',');
                    AppendJsonBool(sb, "isHandled", messages[i].IsHandled); sb.Append(',');
                    AppendJson(sb, "handledAt", messages[i].HandledAt); sb.Append(',');
                    AppendJson(sb, "status", messages[i].Status);
                    sb.Append('}');
                }
            }
            sb.Append(']');
        }

        private static void AppendMetrics(StringBuilder sb, List<DashboardMetric> metrics)
        {
            sb.Append("\"metrics\":[");
            if (metrics != null)
            {
                for (int i = 0; i < metrics.Count; i++)
                {
                    if (i > 0) sb.Append(',');
                    sb.Append('{');
                    AppendJson(sb, "label", metrics[i].Label); sb.Append(',');
                    AppendJsonNumber(sb, "value", metrics[i].Value); sb.Append(',');
                    AppendJson(sb, "unit", metrics[i].Unit); sb.Append(',');
                    AppendJson(sb, "accent", metrics[i].Accent);
                    sb.Append('}');
                }
            }
            sb.Append(']');
        }

        private static void AppendAlarms(StringBuilder sb, List<DashboardAlarm> alarms)
        {
            sb.Append("\"alarms\":[");
            if (alarms != null)
            {
                for (int i = 0; i < alarms.Count; i++)
                {
                    if (i > 0) sb.Append(',');
                    sb.Append('{');
                    AppendJson(sb, "id", alarms[i].Id); sb.Append(',');
                    AppendJson(sb, "code", alarms[i].Code); sb.Append(',');
                    AppendJson(sb, "category", alarms[i].Category); sb.Append(',');
                    AppendJson(sb, "level", alarms[i].Level); sb.Append(',');
                    AppendJson(sb, "title", alarms[i].Title); sb.Append(',');
                    AppendJson(sb, "detail", alarms[i].Detail); sb.Append(',');
                    AppendJson(sb, "status", alarms[i].Status); sb.Append(',');
                    AppendJson(sb, "targetId", alarms[i].TargetId); sb.Append(',');
                    AppendJson(sb, "targetName", alarms[i].TargetName); sb.Append(',');
                    AppendJson(sb, "gate", alarms[i].Gate); sb.Append(',');
                    AppendJson(sb, "deviceIP", alarms[i].DeviceIP); sb.Append(',');
                    AppendJson(sb, "triggeredAt", alarms[i].TriggeredAt);
                    sb.Append('}');
                }
            }
            sb.Append(']');
        }

        private static void AppendRecords(StringBuilder sb, string key, List<DashboardRecord> records)
        {
            sb.Append('"').Append(Escape(key)).Append('"').Append(':').Append('[');
            if (records != null)
            {
                for (int i = 0; i < records.Count; i++)
                {
                    if (i > 0) sb.Append(',');
                    AppendRecord(sb, records[i]);
                }
            }
            sb.Append(']');
        }

        private static void AppendRecord(StringBuilder sb, DashboardRecord record)
        {
            sb.Append('{');
            AppendJson(sb, "id", record.Id); sb.Append(',');
            AppendJson(sb, "name", record.Name); sb.Append(',');
            AppendJson(sb, "department", record.Department); sb.Append(',');
            AppendJson(sb, "role", record.Role); sb.Append(',');
            AppendJson(sb, "enterTime", record.EnterTime); sb.Append(',');
            AppendJson(sb, "gate", record.Gate); sb.Append(',');
            AppendJson(sb, "avatarText", record.AvatarText); sb.Append(',');
            AppendJson(sb, "card", record.Card); sb.Append(',');
            AppendJson(sb, "location", record.Location); sb.Append(',');
            AppendJson(sb, "phone", record.Phone); sb.Append(',');
            AppendJson(sb, "team", record.Team); sb.Append(',');
            AppendJson(sb, "status", record.Status); sb.Append(',');
            AppendJson(sb, "stayDuration", record.StayDuration); sb.Append(',');
            AppendJson(sb, "direction", record.Direction); sb.Append(',');
            AppendJson(sb, "deviceIP", record.DeviceIP); sb.Append(',');
            AppendJson(sb, "imageUrl", ReplaceLocalhostWithIP(record.ImageUrl)); sb.Append(',');
            AppendJsonBool(sb, "isWarning", record.IsWarning);
            sb.Append('}');
        }

        private static string SerializeDevices()
        {
            var devices = DeviceConfigStore.GetEnabledDevices();
            var sb = new StringBuilder();
            sb.Append('[');
            for (int i = 0; i < devices.Count; i++)
            {
                var device = devices[i];
                if (i > 0)
                {
                    sb.Append(',');
                }

                sb.Append('{');
                AppendJson(sb, "ip", device.IP); sb.Append(',');
                AppendJson(sb, "name", device.Name); sb.Append(',');
                AppendJson(sb, "deviceName", device.DeviceName); sb.Append(',');
                AppendJson(sb, "direction", device.Direction); sb.Append(',');
                sb.Append("\"httpPort\":").Append(device.HttpPort > 0 ? device.HttpPort : 80); sb.Append(',');
                AppendJsonBool(sb, "useHttps", device.UseHttps);
                sb.Append('}');
            }
            sb.Append(']');
            return sb.ToString();
        }

        private static string SerializeHistoryError(string message)
        {
            var sb = new StringBuilder();
            sb.Append('{');
            AppendJson(sb, "message", message);
            sb.Append('}');
            return sb.ToString();
        }

        private static string SerializeHistoryResult(AcsEventHistoryResult history)
        {
            var sb = new StringBuilder();
            sb.Append('{');
            sb.Append("\"events\":");
            sb.Append(SerializeEvents(history.Events ?? new List<AcsEvent>())); sb.Append(',');
            sb.Append("\"totalMatches\":").Append(history.TotalMatches); sb.Append(',');
            AppendJsonBool(sb, "hasMore", history.HasMore);
            sb.Append('}');
            return sb.ToString();
        }

        private static string SerializeEvents(List<AcsEvent> list)
        {
            list.Sort(delegate (AcsEvent a, AcsEvent b)
            {
                return b.TimeUtc.CompareTo(a.TimeUtc);
            });

            var sb = new StringBuilder();
            sb.Append('[');
            for (int i = 0; i < list.Count; i++)
            {
                AcsEvent e = list[i];
                if (i > 0) sb.Append(',');
                sb.Append('{');
                AppendJson(sb, "time", e.Time); sb.Append(',');
                AppendJson(sb, "timeUtc", e.TimeUtc.ToString("o")); sb.Append(',');
                AppendJson(sb, "deviceIP", e.DeviceIP); sb.Append(',');
                AppendJson(sb, "deviceName", e.DeviceName); sb.Append(',');
                AppendJson(sb, "deviceID", e.DeviceID); sb.Append(',');
                AppendJson(sb, "areaID", e.AreaID); sb.Append(',');
                AppendJson(sb, "remark", e.Remark); sb.Append(',');
                AppendJson(sb, "majorType", e.MajorType); sb.Append(',');
                AppendJson(sb, "minorType", e.MinorType); sb.Append(',');
                AppendJson(sb, "cardNo", e.CardNo); sb.Append(',');
                AppendJson(sb, "employeeNo", e.EmployeeNo); sb.Append(',');
                AppendJson(sb, "personName", e.PersonName); sb.Append(',');
                AppendJson(sb, "cardType", e.CardType); sb.Append(',');
                sb.Append("\"doorNo\":").Append(e.DoorNo);
                if (!string.IsNullOrEmpty(e.Direction))
                {
                    sb.Append(',');
                    AppendJson(sb, "direction", e.Direction);
                }
                if (!string.IsNullOrEmpty(e.ImageUrl))
                {
                    sb.Append(',');
                    AppendJson(sb, "imageUrl", ReplaceLocalhostWithIP(e.ImageUrl));
                }
                sb.Append('}');
            }
            sb.Append(']');
            return sb.ToString();
        }

        private static List<Dictionary<string, string>> FilterEmployees(string keyword)
        {
            List<Dictionary<string, string>> employees = EmployeeDirectory.Snapshot();
            if (string.IsNullOrEmpty(keyword))
            {
                return employees;
            }

            string expected = keyword.Trim();
            var result = new List<Dictionary<string, string>>();
            foreach (var employee in employees)
            {
                if (employee == null)
                {
                    continue;
                }

                bool matched = false;
                foreach (var pair in employee)
                {
                    string value = (pair.Value ?? string.Empty).Trim();
                    if (value.IndexOf(expected, StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        matched = true;
                        break;
                    }
                }

                if (matched)
                {
                    result.Add(employee);
                }
            }

            return result;
        }

        private static string SerializeEmployees(List<Dictionary<string, string>> employees)
        {
            var sb = new StringBuilder();
            sb.Append('[');
            if (employees != null)
            {
                for (int i = 0; i < employees.Count; i++)
                {
                    if (i > 0)
                    {
                        sb.Append(',');
                    }

                    sb.Append('{');
                    bool first = true;
                    foreach (var pair in employees[i])
                    {
                        if (!first)
                        {
                            sb.Append(',');
                        }

                        AppendJson(sb, pair.Key, pair.Value);
                        first = false;
                    }
                    sb.Append('}');
                }
            }
            sb.Append(']');
            return sb.ToString();
        }

        private static void AppendJson(StringBuilder sb, string key, string value)
        {
            sb.Append('"').Append(Escape(key)).Append('"').Append(':');
            if (value == null)
            {
                sb.Append("null");
            }
            else
            {
                sb.Append('"').Append(Escape(value)).Append('"');
            }
        }

        private static void AppendJsonNumber(StringBuilder sb, string key, int value)
        {
            sb.Append('"').Append(Escape(key)).Append('"').Append(':').Append(value);
        }

        private static void AppendJsonBool(StringBuilder sb, string key, bool value)
        {
            sb.Append('"').Append(Escape(key)).Append('"').Append(':').Append(value ? "true" : "false");
        }

        private static string Escape(string value)
        {
            if (string.IsNullOrEmpty(value)) return value ?? string.Empty;
            var sb = new StringBuilder();
            foreach (char ch in value)
            {
                switch (ch)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\b': sb.Append("\\b"); break;
                    case '\f': sb.Append("\\f"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default:
                        if (ch < 32)
                        {
                            sb.AppendFormat("\\u{0:X4}", (int)ch);
                        }
                        else
                        {
                            sb.Append(ch);
                        }
                        break;
                }
            }
            return sb.ToString();
        }

        private static string UnescapeJson(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return value ?? string.Empty;
            }

            return value
                .Replace("\\\"", "\"")
                .Replace("\\r", "\r")
                .Replace("\\n", "\n")
                .Replace("\\t", "\t")
                .Replace("\\\\", "\\");
        }

        private void HandleAcsEventPictureRequest(SimpleHttpContext ctx)
        {
            try
            {
                string deviceIP = (ctx.Request.QueryString["deviceIP"] ?? string.Empty).Trim();
                string source = (ctx.Request.QueryString["source"] ?? string.Empty).Trim();
                string serialNo = (ctx.Request.QueryString["serialNo"] ?? string.Empty).Trim();
                string employeeNo = (ctx.Request.QueryString["employeeNo"] ?? string.Empty).Trim();

                if (string.IsNullOrEmpty(deviceIP))
                {
                    Respond(ctx, 400, "text/plain", "deviceIP is required");
                    return;
                }

                byte[] imageBytes = AcsEventImageResolver.DownloadPicture(deviceIP, source, serialNo, employeeNo);
                if (imageBytes == null || imageBytes.Length == 0)
                {
                    Respond(ctx, 404, "text/plain", "Image not found");
                    return;
                }

                string contentType = imageBytes.Length >= 2 && imageBytes[0] == 0x89 && imageBytes[1] == 0x50
                    ? "image/png"
                    : "image/jpeg";
                RespondBinary(ctx, 200, contentType, imageBytes);
            }
            catch (Exception ex)
            {
                Respond(ctx, 500, "text/plain", "Internal server error: " + ex.Message);
            }
        }

        private void HandleImageRequest(SimpleHttpContext ctx, string path)
        {
            try
            {
                string imagePath = path.Substring("/images/".Length);
                if (string.IsNullOrEmpty(imagePath))
                {
                    Respond(ctx, 400, "text/plain", "Invalid image path");
                    return;
                }

                string decodedImagePath = Uri.UnescapeDataString(imagePath);
                string pictureDir = Path.GetFullPath("D:/Picture");
                string fullImagePath = Path.GetFullPath(Path.Combine(pictureDir, decodedImagePath));
                if (!fullImagePath.StartsWith(pictureDir, StringComparison.OrdinalIgnoreCase))
                {
                    Respond(ctx, 403, "text/plain", "Access denied");
                    return;
                }
                if (!File.Exists(fullImagePath))
                {
                    Respond(ctx, 404, "text/plain", "Image not found: " + decodedImagePath);
                    return;
                }

                RespondBinary(ctx, 200, GetContentType(Path.GetExtension(fullImagePath)), File.ReadAllBytes(fullImagePath));
            }
            catch (Exception ex)
            {
                Respond(ctx, 500, "text/plain", "Internal server error: " + ex.Message);
            }
        }

        private string BuildImageListPage()
        {
            var sb = new StringBuilder();
            sb.Append("<html><head><meta charset=\"utf-8\"><title>门禁事件图片列表</title>");
            sb.Append("<style>");
            sb.Append("body{font-family:Arial,sans-serif;margin:20px;background:#f5f5f5;}");
            sb.Append(".container{max-width:1200px;margin:0 auto;background:white;padding:20px;border-radius:8px;box-shadow:0 2px 10px rgba(0,0,0,0.1);}");
            sb.Append("h1{color:#333;border-bottom:2px solid #007acc;padding-bottom:10px;}");
            sb.Append(".image-grid{display:grid;grid-template-columns:repeat(auto-fill,minmax(300px,1fr));gap:20px;margin-top:20px;}");
            sb.Append(".image-item{border:1px solid #ddd;border-radius:8px;padding:15px;background:#fafafa;}");
            sb.Append(".image-item img{width:100%;height:200px;object-fit:cover;border-radius:4px;cursor:pointer;}");
            sb.Append(".image-info{margin-top:10px;font-size:12px;color:#666;}");
            sb.Append(".image-url{background:#f0f0f0;padding:5px;border-radius:3px;font-family:monospace;font-size:11px;word-break:break-all;margin-top:5px;}");
            sb.Append(".no-images{text-align:center;color:#999;font-style:italic;padding:40px;}");
            sb.Append("</style>");
            sb.Append("<script>");
            sb.Append("function copyUrl(url) {");
            sb.Append("  navigator.clipboard.writeText(url).then(function() {");
            sb.Append("    alert('URL已复制到剪贴板');");
            sb.Append("  }).catch(function() {");
            sb.Append("    var textArea = document.createElement('textarea');");
            sb.Append("    textArea.value = url;");
            sb.Append("    document.body.appendChild(textArea);");
            sb.Append("    textArea.select();");
            sb.Append("    document.execCommand('copy');");
            sb.Append("    document.body.removeChild(textArea);");
            sb.Append("    alert('URL已复制到剪贴板');");
            sb.Append("  });");
            sb.Append("}");
            sb.Append("</script>");
            sb.Append("</head><body>");
            sb.Append("<div class=\"container\">");
            sb.Append("<h1>门禁事件图片列表</h1>");

            try
            {
                string pictureDir = Path.GetFullPath("D:/Picture");
                if (!Directory.Exists(pictureDir))
                {
                    sb.Append("<div class=\"no-images\">图片目录不存在</div>");
                }
                else
                {
                    List<string> imageFiles = GetImageFiles(pictureDir);
                    if (imageFiles.Count == 0)
                    {
                        sb.Append("<div class=\"no-images\">暂无图片</div>");
                    }
                    else
                    {
                        sb.Append("<div class=\"image-grid\">");
                        foreach (string imageFile in imageFiles)
                        {
                            string relativePath = GetRelativePath(pictureDir, imageFile);
                            string encodedPath = Uri.EscapeDataString(relativePath.Replace('\\', '/')).Replace("%2F", "/");
                            string imageUrl = "/images/" + encodedPath;
                            string fileName = Path.GetFileName(imageFile);
                            string fileSize = GetFileSizeString(new FileInfo(imageFile).Length);
                            string fileTime = File.GetLastWriteTime(imageFile).ToString("yyyy-MM-dd HH:mm:ss");

                            sb.Append("<div class=\"image-item\">");
                            sb.Append("<img src=\"").Append(imageUrl).Append("\" alt=\"").Append(fileName).Append("\" onclick=\"window.open('").Append(imageUrl).Append("')\" title=\"点击查看大图\">");
                            sb.Append("<div class=\"image-info\">");
                            sb.Append("<div><strong>设备:</strong> ").Append(GetPathInfo(relativePath)).Append("</div>");
                            sb.Append("<div><strong>文件名:</strong> ").Append(fileName).Append("</div>");
                            sb.Append("<div><strong>大小:</strong> ").Append(fileSize).Append("</div>");
                            sb.Append("<div><strong>时间:</strong> ").Append(fileTime).Append("</div>");
                            sb.Append("<div class=\"image-url\" onclick=\"copyUrl('").Append(imageUrl).Append("')\" title=\"点击复制URL\">URL: ").Append(imageUrl).Append("</div>");
                            sb.Append("</div>");
                            sb.Append("</div>");
                        }
                        sb.Append("</div>");
                    }
                }
            }
            catch (Exception ex)
            {
                sb.Append("<div class=\"no-images\">加载图片列表时出错: ").Append(ex.Message).Append("</div>");
            }

            sb.Append("</div></body></html>");
            return sb.ToString();
        }

        private string GetContentType(string extension)
        {
            switch ((extension ?? string.Empty).ToLowerInvariant())
            {
                case ".jpg":
                case ".jpeg":
                    return "image/jpeg";
                case ".png":
                    return "image/png";
                case ".gif":
                    return "image/gif";
                case ".bmp":
                    return "image/bmp";
                case ".webp":
                    return "image/webp";
                default:
                    return "application/octet-stream";
            }
        }

        private void RespondBinary(SimpleHttpContext ctx, int status, string contentType, byte[] data)
        {
            ctx.Response.StatusCode = status;
            ctx.Response.ContentType = contentType;
            ctx.Response.OutputStream.SetLength(0);
            ctx.Response.OutputStream.Write(data, 0, data.Length);
        }

        private List<string> GetImageFiles(string directory)
        {
            var imageFiles = new List<string>();
            string[] imageExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif", ".bmp", ".webp" };
            try
            {
                foreach (string file in Directory.GetFiles(directory, "*.*", SearchOption.AllDirectories))
                {
                    string extension = Path.GetExtension(file).ToLowerInvariant();
                    if (Array.Exists(imageExtensions, delegate (string candidate) { return candidate == extension; }))
                    {
                        imageFiles.Add(file);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("获取图片文件列表时出错: " + ex.Message);
            }

            imageFiles.Sort(delegate (string a, string b)
            {
                return File.GetLastWriteTime(b).CompareTo(File.GetLastWriteTime(a));
            });
            return imageFiles;
        }

        private string GetRelativePath(string basePath, string fullPath)
        {
            if (fullPath.StartsWith(basePath, StringComparison.OrdinalIgnoreCase))
            {
                return fullPath.Substring(basePath.Length).TrimStart('\\', '/');
            }
            return fullPath;
        }

        private string GetFileSizeString(long bytes)
        {
            string[] sizes = { "B", "KB", "MB", "GB" };
            double length = bytes;
            int order = 0;
            while (length >= 1024 && order < sizes.Length - 1)
            {
                order++;
                length = length / 1024;
            }
            return string.Format("{0:0.##} {1}", length, sizes[order]);
        }

        private string GetPathInfo(string relativePath)
        {
            try
            {
                string[] parts = relativePath.Split('\\', '/');
                if (parts.Length >= 2)
                {
                    return parts[0] + " / " + parts[1];
                }
                if (parts.Length == 1)
                {
                    return parts[0];
                }
                return "未知路径";
            }
            catch
            {
                return "路径解析错误";
            }
        }
        private static string GetLocalIPAddress()
        {
            return LocalNetworkHelper.GetLocalIPAddress();
        }

        private static string ReplaceLocalhostWithIP(string url)
        {
            if (string.IsNullOrEmpty(url))
            {
                return url ?? string.Empty;
            }

            try
            {
                string localIp = LocalNetworkHelper.GetLocalIPAddress();
                return url
                    .Replace("localhost", localIp)
                    .Replace("127.0.0.1", localIp)
                    .Replace("198.18.0.1", localIp);
            }
            catch
            {
                return url;
            }
        }
    }
}






