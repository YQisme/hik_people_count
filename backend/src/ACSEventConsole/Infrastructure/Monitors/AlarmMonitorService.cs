using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;

namespace ACSEventConsole.Infrastructure.Monitors
{
    public class AlarmMonitorService
    {
        private readonly EventStore _store;
        private readonly object _sync = new object();
        private readonly object _scanSync = new object();
        private Thread _thread;
        private bool _running;
        private DateTime _lastImmediateScanUtc = DateTime.MinValue;
        private Dictionary<string, string> _lastSignatures = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private Dictionary<string, DashboardAlarm> _lastAlarms = new Dictionary<string, DashboardAlarm>(StringComparer.OrdinalIgnoreCase);

        public AlarmMonitorService(EventStore store)
        {
            _store = store;
        }

        public void Start()
        {
            if (_running)
            {
                return;
            }

            _running = true;
            if (_store != null)
            {
                _store.Changed += OnStoreChanged;
            }

            _thread = new Thread(Loop);
            _thread.IsBackground = true;
            _thread.Start();
        }

        public void Stop()
        {
            _running = false;
            if (_store != null)
            {
                _store.Changed -= OnStoreChanged;
            }

            try
            {
                if (_thread != null && _thread.IsAlive)
                {
                    _thread.Join(1500);
                }
            }
            catch
            {
            }
        }

        private void OnStoreChanged()
        {
            if (!_running)
            {
                return;
            }

            lock (_scanSync)
            {
                if ((DateTime.UtcNow - _lastImmediateScanUtc).TotalMilliseconds < 200)
                {
                    return;
                }

                _lastImmediateScanUtc = DateTime.UtcNow;
            }

            try
            {
                ScanAndPublish();
            }
            catch (Exception ex)
            {
                Console.WriteLine("报警即时扫描异常: " + ex.Message);
            }
        }

        private void Loop()
        {
            while (_running)
            {
                try
                {
                    ScanAndPublish();
                }
                catch (Exception ex)
                {
                    Console.WriteLine("报警监测线程异常: " + ex.Message);
                }

                for (int i = 0; i < 600 && _running; i++)
                {
                    Thread.Sleep(100);
                }
            }
        }

        private void ScanAndPublish()
        {
            RuntimeConfig config = RuntimeConfig.LoadDefault();
            DashboardPayload payload = new DashboardDataBuilder(_store.Snapshot()).Build();
            var publisher = new MqttPublisher(config);

            var currentAlarms = new Dictionary<string, DashboardAlarm>(StringComparer.OrdinalIgnoreCase);
            var currentSignatures = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (DashboardAlarm alarm in payload.Alarms)
            {
                if (alarm == null || string.IsNullOrEmpty(alarm.Id))
                {
                    continue;
                }

                currentAlarms[alarm.Id] = alarm;
                currentSignatures[alarm.Id] = BuildSignature(alarm);
            }

            lock (_sync)
            {
                foreach (KeyValuePair<string, DashboardAlarm> pair in currentAlarms)
                {
                    string previousSignature;
                    if (!_lastSignatures.TryGetValue(pair.Key, out previousSignature) || previousSignature != currentSignatures[pair.Key])
                    {
                        PublishAlarm(publisher, payload, pair.Value, "alarm.active");
                    }
                }

                foreach (KeyValuePair<string, DashboardAlarm> pair in _lastAlarms)
                {
                    if (!currentAlarms.ContainsKey(pair.Key))
                    {
                        PublishAlarm(publisher, payload, pair.Value, "alarm.cleared");
                    }
                }

                _lastAlarms = currentAlarms;
                _lastSignatures = currentSignatures;
            }
        }

        private static string BuildSignature(DashboardAlarm alarm)
        {
            return string.Join("|", new[]
            {
                alarm.Id ?? string.Empty,
                alarm.Detail ?? string.Empty,
                alarm.Status ?? string.Empty,
                alarm.Level ?? string.Empty,
                alarm.TriggeredAt ?? string.Empty
            });
        }

        private static void PublishAlarm(MqttPublisher publisher, DashboardPayload payload, DashboardAlarm alarm, string eventType)
        {
            if (alarm == null)
            {
                return;
            }

            string json = BuildMessage(payload, alarm, eventType);
            try
            {
                publisher.Publish(json);
                if (publisher.IsEnabled)
                {
                    Console.WriteLine("MQTT报警已发送: " + alarm.Title + " -> " + eventType);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("MQTT报警发送失败: " + ex.Message);
            }
        }

        private static string BuildMessage(DashboardPayload payload, DashboardAlarm alarm, string eventType)
        {
            int stayPeople = ReadMetric(payload, "区域内停留人员");
            int limitCount = ReadMetric(payload, "限制人数");
            int enterCount = ReadMetric(payload, "进场人数");
            int exitCount = ReadMetric(payload, "出场人数");
            string generatedAt = payload == null ? DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") : payload.GeneratedAt;
            string messageId = string.Format("{0}-{1}-{2}", DateTime.Now.ToString("yyyyMMddHHmmssfff"), eventType, alarm.Id ?? "unknown");

            var sb = new StringBuilder();
            sb.Append('{');
            AppendJson(sb, "messageId", messageId); sb.Append(',');
            AppendJson(sb, "eventType", eventType); sb.Append(',');
            AppendJson(sb, "system", "acs-event-standalone"); sb.Append(',');
            AppendJson(sb, "generatedAt", generatedAt); sb.Append(',');
            sb.Append("\"alarm\":{");
            AppendJson(sb, "id", alarm.Id); sb.Append(',');
            AppendJson(sb, "code", alarm.Code); sb.Append(',');
            AppendJson(sb, "category", alarm.Category); sb.Append(',');
            AppendJson(sb, "level", alarm.Level); sb.Append(',');
            AppendJson(sb, "title", alarm.Title); sb.Append(',');
            AppendJson(sb, "detail", alarm.Detail); sb.Append(',');
            AppendJson(sb, "status", alarm.Status); sb.Append(',');
            AppendJson(sb, "targetId", alarm.TargetId); sb.Append(',');
            AppendJson(sb, "targetName", alarm.TargetName); sb.Append(',');
            AppendJson(sb, "gate", alarm.Gate); sb.Append(',');
            AppendJson(sb, "deviceIP", alarm.DeviceIP); sb.Append(',');
            AppendJson(sb, "triggeredAt", alarm.TriggeredAt);
            sb.Append("},");
            sb.Append("\"metrics\":{");
            AppendJsonNumber(sb, "stayPeople", stayPeople); sb.Append(',');
            AppendJsonNumber(sb, "limitCount", limitCount); sb.Append(',');
            AppendJsonNumber(sb, "enterCount", enterCount); sb.Append(',');
            AppendJsonNumber(sb, "exitCount", exitCount);
            sb.Append("}}");
            return sb.ToString();
        }

        private static int ReadMetric(DashboardPayload payload, string label)
        {
            if (payload == null || payload.Metrics == null)
            {
                return 0;
            }

            foreach (DashboardMetric metric in payload.Metrics)
            {
                if (metric != null && string.Equals(metric.Label, label, StringComparison.OrdinalIgnoreCase))
                {
                    return metric.Value;
                }
            }

            return 0;
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

        private static string Escape(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return value ?? string.Empty;
            }

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
    }
}