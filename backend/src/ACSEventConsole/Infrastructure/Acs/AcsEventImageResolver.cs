using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.Json;

namespace ACSEventConsole.Infrastructure.Acs
{
    public static class AcsEventImageResolver
    {
        private const string PictureRoot = "D:/Picture";
        private static readonly object EmployeePicCacheSync = new object();
        private static readonly Dictionary<string, string> EmployeePicCache = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        public static string Resolve(
            DeviceConfigDeviceEntry device,
            string personName,
            string displayTime,
            DateTime timeUtc,
            string pictureUrl,
            int serialNo,
            int picturesNumber,
            string employeeNo)
        {
            if (device == null)
            {
                return string.Empty;
            }

            string localUrl = TryResolveLocalImage(device, personName, displayTime, timeUtc);
            if (!string.IsNullOrEmpty(localUrl))
            {
                return localUrl;
            }

            string normalizedPictureUrl = (pictureUrl ?? string.Empty).Trim();
            if (!string.IsNullOrEmpty(normalizedPictureUrl))
            {
                return BuildProxyUrl(device.IP, "source", normalizedPictureUrl);
            }

            if (picturesNumber > 0 && serialNo > 0)
            {
                return BuildProxyUrl(device.IP, "serialNo", serialNo.ToString(CultureInfo.InvariantCulture));
            }

            return string.Empty;
        }

        public static byte[] DownloadPicture(string deviceIP, string source, string serialNo, string employeeNo)
        {
            var device = DeviceConfigStore.GetDevice(deviceIP);
            if (device == null)
            {
                return null;
            }

            var candidates = BuildDownloadCandidates(source, serialNo, employeeNo);
            foreach (string candidate in candidates)
            {
                byte[] bytes = HikvisionIsapiHttp.DownloadBytes(device, candidate);
                if (bytes != null && bytes.Length > 0 && LooksLikeImage(bytes))
                {
                    return bytes;
                }
            }

            if (!string.IsNullOrWhiteSpace(employeeNo))
            {
                string employeePicUrl = ResolveEmployeePictureUrl(device, employeeNo.Trim());
                if (!string.IsNullOrEmpty(employeePicUrl))
                {
                    byte[] bytes = HikvisionIsapiHttp.DownloadBytes(device, employeePicUrl);
                    if (bytes != null && bytes.Length > 0 && LooksLikeImage(bytes))
                    {
                        return bytes;
                    }
                }
            }

            return null;
        }

        private static List<string> BuildDownloadCandidates(string source, string serialNo, string employeeNo)
        {
            var candidates = new List<string>();
            string normalizedSource = (source ?? string.Empty).Trim();
            if (!string.IsNullOrEmpty(normalizedSource))
            {
                candidates.Add(normalizedSource);
            }

            if (!string.IsNullOrWhiteSpace(serialNo))
            {
                candidates.Add("/ISAPI/AccessControl/Event/picture?format=json&serialNo=" + serialNo);
                candidates.Add("/ISAPI/AccessControl/CapturePicture?format=json&serialNo=" + serialNo);
                candidates.Add("/ISAPI/AccessControl/Event/picture?serialNo=" + serialNo);
            }

            return candidates;
        }

        private static string ResolveEmployeePictureUrl(DeviceConfigDeviceEntry device, string employeeNo)
        {
            string cacheKey = (device.IP ?? string.Empty) + "|" + employeeNo;
            lock (EmployeePicCacheSync)
            {
                string cached;
                if (EmployeePicCache.TryGetValue(cacheKey, out cached))
                {
                    return cached;
                }
            }

            string picUrl = FetchEmployeePictureUrl(device, employeeNo);
            lock (EmployeePicCacheSync)
            {
                EmployeePicCache[cacheKey] = picUrl ?? string.Empty;
            }

            return picUrl ?? string.Empty;
        }

        private static string FetchEmployeePictureUrl(DeviceConfigDeviceEntry device, string employeeNo)
        {
            try
            {
                string body = "{\"UserInfoSearchCond\":{\"searchID\":\"img" + Guid.NewGuid().ToString("N").Substring(0, 12) +
                              "\",\"searchResultPosition\":0,\"maxResults\":30}}";
                string responseText = HikvisionIsapiHttp.PostJson(
                    device,
                    "/ISAPI/AccessControl/UserInfo/Search?format=json",
                    body);

                using (JsonDocument document = JsonDocument.Parse(responseText))
                {
                    JsonElement root = document.RootElement;
                    JsonElement search;
                    if (!root.TryGetProperty("UserInfoSearch", out search))
                    {
                        return string.Empty;
                    }

                    if (!search.TryGetProperty("UserInfo", out JsonElement users))
                    {
                        return string.Empty;
                    }

                    if (users.ValueKind == JsonValueKind.Object)
                    {
                        return ReadEmployeePicUrl(users, employeeNo);
                    }

                    if (users.ValueKind == JsonValueKind.Array)
                    {
                        foreach (JsonElement user in users.EnumerateArray())
                        {
                            string picUrl = ReadEmployeePicUrl(user, employeeNo);
                            if (!string.IsNullOrEmpty(picUrl))
                            {
                                return picUrl;
                            }
                        }
                    }
                }
            }
            catch
            {
            }

            return string.Empty;
        }

        private static string ReadEmployeePicUrl(JsonElement user, string employeeNo)
        {
            string currentEmployeeNo = ReadJsonString(user, "employeeNo");
            if (!string.Equals(currentEmployeeNo, employeeNo, StringComparison.OrdinalIgnoreCase))
            {
                return string.Empty;
            }

            return FirstNonEmpty(
                ReadJsonString(user, "picURL"),
                ReadJsonString(user, "faceURL"),
                ReadJsonString(user, "pictureURL"));
        }

        private static string TryResolveLocalImage(
            DeviceConfigDeviceEntry device,
            string personName,
            string displayTime,
            DateTime timeUtc)
        {
            string pictureDir = Path.GetFullPath(PictureRoot);
            if (!Directory.Exists(pictureDir))
            {
                return string.Empty;
            }

            string safePersonName = GetSafeFolderName(personName);
            string timeToken = BuildFileTimeToken(displayTime, timeUtc);
            var deviceNames = BuildDeviceFolderNames(device);

            foreach (string deviceName in deviceNames)
            {
                string personDir = Path.Combine(pictureDir, deviceName, safePersonName);
                if (!Directory.Exists(personDir))
                {
                    continue;
                }

                string fileName = safePersonName + "_" + timeToken + ".jpg";
                string exactPath = Path.Combine(personDir, fileName);
                if (File.Exists(exactPath))
                {
                    return BuildLocalImageUrl(deviceName, safePersonName, fileName);
                }
            }

            return string.Empty;
        }

        private static List<string> BuildDeviceFolderNames(DeviceConfigDeviceEntry device)
        {
            var names = new List<string>();
            AddIfMissing(names, device.DeviceName);
            AddIfMissing(names, device.Name);
            AddIfMissing(names, device.IP);
            return names;
        }

        private static void AddIfMissing(List<string> list, string value)
        {
            string cleaned = (value ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(cleaned))
            {
                return;
            }

            foreach (string existing in list)
            {
                if (string.Equals(existing, cleaned, StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }
            }

            list.Add(cleaned);
        }

        private static string BuildFileTimeToken(string displayTime, DateTime timeUtc)
        {
            DateTime local = timeUtc.ToLocalTime();
            if (!string.IsNullOrWhiteSpace(displayTime))
            {
                string normalized = displayTime.Trim().Replace('T', ' ');
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

                DateTime parsed;
                if (DateTime.TryParse(normalized, out parsed))
                {
                    local = parsed;
                }
            }

            return string.Format(
                CultureInfo.InvariantCulture,
                "{0:D4}{1:D2}{2:D2}_{3:D2}{4:D2}{5:D2}",
                local.Year,
                local.Month,
                local.Day,
                local.Hour,
                local.Minute,
                local.Second);
        }

        private static string BuildLocalImageUrl(string deviceName, string personName, string fileName)
        {
            int webPort = DeviceConfigStore.GetWebPort();
            string baseUrl = LocalNetworkHelper.BuildWebServerUrl(webPort);
            return baseUrl + "/images/" +
                   Uri.EscapeDataString(deviceName) + "/" +
                   Uri.EscapeDataString(personName) + "/" +
                   Uri.EscapeDataString(fileName);
        }

        private static string BuildProxyUrl(string deviceIP, string key, string value)
        {
            int webPort = DeviceConfigStore.GetWebPort();
            string baseUrl = LocalNetworkHelper.BuildWebServerUrl(webPort);
            return baseUrl + "/api/acs-events/picture?deviceIP=" + Uri.EscapeDataString(deviceIP ?? string.Empty) +
                   "&" + key + "=" + Uri.EscapeDataString(value ?? string.Empty);
        }

        private static string GetSafeFolderName(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return "未知员工";
            }

            string safeName = name.Trim();
            char[] invalidChars = Path.GetInvalidFileNameChars();
            foreach (char invalidChar in invalidChars)
            {
                safeName = safeName.Replace(invalidChar, '_');
            }

            return string.IsNullOrWhiteSpace(safeName) ? "未知员工" : safeName;
        }

        private static bool LooksLikeImage(byte[] bytes)
        {
            if (bytes == null || bytes.Length < 4)
            {
                return false;
            }

            bool isJpeg = bytes[0] == 0xFF && bytes[1] == 0xD8;
            bool isPng = bytes[0] == 0x89 && bytes[1] == 0x50 && bytes[2] == 0x4E && bytes[3] == 0x47;
            return isJpeg || isPng;
        }

        private static string ReadJsonString(JsonElement element, string propertyName)
        {
            JsonElement value;
            if (!element.TryGetProperty(propertyName, out value))
            {
                return string.Empty;
            }

            return value.ValueKind == JsonValueKind.String ? (value.GetString() ?? string.Empty) : value.GetRawText();
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
