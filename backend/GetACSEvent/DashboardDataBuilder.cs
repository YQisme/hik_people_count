using System;
using System.Collections.Generic;

namespace GetACSEvent
{
    public class DashboardPayload
    {
        public string GeneratedAt { get; set; }
        public List<DashboardMetric> Metrics { get; set; }
        public List<DashboardAlarm> Alarms { get; set; }
        public List<DashboardRecord> RecentRecords { get; set; }
        public List<DashboardRecord> StayPeople { get; set; }
        public string SelectedRecordId { get; set; }
        public AreaAlertInfo AreaAlert { get; set; }
        public List<AbnormalMessageInfo> AbnormalMessages { get; set; }
    }

    public class DashboardMetric
    {
        public string Label { get; set; }
        public int Value { get; set; }
        public string Unit { get; set; }
        public string Accent { get; set; }
    }

    public class DashboardAlarm
    {
        public string Id { get; set; }
        public string Code { get; set; }
        public string Category { get; set; }
        public string Level { get; set; }
        public string Title { get; set; }
        public string Detail { get; set; }
        public string Status { get; set; }
        public string TargetId { get; set; }
        public string TargetName { get; set; }
        public string Gate { get; set; }
        public string DeviceIP { get; set; }
        public string TriggeredAt { get; set; }
    }

    public class DashboardRecord
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Department { get; set; }
        public string Role { get; set; }
        public string EnterTime { get; set; }
        public string Gate { get; set; }
        public string AvatarText { get; set; }
        public string Card { get; set; }
        public string Location { get; set; }
        public string Phone { get; set; }
        public string Team { get; set; }
        public string Status { get; set; }
        public string StayDuration { get; set; }
        public string Direction { get; set; }
        public string DeviceIP { get; set; }
        public string ImageUrl { get; set; }
        public bool IsWarning { get; set; }
        public DateTime SortTimeUtc { get; set; }
        public long SortDurationSeconds { get; set; }
    }

    internal class DashboardConfig
    {
        public int LimitCount;
        public int StayWarningMinutes;
        public int RecentRecordCount;
        public double CapacityWarningRatio;

        public DashboardConfig(RuntimeConfig runtime)
        {
            LimitCount = runtime == null ? 500 : runtime.LimitCount;
            StayWarningMinutes = runtime == null ? 30 : runtime.StayWarningMinutes;
            RecentRecordCount = runtime == null ? 10 : runtime.RecentRecordCount;
            CapacityWarningRatio = runtime == null ? 0.9d : runtime.CapacityWarningRatio;
        }
    }

    public class DashboardDataBuilder
    {
        private readonly List<AcsEvent> _events;
        private readonly HashSet<string> _deviceFilter;
        private readonly int? _overrideLimitCount;

        public DashboardDataBuilder(List<AcsEvent> events, string channelId = null)
        {
            _events = events ?? new List<AcsEvent>();
            if (!string.IsNullOrWhiteSpace(channelId))
            {
                _deviceFilter = DeviceConfigStore.GetChannelDeviceIPs(channelId);
                int limitCount = DeviceConfigStore.GetChannelLimitCount(channelId);
                if (limitCount > 0)
                {
                    _overrideLimitCount = limitCount;
                }
            }
        }

        public DashboardPayload Build()
        {
            var config = ReadConfig();
            var filteredEvents = GetFilteredEvents();
            var sortedEvents = new List<AcsEvent>(filteredEvents);
            sortedEvents.Sort(delegate (AcsEvent a, AcsEvent b)
            {
                return b.TimeUtc.CompareTo(a.TimeUtc);
            });

            var recentRecords = BuildRecentRecords(sortedEvents, config);
            var stayPeople = BuildStayPeople(config);
            var peopleCount = PeopleCountState.GetSnapshot();
            var metrics = BuildMetrics(sortedEvents, stayPeople, config, peopleCount);
            var areaAlert = AreaAlertState.GetSnapshot();
            var abnormalMessages = AbnormalMessageState.GetSnapshot();
            var alarms = BuildAlarms(stayPeople, config, areaAlert, peopleCount);

            string selectedRecordId = string.Empty;
            if (recentRecords.Count > 0)
            {
                selectedRecordId = recentRecords[0].Id;
            }
            else if (stayPeople.Count > 0)
            {
                selectedRecordId = stayPeople[0].Id;
            }

            return new DashboardPayload
            {
                GeneratedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                Metrics = metrics,
                Alarms = alarms,
                RecentRecords = recentRecords,
                StayPeople = stayPeople,
                SelectedRecordId = selectedRecordId,
                AreaAlert = areaAlert,
                AbnormalMessages = abnormalMessages
            };
        }

        private List<DashboardRecord> BuildRecentRecords(List<AcsEvent> sortedEvents, DashboardConfig config)
        {
            var list = new List<DashboardRecord>();
            int count = sortedEvents.Count < config.RecentRecordCount ? sortedEvents.Count : config.RecentRecordCount;
            for (int i = 0; i < count; i++)
            {
                var record = CreateRecord(sortedEvents[i], null, config);
                record.Status = ResolvePassStatus(sortedEvents[i]);
                list.Add(record);
            }
            return list;
        }

        private List<DashboardRecord> BuildStayPeople(DashboardConfig config)
        {
            var latestPerPerson = new Dictionary<string, AcsEvent>(StringComparer.OrdinalIgnoreCase);
            var eventsAsc = new List<AcsEvent>(GetFilteredEvents());
            eventsAsc.Sort(delegate (AcsEvent a, AcsEvent b)
            {
                return a.TimeUtc.CompareTo(b.TimeUtc);
            });

            foreach (var ev in eventsAsc)
            {
                string key = GetPersonKey(ev);
                if (string.IsNullOrEmpty(key))
                {
                    continue;
                }

                latestPerPerson[key] = ev;
            }

            var result = new List<DashboardRecord>();
            foreach (var pair in latestPerPerson)
            {
                if (!IsEntryEvent(pair.Value))
                {
                    continue;
                }

                TimeSpan stayDuration = DateTime.UtcNow - pair.Value.TimeUtc;
                var record = CreateRecord(pair.Value, stayDuration, config);
                record.IsWarning = stayDuration.TotalMinutes >= config.StayWarningMinutes;
                record.Status = record.IsWarning ? "停留预警" : "停留正常";
                result.Add(record);
            }

            result.Sort(delegate (DashboardRecord a, DashboardRecord b)
            {
                return b.SortDurationSeconds.CompareTo(a.SortDurationSeconds);
            });

            return result;
        }

        private List<DashboardMetric> BuildMetrics(List<AcsEvent> sortedEvents, List<DashboardRecord> stayPeople, DashboardConfig config, PeopleCountInfo peopleCount)
        {
            int enterCount = 0;
            int exitCount = 0;
            int totalCount = stayPeople.Count;

            foreach (var ev in sortedEvents)
            {
                if (IsEntryEvent(ev))
                {
                    enterCount++;
                }
                else if (IsExitEvent(ev))
                {
                    exitCount++;
                }
            }

            if (peopleCount != null && peopleCount.HasData && !HasDeviceFilter())
            {
                enterCount = peopleCount.InCount;
                exitCount = peopleCount.OutCount;
                totalCount = peopleCount.TotalCount;
            }

            return new List<DashboardMetric>
            {
                new DashboardMetric { Label = "进场人数", Value = enterCount, Unit = "人", Accent = "cyan" },
                new DashboardMetric { Label = "出场人数", Value = exitCount, Unit = "人", Accent = "teal" },
                new DashboardMetric { Label = "限制人数", Value = config.LimitCount, Unit = "人", Accent = "amber" },
                new DashboardMetric { Label = "区域内停留人员", Value = totalCount, Unit = "人", Accent = "lime" }
            };
        }

        private List<DashboardAlarm> BuildAlarms(List<DashboardRecord> stayPeople, DashboardConfig config, AreaAlertInfo areaAlert, PeopleCountInfo peopleCount)
        {
            var alarms = new List<DashboardAlarm>();
            int currentStayCount = stayPeople.Count;
            string triggeredAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

            if (peopleCount != null && peopleCount.HasData && !HasDeviceFilter())
            {
                currentStayCount = peopleCount.TotalCount;
                if (!string.IsNullOrEmpty(peopleCount.UpdatedAt))
                {
                    triggeredAt = peopleCount.UpdatedAt;
                }
            }

            if (areaAlert != null && areaAlert.IsActive && !HasDeviceFilter())
            {
                alarms.Add(new DashboardAlarm
                {
                    Id = string.IsNullOrEmpty(areaAlert.AlertId) ? "area-alert" : areaAlert.AlertId,
                    Code = "AREA_INTRUSION",
                    Category = "area",
                    Level = "区域报警",
                    Title = "检测到区域报警",
                    Detail = string.Format("{0} 触发区域报警，检测人数 {1}", string.IsNullOrEmpty(areaAlert.ZoneName) ? "未知区域" : areaAlert.ZoneName, areaAlert.HasPeople),
                    Status = "处理中",
                    TargetId = string.Empty,
                    TargetName = string.Empty,
                    Gate = areaAlert.ZoneName,
                    DeviceIP = string.Empty,
                    TriggeredAt = areaAlert.TriggeredAt
                });
            }

            if (currentStayCount > config.LimitCount)
            {
                alarms.Add(new DashboardAlarm
                {
                    Id = string.Format("capacity-exceeded-{0}-{1}", config.LimitCount, currentStayCount),
                    Code = "CAPACITY_EXCEEDED",
                    Category = "capacity",
                    Level = "严重",
                    Title = "区域超员报警",
                    Detail = string.Format("区域内停留 {0} 人，已超出限制人数 {1} 人，请立即疏导，仅允许出场。", currentStayCount, config.LimitCount),
                    Status = "立即处理",
                    TargetId = string.Empty,
                    TargetName = string.Empty,
                    Gate = string.Empty,
                    DeviceIP = string.Empty,
                    TriggeredAt = triggeredAt
                });
            }
            else if (currentStayCount == config.LimitCount)
            {
                alarms.Add(new DashboardAlarm
                {
                    Id = string.Format("capacity-full-{0}", config.LimitCount),
                    Code = "CAPACITY_FULL",
                    Category = "capacity",
                    Level = "高",
                    Title = "区域满员预警",
                    Detail = string.Format("区域人数已达限制人数 {0} 人，当前可以出但不能进。", config.LimitCount),
                    Status = "只出不进",
                    TargetId = string.Empty,
                    TargetName = string.Empty,
                    Gate = string.Empty,
                    DeviceIP = string.Empty,
                    TriggeredAt = triggeredAt
                });
            }
            else if (currentStayCount == config.LimitCount - 1)
            {
                alarms.Add(new DashboardAlarm
                {
                    Id = string.Format("capacity-near-limit-{0}-{1}", config.LimitCount, currentStayCount),
                    Code = "CAPACITY_NEAR_LIMIT",
                    Category = "capacity",
                    Level = "中",
                    Title = "限员临界预警",
                    Detail = string.Format("区域内停留 {0} 人，距离限制人数 {1} 仅差 1 人，请提前预警。", currentStayCount, config.LimitCount),
                    Status = "预警中",
                    TargetId = string.Empty,
                    TargetName = string.Empty,
                    Gate = string.Empty,
                    DeviceIP = string.Empty,
                    TriggeredAt = triggeredAt
                });
            }

            int warningCount = 0;
            foreach (var person in stayPeople)
            {
                if (!person.IsWarning)
                {
                    continue;
                }

                alarms.Add(new DashboardAlarm
                {
                    Id = "stay-" + person.Id,
                    Code = "STAY_TIMEOUT",
                    Category = "stay",
                    Level = "行为告警",
                    Title = "检测到异常滞留",
                    Detail = string.Format("{0} 已停留 {1}，当前位置 {2}", person.Name, person.StayDuration, person.Location),
                    Status = "处理中",
                    TargetId = person.Id,
                    TargetName = person.Name,
                    Gate = person.Gate,
                    DeviceIP = person.DeviceIP,
                    TriggeredAt = person.EnterTime
                });

                warningCount++;
                if (warningCount >= 5)
                {
                    break;
                }
            }

            return alarms;
        }

        private DashboardRecord CreateRecord(AcsEvent ev, TimeSpan? stayDuration, DashboardConfig config)
        {
            var employee = EmployeeDirectory.FindBestMatch(ev.EmployeeNo, ev.CardNo, ev.PersonName);
            string id = FirstNonEmpty(employee, "employeeId", "employeeNo", "jobNo", "workNo", "staffNo", "personId", "id");
            if (string.IsNullOrEmpty(id))
            {
                id = CleanValue(ev.EmployeeNo);
            }
            if (string.IsNullOrEmpty(id))
            {
                id = CleanValue(ev.CardNo);
            }
            if (string.IsNullOrEmpty(id))
            {
                id = string.Format("{0}-{1}-{2}", CleanValue(ev.DeviceIP), ev.DoorNo, ev.TimeUtc.Ticks);
            }

            string name = ResolveName(ev, employee, id);
            string gate = BuildGate(ev, employee);
            string location = BuildLocation(ev, employee, gate);
            string card = FirstNonEmpty(employee, "cardType", "card", "cardName");
            if (string.IsNullOrEmpty(card))
            {
                card = CleanValue(ev.CardType);
            }
            if (string.IsNullOrEmpty(card))
            {
                card = "门禁识别";
            }

            var record = new DashboardRecord
            {
                Id = id,
                Name = name,
                Department = DefaultText(FirstNonEmpty(employee, "department", "departmentName", "deptName", "orgName", "organizationName"), "未分配部门"),
                Role = DefaultText(FirstNonEmpty(employee, "role", "post", "position", "jobTitle", "duty"), "未设置岗位"),
                EnterTime = ResolveEventTime(ev),
                Gate = gate,
                AvatarText = BuildAvatarText(name, id),
                Card = card,
                Location = location,
                Phone = DefaultText(FirstNonEmpty(employee, "phone", "mobile", "mobilePhone", "telephone", "contactPhone", "tel"), "未登记"),
                Team = DefaultText(FirstNonEmpty(employee, "team", "teamName", "group", "groupName", "workGroup"), "未分组"),
                Status = ResolvePassStatus(ev),
                StayDuration = stayDuration.HasValue ? FormatDuration(stayDuration.Value) : string.Empty,
                Direction = NormalizeDirection(ev.Direction),
                DeviceIP = CleanValue(ev.DeviceIP),
                ImageUrl = CleanValue(ev.ImageUrl),
                IsWarning = stayDuration.HasValue && stayDuration.Value.TotalMinutes >= config.StayWarningMinutes,
                SortTimeUtc = ev.TimeUtc,
                SortDurationSeconds = stayDuration.HasValue ? (long)stayDuration.Value.TotalSeconds : 0L
            };

            return record;
        }

        private static string ResolveName(AcsEvent ev, Dictionary<string, string> employee, string fallbackId)
        {
            string eventName = CleanValue(ev.PersonName);
            if (!string.IsNullOrEmpty(eventName) &&
                !eventName.StartsWith("未知", StringComparison.OrdinalIgnoreCase))
            {
                return eventName;
            }

            string employeeName = FirstNonEmpty(employee, "name", "employeeName", "personName", "realName", "staffName");
            if (!string.IsNullOrEmpty(employeeName))
            {
                return employeeName;
            }

            if (!string.IsNullOrEmpty(eventName))
            {
                return eventName;
            }

            return string.IsNullOrEmpty(fallbackId) ? "未知人员" : "未知人员(" + fallbackId + ")";
        }

        private static string BuildGate(AcsEvent ev, Dictionary<string, string> employee)
        {
            string gate = FirstNonEmpty(employee, "gate", "gateName", "entranceName");
            if (!string.IsNullOrEmpty(gate))
            {
                return gate;
            }

            string deviceName = CleanValue(ev.DeviceName);
            if (string.IsNullOrEmpty(deviceName))
            {
                deviceName = CleanValue(ev.Remark);
            }
            if (string.IsNullOrEmpty(deviceName))
            {
                deviceName = string.IsNullOrEmpty(ev.DeviceIP) ? "门禁设备" : ev.DeviceIP;
            }

            if (ev.DoorNo > 0)
            {
                return string.Format("{0} / {1}号门", deviceName, ev.DoorNo);
            }

            return deviceName;
        }

        private static string BuildLocation(AcsEvent ev, Dictionary<string, string> employee, string gate)
        {
            string location = FirstNonEmpty(employee, "location", "locationName", "address", "areaName", "workArea");
            if (!string.IsNullOrEmpty(location))
            {
                return location;
            }

            string remark = CleanValue(ev.Remark);
            if (!string.IsNullOrEmpty(remark))
            {
                return remark;
            }

            return gate;
        }

        private static string ResolveEventTime(AcsEvent ev)
        {
            string value = CleanValue(ev.Time);
            if (!string.IsNullOrEmpty(value))
            {
                return value;
            }

            return ev.TimeUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss");
        }

        private static string ResolvePassStatus(AcsEvent ev)
        {
            if (IsExitEvent(ev))
            {
                return "离场记录";
            }

            if (IsEntryEvent(ev))
            {
                return "识别通过";
            }

            return "待人工复核";
        }

        private static bool IsEntryEvent(AcsEvent ev)
        {
            string direction = NormalizeDirection(ev.Direction);
            if (direction == "进")
            {
                return true;
            }

            if (direction == "出")
            {
                return false;
            }

            return !string.IsNullOrEmpty(CleanValue(ev.EmployeeNo)) || !string.IsNullOrEmpty(CleanValue(ev.CardNo));
        }

        private static bool IsExitEvent(AcsEvent ev)
        {
            return NormalizeDirection(ev.Direction) == "出";
        }

        private static string NormalizeDirection(string rawDirection)
        {
            string value = CleanValue(rawDirection).ToLowerInvariant();
            if (value == "in" || value == "进入" || value == "进")
            {
                return "进";
            }

            if (value == "out" || value == "出去" || value == "出")
            {
                return "出";
            }

            return string.Empty;
        }

        private static string GetPersonKey(AcsEvent ev)
        {
            string value = CleanValue(ev.EmployeeNo);
            if (!string.IsNullOrEmpty(value))
            {
                return value;
            }

            value = CleanValue(ev.CardNo);
            if (!string.IsNullOrEmpty(value))
            {
                return value;
            }

            value = CleanValue(ev.PersonName);
            if (!string.IsNullOrEmpty(value))
            {
                return value;
            }

            return string.Empty;
        }

        private static string BuildAvatarText(string name, string id)
        {
            string source = CleanValue(name);
            if (!string.IsNullOrEmpty(source) && source.Length >= 2)
            {
                return source.Substring(0, 2).ToUpperInvariant();
            }

            if (!string.IsNullOrEmpty(source))
            {
                return source.ToUpperInvariant();
            }

            source = CleanValue(id);
            if (source.Length > 2)
            {
                return source.Substring(source.Length - 2).ToUpperInvariant();
            }

            return string.IsNullOrEmpty(source) ? "--" : source.ToUpperInvariant();
        }

        private static string FormatDuration(TimeSpan duration)
        {
            if (duration.TotalSeconds < 0)
            {
                duration = TimeSpan.Zero;
            }

            int hours = (int)duration.TotalHours;
            return string.Format("{0:D2}:{1:D2}:{2:D2}", hours, duration.Minutes, duration.Seconds);
        }

        private static string DefaultText(string value, string fallback)
        {
            return string.IsNullOrEmpty(CleanValue(value)) ? fallback : CleanValue(value);
        }

        private static string FirstNonEmpty(Dictionary<string, string> source, params string[] keys)
        {
            if (source == null)
            {
                return string.Empty;
            }

            foreach (var key in keys)
            {
                string value;
                if (source.TryGetValue(key, out value) && !string.IsNullOrEmpty(CleanValue(value)))
                {
                    return CleanValue(value);
                }
            }

            return string.Empty;
        }

        private static string CleanValue(string value)
        {
            return (value ?? string.Empty).Trim();
        }

        private DashboardConfig ReadConfig()
        {
            var config = new DashboardConfig(RuntimeConfig.LoadDefault());
            if (_overrideLimitCount.HasValue)
            {
                config.LimitCount = _overrideLimitCount.Value;
            }

            return config;
        }

        private List<AcsEvent> GetFilteredEvents()
        {
            if (!HasDeviceFilter())
            {
                return _events;
            }

            var filtered = new List<AcsEvent>();
            foreach (var ev in _events)
            {
                if (ev == null || string.IsNullOrEmpty(ev.DeviceIP))
                {
                    continue;
                }

                if (_deviceFilter.Contains(ev.DeviceIP))
                {
                    filtered.Add(ev);
                }
            }

            return filtered;
        }

        private bool HasDeviceFilter()
        {
            return _deviceFilter != null && _deviceFilter.Count > 0;
        }
    }
}




