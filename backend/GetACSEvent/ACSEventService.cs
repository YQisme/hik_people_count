using System;
using System.Collections.Generic;
using System.Text;
using System.Runtime.InteropServices;
using System.IO;
using System.Threading;

namespace GetACSEvent
{
    /// <summary>
    /// 门禁事件后台服务类
    /// 实时获取门禁事件并在控制台输出
    /// </summary>
    class ACSEventService
    {
        private int m_UserID = -1;
        private int m_lAlarmHandle = -1;
        private string m_DeviceIP = string.Empty;
        private string m_UserName = string.Empty;
        private string m_Password = string.Empty;
        private ushort m_Port = 8000;
        private bool m_bRunning = false;
        private Thread m_AlarmThread = null;
        private readonly object m_GatewayStateSync = new object();
        private readonly Dictionary<int, bool> m_BlockedGateways = new Dictionary<int, bool>();
        private const uint LegacyGatewayStateAlwaysClosed = 3;
        private const uint LegacyGatewayStateCloseOnce = 0;
        private const uint LegacyGatewayStateOpenOnce = 1;
        private const byte RemoteGatewayCommandOpenOnce = 1;
        private const byte RemoteGatewayCommandRestoreNormal = 3;

        // 报警回调函数
        private CHCNetSDK.MSGCallBack m_MsgCallback = null;

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="deviceIP">门禁设备IP地址</param>
        /// <param name="userName">用户名</param>
        /// <param name="password">密码</param>
        /// <param name="port">端口号，默认8000</param>
        public ACSEventService(string deviceIP, string userName, string password, ushort port = 8000)
        {
            m_DeviceIP = deviceIP;
            m_UserName = userName;
            m_Password = password;
            m_Port = port;
        }

        /// <summary>
        /// 初始化SDK
        /// </summary>
        /// <returns>是否初始化成功</returns>
        public bool InitSDK()
        {
            bool result = CHCNetSDK.NET_DVR_Init();
            if (result)
            {
                CHCNetSDK.NET_DVR_SetLogToFile(3, "./SdkLog/", true);
                Console.WriteLine("SDK初始化成功");
            }
            else
            {
                Console.WriteLine("SDK初始化失败，错误码：" + CHCNetSDK.NET_DVR_GetLastError());
            }
            return result;
        }

        /// <summary>
        /// 登录设备
        /// </summary>
        /// <returns>是否登录成功</returns>
        public bool Login()
        {
            CHCNetSDK.NET_DVR_USER_LOGIN_INFO struLoginInfo = new CHCNetSDK.NET_DVR_USER_LOGIN_INFO();
            CHCNetSDK.NET_DVR_DEVICEINFO_V40 struDeviceInfoV40 = new CHCNetSDK.NET_DVR_DEVICEINFO_V40();
            struDeviceInfoV40.struDeviceV30.sSerialNumber = new byte[CHCNetSDK.SERIALNO_LEN];

            struLoginInfo.sDeviceAddress = System.Text.Encoding.Default.GetBytes(m_DeviceIP.Trim().PadRight(129, '\0').ToCharArray());
            struLoginInfo.sUserName = System.Text.Encoding.Default.GetBytes(m_UserName.Trim().PadRight(64, '\0').ToCharArray());
            struLoginInfo.sPassword = System.Text.Encoding.Default.GetBytes(m_Password.Trim().PadRight(64, '\0').ToCharArray());
            struLoginInfo.wPort = m_Port;

            m_UserID = CHCNetSDK.NET_DVR_Login_V40(ref struLoginInfo, ref struDeviceInfoV40);
            if (m_UserID >= 0)
            {
                Console.WriteLine("登录成功，设备IP：" + m_DeviceIP + "，用户ID：" + m_UserID);
                return true;
            }
            else
            {
                uint nErr = CHCNetSDK.NET_DVR_GetLastError();
                if (nErr == CHCNetSDK.NET_DVR_PASSWORD_ERROR)
                {
                    Console.WriteLine("设备IP：" + m_DeviceIP + "，用户名或密码错误！");
                    if (1 == struDeviceInfoV40.bySupportLock)
                    {
                        Console.WriteLine("剩余尝试次数：" + struDeviceInfoV40.byRetryLoginTime);
                    }
                }
                else if (nErr == CHCNetSDK.NET_DVR_USER_LOCKED)
                {
                    if (1 == struDeviceInfoV40.bySupportLock)
                    {
                        Console.WriteLine("设备IP：" + m_DeviceIP + "，用户被锁定，剩余锁定时间：" + struDeviceInfoV40.dwSurplusLockTime);
                    }
                }
                else
                {
                    Console.WriteLine("设备IP：" + m_DeviceIP + "，登录失败，错误码：" + nErr);
                }
                return false;
            }
        }

        /// <summary>
        /// 注销登录
        /// </summary>
        public void Logout()
        {
            if (m_UserID >= 0)
            {
                CHCNetSDK.NET_DVR_Logout_V30(m_UserID);
                m_UserID = -1;
                Console.WriteLine("设备IP：" + m_DeviceIP + "，已注销登录");
            }
        }

        /// <summary>
        /// 清理SDK资源
        /// </summary>
        public void Cleanup()
        {
            CHCNetSDK.NET_DVR_Cleanup();
            Console.WriteLine("SDK资源已释放");
        }

        /// <summary>
        /// 设置报警回调函数
        /// </summary>
        /// <param name="msgCallback">外部提供的回调函数，如果为null则使用内部回调函数</param>
        /// <returns>是否设置成功</returns>
        public bool SetupAlarmChan(CHCNetSDK.MSGCallBack msgCallback = null)
        {
            if (m_UserID < 0)
            {
                Console.WriteLine("设备IP：" + m_DeviceIP + "，请先登录设备");
                return false;
            }

            if (m_lAlarmHandle < 0)
            {
                // 如果外部提供了回调函数，则使用外部回调函数
                if (msgCallback != null)
                {
                    m_MsgCallback = msgCallback;
                }
                else
                {
                    // 否则使用内部回调函数
                    m_MsgCallback = new CHCNetSDK.MSGCallBack(MsgCallback);
                }

                bool bRet = CHCNetSDK.NET_DVR_SetDVRMessageCallBack_V30(m_MsgCallback, IntPtr.Zero);
                if (!bRet)
                {
                    uint nErr = CHCNetSDK.NET_DVR_GetLastError();
                    Console.WriteLine("设备IP：" + m_DeviceIP + "，设置回调函数失败，错误码：" + nErr);
                    return false;
                }
                
                CHCNetSDK.NET_DVR_SETUPALARM_PARAM struAlarmParam = new CHCNetSDK.NET_DVR_SETUPALARM_PARAM();
                struAlarmParam.dwSize = (uint)Marshal.SizeOf(struAlarmParam);
                struAlarmParam.byLevel = 1; // 智能交通布防优先级：0- 一等级（高），1- 二等级（中），2- 三等级（低）
                struAlarmParam.byAlarmInfoType = 1; // 智能交通报警信息上传类型：0- 老报警信息（NET_DVR_PLATE_RESULT），1- 新报警信息(NET_ITS_PLATE_RESULT)
                struAlarmParam.byDeployType = 1; // 布防类型：0-客户端布防，1-实时布防
                
                int lHandle = CHCNetSDK.NET_DVR_SetupAlarmChan_V41(m_UserID, ref struAlarmParam);
                if (lHandle < 0)
                {
                    uint nErr = CHCNetSDK.NET_DVR_GetLastError();
                    Console.WriteLine("设备IP：" + m_DeviceIP + "，布防失败，错误码：" + nErr);
                    return false;
                }
                else
                {
                    m_lAlarmHandle = lHandle;
                    Console.WriteLine("设备IP：" + m_DeviceIP + "，布防成功，布防句柄：" + m_lAlarmHandle);
                    return true;
                }
            }
            else
            {
                Console.WriteLine("设备IP：" + m_DeviceIP + "，已经布防过，布防句柄：" + m_lAlarmHandle);
                return true;
            }
        }

        /// <summary>
        /// 撤销布防
        /// </summary>
        public void CloseAlarmChan()
        {
            if (m_lAlarmHandle >= 0)
            {
                if (CHCNetSDK.NET_DVR_CloseAlarmChan_V30(m_lAlarmHandle))
                {
                    m_lAlarmHandle = -1;
                    Console.WriteLine("设备IP：" + m_DeviceIP + "，撤销布防成功");
                }
                else
                {
                    Console.WriteLine("设备IP：" + m_DeviceIP + "，撤销布防失败，错误码：" + CHCNetSDK.NET_DVR_GetLastError());
                }
            }
        }

        /// <summary>
        /// 报警回调函数
        /// </summary>
        public void MsgCallback(int lCommand, ref CHCNetSDK.NET_DVR_ALARMER pAlarmer, IntPtr pAlarmInfo, uint dwBufLen, IntPtr pUser)
        {
            try
            {
                // 处理门禁主机报警信息
                if (lCommand == CHCNetSDK.COMM_ALARM_ACS)
                {
                    ProcessCommAlarmAcs(pAlarmInfo, dwBufLen, ref pAlarmer);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("设备IP：" + m_DeviceIP + "，回调函数异常：" + ex.Message);
            }
        }

        /// <summary>
        /// 处理门禁主机报警信息（SDK 回调线程：仅拷贝数据并入队）
        /// </summary>
        public void ProcessCommAlarmAcs(IntPtr pAlarmInfo, uint dwBufLen, ref CHCNetSDK.NET_DVR_ALARMER pAlarmer)
        {
            string alarmDeviceIP = m_DeviceIP;
            if (pAlarmer.sDeviceIP != null && pAlarmer.sDeviceIP.Length > 0)
            {
                string fromAlarmer = Encoding.UTF8.GetString(pAlarmer.sDeviceIP).TrimEnd('\0');
                if (!string.IsNullOrEmpty(fromAlarmer))
                {
                    alarmDeviceIP = fromAlarmer;
                }
            }

            if (!string.Equals(alarmDeviceIP, m_DeviceIP, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            AcsEventProcessingQueue.TryEnqueueFromCallback(this, m_DeviceIP, pAlarmInfo, ref pAlarmer);
        }

        /// <summary>
        /// 后台线程处理门禁刷脸事件
        /// </summary>
        internal void ProcessPendingAlarm(PendingAcsAlarm pending)
        {
            if (pending == null)
            {
                return;
            }

            if (!string.Equals(pending.DeviceIP, m_DeviceIP, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            string deviceIP = pending.DeviceIP;
            string majorType = GetMajorTypeString(pending.DwMajor);
            string minorType = GetMinorTypeString(pending.DwMajor, pending.DwMinor);
            string time = string.Format("{0:D4}-{1:D2}-{2:D2} {3:D2}:{4:D2}:{5:D2}",
                pending.EventTime.dwYear,
                pending.EventTime.dwMonth,
                pending.EventTime.dwDay,
                pending.EventTime.dwHour,
                pending.EventTime.dwMinute,
                pending.EventTime.dwSecond);

            string cardNo = pending.CardNo ?? string.Empty;
            string employeeNo = pending.EmployeeNo ?? string.Empty;
            string personName = ResolvePersonName(employeeNo, pending.SNetUser);
            uint doorNo = pending.DoorNo;
            string cardType = GetCardTypeString(pending.CardType);

            AcsEvent faceEvent = new AcsEvent
            {
                TimeUtc = DateTime.UtcNow,
                Time = time,
                DeviceIP = deviceIP,
                DeviceName = GetDeviceName(deviceIP),
                DeviceID = GetDeviceID(deviceIP),
                AreaID = GetAreaID(deviceIP),
                Remark = GetRemark(deviceIP),
                MajorType = majorType,
                MinorType = minorType,
                CardNo = cardNo,
                EmployeeNo = employeeNo,
                PersonName = personName,
                CardType = cardType,
                DoorNo = doorNo,
                Direction = GetDoorDirection(deviceIP, doorNo),
                ImageUrl = string.Empty
            };

            ACSEventMultiDeviceService.SharedEventStore.Add(faceEvent);

            try
            {
                string direction = faceEvent.Direction;
                if (string.Equals(direction, "出", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(direction, "出", StringComparison.CurrentCulture))
                {
                    ACSEventMultiDeviceService sharedService = ACSEventMultiDeviceService.SharedInstance;
                    if (sharedService != null)
                    {
                        sharedService.TryHandleExitPass(deviceIP, (int)doorNo);
                    }
                }
            }
            catch (Exception openEx)
            {
                Console.WriteLine("设备IP：" + m_DeviceIP + "，出门联动开门失败：" + openEx.Message);
            }

            Console.WriteLine("--------------------------------------------------");
            Console.WriteLine("设备IP: " + deviceIP);
            Console.WriteLine("设备名称: " + GetDeviceName(deviceIP));
            Console.WriteLine("设备ID: " + GetDeviceID(deviceIP));
            Console.WriteLine("区域ID: " + GetAreaID(deviceIP));
            Console.WriteLine("备注: " + GetRemark(deviceIP));
            Console.WriteLine("时间: " + time);
            Console.WriteLine("事件类型: " + majorType + " - " + minorType);
            Console.WriteLine("卡号: " + cardNo);
            Console.WriteLine("员工号: " + employeeNo);
            Console.WriteLine("姓名: " + personName);
            Console.WriteLine("卡类型: " + cardType);
            Console.WriteLine("门编号: " + doorNo);

            if (pending.PicData != null && pending.PicData.Length > 0)
            {
                string deviceName = GetDeviceName(deviceIP);
                if (string.IsNullOrEmpty(deviceName) || deviceName == "未知设备")
                {
                    deviceName = deviceIP;
                }

                string safePersonName = GetSafeFolderName(personName);
                if (string.IsNullOrEmpty(safePersonName) || safePersonName == "未知")
                {
                    safePersonName = "未知员工";
                }

                string dirPath = "D:/Picture/" + deviceName + "/" + safePersonName + "/";
                if (!Directory.Exists(dirPath))
                {
                    Directory.CreateDirectory(dirPath);
                }

                string timeStr = string.Format("{0:D4}{1:D2}{2:D2}_{3:D2}{4:D2}{5:D2}",
                    pending.EventTime.dwYear, pending.EventTime.dwMonth, pending.EventTime.dwDay,
                    pending.EventTime.dwHour, pending.EventTime.dwMinute, pending.EventTime.dwSecond);

                string fileName = safePersonName + "_" + timeStr + ".jpg";
                string filePath = dirPath + fileName;

                try
                {
                    File.WriteAllBytes(filePath, pending.PicData);
                    Console.WriteLine("图片已保存: " + filePath);

                    string imageUrl = GetImageUrl(deviceName, safePersonName, fileName);
                    Console.WriteLine("图片URL: " + imageUrl);
                    ACSEventMultiDeviceService.SharedEventStore.TryUpdateImageUrl(deviceIP, employeeNo, cardNo, imageUrl);
                    faceEvent.ImageUrl = imageUrl;
                }
                catch (Exception ex)
                {
                    Console.WriteLine("保存图片失败: " + ex.Message);
                }
            }

            try
            {
                PersonInfoPublisher.PublishFaceEvent(faceEvent);
            }
            catch (Exception ex)
            {
                Console.WriteLine("发送personinfo失败: " + ex.Message);
            }
        }

        private string ResolvePersonName(string employeeNo, string sNetUser)
        {
            string personName = "未知";

            if (!string.IsNullOrEmpty(employeeNo) && !string.Equals(employeeNo, "0"))
            {
                if (ACSEventConsole.EmployeeNameMap.ContainsKey(employeeNo))
                {
                    return ACSEventConsole.EmployeeNameMap[employeeNo];
                }

                string[] possibleKeys = {
                    employeeNo,
                    employeeNo.Trim(),
                    employeeNo.TrimStart('0'),
                    employeeNo.PadLeft(employeeNo.Length + 1, '0'),
                    employeeNo.ToUpper(),
                    employeeNo.ToLower()
                };

                foreach (string key in possibleKeys)
                {
                    if (ACSEventConsole.EmployeeNameMap.ContainsKey(key))
                    {
                        return ACSEventConsole.EmployeeNameMap[key];
                    }
                }

                if (!string.IsNullOrEmpty(sNetUser))
                {
                    return sNetUser;
                }

                return "未知员工(" + employeeNo + ")";
            }

            if (!string.IsNullOrEmpty(sNetUser))
            {
                return sNetUser;
            }

            return personName;
        }

        /// <summary>
        /// 获取主类型字符串
        /// </summary>
        private string GetMajorTypeString(uint dwMajor)
        {
            switch (dwMajor)
            {
                case CHCNetSDK.MAJOR_ALARM:
                    return "报警";
                case CHCNetSDK.MAJOR_EXCEPTION:
                    return "异常";
                case CHCNetSDK.MAJOR_OPERATION:
                    return "操作";
                case CHCNetSDK.MAJOR_EVENT:
                    return "事件";
                default:
                    return "未知类型(" + dwMajor + ")";
            }
        }

        /// <summary>
        /// 获取次类型字符串
        /// </summary>
        private string GetMinorTypeString(uint dwMajor, uint dwMinor)
        {
            switch (dwMajor)
            {
                case CHCNetSDK.MAJOR_ALARM:
                    return GetAlarmMinorTypeString(dwMinor);
                case CHCNetSDK.MAJOR_EXCEPTION:
                    return GetExceptionMinorTypeString(dwMinor);
                case CHCNetSDK.MAJOR_OPERATION:
                    return GetOperationMinorTypeString(dwMinor);
                case CHCNetSDK.MAJOR_EVENT:
                    return GetEventMinorTypeString(dwMinor);
                default:
                    return "未知次类型(" + dwMinor + ")";
            }
        }

        /// <summary>
        /// 获取报警次类型字符串
        /// </summary>
        private string GetAlarmMinorTypeString(uint dwMinor)
        {
            switch (dwMinor)
            {
                case CHCNetSDK.MINOR_ALARMIN_SHORT_CIRCUIT:
                    return "防区短路报警";
                case CHCNetSDK.MINOR_ALARMIN_BROKEN_CIRCUIT:
                    return "防区断路报警";
                case CHCNetSDK.MINOR_ALARMIN_EXCEPTION:
                    return "防区异常报警";
                case CHCNetSDK.MINOR_ALARMIN_RESUME:
                    return "防区报警恢复";
                case CHCNetSDK.MINOR_CASE_SENSOR_ALARM:
                    return "事件输入报警";
                case CHCNetSDK.MINOR_CASE_SENSOR_RESUME:
                    return "事件输入恢复";
                case CHCNetSDK.MINOR_STRESS_ALARM:
                    return "胁迫报警";
                default:
                    return "未知报警类型(" + dwMinor + ")";
            }
        }

        /// <summary>
        /// 获取异常次类型字符串
        /// </summary>
        private string GetExceptionMinorTypeString(uint dwMinor)
        {
            switch (dwMinor)
            {
                case CHCNetSDK.MINOR_NET_BROKEN:
                    return "网络断开";
                case CHCNetSDK.MINOR_RS485_DEVICE_ABNORMAL:
                    return "RS485连接状态异常";
                case CHCNetSDK.MINOR_RS485_DEVICE_REVERT:
                    return "RS485连接状态恢复";
                case CHCNetSDK.MINOR_DEV_POWER_ON:
                    return "设备上电启动";
                case CHCNetSDK.MINOR_DEV_POWER_OFF:
                    return "设备掉电关闭";
                case CHCNetSDK.MINOR_WATCH_DOG_RESET:
                    return "看门狗复位";
                case CHCNetSDK.MINOR_LOW_BATTERY:
                    return "蓄电池电压低";
                case CHCNetSDK.MINOR_BATTERY_RESUME:
                    return "蓄电池电压恢复正常";
                case CHCNetSDK.MINOR_AC_OFF:
                    return "交流电断电";
                case CHCNetSDK.MINOR_AC_RESUME:
                    return "交流电恢复";
                case CHCNetSDK.MINOR_NET_RESUME:
                    return "网络恢复";
                case CHCNetSDK.MINOR_FLASH_ABNORMAL:
                    return "FLASH读写异常";
                case CHCNetSDK.MINOR_CARD_READER_OFFLINE:
                    return "读卡器掉线";
                case CHCNetSDK.MINOR_CARD_READER_RESUME:
                    return "读卡器掉线恢复";
                default:
                    return "未知异常类型(" + dwMinor + ")";
            }
        }

        /// <summary>
        /// 获取操作次类型字符串
        /// </summary>
        private string GetOperationMinorTypeString(uint dwMinor)
        {
            switch (dwMinor)
            {
                case CHCNetSDK.MINOR_LOCAL_UPGRADE:
                    return "本地升级";
                case CHCNetSDK.MINOR_REMOTE_LOGIN:
                    return "远程登录";
                case CHCNetSDK.MINOR_REMOTE_LOGOUT:
                    return "远程注销登陆";
                case CHCNetSDK.MINOR_REMOTE_ARM:
                    return "远程布防";
                case CHCNetSDK.MINOR_REMOTE_DISARM:
                    return "远程撤防";
                case CHCNetSDK.MINOR_REMOTE_REBOOT:
                    return "远程重启";
                case CHCNetSDK.MINOR_REMOTE_UPGRADE:
                    return "远程升级";
                case CHCNetSDK.MINOR_REMOTE_CFGFILE_OUTPUT:
                    return "远程导出配置文件";
                case CHCNetSDK.MINOR_REMOTE_CFGFILE_INTPUT:
                    return "远程导入配置文件";
                case CHCNetSDK.MINOR_REMOTE_OPEN_DOOR:
                    return "远程开门";
                default:
                    return "未知操作类型(" + dwMinor + ")";
            }
        }

        /// <summary>
        /// 获取事件次类型字符串
        /// </summary>
        private string GetEventMinorTypeString(uint dwMinor)
        {
            switch (dwMinor)
            {
                case CHCNetSDK.MINOR_LEGAL_CARD_PASS:
                    return "合法卡通过";
                case CHCNetSDK.MINOR_CARD_AND_PSW_PASS:
                    return "刷卡加密码通过";
                case CHCNetSDK.MINOR_CARD_AND_PSW_FAIL:
                    return "刷卡加密码失败";
                case CHCNetSDK.MINOR_CARD_AND_PSW_TIMEOUT:
                    return "数卡加密码超时";
                case CHCNetSDK.MINOR_CARD_NO_RIGHT:
                    return "卡无权限";
                case CHCNetSDK.MINOR_CARD_INVALID_PERIOD:
                    return "卡不在有效期";
                case CHCNetSDK.MINOR_CARD_OUT_OF_DATE:
                    return "卡过期";
                case CHCNetSDK.MINOR_INVALID_CARD:
                    return "无效卡";
                case CHCNetSDK.MINOR_DOOR_OPEN_NORMAL:
                    return "门正常打开";
                case CHCNetSDK.MINOR_DOOR_CLOSE_NORMAL:
                    return "门正常关闭";
                case CHCNetSDK.MINOR_DOOR_OPEN_ABNORMAL:
                    return "门异常打开";
                case CHCNetSDK.MINOR_DOOR_OPEN_TIMEOUT:
                    return "门打开超时";
                case CHCNetSDK.MINOR_ALARMOUT_ON:
                    return "报警输出开启";
                case CHCNetSDK.MINOR_ALARMOUT_OFF:
                    return "报警输出关闭";
                case CHCNetSDK.MINOR_ALWAYS_OPEN_BEGIN:
                    return "常开状态开始";
                case CHCNetSDK.MINOR_ALWAYS_OPEN_END:
                    return "常开状态结束";
                default:
                    return "未知事件类型(" + dwMinor + ")";
            }
        }

        /// <summary>
        /// 获取卡类型字符串
        /// </summary>
        private string GetCardTypeString(byte byCardType)
        {
            switch (byCardType)
            {
                case 1:
                    return "普通卡";
                case 2:
                    return "残疾人卡";
                case 3:
                    return "黑名单卡";
                case 4:
                    return "巡更卡";
                case 5:
                    return "胁迫卡";
                case 6:
                    return "超级卡";
                case 7:
                    return "来宾卡";
                case 8:
                    return "解除卡";
                default:
                    return "未知卡类型(" + byCardType + ")";
            }
        }

        /// <summary>
        /// 获取设备名称
        /// </summary>
        private string GetDeviceName(string deviceIP)
        {
            // 优先读取DeviceName，其次Name
            string deviceName = GetDeviceField(deviceIP, "DeviceName");
            if (!string.IsNullOrEmpty(deviceName))
            {
                return deviceName;
            }
            string name = GetDeviceField(deviceIP, "Name");
            if (!string.IsNullOrEmpty(name))
            {
                return name;
            }
            return "未知设备";
        }
        
        /// <summary>
        /// 获取设备ID
        /// </summary>
        private string GetDeviceID(string deviceIP)
        {
            string id = GetDeviceField(deviceIP, "DeviceID");
            return string.IsNullOrEmpty(id) ? "未知ID" : id;
        }
        
        /// <summary>
        /// 获取区域ID
        /// </summary>
        private string GetAreaID(string deviceIP)
        {
            string area = GetDeviceField(deviceIP, "AreaID");
            return string.IsNullOrEmpty(area) ? "未知区域" : area;
        }
        
        /// <summary>
        /// 获取备注（Remark）
        /// </summary>
        private string GetRemark(string deviceIP)
        {
            string remark = GetDeviceField(deviceIP, "Remark");
            return string.IsNullOrEmpty(remark) ? "无备注" : remark;
        }

        /// <summary>
        /// 从运行目录下的DeviceConfig.xml读取指定设备的字段
        /// </summary>
        private string GetDeviceField(string deviceIP, string nodeName)
        {
            return DeviceConfigStore.GetDeviceField(deviceIP, nodeName);
        }

        /// <summary>
        /// 启动服务
        /// </summary>
        public void Start()
        {
            if (m_bRunning)
            {
                Console.WriteLine("设备IP：" + m_DeviceIP + "，服务已经在运行中");
                return;
            }

            if (!InitSDK())
            {
                return;
            }

            if (!Login())
            {
                return;
            }

            if (!SetupAlarmChan())
            {
                Logout();
                return;
            }

            m_bRunning = true;
            m_AlarmThread = new Thread(AlarmThreadProc);
            m_AlarmThread.IsBackground = true;
            m_AlarmThread.Start();

            Console.WriteLine("设备IP：" + m_DeviceIP + "，门禁事件监控服务已启动");
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

            m_bRunning = false;

            if (m_AlarmThread != null)
            {
                m_AlarmThread.Join(1000);
                m_AlarmThread = null;
            }

            CloseAlarmChan();
            Logout();

            Console.WriteLine("设备IP：" + m_DeviceIP + "，门禁事件监控服务已停止");
        }

        /// <summary>
        /// 报警线程处理函数
        /// </summary>
        private void AlarmThreadProc()
        {
            while (m_bRunning)
            {
                // 保持线程运行，实际的报警处理在回调函数中进行
                Thread.Sleep(1000);
            }
        }

        /// <summary>
        /// 获取图片的URL地址
        /// </summary>
        /// <param name="deviceName">设备名称</param>
        /// <param name="personName">员工姓名</param>
        /// <param name="fileName">文件名</param>
        /// <returns>图片URL地址</returns>
        private string GetImageUrl(string deviceName, string personName, string fileName)
        {
            try
            {
                // 获取Web服务器的地址和端口
                string webServerUrl = GetWebServerUrl();
                
                // URL编码，确保中文路径能够正确访问
                string encodedDeviceName = Uri.EscapeDataString(deviceName);
                string encodedPersonName = Uri.EscapeDataString(personName);
                string encodedFileName = Uri.EscapeDataString(fileName);
                
                // 使用配置的Web服务器URL
                if (string.IsNullOrEmpty(webServerUrl))
                {
                    int webPort = GetWebPort();
                    webServerUrl = LocalNetworkHelper.BuildWebServerUrl(webPort);
                }
                
                return webServerUrl + "/images/" + encodedDeviceName + "/" + encodedPersonName + "/" + encodedFileName;
            }
            catch
            {
                // 如果获取失败，使用配置的端口号
                try
                {
                    int webPort = GetWebPort();
                    return LocalNetworkHelper.BuildWebServerUrl(webPort) + "/images/" + deviceName + "/" + personName + "/" + fileName;
                }
                catch
                {
                    return LocalNetworkHelper.BuildWebServerUrl(8080) + "/images/" + deviceName + "/" + personName + "/" + fileName;
                }
            }
        }

        /// <summary>
        /// 获取安全的文件夹名称（移除特殊字符）
        /// </summary>
        /// <param name="name">原始名称</param>
        /// <returns>安全的文件夹名称</returns>
        private string GetSafeFolderName(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                return "未知员工";
            }

            // 移除或替换Windows文件系统不允许的字符
            string safeName = name;
            char[] invalidChars = Path.GetInvalidFileNameChars();
            foreach (char c in invalidChars)
            {
                safeName = safeName.Replace(c, '_');
            }

            // 移除前后空格
            safeName = safeName.Trim();

            // 如果处理后为空，返回默认值
            if (string.IsNullOrEmpty(safeName))
            {
                return "未知员工";
            }

            return safeName;
        }

        /// <summary>
        /// 获取Web服务器URL
        /// </summary>
        /// <returns>Web服务器URL</returns>
        private string GetWebServerUrl()
        {
            try
            {
                int webPort = GetWebPort();
                return LocalNetworkHelper.BuildWebServerUrl(webPort);
            }
            catch
            {
                return LocalNetworkHelper.BuildWebServerUrl(8080);
            }
        }

        /// <summary>
        /// 从DeviceConfig.xml读取WebPort，若不存在则返回默认值 8080
        /// </summary>
        private int GetWebPort()
        {
            return DeviceConfigStore.GetWebPort(fallback: 8080);
        }

        /// <summary>
        /// 根据设备配置获取门的进出方向（进/出/未知）
        /// </summary>
        /// <param name="deviceIP">设备IP</param>
        /// <param name="doorNo">门编号</param>
        /// <returns>方向字符串：进/出/未知</returns>
        private string GetDoorDirection(string deviceIP, uint doorNo)
        {
            return DeviceConfigStore.GetDoorDirection(deviceIP, doorNo);
        }

        public string DeviceIP
        {
            get { return m_DeviceIP; }
        }

        public bool BlockGateway(int gatewayIndex)
        {
            if (gatewayIndex <= 0)
            {
                gatewayIndex = 1;
            }

            lock (m_GatewayStateSync)
            {
                bool isBlocked;
                if (m_BlockedGateways.TryGetValue(gatewayIndex, out isBlocked) && isBlocked)
                {
                    return true;
                }
            }

            if (m_UserID < 0)
            {
                Console.WriteLine("设备IP：" + m_DeviceIP + "，未登录，无法控制门禁状态");
                return false;
            }

            bool result = CHCNetSDK.NET_DVR_ControlGateway(m_UserID, gatewayIndex, LegacyGatewayStateAlwaysClosed);
            if (!result)
            {
                Console.WriteLine("设备IP：" + m_DeviceIP + "，控制门禁失败，门号：" + gatewayIndex + "，状态：常关，错误码：" + CHCNetSDK.NET_DVR_GetLastError());
                return false;
            }

            lock (m_GatewayStateSync)
            {
                m_BlockedGateways[gatewayIndex] = true;
            }

            Console.WriteLine("设备IP：" + m_DeviceIP + "，门号：" + gatewayIndex + " 已切换为禁入状态");
            return true;
        }

        public bool RestoreGatewayNormal(int gatewayIndex)
        {
            if (gatewayIndex <= 0)
            {
                gatewayIndex = 1;
            }

            bool wasBlocked = false;
            lock (m_GatewayStateSync)
            {
                bool isBlocked;
                if (m_BlockedGateways.TryGetValue(gatewayIndex, out isBlocked))
                {
                    wasBlocked = isBlocked;
                }
            }

            if (!wasBlocked)
            {
                return true;
            }

            bool result = CHCNetSDK.NET_DVR_ControlGateway(m_UserID, gatewayIndex, LegacyGatewayStateCloseOnce);
            if (!result)
            {
                Console.WriteLine("设备IP：" + m_DeviceIP + "，老接口恢复门禁失败，门号：" + gatewayIndex + "，状态：0，错误码：" + CHCNetSDK.NET_DVR_GetLastError());
                result = SendRemoteGatewayCommand(gatewayIndex, RemoteGatewayCommandRestoreNormal, "capacity-restore");
            }
            if (!result)
            {
                return false;
            }

            lock (m_GatewayStateSync)
            {
                m_BlockedGateways[gatewayIndex] = false;
            }

            Console.WriteLine("设备IP：" + m_DeviceIP + "，门号：" + gatewayIndex + " 已恢复正常受控状态");
            return true;
        }

        public bool OpenGatewayOnce(int gatewayIndex, string controlSource)
        {
            if (gatewayIndex <= 0)
            {
                gatewayIndex = 1;
            }

            bool result = CHCNetSDK.NET_DVR_ControlGateway(m_UserID, gatewayIndex, LegacyGatewayStateOpenOnce);
            if (result)
            {
                return true;
            }

            Console.WriteLine("设备IP：" + m_DeviceIP + "，老接口单次开门失败，门号：" + gatewayIndex + "，状态：1，错误码：" + CHCNetSDK.NET_DVR_GetLastError());

            return SendRemoteGatewayCommand(gatewayIndex, RemoteGatewayCommandOpenOnce, controlSource);
        }

        private bool SendRemoteGatewayCommand(int gatewayIndex, byte command, string controlSource)
        {
            if (m_UserID < 0)
            {
                Console.WriteLine("设备IP：" + m_DeviceIP + "，未登录，无法远程控制门禁");
                return false;
            }

            CHCNetSDK.NET_DVR_CONTROL_GATEWAY gateway = new CHCNetSDK.NET_DVR_CONTROL_GATEWAY();
            gateway.dwSize = (uint)Marshal.SizeOf(typeof(CHCNetSDK.NET_DVR_CONTROL_GATEWAY));
            gateway.dwGatewayIndex = (uint)gatewayIndex;
            gateway.byCommand = command;
            gateway.byLockType = 0;
            gateway.wLockID = 0;
            gateway.byControlSrc = new byte[CHCNetSDK.NAME_LEN];
            gateway.byRes3 = new byte[3];
            gateway.byPassword = new byte[CHCNetSDK.PASSWD_LEN];
            gateway.byRes2 = new byte[108];
            gateway.byControlType = 1;

            byte[] srcBytes = Encoding.ASCII.GetBytes(string.IsNullOrEmpty(controlSource) ? "capacity-control" : controlSource);
            int copyLength = srcBytes.Length < gateway.byControlSrc.Length ? srcBytes.Length : gateway.byControlSrc.Length - 1;
            Array.Copy(srcBytes, gateway.byControlSrc, copyLength);

            IntPtr buffer = IntPtr.Zero;
            try
            {
                buffer = Marshal.AllocHGlobal((int)gateway.dwSize);
                Marshal.StructureToPtr(gateway, buffer, false);

                bool result = CHCNetSDK.NET_DVR_RemoteControl(m_UserID, CHCNetSDK.NET_DVR_REMOTECONTROL_GATEWAY, buffer, (int)gateway.dwSize);
                if (!result)
                {
                    Console.WriteLine("设备IP：" + m_DeviceIP + "，远程门控失败，门号：" + gatewayIndex + "，命令：" + command + "，错误码：" + CHCNetSDK.NET_DVR_GetLastError());
                    return false;
                }

                return true;
            }
            finally
            {
                if (buffer != IntPtr.Zero)
                {
                    Marshal.FreeHGlobal(buffer);
                }
            }
        }
    }
}



