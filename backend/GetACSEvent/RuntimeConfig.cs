using System;
using System.IO;

namespace GetACSEvent
{
    public class RuntimeConfig
    {
        public int WebPort { get; set; }
        public int LimitCount { get; set; }
        public int StayWarningMinutes { get; set; }
        public int RecentRecordCount { get; set; }
        public int ExitGraceSeconds { get; set; }
        public double CapacityWarningRatio { get; set; }

        public bool MqttEnabled { get; set; }
        public string MqttHost { get; set; }
        public int MqttPort { get; set; }
        public string MqttTopic { get; set; }
        public string MqttClientId { get; set; }
        public string MqttUsername { get; set; }
        public string MqttPassword { get; set; }
        public int AlarmScanSeconds { get; set; }

        public bool PersonInfoMqttEnabled { get; set; }
        public string PersonInfoMqttHost { get; set; }
        public int PersonInfoMqttPort { get; set; }
        public string PersonInfoMqttTopic { get; set; }
        public string PersonInMqttTopic { get; set; }
        public string PersonOutMqttTopic { get; set; }
        public string PersonInfoMqttClientId { get; set; }

        public bool AreaAlertMqttEnabled { get; set; }
        public string AreaAlertMqttHost { get; set; }
        public int AreaAlertMqttPort { get; set; }
        public string AreaAlertMqttTopic { get; set; }
        public string AreaAlertMqttClientId { get; set; }

        public bool AbnormalMqttEnabled { get; set; }
        public string AbnormalMqttHost { get; set; }
        public int AbnormalMqttPort { get; set; }
        public string AbnormalMqttTopic { get; set; }
        public string AbnormalMqttClientId { get; set; }

        public bool PeopleCountMqttEnabled { get; set; }
        public string PeopleCountMqttHost { get; set; }
        public int PeopleCountMqttPort { get; set; }
        public string PeopleCountMqttTopic { get; set; }
        public string PeopleCountMqttClientId { get; set; }

        public RuntimeConfig()
        {
            ApplySettings(this, DeviceConfigGlobalSettings.CreateDefault());
        }

        public static RuntimeConfig LoadDefault()
        {
            return Load(ConfigPaths.DeviceConfigPath);
        }

        public static RuntimeConfig Load(string configPath)
        {
            var config = new RuntimeConfig();
            try
            {
                var document = DeviceConfigStore.Load(configPath);
                if (document?.Config != null)
                {
                    ApplySettings(config, document.Config);
                }
            }
            catch
            {
            }

            Normalize(config);
            return config;
        }

        private static void ApplySettings(RuntimeConfig config, DeviceConfigGlobalSettings source)
        {
            config.WebPort = source.WebPort;
            config.LimitCount = source.LimitCount;
            config.StayWarningMinutes = source.StayWarningMinutes;
            config.RecentRecordCount = source.RecentRecordCount;
            config.ExitGraceSeconds = source.ExitGraceSeconds;
            config.CapacityWarningRatio = source.CapacityWarningRatio;
            config.MqttEnabled = source.MqttEnabled;
            config.MqttHost = source.MqttHost ?? string.Empty;
            config.MqttPort = source.MqttPort;
            config.MqttTopic = source.MqttTopic ?? string.Empty;
            config.MqttClientId = source.MqttClientId ?? string.Empty;
            config.MqttUsername = source.MqttUsername ?? string.Empty;
            config.MqttPassword = source.MqttPassword ?? string.Empty;
            config.AlarmScanSeconds = source.AlarmScanSeconds;
            config.PersonInfoMqttEnabled = source.PersonInfoMqttEnabled;
            config.PersonInfoMqttHost = source.PersonInfoMqttHost ?? string.Empty;
            config.PersonInfoMqttPort = source.PersonInfoMqttPort;
            config.PersonInfoMqttTopic = source.PersonInfoMqttTopic ?? string.Empty;
            config.PersonInMqttTopic = source.PersonInMqttTopic ?? string.Empty;
            config.PersonOutMqttTopic = source.PersonOutMqttTopic ?? string.Empty;
            config.PersonInfoMqttClientId = source.PersonInfoMqttClientId ?? string.Empty;
            config.AreaAlertMqttEnabled = source.AreaAlertMqttEnabled;
            config.AreaAlertMqttHost = source.AreaAlertMqttHost ?? string.Empty;
            config.AreaAlertMqttPort = source.AreaAlertMqttPort;
            config.AreaAlertMqttTopic = source.AreaAlertMqttTopic ?? string.Empty;
            config.AreaAlertMqttClientId = source.AreaAlertMqttClientId ?? string.Empty;
            config.AbnormalMqttEnabled = source.AbnormalMqttEnabled;
            config.AbnormalMqttHost = source.AbnormalMqttHost ?? string.Empty;
            config.AbnormalMqttPort = source.AbnormalMqttPort;
            config.AbnormalMqttTopic = source.AbnormalMqttTopic ?? string.Empty;
            config.AbnormalMqttClientId = source.AbnormalMqttClientId ?? string.Empty;
            config.PeopleCountMqttEnabled = source.PeopleCountMqttEnabled;
            config.PeopleCountMqttHost = source.PeopleCountMqttHost ?? string.Empty;
            config.PeopleCountMqttPort = source.PeopleCountMqttPort;
            config.PeopleCountMqttTopic = source.PeopleCountMqttTopic ?? string.Empty;
            config.PeopleCountMqttClientId = source.PeopleCountMqttClientId ?? string.Empty;
        }

        private static void Normalize(RuntimeConfig config)
        {
            if (config.WebPort <= 0 || config.WebPort > 65535)
            {
                config.WebPort = 8081;
            }
            if (config.LimitCount <= 0)
            {
                config.LimitCount = 500;
            }
            if (config.StayWarningMinutes <= 0)
            {
                config.StayWarningMinutes = 30;
            }
            if (config.RecentRecordCount <= 0)
            {
                config.RecentRecordCount = 10;
            }
            if (config.ExitGraceSeconds <= 0)
            {
                config.ExitGraceSeconds = 8;
            }
            if (config.CapacityWarningRatio <= 0 || config.CapacityWarningRatio >= 1)
            {
                config.CapacityWarningRatio = 0.9d;
            }

            if (config.MqttPort <= 0 || config.MqttPort > 65535)
            {
                config.MqttPort = 1883;
            }
            if (string.IsNullOrEmpty(config.MqttHost))
            {
                config.MqttEnabled = false;
            }
            if (string.IsNullOrEmpty(config.MqttTopic))
            {
                config.MqttTopic = "acs/alarm/event";
            }
            if (string.IsNullOrEmpty(config.MqttClientId))
            {
                config.MqttClientId = "acs-event-standalone";
            }
            if (config.AlarmScanSeconds <= 0)
            {
                config.AlarmScanSeconds = 5;
            }

            if (config.PersonInfoMqttPort <= 0 || config.PersonInfoMqttPort > 65535)
            {
                config.PersonInfoMqttPort = 1883;
            }
            if (string.IsNullOrEmpty(config.PersonInfoMqttHost))
            {
                config.PersonInfoMqttEnabled = false;
            }
            if (string.IsNullOrEmpty(config.PersonInfoMqttTopic))
            {
                config.PersonInfoMqttTopic = "personinfo";
            }
            if (string.IsNullOrEmpty(config.PersonInMqttTopic))
            {
                config.PersonInMqttTopic = "person_in";
            }
            if (string.IsNullOrEmpty(config.PersonOutMqttTopic))
            {
                config.PersonOutMqttTopic = "person_out";
            }
            if (string.IsNullOrEmpty(config.PersonInfoMqttClientId))
            {
                config.PersonInfoMqttClientId = "acs-personinfo-publisher";
            }

            if (config.AreaAlertMqttPort <= 0 || config.AreaAlertMqttPort > 65535)
            {
                config.AreaAlertMqttPort = 1883;
            }
            if (string.IsNullOrEmpty(config.AreaAlertMqttHost))
            {
                config.AreaAlertMqttEnabled = false;
            }
            if (string.IsNullOrEmpty(config.AreaAlertMqttTopic))
            {
                config.AreaAlertMqttTopic = "area_alert";
            }
            if (string.IsNullOrEmpty(config.AreaAlertMqttClientId))
            {
                config.AreaAlertMqttClientId = "acs-area-alert-subscriber";
            }

            if (config.AbnormalMqttPort <= 0 || config.AbnormalMqttPort > 65535)
            {
                config.AbnormalMqttPort = 1883;
            }
            if (string.IsNullOrEmpty(config.AbnormalMqttHost))
            {
                config.AbnormalMqttEnabled = false;
            }
            if (string.IsNullOrEmpty(config.AbnormalMqttTopic))
            {
                config.AbnormalMqttTopic = "abnormal";
            }
            if (string.IsNullOrEmpty(config.AbnormalMqttClientId))
            {
                config.AbnormalMqttClientId = "acs-abnormal-subscriber";
            }

            if (config.PeopleCountMqttPort <= 0 || config.PeopleCountMqttPort > 65535)
            {
                config.PeopleCountMqttPort = 1883;
            }
            if (string.IsNullOrEmpty(config.PeopleCountMqttHost))
            {
                config.PeopleCountMqttEnabled = false;
            }
            if (string.IsNullOrEmpty(config.PeopleCountMqttTopic))
            {
                config.PeopleCountMqttTopic = "people_count";
            }
            if (string.IsNullOrEmpty(config.PeopleCountMqttClientId))
            {
                config.PeopleCountMqttClientId = "acs-people-count-subscriber";
            }
        }
    }
}
