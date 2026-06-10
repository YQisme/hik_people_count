using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using ACSEventConsole.Infrastructure.Acs;
using ACSEventConsole.Infrastructure.Builders;
using ACSEventConsole.Infrastructure.Config;
using ACSEventConsole.Infrastructure.Network;
using ACSEventConsole.Infrastructure.State;
using ACSEventConsole.Infrastructure.Storage;

namespace ACSEventConsole.Services
{
    public static class ApiJsonSerializer
    {
        public static string BuildHomePage(int webPort)
        {
            var sb = new StringBuilder();
            sb.Append("<html><head><meta charset=\"utf-8\"><title>GetACSEvent Web</title>");
            sb.Append("<style>body{font-family:Segoe UI,Arial,sans-serif;max-width:900px;margin:24px auto;padding:0 16px;color:#1f2937;} h1{margin-bottom:8px;} ul{line-height:1.9;} code{background:#f3f4f6;padding:2px 6px;border-radius:6px;}</style>");
            sb.Append("</head><body>");
            sb.Append("<h1>GetACSEvent 独立服务</h1>");
            sb.Append("<p>当前服务已同时支持门禁事件查看、配置编辑，以及供前端看板使用的数据接口。</p>");
            sb.Append("<ul>");
            sb.Append("<li><a href=\"/api/dashboard\">/api/dashboard</a> - newdemo 看板聚合接口</li>");
            sb.Append("<li><a href=\"/api/dashboard/stream\">/api/dashboard/stream</a> - 看板 SSE 实时推送</li>");
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
            sb.Append("<p>前端默认读取 <code>http://" + LocalNetworkHelper.GetLocalIPAddress() + ":" + webPort + "/api/dashboard</code>，也可以通过 <code>VITE_API_BASE_URL</code> 指向其他地址。</p>");
            sb.Append("</body></html>");
            return sb.ToString();
        }

        public static string BuildEditorPage(string json, bool saved)
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

        public static Dictionary<string, string> ParseForm(string body)
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

        public static string UrlDecodeFormComponent(string value)
        {
            if (string.IsNullOrEmpty(value)) return string.Empty;
            value = value.Replace('+', ' ');
            try { return Uri.UnescapeDataString(value); } catch { return value; }
        }

        public static string SerializeDashboard(DashboardPayload payload)
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

        public static string SerializeLimitCount(int limitCount)
        {
            var sb = new StringBuilder();
            sb.Append('{');
            AppendJsonNumber(sb, "limitCount", limitCount);
            sb.Append('}');
            return sb.ToString();
        }

        public static string SerializeAbnormalCloseResult(string id, bool success)
        {
            var sb = new StringBuilder();
            sb.Append('{');
            AppendJson(sb, "id", id); sb.Append(',');
            AppendJsonBool(sb, "success", success);
            sb.Append('}');
            return sb.ToString();
        }

        public static int ReadLimitCountFromRequest(string body, string contentType)
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

        public static int ExtractIntValue(string body, string key)
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

        public static string ExtractStringValue(string body, string key)
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

        public static string ExtractRawValue(string body, string key)
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

        public static bool TrySaveLimitCount(int limitCount, out string errorMessage)
        {
            return DeviceConfigStore.UpdateLimitCount(limitCount, out errorMessage);
        }

        public static void AppendAreaAlert(StringBuilder sb, AreaAlertInfo areaAlert)
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

        public static void AppendAbnormalMessages(StringBuilder sb, List<AbnormalMessageInfo> messages)
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

        public static void AppendMetrics(StringBuilder sb, List<DashboardMetric> metrics)
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

        public static void AppendAlarms(StringBuilder sb, List<DashboardAlarm> alarms)
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

        public static void AppendRecords(StringBuilder sb, string key, List<DashboardRecord> records)
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

        public static void AppendRecord(StringBuilder sb, DashboardRecord record)
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

        public static string SerializeDevices(string channelId = null)
        {
            var devices = DeviceConfigStore.GetEnabledDevices();
            HashSet<string> channelDeviceIPs = null;
            if (!string.IsNullOrWhiteSpace(channelId))
            {
                channelDeviceIPs = DeviceConfigStore.GetChannelDeviceIPs(channelId);
            }

            var sb = new StringBuilder();
            sb.Append('[');
            bool first = true;
            for (int i = 0; i < devices.Count; i++)
            {
                var device = devices[i];
                if (channelDeviceIPs != null && channelDeviceIPs.Count > 0 &&
                    !channelDeviceIPs.Contains(device.IP ?? string.Empty))
                {
                    continue;
                }

                if (!first)
                {
                    sb.Append(',');
                }

                first = false;
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

        public static string SerializeChannels()
        {
            var channels = DeviceConfigStore.GetChannels();
            var sb = new StringBuilder();
            sb.Append('[');
            for (int i = 0; i < channels.Count; i++)
            {
                var channel = channels[i];
                if (i > 0)
                {
                    sb.Append(',');
                }

                sb.Append('{');
                AppendJson(sb, "id", channel.Id); sb.Append(',');
                AppendJson(sb, "name", channel.Name); sb.Append(',');
                sb.Append("\"limitCount\":").Append(channel.LimitCount > 0 ? channel.LimitCount : DeviceConfigStore.GetChannelLimitCount(channel.Id)); sb.Append(',');
                sb.Append("\"deviceCount\":").Append(DeviceConfigStore.CountChannelDevices(channel.Id));
                sb.Append('}');
            }
            sb.Append(']');
            return sb.ToString();
        }

        public static string SerializeChannelOverview(ChannelOverviewPayload payload)
        {
            var sb = new StringBuilder();
            sb.Append('{');
            AppendJson(sb, "generatedAt", payload == null ? string.Empty : payload.GeneratedAt); sb.Append(',');
            sb.Append("\"channels\":[");
            if (payload?.Channels != null)
            {
                for (int i = 0; i < payload.Channels.Count; i++)
                {
                    var item = payload.Channels[i];
                    if (i > 0)
                    {
                        sb.Append(',');
                    }

                    sb.Append('{');
                    AppendJson(sb, "id", item.Id); sb.Append(',');
                    AppendJson(sb, "name", item.Name); sb.Append(',');
                    sb.Append("\"limitCount\":").Append(item.LimitCount); sb.Append(',');
                    sb.Append("\"enterCount\":").Append(item.EnterCount); sb.Append(',');
                    sb.Append("\"exitCount\":").Append(item.ExitCount); sb.Append(',');
                    sb.Append("\"stayCount\":").Append(item.StayCount); sb.Append(',');
                    sb.Append("\"alarmCount\":").Append(item.AlarmCount); sb.Append(',');
                    AppendJson(sb, "accessRuleMode", item.AccessRuleMode); sb.Append(',');
                    sb.Append("\"deviceCount\":").Append(item.DeviceCount); sb.Append(',');
                    sb.Append("\"onlineDeviceCount\":").Append(item.OnlineDeviceCount); sb.Append(',');
                    AppendChannelDevices(sb, item.Devices);
                    sb.Append('}');
                }
            }
            sb.Append(']');
            sb.Append('}');
            return sb.ToString();
        }

        public static void AppendChannelDevices(StringBuilder sb, List<ChannelDeviceStatusItem> devices)
        {
            sb.Append("\"devices\":[");
            if (devices != null)
            {
                for (int i = 0; i < devices.Count; i++)
                {
                    var device = devices[i];
                    if (i > 0)
                    {
                        sb.Append(',');
                    }

                    sb.Append('{');
                    AppendJson(sb, "ip", device.Ip); sb.Append(',');
                    AppendJson(sb, "name", device.Name); sb.Append(',');
                    AppendJson(sb, "deviceName", device.DeviceName); sb.Append(',');
                    AppendJson(sb, "direction", device.Direction); sb.Append(',');
                    AppendJsonBool(sb, "online", device.Online); sb.Append(',');
                    AppendJson(sb, "status", device.Status);
                    sb.Append('}');
                }
            }
            sb.Append(']');
        }

        public static string SerializeHistoryError(string message)
        {
            var sb = new StringBuilder();
            sb.Append('{');
            AppendJson(sb, "message", message);
            sb.Append('}');
            return sb.ToString();
        }

        public static string SerializeHistoryResult(AcsEventHistoryResult history)
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

        public static string SerializeEvents(List<AcsEvent> list)
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

        public static List<Dictionary<string, string>> FilterEmployees(string keyword)
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

        public static string SerializeEmployees(List<Dictionary<string, string>> employees)
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

        public static void AppendJson(StringBuilder sb, string key, string value)
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

        public static void AppendJsonNumber(StringBuilder sb, string key, int value)
        {
            sb.Append('"').Append(Escape(key)).Append('"').Append(':').Append(value);
        }

        public static void AppendJsonBool(StringBuilder sb, string key, bool value)
        {
            sb.Append('"').Append(Escape(key)).Append('"').Append(':').Append(value ? "true" : "false");
        }

        public static string Escape(string value)
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

        public static string UnescapeJson(string value)
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

        public static string BuildImageListPage()
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

        public static string GetContentType(string extension)
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

        public static List<string> GetImageFiles(string directory)
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

        public static string GetRelativePath(string basePath, string fullPath)
        {
            if (fullPath.StartsWith(basePath, StringComparison.OrdinalIgnoreCase))
            {
                return fullPath.Substring(basePath.Length).TrimStart('\\', '/');
            }
            return fullPath;
        }

        public static string GetFileSizeString(long bytes)
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

        public static string GetPathInfo(string relativePath)
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

        public static string GetLocalIPAddress()
        {
            return LocalNetworkHelper.GetLocalIPAddress();
        }

        public static string ReplaceLocalhostWithIP(string url)
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
