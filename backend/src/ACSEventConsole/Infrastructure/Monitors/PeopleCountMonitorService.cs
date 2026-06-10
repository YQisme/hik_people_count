using System;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace ACSEventConsole.Infrastructure.Monitors
{
    public class PeopleCountMonitorService
    {
        private readonly RuntimeConfig _config;
        private Thread _thread;
        private bool _running;

        public PeopleCountMonitorService(RuntimeConfig config)
        {
            _config = config ?? new RuntimeConfig();
        }

        public void Start()
        {
            if (_running)
            {
                return;
            }

            _running = true;
            _thread = new Thread(RunLoop);
            _thread.IsBackground = true;
            _thread.Start();
        }

        public void Stop()
        {
            _running = false;
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

        private void RunLoop()
        {
            while (_running)
            {
                if (!_config.PeopleCountMqttEnabled || string.IsNullOrEmpty(_config.PeopleCountMqttHost) || string.IsNullOrEmpty(_config.PeopleCountMqttTopic))
                {
                    Thread.Sleep(5000);
                    continue;
                }

                TcpClient client = null;
                NetworkStream stream = null;
                try
                {
                    client = new TcpClient();
                    IAsyncResult connectResult = client.BeginConnect(_config.PeopleCountMqttHost, _config.PeopleCountMqttPort, null, null);
                    if (!connectResult.AsyncWaitHandle.WaitOne(4000, false))
                    {
                        throw new IOException("连接人数统计MQTT超时");
                    }

                    client.EndConnect(connectResult);
                    client.ReceiveTimeout = 1000;
                    client.SendTimeout = 4000;
                    stream = client.GetStream();

                    WritePacket(stream, BuildConnectPacket());
                    ReadConnAck(stream);
                    WritePacket(stream, BuildSubscribePacket(_config.PeopleCountMqttTopic, 2));
                    ReadSubAck(stream);
                    Console.WriteLine("人数统计MQTT已订阅: " + _config.PeopleCountMqttHost + ":" + _config.PeopleCountMqttPort + " / " + _config.PeopleCountMqttTopic);

                    DateTime lastPingAt = DateTime.Now;
                    while (_running)
                    {
                        try
                        {
                            byte header = ReadByte(stream);
                            int remainingLength = ReadRemainingLength(stream);
                            byte[] body = ReadExact(stream, remainingLength);
                            int packetType = (header >> 4) & 0x0F;

                            if (packetType == 3)
                            {
                                HandlePublish(header, body);
                            }

                            lastPingAt = DateTime.Now;
                        }
                        catch (IOException ioEx)
                        {
                            if (!_running)
                            {
                                break;
                            }

                            if (ioEx.Message == "TIMEOUT")
                            {
                                if ((DateTime.Now - lastPingAt).TotalSeconds >= 20)
                                {
                                    WritePacket(stream, new byte[] { 0xC0, 0x00 });
                                    lastPingAt = DateTime.Now;
                                }
                                continue;
                            }

                            throw;
                        }
                    }
                }
                catch (Exception ex)
                {
                    if (_running)
                    {
                        Console.WriteLine("人数统计MQTT监听异常: " + ex.Message);
                        Thread.Sleep(3000);
                    }
                }
                finally
                {
                    try { if (stream != null) stream.Dispose(); } catch { }
                    try { if (client != null) client.Close(); } catch { }
                }
            }
        }

        private void HandlePublish(byte header, byte[] body)
        {
            if (body == null || body.Length < 2)
            {
                return;
            }

            int topicLength = (body[0] << 8) | body[1];
            if (body.Length < 2 + topicLength)
            {
                return;
            }

            string topic = Encoding.UTF8.GetString(body, 2, topicLength);
            int offset = 2 + topicLength;
            int qos = (header >> 1) & 0x03;
            if (qos > 0)
            {
                offset += 2;
            }

            if (offset > body.Length)
            {
                return;
            }

            string payload = Encoding.UTF8.GetString(body, offset, body.Length - offset);
            string zoneId = ExtractStringValue(payload, "zoneId");
            string zoneName = ExtractStringValue(payload, "zoneName");
            int inCount = ExtractIntValue(payload, "inCount", 0);
            int outCount = ExtractIntValue(payload, "outCount", 0);
            int totalCount = ExtractIntValue(payload, "totalCount", 0);

            PeopleCountState.Update(zoneId, zoneName, inCount, outCount, totalCount, topic, payload);
            Console.WriteLine("收到人数统计MQTT: zone=" + zoneName + ", in=" + inCount + ", out=" + outCount + ", total=" + totalCount);
        }

        private byte[] BuildConnectPacket()
        {
            using (var body = new MemoryStream())
            {
                WriteMqttString(body, "MQTT");
                body.WriteByte(0x04);

                byte flags = 0x02;
                if (!string.IsNullOrEmpty(_config.MqttUsername))
                {
                    flags |= 0x80;
                }
                if (!string.IsNullOrEmpty(_config.MqttPassword))
                {
                    flags |= 0x40;
                }
                body.WriteByte(flags);
                body.WriteByte(0x00);
                body.WriteByte(0x3C);

                WriteMqttString(body, string.IsNullOrEmpty(_config.PeopleCountMqttClientId) ? "acs-people-count-subscriber" : _config.PeopleCountMqttClientId);
                if (!string.IsNullOrEmpty(_config.MqttUsername))
                {
                    WriteMqttString(body, _config.MqttUsername);
                }
                if (!string.IsNullOrEmpty(_config.MqttPassword))
                {
                    WriteMqttString(body, _config.MqttPassword);
                }

                return BuildPacket(0x10, body.ToArray());
            }
        }

        private static byte[] BuildSubscribePacket(string topic, ushort packetId)
        {
            using (var body = new MemoryStream())
            {
                body.WriteByte((byte)((packetId >> 8) & 0xFF));
                body.WriteByte((byte)(packetId & 0xFF));
                WriteMqttString(body, topic ?? string.Empty);
                body.WriteByte(0x00);
                return BuildPacket(0x82, body.ToArray());
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
            byte header = ReadByte(stream);
            if ((header >> 4) != 2)
            {
                throw new IOException("MQTT未返回CONNACK");
            }
            int remainingLength = ReadRemainingLength(stream);
            byte[] body = ReadExact(stream, remainingLength);
            if (body.Length < 2 || body[1] != 0x00)
            {
                throw new IOException("MQTT连接被拒绝");
            }
        }

        private static void ReadSubAck(NetworkStream stream)
        {
            byte header = ReadByte(stream);
            if ((header >> 4) != 9)
            {
                throw new IOException("MQTT未返回SUBACK");
            }
            int remainingLength = ReadRemainingLength(stream);
            byte[] body = ReadExact(stream, remainingLength);
            if (body.Length < 3 || body[2] == 0x80)
            {
                throw new IOException("MQTT订阅失败");
            }
        }

        private static byte ReadByte(NetworkStream stream)
        {
            try
            {
                int value = stream.ReadByte();
                if (value < 0)
                {
                    throw new IOException("连接已关闭");
                }
                return (byte)value;
            }
            catch (IOException ex)
            {
                SocketException socketEx = ex.InnerException as SocketException;
                if (socketEx != null && socketEx.SocketErrorCode == SocketError.TimedOut)
                {
                    throw new IOException("TIMEOUT");
                }
                throw;
            }
        }

        private static int ReadRemainingLength(NetworkStream stream)
        {
            int multiplier = 1;
            int value = 0;
            int encoded;
            do
            {
                encoded = ReadByte(stream);
                value += (encoded & 127) * multiplier;
                multiplier *= 128;
            }
            while ((encoded & 128) != 0);
            return value;
        }

        private static byte[] ReadExact(NetworkStream stream, int length)
        {
            byte[] buffer = new byte[length];
            int offset = 0;
            while (offset < length)
            {
                try
                {
                    int read = stream.Read(buffer, offset, length - offset);
                    if (read <= 0)
                    {
                        throw new IOException("连接已关闭");
                    }
                    offset += read;
                }
                catch (IOException ex)
                {
                    SocketException socketEx = ex.InnerException as SocketException;
                    if (socketEx != null && socketEx.SocketErrorCode == SocketError.TimedOut)
                    {
                        throw new IOException("TIMEOUT");
                    }
                    throw;
                }
            }
            return buffer;
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

        private static int ExtractIntValue(string json, string key, int fallback)
        {
            string raw = ExtractRawValue(json, key);
            int value;
            return int.TryParse(raw, out value) ? value : fallback;
        }

        private static string ExtractStringValue(string json, string key)
        {
            string raw = ExtractRawValue(json, key);
            if (string.IsNullOrEmpty(raw))
            {
                return string.Empty;
            }
            raw = raw.Trim();
            if (raw.StartsWith("\"") && raw.EndsWith("\"") && raw.Length >= 2)
            {
                raw = raw.Substring(1, raw.Length - 2);
            }
            return UnescapeJson(raw);
        }

        private static string ExtractRawValue(string json, string key)
        {
            if (string.IsNullOrEmpty(json) || string.IsNullOrEmpty(key))
            {
                return string.Empty;
            }

            string pattern = "\"" + key + "\"";
            int keyIndex = json.IndexOf(pattern, StringComparison.OrdinalIgnoreCase);
            if (keyIndex < 0)
            {
                return string.Empty;
            }

            int colonIndex = json.IndexOf(':', keyIndex + pattern.Length);
            if (colonIndex < 0)
            {
                return string.Empty;
            }

            int index = colonIndex + 1;
            while (index < json.Length && char.IsWhiteSpace(json[index]))
            {
                index++;
            }

            if (index >= json.Length)
            {
                return string.Empty;
            }

            if (json[index] == '"')
            {
                int end = index + 1;
                bool escaped = false;
                while (end < json.Length)
                {
                    char ch = json[end];
                    if (ch == '"' && !escaped)
                    {
                        return json.Substring(index, end - index + 1);
                    }
                    escaped = ch == '\\' && !escaped;
                    if (ch != '\\')
                    {
                        escaped = false;
                    }
                    end++;
                }
                return json.Substring(index);
            }

            int tail = index;
            while (tail < json.Length && json[tail] != ',' && json[tail] != '}' && json[tail] != ']')
            {
                tail++;
            }
            return json.Substring(index, tail - index).Trim();
        }

        private static string UnescapeJson(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return value ?? string.Empty;
            }

            return value
                .Replace("\\\"", "\"")
                .Replace("\\r", "\r")
                .Replace("\\n", "\n")
                .Replace("\\t", "\t")
                .Replace("\\\\", "\\");
        }
    }
}
