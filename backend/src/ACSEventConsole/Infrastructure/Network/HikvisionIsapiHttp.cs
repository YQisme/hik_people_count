using System;
using System.Net;
using System.Net.Http;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using System.Text;

namespace ACSEventConsole.Infrastructure.Network
{
    public static class HikvisionIsapiHttp
    {
        private const int DefaultHttpPort = 80;
        private const int DefaultHttpsPort = 443;

        public static HttpClient CreateClient(DeviceConfigDeviceEntry device)
        {
            string username = string.IsNullOrWhiteSpace(device.UserName) ? "admin" : device.UserName.Trim();
            string password = device.Password ?? string.Empty;
            bool useHttps = device.UseHttps;

            var handler = new HttpClientHandler
            {
                Credentials = new NetworkCredential(username, password),
                PreAuthenticate = false
            };

            if (useHttps)
            {
                handler.ServerCertificateCustomValidationCallback = AcceptAnyCertificate;
            }

            return new HttpClient(handler)
            {
                Timeout = TimeSpan.FromSeconds(30)
            };
        }

        public static string GetDeviceBaseUrl(DeviceConfigDeviceEntry device)
        {
            bool useHttps = device.UseHttps;
            int httpPort = device.HttpPort > 0
                ? device.HttpPort
                : (useHttps ? DefaultHttpsPort : DefaultHttpPort);
            string scheme = useHttps ? "https" : "http";
            return scheme + "://" + (device.IP ?? string.Empty).Trim() + ":" + httpPort;
        }

        public static string NormalizeDeviceUrl(DeviceConfigDeviceEntry device, string source)
        {
            string raw = (source ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(raw))
            {
                return string.Empty;
            }

            if (raw.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                raw.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                return raw;
            }

            if (!raw.StartsWith("/"))
            {
                raw = "/" + raw;
            }

            return GetDeviceBaseUrl(device) + raw;
        }

        public static byte[] DownloadBytes(DeviceConfigDeviceEntry device, string source)
        {
            string url = NormalizeDeviceUrl(device, source);
            if (string.IsNullOrEmpty(url))
            {
                return null;
            }

            using (var client = CreateClient(device))
            using (HttpResponseMessage response = client.GetAsync(url).GetAwaiter().GetResult())
            {
                if (!response.IsSuccessStatusCode)
                {
                    return null;
                }

                return response.Content.ReadAsByteArrayAsync().GetAwaiter().GetResult();
            }
        }

        public static string PostJson(DeviceConfigDeviceEntry device, string path, string body)
        {
            string url = NormalizeDeviceUrl(device, path);
            using (var client = CreateClient(device))
            using (var content = new StringContent(body ?? string.Empty, Encoding.UTF8, "application/json"))
            using (HttpResponseMessage response = client.PostAsync(url, content).GetAwaiter().GetResult())
            {
                string responseText = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
                if (!response.IsSuccessStatusCode)
                {
                    throw new InvalidOperationException("HTTP " + (int)response.StatusCode + ": " + responseText);
                }

                return responseText;
            }
        }

        private static bool AcceptAnyCertificate(
            HttpRequestMessage message,
            X509Certificate2 certificate,
            X509Chain chain,
            SslPolicyErrors errors)
        {
            return true;
        }
    }
}
