using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace GetACSEvent
{
    internal sealed class MqttPublishRequest
    {
        public string Topic { get; set; }
        public string Payload { get; set; }
    }

    internal sealed class MqttPersistentClient : IDisposable
    {
        private readonly string _host;
        private readonly int _port;
        private readonly string _clientId;
        private readonly string _username;
        private readonly string _password;
        private readonly BlockingCollection<MqttPublishRequest> _queue = new BlockingCollection<MqttPublishRequest>();
        private readonly Thread _worker;
        private bool _disposed;

        public MqttPersistentClient(string host, int port, string clientId, string username, string password)
        {
            _host = host ?? string.Empty;
            _port = port;
            _clientId = string.IsNullOrEmpty(clientId) ? "acs-mqtt-publisher" : clientId;
            _username = username ?? string.Empty;
            _password = password ?? string.Empty;
            _worker = new Thread(WorkerLoop)
            {
                IsBackground = true,
                Name = "MqttPersistentClient-" + _host + ":" + _port
            };
            _worker.Start();
        }

        public void Enqueue(string topic, string payload)
        {
            if (_disposed || string.IsNullOrEmpty(topic) || string.IsNullOrEmpty(payload))
            {
                return;
            }

            _queue.Add(new MqttPublishRequest
            {
                Topic = topic,
                Payload = payload
            });
        }

        private TcpClient _client;
        private NetworkStream _stream;
        private readonly object _connectionSync = new object();

        private void WorkerLoop()
        {
            while (!_disposed)
            {
                MqttPublishRequest request;
                try
                {
                    request = _queue.Take();
                }
                catch (InvalidOperationException)
                {
                    break;
                }

                try
                {
                    PublishWithPersistentConnection(request.Topic, request.Payload);
                }
                catch (Exception ex)
                {
                    Console.WriteLine("MQTT发送失败: " + ex.Message);
                    CloseConnection();
                    Thread.Sleep(300);
                    try
                    {
                        PublishWithPersistentConnection(request.Topic, request.Payload);
                    }
                    catch (Exception retryEx)
                    {
                        Console.WriteLine("MQTT重试发送失败: " + retryEx.Message);
                        CloseConnection();
                    }
                }
            }

            CloseConnection();
        }

        private void PublishWithPersistentConnection(string topic, string payload)
        {
            lock (_connectionSync)
            {
                EnsureConnected();
                WritePacket(_stream, BuildPublishPacket(topic, payload));
            }
        }

        private void EnsureConnected()
        {
            if (_stream != null && _client != null && _client.Connected)
            {
                return;
            }

            CloseConnection();
            _client = new TcpClient();
            IAsyncResult connectResult = _client.BeginConnect(_host, _port, null, null);
            if (!connectResult.AsyncWaitHandle.WaitOne(4000, false))
            {
                CloseConnection();
                throw new IOException("MQTT连接超时");
            }

            _client.EndConnect(connectResult);
            _client.ReceiveTimeout = 4000;
            _client.SendTimeout = 4000;
            _stream = _client.GetStream();
            WritePacket(_stream, BuildConnectPacket());
            ReadConnAck(_stream);
        }

        private void CloseConnection()
        {
            try { if (_stream != null) _stream.Dispose(); } catch { }
            try { if (_client != null) _client.Close(); } catch { }
            _stream = null;
            _client = null;
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
                byte[] payloadBytes = Encoding.UTF8.GetBytes(payload ?? string.Empty);
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

        public void Dispose()
        {
            _disposed = true;
            _queue.CompleteAdding();
        }
    }

    public static class MqttConnectionPool
    {
        private static readonly object Sync = new object();
        private static readonly Dictionary<string, MqttPersistentClient> Clients =
            new Dictionary<string, MqttPersistentClient>(StringComparer.OrdinalIgnoreCase);

        public static void Publish(
            bool enabled,
            string host,
            int port,
            string topic,
            string clientId,
            string username,
            string password,
            string payload)
        {
            if (!enabled || string.IsNullOrEmpty(host) || string.IsNullOrEmpty(topic) || string.IsNullOrEmpty(payload))
            {
                return;
            }

            string key = BuildKey(host, port, clientId);
            MqttPersistentClient client = GetOrCreateClient(key, host, port, clientId, username, password);
            client.Enqueue(topic, payload);
        }

        private static MqttPersistentClient GetOrCreateClient(
            string key,
            string host,
            int port,
            string clientId,
            string username,
            string password)
        {
            lock (Sync)
            {
                MqttPersistentClient existing;
                if (Clients.TryGetValue(key, out existing))
                {
                    return existing;
                }

                var created = new MqttPersistentClient(host, port, clientId, username, password);
                Clients[key] = created;
                return created;
            }
        }

        private static string BuildKey(string host, int port, string clientId)
        {
            return (host ?? string.Empty).Trim().ToLowerInvariant() + ":" + port + ":" + (clientId ?? string.Empty).Trim();
        }
    }
}
