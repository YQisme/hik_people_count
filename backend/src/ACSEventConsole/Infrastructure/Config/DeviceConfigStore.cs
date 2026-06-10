using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Xml;

namespace ACSEventConsole.Infrastructure.Config
{
    public class DeviceConfigDocument
    {
        public DeviceConfigGlobalSettings Config { get; set; }
        public List<DeviceConfigChannelEntry> Channels { get; set; }
        public List<DeviceConfigDeviceEntry> Devices { get; set; }

        public DeviceConfigDocument()
        {
            Config = DeviceConfigGlobalSettings.CreateDefault();
            Channels = new List<DeviceConfigChannelEntry>();
            Devices = new List<DeviceConfigDeviceEntry>();
        }
    }

    public class DeviceConfigChannelEntry
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public int LimitCount { get; set; }

        public DeviceConfigChannelEntry()
        {
            Id = string.Empty;
            Name = string.Empty;
        }
    }

    public class DeviceConfigGlobalSettings
    {
        public string ApiBaseUrl { get; set; }
        public int WebPort { get; set; }
        public int LimitCount { get; set; }
        public int StayWarningMinutes { get; set; }
        public int RecentRecordCount { get; set; }
        public int ExitGraceSeconds { get; set; }
        public double CapacityWarningRatio { get; set; }
        public bool MqttEnabled { get; set; }
        public string MqttHost { get; set; }
        public int MqttPort { get; set; }
        public string MqttTopic { get; set; }
        public string MqttClientId { get; set; }
        public string MqttUsername { get; set; }
        public string MqttPassword { get; set; }
        public int AlarmScanSeconds { get; set; }
        public bool PersonInfoMqttEnabled { get; set; }
        public string PersonInfoMqttHost { get; set; }
        public int PersonInfoMqttPort { get; set; }
        public string PersonInfoMqttTopic { get; set; }
        public string PersonInMqttTopic { get; set; }
        public string PersonOutMqttTopic { get; set; }
        public string PersonInfoMqttClientId { get; set; }
        public bool AreaAlertMqttEnabled { get; set; }
        public string AreaAlertMqttHost { get; set; }
        public int AreaAlertMqttPort { get; set; }
        public string AreaAlertMqttTopic { get; set; }
        public string AreaAlertMqttClientId { get; set; }
        public bool AbnormalMqttEnabled { get; set; }
        public string AbnormalMqttHost { get; set; }
        public int AbnormalMqttPort { get; set; }
        public string AbnormalMqttTopic { get; set; }
        public string AbnormalMqttClientId { get; set; }
        public bool PeopleCountMqttEnabled { get; set; }
        public string PeopleCountMqttHost { get; set; }
        public int PeopleCountMqttPort { get; set; }
        public string PeopleCountMqttTopic { get; set; }
        public string PeopleCountMqttClientId { get; set; }

        public static DeviceConfigGlobalSettings CreateDefault()
        {
            return new DeviceConfigGlobalSettings
            {
                ApiBaseUrl = "http://192.168.0.14:5000",
                WebPort = 8081,
                LimitCount = 500,
                StayWarningMinutes = 30,
                RecentRecordCount = 10,
                ExitGraceSeconds = 8,
                CapacityWarningRatio = 0.9d,
                MqttEnabled = true,
                MqttHost = "192.168.0.12",
                MqttPort = 1883,
                MqttTopic = "acs/alarm/event",
                MqttClientId = "acs-event-standalone",
                MqttUsername = string.Empty,
                MqttPassword = string.Empty,
                AlarmScanSeconds = 5,
                PersonInfoMqttEnabled = true,
                PersonInfoMqttHost = "192.168.0.12",
                PersonInfoMqttPort = 1883,
                PersonInfoMqttTopic = "personinfo",
                PersonInMqttTopic = "person_in",
                PersonOutMqttTopic = "person_out",
                PersonInfoMqttClientId = "acs-personinfo-publisher",
                AreaAlertMqttEnabled = true,
                AreaAlertMqttHost = "192.168.0.12",
                AreaAlertMqttPort = 1883,
                AreaAlertMqttTopic = "area_alert",
                AreaAlertMqttClientId = "acs-area-alert-subscriber",
                AbnormalMqttEnabled = true,
                AbnormalMqttHost = "192.168.0.12",
                AbnormalMqttPort = 1883,
                AbnormalMqttTopic = "abnormal",
                AbnormalMqttClientId = "acs-abnormal-subscriber",
                PeopleCountMqttEnabled = true,
                PeopleCountMqttHost = "192.168.0.12",
                PeopleCountMqttPort = 1883,
                PeopleCountMqttTopic = "people_count",
                PeopleCountMqttClientId = "acs-people-count-subscriber"
            };
        }
    }

    public class DeviceConfigDeviceEntry
    {
        public string IP { get; set; }
        public string UserName { get; set; }
        public string Password { get; set; }
        public ushort Port { get; set; }
        public int HttpPort { get; set; }
        public bool UseHttps { get; set; }
        public bool Enabled { get; set; }
        public string Name { get; set; }
        public string Remark { get; set; }
        public string DeviceID { get; set; }
        public string AreaID { get; set; }
        public string DeviceName { get; set; }
        public string Direction { get; set; }
        public int ControlDoorNo { get; set; }
        public string ZoneName { get; set; }
        public string DoorName { get; set; }
        public string ChannelId { get; set; }
        public List<DeviceConfigDoorEntry> Doors { get; set; }

        public DeviceConfigDeviceEntry()
        {
            IP = string.Empty;
            UserName = "admin";
            Password = string.Empty;
            Port = 8000;
            Enabled = true;
            Name = string.Empty;
            Remark = string.Empty;
            DeviceID = string.Empty;
            AreaID = string.Empty;
            DeviceName = string.Empty;
            Direction = string.Empty;
            ControlDoorNo = 1;
            ZoneName = string.Empty;
            DoorName = string.Empty;
            ChannelId = string.Empty;
            Doors = new List<DeviceConfigDoorEntry>();
        }

        public DeviceInfo ToDeviceInfo()
        {
            return new DeviceInfo
            {
                IP = IP ?? string.Empty,
                UserName = UserName ?? string.Empty,
                Password = Password ?? string.Empty,
                Port = Port > 0 ? Port : (ushort)8000,
                Enabled = Enabled,
                Name = Name ?? string.Empty,
                Remark = Remark ?? string.Empty,
                DeviceID = DeviceID ?? string.Empty,
                AreaID = AreaID ?? string.Empty,
                DeviceName = DeviceName ?? string.Empty,
                Direction = Direction ?? string.Empty,
                ControlDoorNo = ControlDoorNo > 0 ? ControlDoorNo : 1
            };
        }
    }

    public class DeviceConfigDoorEntry
    {
        public int DoorNo { get; set; }
        public string Direction { get; set; }
        public string DoorName { get; set; }
        public string Name { get; set; }
        public string ZoneName { get; set; }

        public DeviceConfigDoorEntry()
        {
            Direction = string.Empty;
            DoorName = string.Empty;
            Name = string.Empty;
            ZoneName = string.Empty;
        }
    }

    public static class DeviceConfigStore
    {
        private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true,
            PropertyNameCaseInsensitive = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        public static DeviceConfigDocument Load(string path = null)
        {
            path = ResolvePath(path);
            if (!File.Exists(path))
            {
                string legacyXml = Path.Combine(ConfigPaths.ConfigDirectory, "DeviceConfig.xml");
                if (File.Exists(legacyXml))
                {
                    var migrated = LoadFromXml(legacyXml);
                    Save(path, migrated);
                    return migrated;
                }

                return null;
            }

            try
            {
                string json = File.ReadAllText(path, System.Text.Encoding.UTF8);
                var document = JsonSerializer.Deserialize<DeviceConfigDocument>(json, JsonOptions);
                if (document == null)
                {
                    return new DeviceConfigDocument();
                }

                if (document.Config == null)
                {
                    document.Config = DeviceConfigGlobalSettings.CreateDefault();
                }

                if (document.Devices == null)
                {
                    document.Devices = new List<DeviceConfigDeviceEntry>();
                }

                return document;
            }
            catch
            {
                return new DeviceConfigDocument();
            }
        }

        public static void Save(string path, DeviceConfigDocument document)
        {
            path = ResolvePath(path);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            if (document == null)
            {
                document = new DeviceConfigDocument();
            }

            if (document.Config == null)
            {
                document.Config = DeviceConfigGlobalSettings.CreateDefault();
            }

            if (document.Devices == null)
            {
                document.Devices = new List<DeviceConfigDeviceEntry>();
            }

            if (document.Channels == null)
            {
                document.Channels = new List<DeviceConfigChannelEntry>();
            }

            string json = JsonSerializer.Serialize(document, JsonOptions);
            File.WriteAllText(path, json, System.Text.Encoding.UTF8);
        }

        public static void CreateDefault(string path = null)
        {
            var document = new DeviceConfigDocument();
            document.Devices.Add(new DeviceConfigDeviceEntry
            {
                IP = "192.168.0.164",
                UserName = "admin",
                Password = "scyzkj123456",
                Port = 8000,
                Enabled = true,
                Name = "门禁1",
                Remark = "默认门禁设备",
                DeviceID = "8f283fe3ca6947fdaba16db6ef3a7914",
                AreaID = "root000000",
                DeviceName = "测试门禁164",
                Direction = "进",
                ControlDoorNo = 1
            });
            Save(path, document);
        }

        public static string GetApiBaseUrl(string path = null)
        {
            var document = Load(path);
            if (document?.Config == null)
            {
                return string.Empty;
            }

            return (document.Config.ApiBaseUrl ?? string.Empty).Trim();
        }

        public static int GetWebPort(string path = null, int fallback = 8081)
        {
            var document = Load(path);
            int port = document?.Config?.WebPort ?? fallback;
            return port > 0 && port <= 65535 ? port : fallback;
        }

        public static bool UpdateLimitCount(int limitCount, out string errorMessage, string path = null)
        {
            errorMessage = string.Empty;
            try
            {
                path = ResolvePath(path);
                var document = Load(path) ?? new DeviceConfigDocument();
                if (document.Config == null)
                {
                    document.Config = DeviceConfigGlobalSettings.CreateDefault();
                }

                document.Config.LimitCount = limitCount;
                Save(path, document);
                return true;
            }
            catch (Exception ex)
            {
                errorMessage = ex.Message;
                return false;
            }
        }

        public static DeviceConfigDeviceEntry GetDevice(string deviceIP, string path = null)
        {
            return FindDevice(deviceIP, path);
        }

        public static List<DeviceConfigDeviceEntry> GetEnabledDevices(string path = null)
        {
            var document = Load(path);
            var result = new List<DeviceConfigDeviceEntry>();
            if (document?.Devices == null)
            {
                return result;
            }

            foreach (var device in document.Devices)
            {
                if (device == null || !device.Enabled)
                {
                    continue;
                }

                if (string.IsNullOrWhiteSpace(device.IP))
                {
                    continue;
                }

                result.Add(device);
            }

            return result;
        }

        public static List<DeviceConfigChannelEntry> GetChannels(string path = null)
        {
            var document = Load(path);
            var channels = new List<DeviceConfigChannelEntry>();
            if (document?.Channels != null)
            {
                foreach (var channel in document.Channels)
                {
                    if (channel == null || string.IsNullOrWhiteSpace(channel.Id))
                    {
                        continue;
                    }

                    channels.Add(new DeviceConfigChannelEntry
                    {
                        Id = CleanText(channel.Id),
                        Name = string.IsNullOrWhiteSpace(channel.Name) ? CleanText(channel.Id) : CleanText(channel.Name),
                        LimitCount = channel.LimitCount > 0 ? channel.LimitCount : 0
                    });
                }
            }

            if (channels.Count > 0)
            {
                return channels;
            }

            channels.Add(new DeviceConfigChannelEntry
            {
                Id = "default",
                Name = "默认通道",
                LimitCount = document?.Config?.LimitCount > 0 ? document.Config.LimitCount : 500
            });
            return channels;
        }

        public static HashSet<string> GetChannelDeviceIPs(string channelId, string path = null)
        {
            var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (string.IsNullOrWhiteSpace(channelId))
            {
                return result;
            }

            var document = Load(path);
            if (document?.Devices == null)
            {
                return result;
            }

            string normalizedChannelId = CleanText(channelId);
            foreach (var device in document.Devices)
            {
                if (device == null || !device.Enabled || string.IsNullOrWhiteSpace(device.IP))
                {
                    continue;
                }

                string deviceChannelId = CleanText(device.ChannelId);
                if (!string.Equals(deviceChannelId, normalizedChannelId, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                result.Add(CleanText(device.IP));
            }

            return result;
        }

        public static int GetChannelLimitCount(string channelId, string path = null)
        {
            var document = Load(path);
            int globalLimit = document?.Config?.LimitCount > 0 ? document.Config.LimitCount : 500;
            if (string.IsNullOrWhiteSpace(channelId) || document?.Channels == null)
            {
                return globalLimit;
            }

            string normalizedChannelId = CleanText(channelId);
            foreach (var channel in document.Channels)
            {
                if (channel == null || string.IsNullOrWhiteSpace(channel.Id))
                {
                    continue;
                }

                if (!string.Equals(CleanText(channel.Id), normalizedChannelId, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                return channel.LimitCount > 0 ? channel.LimitCount : globalLimit;
            }

            return globalLimit;
        }

        public static int CountChannelDevices(string channelId, string path = null)
        {
            return GetChannelDeviceIPs(channelId, path).Count;
        }

        public static string GetDeviceField(string deviceIP, string fieldName, string path = null)
        {
            var device = FindDevice(deviceIP, path);
            if (device == null || string.IsNullOrEmpty(fieldName))
            {
                return string.Empty;
            }

            switch (fieldName)
            {
                case "IP": return device.IP ?? string.Empty;
                case "UserName": return device.UserName ?? string.Empty;
                case "Password": return device.Password ?? string.Empty;
                case "Port": return device.Port.ToString();
                case "Enabled": return device.Enabled.ToString();
                case "Name": return device.Name ?? string.Empty;
                case "Remark": return device.Remark ?? string.Empty;
                case "DeviceID": return device.DeviceID ?? string.Empty;
                case "AreaID": return device.AreaID ?? string.Empty;
                case "DeviceName": return device.DeviceName ?? string.Empty;
                case "Direction": return device.Direction ?? string.Empty;
                case "ControlDoorNo": return device.ControlDoorNo.ToString();
                case "ZoneName": return device.ZoneName ?? string.Empty;
                case "DoorName": return device.DoorName ?? string.Empty;
                default: return string.Empty;
            }
        }

        public static string GetDoorDirection(string deviceIP, uint doorNo, string path = null)
        {
            try
            {
                var device = FindDevice(deviceIP, path);
                if (device == null)
                {
                    return "未知";
                }

                string deviceDirection = CleanText(device.Direction);
                if (device.Doors == null || device.Doors.Count == 0)
                {
                    if (!string.IsNullOrEmpty(deviceDirection))
                    {
                        return NormalizeDirectionValue(deviceDirection);
                    }

                    return "未知";
                }

                foreach (var door in device.Doors)
                {
                    if (door == null || door.DoorNo != doorNo)
                    {
                        continue;
                    }

                    string direction = CleanText(door.Direction);
                    if (string.IsNullOrEmpty(direction))
                    {
                        direction = deviceDirection;
                    }

                    if (string.IsNullOrEmpty(direction))
                    {
                        return "未知";
                    }

                    return NormalizeDirectionValue(direction);
                }

                if (!string.IsNullOrEmpty(deviceDirection))
                {
                    return NormalizeDirectionValue(deviceDirection);
                }
            }
            catch
            {
            }

            return "未知";
        }

        public static List<DoorControlTarget> ResolveDoorTargetsByZoneName(string zoneName, string path = null)
        {
            var result = new List<DoorControlTarget>();
            var seen = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
            string safeZoneName = CleanText(zoneName);
            if (string.IsNullOrEmpty(safeZoneName))
            {
                return result;
            }

            var document = Load(path);
            if (document?.Devices != null)
            {
                foreach (var device in document.Devices)
                {
                    if (device == null || string.IsNullOrEmpty(device.IP))
                    {
                        continue;
                    }

                    string deviceName = FirstNonEmpty(device.DeviceName, device.Name);
                    string deviceAlias = FirstNonEmpty(device.ZoneName, device.DoorName);
                    string deviceDirection = device.Direction ?? string.Empty;
                    int controlDoorNo = device.ControlDoorNo > 0 ? device.ControlDoorNo : 1;
                    bool matchedDeviceLevel =
                        IsNameMatch(deviceName, safeZoneName) ||
                        IsNameMatch(device.Name, safeZoneName) ||
                        IsNameMatch(deviceAlias, safeZoneName);
                    bool matchedDoorLevel = false;

                    if (device.Doors != null)
                    {
                        foreach (var door in device.Doors)
                        {
                            if (door == null)
                            {
                                continue;
                            }

                            string doorName = FirstNonEmpty(door.DoorName, door.Name, door.ZoneName);
                            if (!IsNameMatch(doorName, safeZoneName))
                            {
                                continue;
                            }

                            matchedDoorLevel = true;
                            int gatewayIndex = door.DoorNo > 0 ? door.DoorNo : controlDoorNo;
                            string direction = FirstNonEmpty(door.Direction, deviceDirection);
                            AddDoorTarget(result, seen, device.IP, deviceName, direction, gatewayIndex, doorName);
                        }
                    }

                    if (!matchedDoorLevel && matchedDeviceLevel)
                    {
                        AddDoorTarget(
                            result,
                            seen,
                            device.IP,
                            deviceName,
                            deviceDirection,
                            controlDoorNo,
                            FirstNonEmpty(deviceAlias, deviceName));
                    }
                }
            }

            return result;
        }

        public static void MergeDevicesByIp(List<Dictionary<string, string>> items, string path = null)
        {
            path = ResolvePath(path);
            var document = Load(path) ?? new DeviceConfigDocument();
            if (document.Devices == null)
            {
                document.Devices = new List<DeviceConfigDeviceEntry>();
            }

            var index = new Dictionary<string, DeviceConfigDeviceEntry>(StringComparer.OrdinalIgnoreCase);
            foreach (var device in document.Devices)
            {
                if (device != null && !string.IsNullOrEmpty(device.IP))
                {
                    index[device.IP.Trim()] = device;
                }
            }

            foreach (var obj in items)
            {
                string deviceID = GetDictValue(obj, "deviceid");
                string areaID = GetDictValue(obj, "areaid");
                string ip = GetDictValue(obj, "ipaddress");
                string deviceName = GetDictValue(obj, "device_name");
                bool ipWasMissing = string.IsNullOrEmpty(ip);
                if (ipWasMissing)
                {
                    ip = "192.168.0.164";
                }

                DeviceConfigDeviceEntry device;
                if (!index.TryGetValue(ip.Trim(), out device))
                {
                    device = new DeviceConfigDeviceEntry { IP = ip };
                    document.Devices.Add(device);
                    index[ip.Trim()] = device;
                }

                if (ipWasMissing)
                {
                    device.UserName = "admin";
                    device.Password = "scyzkj123456";
                    device.Port = 8000;
                    device.Enabled = true;
                    if (string.IsNullOrEmpty(device.Remark))
                    {
                        device.Remark = "位于大厅前门的门禁设备";
                    }
                }
                else
                {
                    device.UserName = string.IsNullOrEmpty(device.UserName) ? "admin" : device.UserName;
                    device.Password = string.IsNullOrEmpty(device.Password) ? "scyzkj123456" : device.Password;
                    if (device.Port == 0)
                    {
                        device.Port = 8000;
                    }
                    device.Enabled = true;
                    if (string.IsNullOrEmpty(device.Remark))
                    {
                        device.Remark = "来自API";
                    }
                }

                if (ipWasMissing)
                {
                    device.DeviceID = string.IsNullOrEmpty(deviceID) ? "8f283fe3ca6947fdaba16db6ef3a7914" : deviceID;
                    device.AreaID = string.IsNullOrEmpty(areaID) ? "root000000" : areaID;
                    string finalName = !string.IsNullOrEmpty(deviceName) ? deviceName : "前门门禁";
                    device.DeviceName = finalName;
                    device.Name = finalName;
                }
                else
                {
                    device.DeviceID = deviceID;
                    device.AreaID = areaID;
                    if (!string.IsNullOrEmpty(deviceName))
                    {
                        device.DeviceName = deviceName;
                        device.Name = deviceName;
                    }
                }
            }

            Save(path, document);
        }

        private static DeviceConfigDeviceEntry FindDevice(string deviceIP, string path)
        {
            var document = Load(path);
            if (document?.Devices == null)
            {
                return null;
            }

            string targetIP = CleanText(deviceIP);
            foreach (var device in document.Devices)
            {
                if (device != null && string.Equals(CleanText(device.IP), targetIP, StringComparison.OrdinalIgnoreCase))
                {
                    return device;
                }
            }

            return null;
        }

        private static string ResolvePath(string path)
        {
            return string.IsNullOrEmpty(path) ? ConfigPaths.DeviceConfigPath : path;
        }

        private static DeviceConfigDocument LoadFromXml(string xmlPath)
        {
            var document = new DeviceConfigDocument();
            var doc = new XmlDocument();
            doc.Load(xmlPath);

            var configNode = doc.SelectSingleNode("//Config");
            if (configNode != null)
            {
                document.Config = new DeviceConfigGlobalSettings
                {
                    ApiBaseUrl = ReadXmlText(configNode, "ApiBaseUrl"),
                    WebPort = ReadXmlInt(configNode, "WebPort", 8081),
                    LimitCount = ReadXmlInt(configNode, "LimitCount", 500),
                    StayWarningMinutes = ReadXmlInt(configNode, "StayWarningMinutes", 30),
                    RecentRecordCount = ReadXmlInt(configNode, "RecentRecordCount", 10),
                    ExitGraceSeconds = ReadXmlInt(configNode, "ExitGraceSeconds", 8),
                    CapacityWarningRatio = ReadXmlDouble(configNode, "CapacityWarningRatio", 0.9d),
                    MqttEnabled = ReadXmlBool(configNode, "MqttEnabled", true),
                    MqttHost = ReadXmlText(configNode, "MqttHost"),
                    MqttPort = ReadXmlInt(configNode, "MqttPort", 1883),
                    MqttTopic = ReadXmlText(configNode, "MqttTopic"),
                    MqttClientId = ReadXmlText(configNode, "MqttClientId"),
                    MqttUsername = ReadXmlText(configNode, "MqttUsername"),
                    MqttPassword = ReadXmlText(configNode, "MqttPassword"),
                    AlarmScanSeconds = ReadXmlInt(configNode, "AlarmScanSeconds", 5),
                    PersonInfoMqttEnabled = ReadXmlBool(configNode, "PersonInfoMqttEnabled", true),
                    PersonInfoMqttHost = ReadXmlText(configNode, "PersonInfoMqttHost"),
                    PersonInfoMqttPort = ReadXmlInt(configNode, "PersonInfoMqttPort", 1883),
                    PersonInfoMqttTopic = ReadXmlText(configNode, "PersonInfoMqttTopic"),
                    PersonInMqttTopic = ReadXmlText(configNode, "PersonInMqttTopic"),
                    PersonOutMqttTopic = ReadXmlText(configNode, "PersonOutMqttTopic"),
                    PersonInfoMqttClientId = ReadXmlText(configNode, "PersonInfoMqttClientId"),
                    AreaAlertMqttEnabled = ReadXmlBool(configNode, "AreaAlertMqttEnabled", true),
                    AreaAlertMqttHost = ReadXmlText(configNode, "AreaAlertMqttHost"),
                    AreaAlertMqttPort = ReadXmlInt(configNode, "AreaAlertMqttPort", 1883),
                    AreaAlertMqttTopic = ReadXmlText(configNode, "AreaAlertMqttTopic"),
                    AreaAlertMqttClientId = ReadXmlText(configNode, "AreaAlertMqttClientId"),
                    AbnormalMqttEnabled = ReadXmlBool(configNode, "AbnormalMqttEnabled", true),
                    AbnormalMqttHost = ReadXmlText(configNode, "AbnormalMqttHost"),
                    AbnormalMqttPort = ReadXmlInt(configNode, "AbnormalMqttPort", 1883),
                    AbnormalMqttTopic = ReadXmlText(configNode, "AbnormalMqttTopic"),
                    AbnormalMqttClientId = ReadXmlText(configNode, "AbnormalMqttClientId"),
                    PeopleCountMqttEnabled = ReadXmlBool(configNode, "PeopleCountMqttEnabled", true),
                    PeopleCountMqttHost = ReadXmlText(configNode, "PeopleCountMqttHost"),
                    PeopleCountMqttPort = ReadXmlInt(configNode, "PeopleCountMqttPort", 1883),
                    PeopleCountMqttTopic = ReadXmlText(configNode, "PeopleCountMqttTopic"),
                    PeopleCountMqttClientId = ReadXmlText(configNode, "PeopleCountMqttClientId")
                };
            }

            var deviceNodes = doc.SelectNodes("//Devices/Device");
            if (deviceNodes != null)
            {
                foreach (XmlNode deviceNode in deviceNodes)
                {
                    var entry = new DeviceConfigDeviceEntry
                    {
                        IP = ReadXmlText(deviceNode, "IP"),
                        UserName = ReadXmlText(deviceNode, "UserName"),
                        Password = ReadXmlText(deviceNode, "Password"),
                        Port = (ushort)ReadXmlInt(deviceNode, "Port", 8000),
                        Enabled = ReadXmlBool(deviceNode, "Enabled", true),
                        Name = ReadXmlText(deviceNode, "Name"),
                        Remark = ReadXmlText(deviceNode, "Remark"),
                        DeviceID = ReadXmlText(deviceNode, "DeviceID"),
                        AreaID = ReadXmlText(deviceNode, "AreaID"),
                        DeviceName = ReadXmlText(deviceNode, "DeviceName"),
                        Direction = ReadXmlText(deviceNode, "Direction"),
                        ControlDoorNo = ReadXmlInt(deviceNode, "ControlDoorNo", 1),
                        ZoneName = ReadXmlText(deviceNode, "ZoneName"),
                        DoorName = ReadXmlText(deviceNode, "DoorName")
                    };

                    var doorNodes = deviceNode.SelectNodes("Doors/Door");
                    if (doorNodes != null)
                    {
                        foreach (XmlNode doorNode in doorNodes)
                        {
                            entry.Doors.Add(new DeviceConfigDoorEntry
                            {
                                DoorNo = ReadXmlInt(doorNode, "DoorNo", 0),
                                Direction = ReadXmlText(doorNode, "Direction"),
                                DoorName = ReadXmlText(doorNode, "DoorName"),
                                Name = ReadXmlText(doorNode, "Name"),
                                ZoneName = ReadXmlText(doorNode, "ZoneName")
                            });
                        }
                    }

                    document.Devices.Add(entry);
                }
            }

            return document;
        }

        private static string ReadXmlText(XmlNode parent, string childName)
        {
            if (parent == null || string.IsNullOrEmpty(childName))
            {
                return string.Empty;
            }

            XmlNode node = parent.SelectSingleNode(childName);
            return node == null ? string.Empty : CleanText(node.InnerText);
        }

        private static int ReadXmlInt(XmlNode parent, string childName, int fallback)
        {
            int value;
            return int.TryParse(ReadXmlText(parent, childName), out value) ? value : fallback;
        }

        private static double ReadXmlDouble(XmlNode parent, string childName, double fallback)
        {
            double value;
            return double.TryParse(ReadXmlText(parent, childName), out value) ? value : fallback;
        }

        private static bool ReadXmlBool(XmlNode parent, string childName, bool fallback)
        {
            bool value;
            return bool.TryParse(ReadXmlText(parent, childName), out value) ? value : fallback;
        }

        private static string GetDictValue(Dictionary<string, string> obj, string key)
        {
            string value;
            return obj != null && obj.TryGetValue(key, out value) ? value : string.Empty;
        }

        private static void AddDoorTarget(
            List<DoorControlTarget> targets,
            Dictionary<string, bool> seen,
            string deviceIP,
            string deviceName,
            string direction,
            int gatewayIndex,
            string matchedName)
        {
            int safeGatewayIndex = gatewayIndex > 0 ? gatewayIndex : 1;
            string gateKey = BuildGateKey(deviceIP, safeGatewayIndex);
            if (seen.ContainsKey(gateKey))
            {
                return;
            }

            seen[gateKey] = true;
            targets.Add(new DoorControlTarget
            {
                DeviceIP = CleanText(deviceIP),
                DeviceName = CleanText(deviceName),
                Direction = CleanText(direction),
                GatewayIndex = safeGatewayIndex,
                MatchedName = CleanText(matchedName)
            });
        }

        private static string BuildGateKey(string deviceIP, int gatewayIndex)
        {
            return CleanText(deviceIP) + "#" + gatewayIndex;
        }

        private static string CleanText(string value)
        {
            return string.IsNullOrEmpty(value) ? string.Empty : value.Trim();
        }

        private static string FirstNonEmpty(params string[] values)
        {
            if (values == null)
            {
                return string.Empty;
            }

            foreach (string value in values)
            {
                string cleaned = CleanText(value);
                if (!string.IsNullOrEmpty(cleaned))
                {
                    return cleaned;
                }
            }

            return string.Empty;
        }

        private static bool IsNameMatch(string candidate, string expected)
        {
            string left = CleanText(candidate);
            string right = CleanText(expected);
            if (string.IsNullOrEmpty(left) || string.IsNullOrEmpty(right))
            {
                return false;
            }

            return string.Equals(left, right, StringComparison.OrdinalIgnoreCase);
        }

        private static string NormalizeDirectionValue(string direction)
        {
            string value = CleanText(direction);
            if (string.IsNullOrEmpty(value))
            {
                return "未知";
            }

            if (value.IndexOf("进", StringComparison.OrdinalIgnoreCase) >= 0 ||
                value.Equals("in", StringComparison.OrdinalIgnoreCase) ||
                value.Equals("entry", StringComparison.OrdinalIgnoreCase))
            {
                return "进";
            }

            if (value.IndexOf("出", StringComparison.OrdinalIgnoreCase) >= 0 ||
                value.Equals("out", StringComparison.OrdinalIgnoreCase) ||
                value.Equals("exit", StringComparison.OrdinalIgnoreCase))
            {
                return "出";
            }

            return value;
        }
    }
}
