using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.Threading;

namespace GetACSEvent
{
    /// <summary>
    /// 门禁设备配置信息
    /// </summary>
    public class DeviceInfo
    {
        public string IP { get; set; }
        public string UserName { get; set; }
        public string Password { get; set; }
        public ushort Port { get; set; }
        public bool Enabled { get; set; }
        public string Name { get; set; }
        public string Remark { get; set; }
        public string DeviceID { get; set; }
        public string AreaID { get; set; }
        public string DeviceName { get; set; }
        public string Direction { get; set; }
        public int ControlDoorNo { get; set; }

        public DeviceInfo()
        {
            IP = string.Empty;
            UserName = string.Empty;
            Password = string.Empty;
            Port = 8000;
            Enabled = true;
            Name = string.Empty;
            Remark = string.Empty;
            DeviceID = string.Empty;
            AreaID = string.Empty;
            DeviceName = string.Empty;
            Direction = string.Empty;
            ControlDoorNo = 1;
        }
    }

    public class DoorControlTarget
    {
        public string DeviceIP { get; set; }
        public string DeviceName { get; set; }
        public string Direction { get; set; }
        public int GatewayIndex { get; set; }
        public string MatchedName { get; set; }

        public DoorControlTarget()
        {
            DeviceIP = string.Empty;
            DeviceName = string.Empty;
            Direction = string.Empty;
            GatewayIndex = 1;
            MatchedName = string.Empty;
        }
    }

    /// <summary>
    /// 多门禁设备事件监控服务
    /// </summary>
    public class ACSEventMultiDeviceService
    {
        private List<DeviceInfo> m_DeviceList = new List<DeviceInfo>();
        private List<ACSEventService> m_ServiceList = new List<ACSEventService>();
        private Dictionary<string, ACSEventService> m_ServiceByIp = new Dictionary<string, ACSEventService>(StringComparer.OrdinalIgnoreCase);
        public static EventStore SharedEventStore = new EventStore(1000);
        public static ACSEventMultiDeviceService SharedInstance { get; private set; }
        private bool m_bRunning = false;
        private bool m_IsEntryBlocked = false;
        private readonly object m_ExitGraceSync = new object();
        private readonly object m_ExitOpenRetrySync = new object();
        private readonly object m_AbnormalDoorSync = new object();
        private readonly Dictionary<string, DateTime> m_ExitOpenRetryUntilUtc = new Dictionary<string, DateTime>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, bool> m_AbnormalBlockedGateways = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
        private DateTime m_EntryUnblockUntilUtc = DateTime.MinValue;
        private int m_ExitGraceStartCount = -1;
        private string m_ConfigFile = ConfigPaths.DeviceConfigPath;
        private bool m_bInitialized = false;
        private CHCNetSDK.MSGCallBack m_MsgCallback = null; // 全局报警回调函数

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="configFile">配置文件路径，默认为 backend/config/DeviceConfig.json</param>
        public ACSEventMultiDeviceService(string configFile = null)
        {
            m_ConfigFile = string.IsNullOrEmpty(configFile) ? ConfigPaths.DeviceConfigPath : configFile;
            SharedInstance = this;
        }

        /// <summary>
        /// 加载设备配置
        /// </summary>
        /// <returns>是否加载成功</returns>
        public bool LoadConfig()
        {
            try
            {
                if (!File.Exists(m_ConfigFile))
                {
                    Console.WriteLine("配置文件不存在，将创建默认配置文件: " + m_ConfigFile);
                    CreateDefaultConfig();
                    return false;
                }

                m_DeviceList.Clear();
                var document = DeviceConfigStore.Load(m_ConfigFile);
                if (document?.Devices != null)
                {
                    foreach (var entry in document.Devices)
                    {
                        if (entry != null)
                        {
                            m_DeviceList.Add(entry.ToDeviceInfo());
                        }
                    }
                }

                Console.WriteLine("成功加载 " + m_DeviceList.Count + " 个设备配置");
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine("加载配置文件失败: " + ex.Message);
                return false;
            }
        }

        /// <summary>
        /// 创建默认配置文件
        /// </summary>
        private void CreateDefaultConfig()
        {
            try
            {
                DeviceConfigStore.CreateDefault(m_ConfigFile);
                Console.WriteLine("已创建默认配置文件: " + m_ConfigFile);
                Console.WriteLine("请编辑配置文件，添加或修改门禁设备信息，然后重新启动程序");
            }
            catch (Exception ex)
            {
                Console.WriteLine("创建默认配置文件失败: " + ex.Message);
            }
        }

        /// <summary>
        /// 初始化服务
        /// </summary>
        /// <returns>是否初始化成功</returns>
        public bool Initialize()
        {
            if (m_bInitialized)
            {
                return true;
            }

            if (!LoadConfig())
            {
                return false;
            }

            // 初始化SDK，只需要初始化一次
            bool result = CHCNetSDK.NET_DVR_Init();
            if (result)
            {
                CHCNetSDK.NET_DVR_SetLogToFile(3, "./SdkLog/", true);
                Console.WriteLine("SDK初始化成功");
            }
            else
            {
                Console.WriteLine("SDK初始化失败，错误码：" + CHCNetSDK.NET_DVR_GetLastError());
                return false;
            }

            m_bInitialized = true;
            return true;
        }

        /// <summary>
        /// 全局报警回调函数
        /// </summary>
        public void GlobalMsgCallback(int lCommand, ref CHCNetSDK.NET_DVR_ALARMER pAlarmer, IntPtr pAlarmInfo, uint dwBufLen, IntPtr pUser)
        {
            try
            {
                // 处理门禁主机报警信息
                if (lCommand == CHCNetSDK.COMM_ALARM_ACS)
                {
                    // 获取设备IP地址
                    string deviceIP = "";
                    if (pAlarmer.sDeviceIP != null && pAlarmer.sDeviceIP.Length > 0)
                    {
                        deviceIP = Encoding.UTF8.GetString(pAlarmer.sDeviceIP).TrimEnd('\0');
                    }

                    // 查找对应的设备信息
                    DeviceInfo deviceInfo = null;
                    foreach (DeviceInfo device in m_DeviceList)
                    {
                        if (device.IP == deviceIP)
                        {
                            deviceInfo = device;
                            break;
                        }
                    }

                    // 查找对应的服务实例并按设备 IP 分发
                    ACSEventService service;
                    if (m_ServiceByIp.TryGetValue(deviceIP, out service))
                    {
                        service.ProcessCommAlarmAcs(pAlarmInfo, dwBufLen, ref pAlarmer);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("全局回调函数异常：" + ex.Message);
            }
        }

        /// <summary>
        /// 启动服务
        /// </summary>
        public void Start()
        {
            if (m_bRunning)
            {
                Console.WriteLine("服务已经在运行中");
                return;
            }

            if (!Initialize())
            {
                return;
            }

            AcsEventProcessingQueue.Start();

            m_ServiceList.Clear();
            m_ServiceByIp.Clear();

            // 注册全局回调函数
            m_MsgCallback = new CHCNetSDK.MSGCallBack(GlobalMsgCallback);
            bool bRet = CHCNetSDK.NET_DVR_SetDVRMessageCallBack_V30(m_MsgCallback, IntPtr.Zero);
            if (!bRet)
            {
                uint nErr = CHCNetSDK.NET_DVR_GetLastError();
                Console.WriteLine("设置全局回调函数失败，错误码：" + nErr);
                return;
            }

            foreach (DeviceInfo device in m_DeviceList)
            {
                if (device.Enabled)
                {
                    ACSEventService service = new ACSEventService(device.IP, device.UserName, device.Password, device.Port);
                    
                    // 不需要再次初始化SDK，因为已经在Initialize方法中初始化过了
                    if (service.Login() && service.SetupAlarmChan(m_MsgCallback))
                    {
                        m_ServiceList.Add(service);
                        m_ServiceByIp[device.IP] = service;
                        string deviceInfo = string.Format("设备 {0} [{1}] ({2}) 启动成功", 
                            device.IP, 
                            string.IsNullOrEmpty(device.Name) ? "无名称" : device.Name, 
                            string.IsNullOrEmpty(device.Remark) ? "无备注" : device.Remark);
                        Console.WriteLine(deviceInfo);
                        
                        // 显示设备的额外信息
                        Console.WriteLine(string.Format("  设备ID: {0}", 
                            string.IsNullOrEmpty(device.DeviceID) ? "未设置" : device.DeviceID));
                        Console.WriteLine(string.Format("  区域ID: {0}", 
                            string.IsNullOrEmpty(device.AreaID) ? "未设置" : device.AreaID));
                        Console.WriteLine(string.Format("  设备名称: {0}", 
                            string.IsNullOrEmpty(device.DeviceName) ? "未设置" : device.DeviceName));
                        Console.WriteLine(string.Format("  进出方向: {0}", 
                            string.IsNullOrEmpty(device.Direction) ? "未设置" : device.Direction));
                    }
                    else
                    {
                        string deviceInfo = string.Format("设备 {0} [{1}] ({2}) 启动失败", 
                            device.IP, 
                            string.IsNullOrEmpty(device.Name) ? "无名称" : device.Name, 
                            string.IsNullOrEmpty(device.Remark) ? "无备注" : device.Remark);
                        Console.WriteLine(deviceInfo);
                    }
                }
                else
                {
                    Console.WriteLine("设备 " + device.IP + " (" + device.Remark + ") 已禁用，跳过");
                }
            }

            if (m_ServiceList.Count > 0)
            {
                m_bRunning = true;
                Console.WriteLine("多门禁事件监控服务已启动，共 " + m_ServiceList.Count + " 个设备");
            }
            else
            {
                Console.WriteLine("没有可用的门禁设备，服务未启动");
                CHCNetSDK.NET_DVR_Cleanup();
            }
        }

        /// <summary>
        /// 停止服务
        /// </summary>
        public void Stop()
        {
            if (!m_bRunning)
            {
                return;
            }

            foreach (ACSEventService service in m_ServiceList)
            {
                service.Stop();
            }

            AcsEventProcessingQueue.Stop();

            m_ServiceList.Clear();
            m_ServiceByIp.Clear();
            m_bRunning = false;

            // 清理SDK资源
            CHCNetSDK.NET_DVR_Cleanup();
            Console.WriteLine("多门禁事件监控服务已停止");
        }

        public bool IsDeviceOnline(string deviceIP)
        {
            if (string.IsNullOrWhiteSpace(deviceIP))
            {
                return false;
            }

            ACSEventService service;
            return m_ServiceByIp.TryGetValue(deviceIP.Trim(), out service) && service != null && service.IsOnline;
        }

        public void ApplyCapacityDoorRule(bool blockEntry)
        {
            m_IsEntryBlocked = blockEntry;
            foreach (DeviceInfo device in m_DeviceList)
            {
                if (device == null || !device.Enabled)
                {
                    continue;
                }

                ACSEventService service;
                if (!m_ServiceByIp.TryGetValue(device.IP, out service) || service == null)
                {
                    continue;
                }

                string direction = NormalizeDirection(device.Direction);
                if (string.IsNullOrEmpty(direction))
                {
                    continue;
                }

                int gatewayIndex = device.ControlDoorNo <= 0 ? 1 : device.ControlDoorNo;
                if (IsAbnormalBlocked(device.IP, gatewayIndex))
                {
                    service.BlockGateway(gatewayIndex);
                    continue;
                }

                if (blockEntry && direction == "进")
                {
                    service.BlockGateway(gatewayIndex);
                    continue;
                }

                if (!blockEntry)
                {
                    service.RestoreGatewayNormal(gatewayIndex);
                }
            }
        }

        public void ApplyAbnormalDoorRule(string abnormalType, string zoneName, int conditionMet)
        {
            string safeType = CleanText(abnormalType);
            if (!string.Equals(safeType, "尾随闯入", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            string safeZoneName = CleanText(zoneName);
            if (string.IsNullOrEmpty(safeZoneName))
            {
                Console.WriteLine("收到尾随闯入MQTT，但未提供 zoneName，忽略门控切换");
                return;
            }

            bool shouldBlock = conditionMet == 1;
            List<DoorControlTarget> targets = ResolveDoorTargetsByZoneName(safeZoneName);
            if (targets.Count == 0)
            {
                Console.WriteLine("收到尾随闯入MQTT，但未找到匹配门配置: " + safeZoneName);
                return;
            }

            foreach (DoorControlTarget target in targets)
            {
                ApplyAbnormalDoorTarget(target, shouldBlock);
            }

            Console.WriteLine(
                "尾随闯入门控已更新: zone=" + safeZoneName +
                ", conditionMet=" + conditionMet +
                ", action=" + (shouldBlock ? "常闭" : "恢复刷脸开门") +
                ", matched=" + targets.Count);
        }

        public void TryHandleExitPass(string deviceIP, int doorNo)
        {
            if (!m_IsEntryBlocked)
            {
                return;
            }

            DeviceInfo targetDevice = null;
            foreach (DeviceInfo device in m_DeviceList)
            {
                if (device == null || !device.Enabled)
                {
                    continue;
                }

                if (!string.Equals(device.IP, deviceIP, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                string direction = NormalizeDirection(device.Direction);
                if (direction != "出")
                {
                    return;
                }

                targetDevice = device;
                break;
            }

            if (targetDevice == null)
            {
                return;
            }

            ACSEventService service;
            if (!m_ServiceByIp.TryGetValue(targetDevice.IP, out service) || service == null)
            {
                return;
            }

            int exitGraceSeconds = RuntimeConfig.LoadDefault().ExitGraceSeconds;
            int currentCount = ResolveCurrentCount();
            int gatewayIndex = targetDevice.ControlDoorNo > 0 ? targetDevice.ControlDoorNo : (doorNo > 0 ? doorNo : 1);
            BeginExitGracePeriod(exitGraceSeconds, currentCount);
            ApplyCapacityDoorRule(false);
            Console.WriteLine("检测到满员状态下的出门刷脸，设备IP：" + targetDevice.IP + "，已临时解除门禁限制 " + exitGraceSeconds + " 秒，等待人数从 " + currentCount + " 下降后再恢复限制");
            ScheduleExitOpenRetries(service, targetDevice.IP, gatewayIndex);
        }

        public bool IsExitGraceActive(int currentCount)
        {
            lock (m_ExitGraceSync)
            {
                if (m_ExitGraceStartCount > 0 && currentCount >= 0 && currentCount < m_ExitGraceStartCount)
                {
                    m_EntryUnblockUntilUtc = DateTime.MinValue;
                    m_ExitGraceStartCount = -1;
                    return false;
                }

                if (DateTime.UtcNow < m_EntryUnblockUntilUtc)
                {
                    return true;
                }

                m_ExitGraceStartCount = -1;
                return false;
            }
        }

        private void BeginExitGracePeriod(int seconds, int currentCount)
        {
            if (seconds <= 0)
            {
                seconds = 5;
            }

            lock (m_ExitGraceSync)
            {
                DateTime nextUntil = DateTime.UtcNow.AddSeconds(seconds);
                if (nextUntil > m_EntryUnblockUntilUtc)
                {
                    m_EntryUnblockUntilUtc = nextUntil;
                }
                m_ExitGraceStartCount = currentCount;
            }
        }

        private void ScheduleExitOpenRetries(ACSEventService service, string deviceIP, int gatewayIndex)
        {
            if (service == null)
            {
                return;
            }

            string gateKey = BuildGateKey(deviceIP, gatewayIndex);
            DateTime nowUtc = DateTime.UtcNow;
            lock (m_ExitOpenRetrySync)
            {
                DateTime busyUntilUtc;
                if (m_ExitOpenRetryUntilUtc.TryGetValue(gateKey, out busyUntilUtc) && busyUntilUtc > nowUtc)
                {
                    return;
                }

                m_ExitOpenRetryUntilUtc[gateKey] = nowUtc.AddSeconds(2);
            }

            ThreadPool.QueueUserWorkItem(delegate
            {
                int[] retryTimelineMs = new int[] { 350, 1100 };
                int elapsedMs = 0;
                try
                {
                    for (int attemptIndex = 0; attemptIndex < retryTimelineMs.Length; attemptIndex++)
                    {
                        int nextDelayMs = retryTimelineMs[attemptIndex] - elapsedMs;
                        if (nextDelayMs > 0)
                        {
                            Thread.Sleep(nextDelayMs);
                            elapsedMs += nextDelayMs;
                        }

                        if (!IsExitGraceActive(ResolveCurrentCount()))
                        {
                            Console.WriteLine("设备IP：" + deviceIP + "，门号：" + gatewayIndex + " 的补发开门任务提前结束，限员解除窗口已关闭");
                            break;
                        }

                        bool opened = service.OpenGatewayOnce(gatewayIndex, "capacity-exit-first-pass-" + (attemptIndex + 1));
                        Console.WriteLine(
                            opened
                                ? "设备IP：" + deviceIP + "，已补发第 " + (attemptIndex + 1) + " 次出门开门命令，门号：" + gatewayIndex
                                : "设备IP：" + deviceIP + "，补发第 " + (attemptIndex + 1) + " 次出门开门命令失败，门号：" + gatewayIndex);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine("设备IP：" + deviceIP + "，补发出门开门命令异常: " + ex.Message);
                }
                finally
                {
                    lock (m_ExitOpenRetrySync)
                    {
                        m_ExitOpenRetryUntilUtc.Remove(gateKey);
                    }
                }
            });
        }

        private static string BuildGateKey(string deviceIP, int gatewayIndex)
        {
            string normalizedIP = (deviceIP ?? string.Empty).Trim();
            return normalizedIP + "#" + gatewayIndex.ToString();
        }

        private void ApplyAbnormalDoorTarget(DoorControlTarget target, bool shouldBlock)
        {
            if (target == null || string.IsNullOrEmpty(target.DeviceIP))
            {
                return;
            }

            ACSEventService service;
            if (!m_ServiceByIp.TryGetValue(target.DeviceIP, out service) || service == null)
            {
                Console.WriteLine("尾随闯入门控匹配成功，但设备服务不可用，设备IP：" + target.DeviceIP);
                return;
            }

            string gateKey = BuildGateKey(target.DeviceIP, target.GatewayIndex);
            if (shouldBlock)
            {
                lock (m_AbnormalDoorSync)
                {
                    m_AbnormalBlockedGateways[gateKey] = true;
                }

                service.BlockGateway(target.GatewayIndex);
                return;
            }

            lock (m_AbnormalDoorSync)
            {
                m_AbnormalBlockedGateways.Remove(gateKey);
            }

            bool shouldKeepCapacityBlocked = m_IsEntryBlocked && NormalizeDirection(target.Direction) == "进";
            if (shouldKeepCapacityBlocked)
            {
                service.BlockGateway(target.GatewayIndex);
            }
            else
            {
                service.RestoreGatewayNormal(target.GatewayIndex);
            }
        }

        private bool IsAbnormalBlocked(string deviceIP, int gatewayIndex)
        {
            string gateKey = BuildGateKey(deviceIP, gatewayIndex);
            lock (m_AbnormalDoorSync)
            {
                bool blocked;
                return m_AbnormalBlockedGateways.TryGetValue(gateKey, out blocked) && blocked;
            }
        }

        private List<DoorControlTarget> ResolveDoorTargetsByZoneName(string zoneName)
        {
            var result = DeviceConfigStore.ResolveDoorTargetsByZoneName(zoneName, m_ConfigFile);
            if (result.Count > 0)
            {
                return result;
            }

            var seen = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
            string safeZoneName = CleanText(zoneName);
            if (string.IsNullOrEmpty(safeZoneName))
            {
                return result;
            }

            foreach (DeviceInfo device in m_DeviceList)
            {
                if (device == null || string.IsNullOrEmpty(device.IP))
                {
                    continue;
                }

                if (!IsNameMatch(device.DeviceName, safeZoneName) && !IsNameMatch(device.Name, safeZoneName))
                {
                    continue;
                }

                AddDoorTarget(
                    result,
                    seen,
                    device.IP,
                    FirstNonEmpty(device.DeviceName, device.Name),
                    device.Direction,
                    device.ControlDoorNo <= 0 ? 1 : device.ControlDoorNo,
                    FirstNonEmpty(device.DeviceName, device.Name));
            }

            return result;
        }

        private static void AddDoorTarget(
            List<DoorControlTarget> targets,
            Dictionary<string, bool> seen,
            string deviceIP,
            string deviceName,
            string direction,
            int gatewayIndex,
            string matchedName)
        {
            int safeGatewayIndex = gatewayIndex > 0 ? gatewayIndex : 1;
            string gateKey = BuildGateKey(deviceIP, safeGatewayIndex);
            if (seen.ContainsKey(gateKey))
            {
                return;
            }

            seen[gateKey] = true;
            targets.Add(new DoorControlTarget
            {
                DeviceIP = CleanText(deviceIP),
                DeviceName = CleanText(deviceName),
                Direction = CleanText(direction),
                GatewayIndex = safeGatewayIndex,
                MatchedName = CleanText(matchedName)
            });
        }

        private static string FirstNonEmpty(params string[] values)
        {
            if (values == null)
            {
                return string.Empty;
            }

            for (int i = 0; i < values.Length; i++)
            {
                string value = CleanText(values[i]);
                if (!string.IsNullOrEmpty(value))
                {
                    return value;
                }
            }

            return string.Empty;
        }

        private static bool IsNameMatch(string left, string right)
        {
            string a = NormalizeName(left);
            string b = NormalizeName(right);
            if (string.IsNullOrEmpty(a) || string.IsNullOrEmpty(b))
            {
                return false;
            }

            return string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
        }

        private static string NormalizeName(string value)
        {
            string safeValue = CleanText(value);
            if (string.IsNullOrEmpty(safeValue))
            {
                return string.Empty;
            }

            return safeValue.Replace(" ", string.Empty);
        }

        private static string CleanText(string value)
        {
            return (value ?? string.Empty).Trim();
        }

        private static int ResolveCurrentCount()
        {
            PeopleCountInfo peopleCount = PeopleCountState.GetSnapshot();
            if (peopleCount != null && peopleCount.HasData)
            {
                return peopleCount.TotalCount;
            }

            List<AcsEvent> events = SharedEventStore.Snapshot();
            if (events == null || events.Count == 0)
            {
                return 0;
            }

            var latestPerPerson = new Dictionary<string, AcsEvent>(StringComparer.OrdinalIgnoreCase);
            events.Sort(delegate (AcsEvent a, AcsEvent b)
            {
                return a.TimeUtc.CompareTo(b.TimeUtc);
            });

            foreach (AcsEvent ev in events)
            {
                string key = (ev == null ? string.Empty : ((ev.EmployeeNo ?? string.Empty).Trim()));
                if (string.IsNullOrEmpty(key))
                {
                    key = ev == null ? string.Empty : ((ev.CardNo ?? string.Empty).Trim());
                }
                if (string.IsNullOrEmpty(key))
                {
                    key = ev == null ? string.Empty : ((ev.PersonName ?? string.Empty).Trim());
                }
                if (string.IsNullOrEmpty(key))
                {
                    continue;
                }

                latestPerPerson[key] = ev;
            }

            int count = 0;
            foreach (KeyValuePair<string, AcsEvent> pair in latestPerPerson)
            {
                string direction = NormalizeDirection(pair.Value == null ? string.Empty : pair.Value.Direction);
                if (direction == "进")
                {
                    count++;
                }
            }

            return count;
        }

        private static string NormalizeDirection(string direction)
        {
            string value = (direction ?? string.Empty).Trim().ToLowerInvariant();
            if (value == "in" || value == "enter" || value == "进入" || value == "入" || value == "进")
            {
                return "进";
            }
            if (value == "out" || value == "exit" || value == "出去" || value == "出")
            {
                return "出";
            }
            return string.Empty;
        }
    }
}



