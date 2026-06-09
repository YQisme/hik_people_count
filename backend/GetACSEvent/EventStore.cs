using System;
using System.Collections.Generic;

namespace GetACSEvent
{
    public class AcsEvent
    {
        public DateTime TimeUtc { get; set; }
        public string Time { get; set; }  // 原始时间字符串，与控制台输出保持一致
        public string DeviceIP { get; set; }
        public string DeviceName { get; set; }
        public string DeviceID { get; set; }
        public string AreaID { get; set; }
        public string Remark { get; set; }
        public string MajorType { get; set; }
        public string MinorType { get; set; }
        public string CardNo { get; set; }
        public string EmployeeNo { get; set; }
        public string PersonName { get; set; }
        public string CardType { get; set; }
        public uint DoorNo { get; set; }
        public string Direction { get; set; }
        public string ImageUrl { get; set; }
    }

    public class EventStore
    {
        private readonly object _sync = new object();
        private readonly List<AcsEvent> _events = new List<AcsEvent>();
        private readonly int _capacity;
        private long _revision;

        public event Action Changed;

        public EventStore(int capacity = 1000)
        {
            _capacity = capacity > 0 ? capacity : 1000;
        }

        public long Revision
        {
            get
            {
                lock (_sync)
                {
                    return _revision;
                }
            }
        }

        public void Add(AcsEvent e)
        {
            if (e == null)
            {
                return;
            }

            lock (_sync)
            {
                _events.Add(e);
                if (_events.Count > _capacity)
                {
                    int remove = _events.Count - _capacity;
                    _events.RemoveRange(0, remove);
                }

                _revision++;
            }

            NotifyChanged();
        }

        public bool TryUpdateImageUrl(string deviceIP, string employeeNo, string cardNo, string imageUrl)
        {
            if (string.IsNullOrEmpty(imageUrl))
            {
                return false;
            }

            bool updated = false;
            lock (_sync)
            {
                for (int i = _events.Count - 1; i >= 0; i--)
                {
                    AcsEvent ev = _events[i];
                    if (ev == null)
                    {
                        continue;
                    }

                    if (ev.DeviceIP == deviceIP &&
                        ev.EmployeeNo == employeeNo &&
                        ev.CardNo == cardNo &&
                        string.IsNullOrEmpty(ev.ImageUrl))
                    {
                        ev.ImageUrl = imageUrl;
                        updated = true;
                        _revision++;
                        break;
                    }
                }
            }

            if (updated)
            {
                NotifyChanged();
            }

            return updated;
        }

        public List<AcsEvent> Snapshot()
        {
            lock (_sync)
            {
                return new List<AcsEvent>(_events);
            }
        }

        private void NotifyChanged()
        {
            Action handler = Changed;
            if (handler != null)
            {
                try
                {
                    handler();
                }
                catch
                {
                }
            }
        }
    }
}
