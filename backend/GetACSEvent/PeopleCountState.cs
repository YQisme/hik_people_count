using System;

namespace GetACSEvent
{
    public class PeopleCountInfo
    {
        public bool HasData { get; set; }
        public string ZoneId { get; set; }
        public string ZoneName { get; set; }
        public int InCount { get; set; }
        public int OutCount { get; set; }
        public int TotalCount { get; set; }
        public string UpdatedAt { get; set; }
        public string SourceTopic { get; set; }
        public string RawPayload { get; set; }
    }

    public static class PeopleCountState
    {
        private static readonly object SyncRoot = new object();
        private static PeopleCountInfo _current = CreateDefault();

        public static PeopleCountInfo GetSnapshot()
        {
            lock (SyncRoot)
            {
                return Clone(_current);
            }
        }

        public static void Update(string zoneId, string zoneName, int inCount, int outCount, int totalCount, string topic, string rawPayload)
        {
            lock (SyncRoot)
            {
                _current = new PeopleCountInfo
                {
                    HasData = true,
                    ZoneId = (zoneId ?? string.Empty).Trim(),
                    ZoneName = (zoneName ?? string.Empty).Trim(),
                    InCount = inCount,
                    OutCount = outCount,
                    TotalCount = totalCount,
                    UpdatedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                    SourceTopic = topic ?? string.Empty,
                    RawPayload = rawPayload ?? string.Empty
                };
            }
        }

        private static PeopleCountInfo CreateDefault()
        {
            return new PeopleCountInfo
            {
                HasData = false,
                ZoneId = string.Empty,
                ZoneName = string.Empty,
                InCount = 0,
                OutCount = 0,
                TotalCount = 0,
                UpdatedAt = string.Empty,
                SourceTopic = string.Empty,
                RawPayload = string.Empty
            };
        }

        private static PeopleCountInfo Clone(PeopleCountInfo source)
        {
            if (source == null)
            {
                return CreateDefault();
            }

            return new PeopleCountInfo
            {
                HasData = source.HasData,
                ZoneId = source.ZoneId,
                ZoneName = source.ZoneName,
                InCount = source.InCount,
                OutCount = source.OutCount,
                TotalCount = source.TotalCount,
                UpdatedAt = source.UpdatedAt,
                SourceTopic = source.SourceTopic,
                RawPayload = source.RawPayload
            };
        }
    }
}
