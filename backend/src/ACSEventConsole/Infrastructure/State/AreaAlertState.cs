using System;

namespace ACSEventConsole.Infrastructure.State
{
    public class AreaAlertInfo
    {
        public bool IsActive { get; set; }
        public int HasPeople { get; set; }
        public string ZoneName { get; set; }
        public string AlertId { get; set; }
        public string TriggeredAt { get; set; }
        public string UpdatedAt { get; set; }
        public string SourceTopic { get; set; }
        public string RawPayload { get; set; }
    }

    public static class AreaAlertState
    {
        private static readonly object SyncRoot = new object();
        private static AreaAlertInfo _current = CreateDefault();

        public static AreaAlertInfo GetSnapshot()
        {
            lock (SyncRoot)
            {
                return Clone(_current);
            }
        }

        public static void Update(bool isActive, int hasPeople, string zoneName, string topic, string rawPayload)
        {
            lock (SyncRoot)
            {
                string now = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                string safeZoneName = string.IsNullOrEmpty(zoneName) ? "未知区域" : zoneName.Trim();
                string alertId = _current.AlertId;
                string triggeredAt = _current.TriggeredAt;

                if (isActive)
                {
                    bool isNewActivation = !_current.IsActive ||
                        !string.Equals(_current.ZoneName, safeZoneName, StringComparison.OrdinalIgnoreCase) ||
                        _current.HasPeople != hasPeople;

                    if (isNewActivation)
                    {
                        alertId = "area-alert-" + DateTime.Now.Ticks;
                        triggeredAt = now;
                    }
                }
                else
                {
                    alertId = string.Empty;
                    triggeredAt = string.Empty;
                }

                _current = new AreaAlertInfo
                {
                    IsActive = isActive,
                    HasPeople = hasPeople,
                    ZoneName = safeZoneName,
                    AlertId = alertId,
                    TriggeredAt = triggeredAt,
                    UpdatedAt = now,
                    SourceTopic = topic ?? string.Empty,
                    RawPayload = rawPayload ?? string.Empty
                };
            }
        }

        public static void MarkDisconnected(string topic)
        {
            lock (SyncRoot)
            {
                _current.UpdatedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                _current.SourceTopic = topic ?? string.Empty;
            }
        }

        private static AreaAlertInfo CreateDefault()
        {
            return new AreaAlertInfo
            {
                IsActive = false,
                HasPeople = 0,
                ZoneName = string.Empty,
                AlertId = string.Empty,
                TriggeredAt = string.Empty,
                UpdatedAt = string.Empty,
                SourceTopic = string.Empty,
                RawPayload = string.Empty
            };
        }

        private static AreaAlertInfo Clone(AreaAlertInfo source)
        {
            if (source == null)
            {
                return CreateDefault();
            }

            return new AreaAlertInfo
            {
                IsActive = source.IsActive,
                HasPeople = source.HasPeople,
                ZoneName = source.ZoneName,
                AlertId = source.AlertId,
                TriggeredAt = source.TriggeredAt,
                UpdatedAt = source.UpdatedAt,
                SourceTopic = source.SourceTopic,
                RawPayload = source.RawPayload
            };
        }
    }
}
