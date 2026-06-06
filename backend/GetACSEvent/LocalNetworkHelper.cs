using System;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace GetACSEvent
{
    internal static class LocalNetworkHelper
    {
        public static string GetLocalIPAddress()
        {
            try
            {
                foreach (NetworkInterface networkInterface in NetworkInterface.GetAllNetworkInterfaces())
                {
                    if (networkInterface.OperationalStatus != OperationalStatus.Up)
                    {
                        continue;
                    }

                    if (networkInterface.NetworkInterfaceType != NetworkInterfaceType.Ethernet &&
                        networkInterface.NetworkInterfaceType != NetworkInterfaceType.Wireless80211)
                    {
                        continue;
                    }

                    IPInterfaceProperties ipProperties = networkInterface.GetIPProperties();
                    foreach (UnicastIPAddressInformation ip in ipProperties.UnicastAddresses)
                    {
                        if (ip.Address.AddressFamily != AddressFamily.InterNetwork ||
                            IPAddress.IsLoopback(ip.Address))
                        {
                            continue;
                        }

                        return ip.Address.ToString();
                    }
                }

                return "localhost";
            }
            catch (Exception ex)
            {
                Console.WriteLine("获取本机IP地址失败: " + ex.Message);
                return "localhost";
            }
        }

        public static string BuildWebServerUrl(int port)
        {
            return "http://" + GetLocalIPAddress() + ":" + port;
        }
    }
}
