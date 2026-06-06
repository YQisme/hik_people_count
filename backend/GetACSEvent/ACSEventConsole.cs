using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.Threading;

namespace GetACSEvent
{
    /// <summary>
    /// 门禁事件监控控制台程序
    /// </summary>
    class ACSEventConsole
    {
        // 员工ID到姓名的哈希表
        public static Dictionary<string, string> EmployeeNameMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        static void Main(string[] args)
        {
            try
            {
                Console.WriteLine("多门禁事件监控服务启动中...");
                Console.WriteLine("配置文件: " + ConfigPaths.DeviceConfigPath);

                // 启动前：从API拉取并更新配置
                // try
                // {
                //     string baseUrl = GetApiBaseUrl();
                //     string apiUrl = (baseUrl.EndsWith("/") ? baseUrl + "api/device" : baseUrl + "/api/device");
                //     string configPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "DeviceConfig.xml");
                //     DeviceConfigUpdater.UpdateFromApi(apiUrl, configPath);
                //     Console.WriteLine("已从API更新配置: " + apiUrl);
                // }
                // catch (Exception ex)
                // {
                //     Console.WriteLine("从API更新配置失败: " + ex.Message);
                // }

                string employeeConfigPath = ConfigPaths.EmployeeConfigPath;

                // 启动前：优先从本地 JSON 读取员工表
                try
                {
                    if (File.Exists(employeeConfigPath))
                    {
                        LoadEmployeeConfigFromFile(employeeConfigPath);
                        Console.WriteLine("已从本地JSON加载员工表: " + employeeConfigPath);
                        Console.WriteLine("已加载 " + EmployeeNameMap.Count + " 个员工信息到哈希表");
                    }
                    else
                    {
                        Console.WriteLine("本地员工表不存在: " + employeeConfigPath);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine("读取本地员工表失败: " + ex.Message);
                }

                // 创建并启动多门禁事件服务
                ACSEventMultiDeviceService service = new ACSEventMultiDeviceService();
                service.Start();
                AlarmMonitorService alarmMonitor = new AlarmMonitorService(ACSEventMultiDeviceService.SharedEventStore);
                alarmMonitor.Start();
                RuntimeConfig runtimeConfig = RuntimeConfig.LoadDefault();
                AreaAlertMonitorService areaAlertMonitor = new AreaAlertMonitorService(runtimeConfig);
                areaAlertMonitor.Start();
                AbnormalMonitorService abnormalMonitor = new AbnormalMonitorService(runtimeConfig);
                abnormalMonitor.Start();
                PeopleCountMonitorService peopleCountMonitor = new PeopleCountMonitorService(runtimeConfig);
                peopleCountMonitor.Start();
                CapacityDoorControlService capacityDoorControl = new CapacityDoorControlService(service);
                capacityDoorControl.Start();
                // 启动内置 Web 服务，监听所有网卡上的指定端口
                int webPort = GetWebPort();
                string localIP = LocalNetworkHelper.GetLocalIPAddress();
                SimpleWebServer web = null;
                bool webStarted = false;
                try
                {
                    web = new SimpleWebServer(webPort, ACSEventMultiDeviceService.SharedEventStore);
                    web.Start();
                    webStarted = true;
                    Console.WriteLine("Web服务已启动: http://localhost:" + webPort + "/");
                    if (!string.IsNullOrEmpty(localIP))
                    {
                        Console.WriteLine("同网段访问地址: http://" + localIP + ":" + webPort + "/");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Web服务启动失败，端口 " + webPort + ": " + ex.Message);
                    Console.WriteLine("请检查端口占用或防火墙设置");
                }

                Console.WriteLine("按ESC键退出...");
                
                // 等待ESC键退出
                while (true)
                {
                    if (Console.KeyAvailable)
                    {
                        ConsoleKeyInfo key = Console.ReadKey(true);
                        if (key.Key == ConsoleKey.Escape)
                        {
                            break;
                        }
                    }
                    System.Threading.Thread.Sleep(100);
                }

                // 停止服务
                try { if (webStarted && web != null) web.Stop(); } catch { }
                try { if (alarmMonitor != null) alarmMonitor.Stop(); } catch { }
                try { if (areaAlertMonitor != null) areaAlertMonitor.Stop(); } catch { }
                try { if (abnormalMonitor != null) abnormalMonitor.Stop(); } catch { }
                try { if (peopleCountMonitor != null) peopleCountMonitor.Stop(); } catch { }
                try { if (capacityDoorControl != null) capacityDoorControl.Stop(); } catch { }
                service.Stop();
            }
            catch (Exception ex)
            {
                Console.WriteLine("程序发生异常：" + ex.Message);
                Console.WriteLine("异常详细信息：" + ex.ToString());
                Console.WriteLine("按任意键退出...");
                Console.ReadKey();
            }
        }

        private static void LoadEmployeeConfigFromFile(string configPath)
        {
            if (string.IsNullOrEmpty(configPath) || !File.Exists(configPath))
            {
                return;
            }

            string json = File.ReadAllText(configPath, Encoding.UTF8);
            if (string.IsNullOrEmpty(json))
            {
                return;
            }

            ParseEmployeeData(json);
        }

        /// <summary>
        /// 解析员工JSON数据，提取employeeId和name到哈希表
        /// </summary>
        /// <param name="json">员工数据JSON字符串</param>
        private static void ParseEmployeeData(string json)
        {
            try
            {
                EmployeeDirectory.LoadFromJson(json);
                EmployeeNameMap.Clear();
                foreach (var entry in EmployeeDirectory.GetNameMapSnapshot())
                {
                    EmployeeNameMap[entry.Key] = entry.Value;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("解析员工数据失败: " + ex.Message);
            }
        }

        /// <summary>
        /// 从字典中获取值
        /// </summary>
        private static string Get(Dictionary<string, string> obj, string key)
        {
            string v; return obj.TryGetValue(key, out v) ? v : string.Empty;
        }

        /// <summary>
        /// 极简JSON数组对象解析器，仅支持扁平字符串键值对，如 [{"k":"v",...}, ...]
        /// </summary>
        private static List<Dictionary<string, string>> ParseArrayOfObjects(string json)
        {
            var list = new List<Dictionary<string, string>>();
            if (string.IsNullOrEmpty(json)) return list;
            int i = 0;
            SkipWs(json, ref i);
            if (i >= json.Length || json[i] != '[') return list;
            i++;
            while (true)
            {
                SkipWs(json, ref i);
                if (i < json.Length && json[i] == ']') { i++; break; }
                var obj = ParseObject(json, ref i);
                if (obj != null) list.Add(obj);
                SkipWs(json, ref i);
                if (i < json.Length && json[i] == ',') { i++; continue; }
                if (i < json.Length && json[i] == ']') { i++; break; }
                break;
            }
            return list;
        }

        /// <summary>
        /// 解析JSON对象
        /// </summary>
        private static Dictionary<string, string> ParseObject(string s, ref int i)
        {
            SkipWs(s, ref i);
            if (i >= s.Length || s[i] != '{') return null;
            i++;
            var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            while (true)
            {
                SkipWs(s, ref i);
                if (i < s.Length && s[i] == '}') { i++; break; }
                string key = ParseString(s, ref i);
                SkipWs(s, ref i);
                if (i >= s.Length || s[i] != ':') break;
                i++;
                SkipWs(s, ref i);
                string val = null;
                if (i < s.Length && s[i] == '"')
                {
                    val = ParseString(s, ref i);
                }
                else if (i < s.Length && (s[i] == '{' || s[i] == '['))
                {
                    // 跳过复杂对象/数组值
                    SkipComplexValue(s, ref i);
                    val = string.Empty;
                }
                else
                {
                    val = ParseNonString(s, ref i);
                }
                dict[key] = val;
                SkipWs(s, ref i);
                if (i < s.Length && s[i] == ',') { i++; continue; }
                if (i < s.Length && s[i] == '}') { i++; break; }
                break;
            }
            return dict;
        }

        /// <summary>
        /// 解析JSON字符串
        /// </summary>
        private static string ParseString(string s, ref int i)
        {
            if (i >= s.Length || s[i] != '"') return string.Empty;
            i++;
            var sb = new StringBuilder();
            while (i < s.Length && s[i] != '"')
            {
                if (s[i] == '\\' && i + 1 < s.Length)
                {
                    i++;
                    switch (s[i])
                    {
                        case '"': sb.Append('"'); break;
                        case '\\': sb.Append('\\'); break;
                        case '/': sb.Append('/'); break;
                        case 'b': sb.Append('\b'); break;
                        case 'f': sb.Append('\f'); break;
                        case 'n': sb.Append('\n'); break;
                        case 'r': sb.Append('\r'); break;
                        case 't': sb.Append('\t'); break;
                        default: sb.Append(s[i]); break;
                    }
                }
                else
                {
                    sb.Append(s[i]);
                }
                i++;
            }
            if (i < s.Length) i++;
            return sb.ToString();
        }

        /// <summary>
        /// 解析非字符串值
        /// </summary>
        private static string ParseNonString(string s, ref int i)
        {
            var sb = new StringBuilder();
            while (i < s.Length && !char.IsWhiteSpace(s[i]) && s[i] != ',' && s[i] != '}' && s[i] != ']')
            {
                sb.Append(s[i]);
                i++;
            }
            return sb.ToString();
        }

        /// <summary>
        /// 跳过空白字符
        /// </summary>
        private static void SkipWs(string s, ref int i)
        {
            while (i < s.Length && char.IsWhiteSpace(s[i])) i++;
        }

        /// <summary>
        /// 跳过复杂值（对象或数组）
        /// </summary>
        private static void SkipComplexValue(string s, ref int i)
        {
            int depth = 0;
            char startChar = s[i];
            char endChar = (startChar == '{') ? '}' : ']';
            i++;
            while (i < s.Length)
            {
                if (s[i] == startChar) depth++;
                else if (s[i] == endChar)
                {
                    if (depth == 0) { i++; break; }
                    depth--;
                }
                i++;
            }
        }

        /// <summary>
        /// 从DeviceConfig.json读取WebPort，若不存在则返回默认值 8080
        /// </summary>
        private static int GetWebPort()
        {
            return DeviceConfigStore.GetWebPort(fallback: 8080);
        }
    }
}









