using System;
using System.Collections.Generic;

namespace GetACSEvent
{
    public class ChannelDeviceStatusItem
    {
        public string Ip { get; set; }
        public string Name { get; set; }
        public string DeviceName { get; set; }
        public string Direction { get; set; }
        public bool Online { get; set; }
        public string Status { get; set; }
    }

    public class ChannelOverviewItem
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public int LimitCount { get; set; }
        public int EnterCount { get; set; }
        public int ExitCount { get; set; }
        public int StayCount { get; set; }
        public int AlarmCount { get; set; }
        public string AccessRuleMode { get; set; }
        public int DeviceCount { get; set; }
        public int OnlineDeviceCount { get; set; }
        public List<ChannelDeviceStatusItem> Devices { get; set; }
    }

    public class ChannelOverviewPayload
    {
        public string GeneratedAt { get; set; }
        public List<ChannelOverviewItem> Channels { get; set; }
    }

    public class ChannelOverviewBuilder
    {
        private readonly List<AcsEvent> _events;

        public ChannelOverviewBuilder(List<AcsEvent> events)
        {
            _events = events ?? new List<AcsEvent>();
        }

        public ChannelOverviewPayload Build()
        {
            var channels = DeviceConfigStore.GetChannels();
            var items = new List<ChannelOverviewItem>();
            foreach (var channel in channels)
            {
                if (channel == null || string.IsNullOrWhiteSpace(channel.Id))
                {
                    continue;
                }

                DashboardPayload payload = new DashboardDataBuilder(_events, channel.Id).Build();
                int stayCount = 0;
                int enterCount = 0;
                int exitCount = 0;
                int limitCount = DeviceConfigStore.GetChannelLimitCount(channel.Id);

                if (payload.Metrics != null)
                {
                    foreach (var metric in payload.Metrics)
                    {
                        if (metric == null || string.IsNullOrEmpty(metric.Label))
                        {
                            continue;
                        }

                        if (metric.Label == "区域内停留人员")
                        {
                            stayCount = metric.Value;
                        }
                        else if (metric.Label == "进场人数")
                        {
                            enterCount = metric.Value;
                        }
                        else if (metric.Label == "出场人数")
                        {
                            exitCount = metric.Value;
                        }
                        else if (metric.Label == "限制人数")
                        {
                            limitCount = metric.Value;
                        }
                    }
                }

                items.Add(new ChannelOverviewItem
                {
                    Id = channel.Id,
                    Name = channel.Name,
                    LimitCount = limitCount,
                    EnterCount = enterCount,
                    ExitCount = exitCount,
                    StayCount = stayCount,
                    AlarmCount = payload.Alarms == null ? 0 : payload.Alarms.Count,
                    AccessRuleMode = ResolveAccessRuleMode(stayCount, limitCount),
                    DeviceCount = DeviceConfigStore.CountChannelDevices(channel.Id),
                    Devices = BuildChannelDevices(channel.Id),
                    OnlineDeviceCount = CountOnlineDevices(channel.Id)
                });
            }

            return new ChannelOverviewPayload
            {
                GeneratedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                Channels = items
            };
        }

        private static string ResolveAccessRuleMode(int stayCount, int limitCount)
        {
            if (stayCount > limitCount)
            {
                return "blocked";
            }

            if (stayCount == limitCount)
            {
                return "exit-only";
            }

            if (stayCount == limitCount - 1)
            {
                return "warning";
            }

            return "normal";
        }

        private static List<ChannelDeviceStatusItem> BuildChannelDevices(string channelId)
        {
            var result = new List<ChannelDeviceStatusItem>();
            var document = DeviceConfigStore.Load();
            if (document?.Devices == null)
            {
                return result;
            }

            ACSEventMultiDeviceService serviceInstance = ACSEventMultiDeviceService.SharedInstance;
            string normalizedChannelId = (channelId ?? string.Empty).Trim();
            foreach (var device in document.Devices)
            {
                if (device == null || !device.Enabled || string.IsNullOrWhiteSpace(device.IP))
                {
                    continue;
                }

                string deviceChannelId = (device.ChannelId ?? string.Empty).Trim();
                if (!string.Equals(deviceChannelId, normalizedChannelId, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                bool online = serviceInstance != null && serviceInstance.IsDeviceOnline(device.IP);
                result.Add(new ChannelDeviceStatusItem
                {
                    Ip = device.IP.Trim(),
                    Name = string.IsNullOrWhiteSpace(device.Name) ? device.IP.Trim() : device.Name.Trim(),
                    DeviceName = string.IsNullOrWhiteSpace(device.DeviceName) ? device.IP.Trim() : device.DeviceName.Trim(),
                    Direction = string.IsNullOrWhiteSpace(device.Direction) ? "未知" : device.Direction.Trim(),
                    Online = online,
                    Status = online ? "在线" : "离线"
                });
            }

            return result;
        }

        private static int CountOnlineDevices(string channelId)
        {
            int count = 0;
            foreach (var device in BuildChannelDevices(channelId))
            {
                if (device.Online)
                {
                    count++;
                }
            }

            return count;
        }
    }
}
