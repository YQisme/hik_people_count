using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net;
using System.Net.Http;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;

namespace ACSEventConsole.Infrastructure.Acs
{
    public class AcsEventHistoryQuery
    {
        public string DeviceIP { get; set; }
        public string StartTime { get; set; }
        public string EndTime { get; set; }
        public int Major { get; set; }
        public int Minor { get; set; }
        public int MaxResults { get; set; }
        public int SearchResultPosition { get; set; }
        public bool FetchAll { get; set; }
        public int MaxTotal { get; set; }

        public AcsEventHistoryQuery()
        {
            DeviceIP = string.Empty;
            StartTime = string.Empty;
            EndTime = string.Empty;
            Major = 5;
            Minor = 0;
            MaxResults = 30;
            SearchResultPosition = 0;
            FetchAll = true;
            MaxTotal = 500;
        }
    }

    public class AcsEventHistoryResult
    {
        public List<AcsEvent> Events { get; set; }
        public int TotalMatches { get; set; }
        public bool HasMore { get; set; }
        public string ErrorMessage { get; set; }

        public AcsEventHistoryResult()
        {
            Events = new List<AcsEvent>();
            ErrorMessage = string.Empty;
        }
    }

    public static class AcsEventIsapiClient
    {
        private const int DefaultHttpPort = 80;
        private const int DefaultHttpsPort = 443;
        private const int DefaultPageSize = 30;
        private const int DefaultMaxTotal = 500;

        public static AcsEventHistoryResult QueryHistory(AcsEventHistoryQuery query)
        {
            var result = new AcsEventHistoryResult();
            if (query == null)
            {
                result.ErrorMessage = "query is required";
                return result;
            }

            string deviceIP = (query.DeviceIP ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(deviceIP))
            {
                result.ErrorMessage = "deviceIP is required";
                return result;
            }

            var device = DeviceConfigStore.GetDevice(deviceIP);
            if (device == null)
            {
                result.ErrorMessage = "device not found in DeviceConfig.json";
                return result;
            }

            string startTime = NormalizeIsapiTime((query.StartTime ?? string.Empty).Trim());
            string endTime = NormalizeIsapiTime((query.EndTime ?? string.Empty).Trim());
            if (string.IsNullOrEmpty(startTime) || string.IsNullOrEmpty(endTime))
            {
                result.ErrorMessage = "startTime and endTime are required";
                return result;
            }

            if (string.CompareOrdinal(startTime, endTime) > 0)
            {
                result.ErrorMessage = "startTime must be earlier than endTime";
                return result;
            }

            int pageSize = query.MaxResults > 0 ? query.MaxResults : DefaultPageSize;
            int maxTotal = query.MaxTotal > 0 ? query.MaxTotal : DefaultMaxTotal;
            int major = query.Major >= 0 ? query.Major : 5;
            int minor = query.Minor >= 0 ? query.Minor : 0;
            bool fetchAll = query.FetchAll;
            int position = query.SearchResultPosition >= 0 ? query.SearchResultPosition : 0;

            string username = string.IsNullOrWhiteSpace(device.UserName) ? "admin" : device.UserName.Trim();
            string password = device.Password ?? string.Empty;
            bool useHttps = device.UseHttps;
            int httpPort = device.HttpPort > 0
                ? device.HttpPort
                : (useHttps ? DefaultHttpsPort : DefaultHttpPort);

            string scheme = useHttps ? "https" : "http";
            string baseUrl = scheme + "://" + deviceIP + ":" + httpPort;
            string searchUrl = baseUrl + "/ISAPI/AccessControl/AcsEvent?format=json";
            string searchId = Guid.NewGuid().ToString("N");
            if (searchId.Length > 20)
            {
                searchId = searchId.Substring(0, 20);
            }

            try
            {
                using (var client = CreateHttpClient(username, password, useHttps))
                {
                    int totalMatches = 0;
                    string responseStatus = string.Empty;
                    var collected = new List<AcsEvent>();

                    while (true)
                    {
                        string requestBody = BuildRequestBody(searchId, position, pageSize, major, minor, startTime, endTime);
                        string responseText = PostJson(client, searchUrl, requestBody);
                        if (string.IsNullOrWhiteSpace(responseText))
                        {
                            result.ErrorMessage = "device returned empty response";
                            return result;
                        }

                        using (JsonDocument document = JsonDocument.Parse(responseText))
                        {
                            JsonElement root = document.RootElement;
                            if (TryReadError(root, out string errorMessage))
                            {
                                result.ErrorMessage = errorMessage;
                                return result;
                            }

                            JsonElement acsEvent;
                            if (!TryGetAcsEventRoot(root, out acsEvent))
                            {
                                result.ErrorMessage = "unexpected device response format";
                                return result;
                            }

                            if (totalMatches <= 0)
                            {
                                totalMatches = ReadInt(acsEvent, "totalMatches");
                            }

                            responseStatus = ReadString(acsEvent, "responseStatusStrg");
                            int pageCount = AppendInfoList(acsEvent, device, collected);
                            if (pageCount <= 0)
                            {
                                break;
                            }

                            position += pageCount;
                            bool hasMore = string.Equals(responseStatus, "MORE", StringComparison.OrdinalIgnoreCase)
                                || (totalMatches > 0 && position < totalMatches);

                            if (!fetchAll || !hasMore || collected.Count >= maxTotal)
                            {
                                if (!fetchAll)
                                {
                                    result.HasMore = hasMore;
                                }
                                else
                                {
                                    result.HasMore = hasMore && collected.Count < totalMatches;
                                }
                                break;
                            }
                        }
                    }

                    if (collected.Count > maxTotal)
                    {
                        collected = collected.GetRange(0, maxTotal);
                        result.HasMore = true;
                    }

                    result.Events = collected;
                    result.TotalMatches = totalMatches > 0 ? totalMatches : collected.Count;
                    return result;
                }
            }
            catch (Exception ex)
            {
                result.ErrorMessage = ex.Message;
                return result;
            }
        }

        private static HttpClient CreateHttpClient(string username, string password, bool useHttps)
        {
            var handler = new HttpClientHandler
            {
                Credentials = new NetworkCredential(username, password),
                PreAuthenticate = false
            };

            if (useHttps)
            {
                handler.ServerCertificateCustomValidationCallback = AcceptAnyCertificate;
            }

            var client = new HttpClient(handler)
            {
                Timeout = TimeSpan.FromSeconds(30)
            };
            return client;
        }

        private static bool AcceptAnyCertificate(
            HttpRequestMessage message,
            X509Certificate2 certificate,
            X509Chain chain,
            SslPolicyErrors errors)
        {
            return true;
        }

        private static string PostJson(HttpClient client, string url, string body)
        {
            using (var content = new StringContent(body, Encoding.UTF8, "application/json"))
            using (HttpResponseMessage response = client.PostAsync(url, content).GetAwaiter().GetResult())
            {
                string responseText = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
                if (!response.IsSuccessStatusCode)
                {
                    string detail = string.IsNullOrWhiteSpace(responseText)
                        ? response.ReasonPhrase
                        : responseText;
                    throw new InvalidOperationException("HTTP " + (int)response.StatusCode + ": " + detail);
                }

                return responseText;
            }
        }

        private static string BuildRequestBody(
            string searchId,
            int position,
            int maxResults,
            int major,
            int minor,
            string startTime,
            string endTime)
        {
            var cond = new Dictionary<string, object>
            {
                { "searchID", searchId },
                { "searchResultPosition", position },
                { "maxResults", maxResults },
                { "major", major },
                { "minor", minor },
                { "startTime", startTime },
                { "endTime", endTime }
            };

            var payload = new Dictionary<string, object>
            {
                { "AcsEventCond", cond }
            };

            return JsonSerializer.Serialize(payload);
        }

        private static string NormalizeIsapiTime(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                return string.Empty;
            }

            raw = raw.Trim();

            int tIndex = raw.IndexOf('T');
            if (tIndex > 0)
            {
                string timePart = raw.Substring(tIndex + 1);
                int colonCount = 0;
                foreach (char ch in timePart)
                {
                    if (ch == ':')
                    {
                        colonCount++;
                    }
                }

                if (colonCount == 1)
                {
                    int zoneIndex = IndexOfTimeZone(raw);
                    if (zoneIndex > 0)
                    {
                        raw = raw.Substring(0, zoneIndex) + ":00" + raw.Substring(zoneIndex);
                    }
                    else
                    {
                        raw += ":00";
                    }
                }
            }

            DateTimeOffset parsedOffset;
            if (DateTimeOffset.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out parsedOffset))
            {
                return parsedOffset.ToString("yyyy-MM-dd'T'HH:mm:sszzz", CultureInfo.InvariantCulture);
            }

            DateTime parsedLocal;
            if (DateTime.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out parsedLocal))
            {
                var offset = TimeZoneInfo.Local.GetUtcOffset(parsedLocal);
                return new DateTimeOffset(parsedLocal, offset).ToString("yyyy-MM-dd'T'HH:mm:sszzz", CultureInfo.InvariantCulture);
            }

            return raw;
        }

        private static int IndexOfTimeZone(string raw)
        {
            int tIndex = raw.IndexOf('T');
            if (tIndex < 0)
            {
                return -1;
            }

            for (int i = tIndex + 1; i < raw.Length; i++)
            {
                char ch = raw[i];
                if (ch == '+' || ch == '-')
                {
                    return i;
                }

                if (ch == 'Z' || ch == 'z')
                {
                    return i;
                }
            }

            return -1;
        }

        private static bool TryGetAcsEventRoot(JsonElement root, out JsonElement acsEvent)
        {
            if (root.ValueKind == JsonValueKind.Object && root.TryGetProperty("AcsEvent", out acsEvent))
            {
                return true;
            }

            acsEvent = default(JsonElement);
            return false;
        }

        private static bool TryReadError(JsonElement root, out string message)
        {
            message = string.Empty;
            if (root.ValueKind != JsonValueKind.Object)
            {
                return false;
            }

            int statusCode = ReadInt(root, "statusCode");
            if (statusCode > 0 && statusCode != 1)
            {
                string statusString = ReadString(root, "statusString");
                string subStatusCode = ReadString(root, "subStatusCode");
                string errorMsg = ReadString(root, "errorMsg");
                var parts = new List<string>();
                if (statusCode > 0)
                {
                    parts.Add("statusCode=" + statusCode);
                }
                if (!string.IsNullOrWhiteSpace(statusString))
                {
                    parts.Add(statusString);
                }
                if (!string.IsNullOrWhiteSpace(subStatusCode))
                {
                    parts.Add(subStatusCode);
                }
                if (!string.IsNullOrWhiteSpace(errorMsg))
                {
                    parts.Add(errorMsg);
                }
                message = string.Join(" / ", parts);
                return true;
            }

            return false;
        }

        private static int AppendInfoList(JsonElement acsEvent, DeviceConfigDeviceEntry device, List<AcsEvent> target)
        {
            if (!acsEvent.TryGetProperty("InfoList", out JsonElement infoList))
            {
                return 0;
            }

            if (infoList.ValueKind == JsonValueKind.Array)
            {
                int count = 0;
                foreach (JsonElement item in infoList.EnumerateArray())
                {
                    target.Add(MapEvent(item, device));
                    count++;
                }

                return count;
            }

            if (infoList.ValueKind == JsonValueKind.Object)
            {
                target.Add(MapEvent(infoList, device));
                return 1;
            }

            return 0;
        }

        private static AcsEvent MapEvent(JsonElement item, DeviceConfigDeviceEntry device)
        {
            string deviceIP = device.IP ?? string.Empty;
            uint major = (uint)ReadInt(item, "major");
            uint minor = (uint)ReadInt(item, "minor");
            string time = ReadString(item, "time");
            if (string.IsNullOrEmpty(time))
            {
                time = ReadString(item, "dateTime");
            }

            string employeeNo = FirstNonEmpty(
                ReadString(item, "employeeNoString"),
                ReadString(item, "employeeNo"),
                ReadString(item, "employeeID"));
            string cardNo = ReadString(item, "cardNo");
            string personName = ReadString(item, "name");
            if (string.IsNullOrEmpty(personName))
            {
                personName = ReadString(item, "personName");
            }

            if (string.IsNullOrEmpty(personName))
            {
                var employee = EmployeeDirectory.FindBestMatch(employeeNo, cardNo, personName);
                if (employee != null)
                {
                    foreach (string key in new[] { "name", "employeeName", "personName", "realName" })
                    {
                        string value;
                        if (employee.TryGetValue(key, out value) && !string.IsNullOrWhiteSpace(value))
                        {
                            personName = value.Trim();
                            break;
                        }
                    }
                }
            }

            if (string.IsNullOrEmpty(personName))
            {
                personName = "未知人员";
            }

            uint doorNo = (uint)ReadInt(item, "doorNo");
            string pictureUrl = FirstNonEmpty(
                ReadString(item, "pictureURL"),
                ReadString(item, "pictureUrl"),
                ReadString(item, "picURL"));
            int serialNo = ReadInt(item, "serialNo");
            int picturesNumber = ReadInt(item, "picturesNumber");

            DateTime timeUtc = ParseEventTimeUtc(time);
            string displayTime = FormatDisplayTime(time, timeUtc);
            return new AcsEvent
            {
                TimeUtc = timeUtc,
                Time = displayTime,
                DeviceIP = deviceIP,
                DeviceName = string.IsNullOrWhiteSpace(device.DeviceName) ? (device.Name ?? deviceIP) : device.DeviceName,
                DeviceID = device.DeviceID ?? string.Empty,
                AreaID = device.AreaID ?? string.Empty,
                Remark = device.Remark ?? string.Empty,
                MajorType = AcsEventTypeHelper.GetMajorTypeString(major),
                MinorType = AcsEventTypeHelper.GetMinorTypeString(major, minor),
                CardNo = cardNo,
                EmployeeNo = employeeNo,
                PersonName = personName,
                CardType = AcsEventTypeHelper.GetCardTypeString((byte)ReadInt(item, "cardType")),
                DoorNo = doorNo,
                Direction = DeviceConfigStore.GetDoorDirection(deviceIP, doorNo),
                ImageUrl = AcsEventImageResolver.Resolve(
                    device,
                    personName,
                    displayTime,
                    timeUtc,
                    pictureUrl,
                    serialNo,
                    picturesNumber,
                    employeeNo)
            };
        }

        private static DateTime ParseEventTimeUtc(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                return DateTime.UtcNow;
            }

            DateTime parsed;
            if (DateTime.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out parsed))
            {
                return parsed.ToUniversalTime();
            }

            if (DateTime.TryParse(raw, out parsed))
            {
                return parsed.ToUniversalTime();
            }

            return DateTime.UtcNow;
        }

        private static string FormatDisplayTime(string raw, DateTime timeUtc)
        {
            if (!string.IsNullOrWhiteSpace(raw))
            {
                string normalized = raw.Replace('T', ' ');
                int plusIndex = normalized.IndexOf('+');
                if (plusIndex > 0)
                {
                    normalized = normalized.Substring(0, plusIndex);
                }

                int zIndex = normalized.IndexOf('Z');
                if (zIndex > 0)
                {
                    normalized = normalized.Substring(0, zIndex);
                }

                if (normalized.Length >= 19)
                {
                    return normalized.Substring(0, 19);
                }

                return normalized.Trim();
            }

            return timeUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss");
        }

        private static string ReadString(JsonElement element, string propertyName)
        {
            if (element.ValueKind != JsonValueKind.Object || !element.TryGetProperty(propertyName, out JsonElement value))
            {
                return string.Empty;
            }

            switch (value.ValueKind)
            {
                case JsonValueKind.String:
                    return value.GetString() ?? string.Empty;
                case JsonValueKind.Number:
                    return value.GetRawText();
                case JsonValueKind.True:
                    return "true";
                case JsonValueKind.False:
                    return "false";
                default:
                    return string.Empty;
            }
        }

        private static int ReadInt(JsonElement element, string propertyName)
        {
            if (element.ValueKind != JsonValueKind.Object || !element.TryGetProperty(propertyName, out JsonElement value))
            {
                return 0;
            }

            switch (value.ValueKind)
            {
                case JsonValueKind.Number:
                    if (value.TryGetInt32(out int number))
                    {
                        return number;
                    }
                    return 0;
                case JsonValueKind.String:
                    int parsed;
                    return int.TryParse(value.GetString(), out parsed) ? parsed : 0;
                default:
                    return 0;
            }
        }

        private static string FirstNonEmpty(params string[] values)
        {
            foreach (string value in values)
            {
                if (!string.IsNullOrWhiteSpace(value))
                {
                    return value.Trim();
                }
            }

            return string.Empty;
        }
    }

}
