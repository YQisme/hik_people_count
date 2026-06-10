using System;
using System.Collections.Generic;

namespace ACSEventConsole.Infrastructure.State
{
    public class AbnormalMessageInfo
    {
        public string Id { get; set; }
        public string Type { get; set; }
        public string Time { get; set; }
        public string Topic { get; set; }
        public string ReceivedAt { get; set; }
        public string UpdatedAt { get; set; }
        public string RawPayload { get; set; }
        public bool IsHandled { get; set; }
        public string HandledAt { get; set; }
        public string Status { get; set; }
    }

    public static class AbnormalMessageState
    {
        private const int MaxMessages = 20;
        private static readonly object SyncRoot = new object();
        private static readonly List<AbnormalMessageInfo> Messages = new List<AbnormalMessageInfo>();

        public static List<AbnormalMessageInfo> GetSnapshot()
        {
            lock (SyncRoot)
            {
                var result = new List<AbnormalMessageInfo>();
                foreach (AbnormalMessageInfo message in Messages)
                {
                    result.Add(Clone(message));
                }
                return result;
            }
        }

        public static void Update(string topic, string type, string time, string rawPayload)
        {
            string now = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            string safeTopic = (topic ?? string.Empty).Trim();
            string safeType = string.IsNullOrEmpty(type) ? "abnormal" : type.Trim();
            string safeTime = string.IsNullOrEmpty(time) ? now : time.Trim();
            string messageId = BuildMessageId(safeTopic, safeType, safeTime);

            lock (SyncRoot)
            {
                AbnormalMessageInfo existing = null;
                int existingIndex = -1;
                for (int i = 0; i < Messages.Count; i++)
                {
                    if (string.Equals(Messages[i].Id, messageId, StringComparison.OrdinalIgnoreCase))
                    {
                        existing = Messages[i];
                        existingIndex = i;
                        break;
                    }
                }

                var next = new AbnormalMessageInfo
                {
                    Id = messageId,
                    Type = safeType,
                    Time = safeTime,
                    Topic = safeTopic,
                    ReceivedAt = existing == null ? now : existing.ReceivedAt,
                    UpdatedAt = now,
                    RawPayload = rawPayload ?? string.Empty,
                    IsHandled = false,
                    HandledAt = string.Empty,
                    Status = "待处理"
                };

                if (existingIndex >= 0)
                {
                    Messages.RemoveAt(existingIndex);
                }

                Messages.Insert(0, next);
                while (Messages.Count > MaxMessages)
                {
                    Messages.RemoveAt(Messages.Count - 1);
                }
            }
        }

        public static bool MarkHandled(string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                return false;
            }

            lock (SyncRoot)
            {
                for (int i = 0; i < Messages.Count; i++)
                {
                    if (!string.Equals(Messages[i].Id, id, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    Messages[i].IsHandled = true;
                    Messages[i].HandledAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                    Messages[i].Status = "已处理";
                    Messages[i].UpdatedAt = Messages[i].HandledAt;
                    return true;
                }
            }

            return false;
        }

        public static bool MarkHandled(string topic, string type, string time)
        {
            string safeTopic = (topic ?? string.Empty).Trim();
            string safeType = string.IsNullOrEmpty(type) ? "abnormal" : type.Trim();
            string safeTime = string.IsNullOrEmpty(time) ? string.Empty : time.Trim();
            return MarkHandled(BuildMessageId(safeTopic, safeType, safeTime));
        }

        private static string BuildMessageId(string topic, string type, string time)
        {
            return string.Format("abnormal::{0}::{1}::{2}", topic ?? string.Empty, type ?? string.Empty, time ?? string.Empty);
        }

        private static AbnormalMessageInfo Clone(AbnormalMessageInfo source)
        {
            if (source == null)
            {
                return new AbnormalMessageInfo();
            }

            return new AbnormalMessageInfo
            {
                Id = source.Id,
                Type = source.Type,
                Time = source.Time,
                Topic = source.Topic,
                ReceivedAt = source.ReceivedAt,
                UpdatedAt = source.UpdatedAt,
                RawPayload = source.RawPayload,
                IsHandled = source.IsHandled,
                HandledAt = source.HandledAt,
                Status = source.Status
            };
        }
    }
}
