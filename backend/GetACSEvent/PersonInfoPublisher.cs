using System;
using System.Text;

namespace GetACSEvent
{
    public static class PersonInfoPublisher
    {
        public static void PublishFaceEvent(AcsEvent ev)
        {
            if (ev == null)
            {
                return;
            }

            RuntimeConfig config = RuntimeConfig.LoadDefault();
            if (!config.PersonInfoMqttEnabled || string.IsNullOrEmpty(config.PersonInfoMqttHost))
            {
                return;
            }

            string topic = ResolveTopic(ev, config);
            if (string.IsNullOrEmpty(topic))
            {
                return;
            }

            string payload = BuildPayload(ev, topic);
            MqttConnectionPool.Publish(
                config.PersonInfoMqttEnabled,
                config.PersonInfoMqttHost,
                config.PersonInfoMqttPort,
                topic,
                config.PersonInfoMqttClientId,
                config.MqttUsername,
                config.MqttPassword,
                payload);
            Console.WriteLine("刷脸信息已发送到MQTT " + topic + ": " + (ev.PersonName ?? string.Empty));
        }

        private static string BuildPayload(AcsEvent ev, string topic)
        {
            var sb = new StringBuilder();
            sb.Append('{');
            AppendJson(sb, "messageType", Safe(topic)); sb.Append(',');
            AppendJson(sb, "deviceId", Safe(ev.DeviceID)); sb.Append(',');
            AppendJson(sb, "deviceCode", GetDeviceCode(ev)); sb.Append(',');
            AppendJson(sb, "deviceIp", Safe(ev.DeviceIP)); sb.Append(',');
            AppendJson(sb, "employeeNo", Safe(ev.EmployeeNo)); sb.Append(',');
            AppendJson(sb, "eventTime", Safe(ev.Time)); sb.Append(',');
            AppendJson(sb, "direction", NormalizeDirection(ev.Direction, ev.DeviceIP));
            sb.Append('}');
            return sb.ToString();
        }

        private static string ResolveTopic(AcsEvent ev, RuntimeConfig config)
        {
            string deviceIp = Safe(ev == null ? string.Empty : ev.DeviceIP);
            if (string.Equals(deviceIp, "192.168.0.164", StringComparison.OrdinalIgnoreCase))
            {
                return Safe(config.PersonInMqttTopic);
            }

            if (string.Equals(deviceIp, "192.168.0.165", StringComparison.OrdinalIgnoreCase))
            {
                return Safe(config.PersonOutMqttTopic);
            }

            string direction = NormalizeDirection(ev == null ? string.Empty : ev.Direction, deviceIp);
            if (direction == "进")
            {
                return Safe(config.PersonInMqttTopic);
            }

            if (direction == "出")
            {
                return Safe(config.PersonOutMqttTopic);
            }

            return Safe(config.PersonInfoMqttTopic);
        }

        private static string GetDeviceCode(AcsEvent ev)
        {
            string deviceCode = Safe(ev.DeviceName);
            if (!string.IsNullOrEmpty(deviceCode))
            {
                return deviceCode;
            }

            return Safe(ev.DeviceIP);
        }

        private static string NormalizeDirection(string direction, string deviceIp)
        {
            string value = Safe(direction);
            if (string.IsNullOrEmpty(value))
            {
                value = string.Equals(Safe(deviceIp), "192.168.0.164", StringComparison.OrdinalIgnoreCase) ? "进" : string.Empty;
            }

            string lowerValue = value.ToLowerInvariant();
            if (lowerValue == "in" || lowerValue == "enter" || lowerValue == "进入" || lowerValue == "入" || lowerValue == "进")
            {
                return "进";
            }

            if (lowerValue == "out" || lowerValue == "exit" || lowerValue == "出去" || lowerValue == "出")
            {
                return "出";
            }

            return value;
        }

        private static string Safe(string value)
        {
            return (value ?? string.Empty).Trim();
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
