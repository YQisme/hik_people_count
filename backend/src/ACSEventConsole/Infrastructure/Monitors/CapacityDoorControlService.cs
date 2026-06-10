using System;
using System.Collections.Generic;
using System.Threading;

namespace ACSEventConsole.Infrastructure.Monitors
{
    public class CapacityDoorControlService
    {
        private readonly ACSEventMultiDeviceService _deviceService;
        private Thread _thread;
        private bool _running;
        private bool? _lastBlocked;
        private readonly object _evaluateSync = new object();
        private DateTime _lastImmediateEvaluateUtc = DateTime.MinValue;

        public CapacityDoorControlService(ACSEventMultiDeviceService deviceService)
        {
            _deviceService = deviceService;
        }

        public void Start()
        {
            if (_running || _deviceService == null)
            {
                return;
            }

            _running = true;
            EventStore store = ACSEventMultiDeviceService.SharedEventStore;
            if (store != null)
            {
                store.Changed += OnStoreChanged;
            }

            _thread = new Thread(RunLoop);
            _thread.IsBackground = true;
            _thread.Start();
        }

        public void Stop()
        {
            _running = false;
            EventStore store = ACSEventMultiDeviceService.SharedEventStore;
            if (store != null)
            {
                store.Changed -= OnStoreChanged;
            }

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

        private void OnStoreChanged()
        {
            if (!_running)
            {
                return;
            }

            lock (_evaluateSync)
            {
                if ((DateTime.UtcNow - _lastImmediateEvaluateUtc).TotalMilliseconds < 200)
                {
                    return;
                }

                _lastImmediateEvaluateUtc = DateTime.UtcNow;
            }

            EvaluateCapacity();
        }

        private void RunLoop()
        {
            while (_running)
            {
                try
                {
                    EvaluateCapacity();
                }
                catch (Exception ex)
                {
                    Console.WriteLine("限员门控服务异常: " + ex.Message);
                }

                Thread.Sleep(15000);
            }
        }

        private void EvaluateCapacity()
        {
            if (_deviceService == null)
            {
                return;
            }

            RuntimeConfig runtimeConfig = RuntimeConfig.LoadDefault();
            int limitCount = runtimeConfig == null ? 500 : runtimeConfig.LimitCount;
            int currentCount = ResolveCurrentCount();
            bool shouldBlockEntry = currentCount >= limitCount;
            bool exitGraceActive = _deviceService.IsExitGraceActive(currentCount);
            if (exitGraceActive)
            {
                shouldBlockEntry = false;
            }

            _deviceService.ApplyCapacityDoorRule(shouldBlockEntry);

            if (!_lastBlocked.HasValue || _lastBlocked.Value != shouldBlockEntry)
            {
                Console.WriteLine(
                    shouldBlockEntry
                        ? "限员控制已开启：当前人数 " + currentCount + "，限制人数 " + limitCount + "，进门禁用，出门保持可用"
                        : exitGraceActive
                            ? "检测到出门刷脸，已临时解除限员门禁限制，等待人数下降后再恢复限制"
                            : "限员控制已恢复：当前人数 " + currentCount + "，限制人数 " + limitCount + "，门禁恢复正常受控状态");
                _lastBlocked = shouldBlockEntry;
            }
        }

        private static int ResolveCurrentCount()
        {
            PeopleCountInfo peopleCount = PeopleCountState.GetSnapshot();
            if (peopleCount != null && peopleCount.HasData)
            {
                return peopleCount.TotalCount;
            }

            List<AcsEvent> events = ACSEventMultiDeviceService.SharedEventStore.Snapshot();
            return EstimateStayCountFromEvents(events);
        }

        private static int EstimateStayCountFromEvents(List<AcsEvent> events)
        {
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
                string key = GetPersonKey(ev);
                if (string.IsNullOrEmpty(key))
                {
                    continue;
                }

                latestPerPerson[key] = ev;
            }

            int count = 0;
            foreach (KeyValuePair<string, AcsEvent> pair in latestPerPerson)
            {
                if (IsEntryEvent(pair.Value))
                {
                    count++;
                }
            }

            return count;
        }

        private static string GetPersonKey(AcsEvent ev)
        {
            string value = CleanValue(ev == null ? string.Empty : ev.EmployeeNo);
            if (!string.IsNullOrEmpty(value))
            {
                return value;
            }

            value = CleanValue(ev == null ? string.Empty : ev.CardNo);
            if (!string.IsNullOrEmpty(value))
            {
                return value;
            }

            return CleanValue(ev == null ? string.Empty : ev.PersonName);
        }

        private static bool IsEntryEvent(AcsEvent ev)
        {
            string direction = NormalizeDirection(ev == null ? string.Empty : ev.Direction);
            if (direction == "进")
            {
                return true;
            }

            if (direction == "出")
            {
                return false;
            }

            return !string.IsNullOrEmpty(CleanValue(ev == null ? string.Empty : ev.EmployeeNo)) ||
                   !string.IsNullOrEmpty(CleanValue(ev == null ? string.Empty : ev.CardNo));
        }

        private static string NormalizeDirection(string direction)
        {
            string value = CleanValue(direction).ToLowerInvariant();
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

        private static string CleanValue(string value)
        {
            return (value ?? string.Empty).Trim();
        }
    }
}
