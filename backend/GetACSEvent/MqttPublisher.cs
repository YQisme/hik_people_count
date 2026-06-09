using System;
using System.IO;
using System.Net.Sockets;
using System.Text;

namespace GetACSEvent
{
    public class MqttPublisher
    {
        private readonly bool _enabled;
        private readonly string _host;
        private readonly int _port;
        private readonly string _topic;
        private readonly string _clientId;
        private readonly string _username;
        private readonly string _password;

        public MqttPublisher(RuntimeConfig config)
            : this(
                config == null ? false : config.MqttEnabled,
                config == null ? string.Empty : config.MqttHost,
                config == null ? 1883 : config.MqttPort,
                config == null ? string.Empty : config.MqttTopic,
                config == null ? "acs-event-standalone" : config.MqttClientId,
                config == null ? string.Empty : config.MqttUsername,
                config == null ? string.Empty : config.MqttPassword)
        {
        }

        public MqttPublisher(bool enabled, string host, int port, string topic, string clientId, string username, string password)
        {
            _enabled = enabled;
            _host = host ?? string.Empty;
            _port = port;
            _topic = topic ?? string.Empty;
            _clientId = string.IsNullOrEmpty(clientId) ? "acs-mqtt-publisher" : clientId;
            _username = username ?? string.Empty;
            _password = password ?? string.Empty;
        }

        public bool IsEnabled
        {
            get { return _enabled && !string.IsNullOrEmpty(_host) && !string.IsNullOrEmpty(_topic); }
        }

        public void Publish(string payload)
        {
            if (!IsEnabled || string.IsNullOrEmpty(payload))
            {
                return;
            }

            MqttConnectionPool.Publish(
                _enabled,
                _host,
                _port,
                _topic,
                _clientId,
                _username,
                _password,
                payload);
        }

        private byte[] BuildConnectPacket()
        {
            using (var body = new MemoryStream())
            {
                WriteMqttString(body, "MQTT");
                body.WriteByte(0x04);

                byte flags = 0x02;
                if (!string.IsNullOrEmpty(_username))
                {
                    flags |= 0x80;
                }
                if (!string.IsNullOrEmpty(_password))
                {
                    flags |= 0x40;
                }
                body.WriteByte(flags);
                body.WriteByte(0x00);
                body.WriteByte(0x3C);

                WriteMqttString(body, _clientId);
                if (!string.IsNullOrEmpty(_username))
                {
                    WriteMqttString(body, _username);
                }
                if (!string.IsNullOrEmpty(_password))
                {
                    WriteMqttString(body, _password);
                }

                return BuildPacket(0x10, body.ToArray());
            }
        }

        private static byte[] BuildPublishPacket(string topic, string payload)
        {
            using (var body = new MemoryStream())
            {
                WriteMqttString(body, topic);
                byte[] payloadBytes = Encoding.UTF8.GetBytes(payload);
                body.Write(payloadBytes, 0, payloadBytes.Length);
                return BuildPacket(0x30, body.ToArray());
            }
        }

        private static byte[] BuildPacket(byte packetType, byte[] body)
        {
            using (var stream = new MemoryStream())
            {
                stream.WriteByte(packetType);
                WriteRemainingLength(stream, body.Length);
                stream.Write(body, 0, body.Length);
                return stream.ToArray();
            }
        }

        private static void WritePacket(NetworkStream stream, byte[] packet)
        {
            stream.Write(packet, 0, packet.Length);
            stream.Flush();
        }

        private static void ReadConnAck(NetworkStream stream)
        {
            byte[] buffer = new byte[4];
            int read = 0;
            while (read < buffer.Length)
            {
                int count = stream.Read(buffer, read, buffer.Length - read);
                if (count <= 0)
                {
                    throw new IOException("MQTT未返回CONNACK");
                }
                read += count;
            }

            if (buffer[0] != 0x20 || buffer[1] != 0x02 || buffer[3] != 0x00)
            {
                throw new IOException("MQTT连接被拒绝，返回码: " + buffer[3]);
            }
        }

        private static void WriteMqttString(Stream stream, string value)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(value ?? string.Empty);
            stream.WriteByte((byte)((bytes.Length >> 8) & 0xFF));
            stream.WriteByte((byte)(bytes.Length & 0xFF));
            stream.Write(bytes, 0, bytes.Length);
        }

        private static void WriteRemainingLength(Stream stream, int length)
        {
            do
            {
                int encoded = length % 128;
                length = length / 128;
                if (length > 0)
                {
                    encoded = encoded | 0x80;
                }
                stream.WriteByte((byte)encoded);
            }
            while (length > 0);
        }
    }
}
