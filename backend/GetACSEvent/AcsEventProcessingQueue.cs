using System;
using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using System.Threading;

namespace GetACSEvent
{
    internal sealed class PendingAcsAlarm
    {
        public ACSEventService Service { get; set; }
        public string DeviceIP { get; set; }
        public uint DwMajor { get; set; }
        public uint DwMinor { get; set; }
        public CHCNetSDK.NET_DVR_TIME EventTime { get; set; }
        public string CardNo { get; set; }
        public string EmployeeNo { get; set; }
        public string SNetUser { get; set; }
        public uint DoorNo { get; set; }
        public byte CardType { get; set; }
        public byte[] PicData { get; set; }
    }

    internal static class AcsEventProcessingQueue
    {
        private static readonly ConcurrentQueue<PendingAcsAlarm> Queue = new ConcurrentQueue<PendingAcsAlarm>();
        private static Thread _worker;
        private static bool _running;

        public static void Start()
        {
            if (_running)
            {
                return;
            }

            _running = true;
            _worker = new Thread(WorkerLoop)
            {
                IsBackground = true,
                Name = "AcsEventProcessingQueue"
            };
            _worker.Start();
        }

        public static void Stop()
        {
            _running = false;
            try
            {
                if (_worker != null && _worker.IsAlive)
                {
                    _worker.Join(2000);
                }
            }
            catch
            {
            }
        }

        public static bool TryEnqueueFromCallback(
            ACSEventService service,
            string deviceIP,
            IntPtr pAlarmInfo,
            ref CHCNetSDK.NET_DVR_ALARMER pAlarmer)
        {
            if (service == null || pAlarmInfo == IntPtr.Zero)
            {
                return false;
            }

            CHCNetSDK.NET_DVR_ACS_ALARM_INFO alarmInfo =
                (CHCNetSDK.NET_DVR_ACS_ALARM_INFO)Marshal.PtrToStructure(
                    pAlarmInfo,
                    typeof(CHCNetSDK.NET_DVR_ACS_ALARM_INFO));

            string resolvedDeviceIP = deviceIP ?? string.Empty;
            if (pAlarmer.sDeviceIP != null && pAlarmer.sDeviceIP.Length > 0)
            {
                string alarmDeviceIP = System.Text.Encoding.UTF8.GetString(pAlarmer.sDeviceIP).TrimEnd('\0');
                if (!string.IsNullOrEmpty(alarmDeviceIP))
                {
                    resolvedDeviceIP = alarmDeviceIP;
                }
            }

            if (alarmInfo.dwMinor != 75 || alarmInfo.dwMajor != CHCNetSDK.MAJOR_EVENT)
            {
                return false;
            }

            string employeeNo = alarmInfo.struAcsEventInfo.dwEmployeeNo.ToString();
            if (alarmInfo.byAcsEventInfoExtend == 1 && alarmInfo.pAcsEventInfoExtend != IntPtr.Zero)
            {
                CHCNetSDK.NET_DVR_ACS_EVENT_INFO_EXTEND extendInfo =
                    (CHCNetSDK.NET_DVR_ACS_EVENT_INFO_EXTEND)Marshal.PtrToStructure(
                        alarmInfo.pAcsEventInfoExtend,
                        typeof(CHCNetSDK.NET_DVR_ACS_EVENT_INFO_EXTEND));
                string extendEmployeeNo = System.Text.Encoding.UTF8.GetString(extendInfo.byEmployeeNo).TrimEnd('\0');
                if (!string.IsNullOrEmpty(extendEmployeeNo))
                {
                    employeeNo = extendEmployeeNo;
                }
            }

            byte[] picData = null;
            if (alarmInfo.dwPicDataLen > 0 && alarmInfo.pPicData != IntPtr.Zero)
            {
                picData = new byte[alarmInfo.dwPicDataLen];
                Marshal.Copy(alarmInfo.pPicData, picData, 0, (int)alarmInfo.dwPicDataLen);
            }

            var pending = new PendingAcsAlarm
            {
                Service = service,
                DeviceIP = resolvedDeviceIP,
                DwMajor = alarmInfo.dwMajor,
                DwMinor = alarmInfo.dwMinor,
                EventTime = alarmInfo.struTime,
                CardNo = System.Text.Encoding.UTF8.GetString(alarmInfo.struAcsEventInfo.byCardNo).TrimEnd('\0'),
                EmployeeNo = employeeNo,
                SNetUser = System.Text.Encoding.UTF8.GetString(alarmInfo.sNetUser).Trim('\0'),
                DoorNo = alarmInfo.struAcsEventInfo.dwDoorNo,
                CardType = alarmInfo.struAcsEventInfo.byCardType,
                PicData = picData
            };

            Queue.Enqueue(pending);
            return true;
        }

        private static void WorkerLoop()
        {
            while (_running)
            {
                PendingAcsAlarm pending;
                if (!Queue.TryDequeue(out pending))
                {
                    Thread.Sleep(10);
                    continue;
                }

                try
                {
                    if (pending.Service != null)
                    {
                        pending.Service.ProcessPendingAlarm(pending);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine("异步处理门禁事件失败: " + ex.Message);
                }
            }
        }
    }
}
