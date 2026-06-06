using System;
using System.IO;

namespace GetACSEvent
{
    public static class ConfigPaths
    {
        private static string _configDirectory;

        public static string ConfigDirectory
        {
            get
            {
                if (_configDirectory == null)
                {
                    _configDirectory = ResolveConfigDirectory();
                    Directory.CreateDirectory(_configDirectory);
                }
                return _configDirectory;
            }
        }

        public static string DeviceConfigPath => Path.Combine(ConfigDirectory, "DeviceConfig.json");

        public static string EmployeeConfigPath => Path.Combine(ConfigDirectory, "EmployeeConfig.json");

        private static string ResolveConfigDirectory()
        {
            string[] startPoints = new[]
            {
                AppDomain.CurrentDomain.BaseDirectory,
                Directory.GetCurrentDirectory()
            };

            foreach (string start in startPoints)
            {
                if (string.IsNullOrEmpty(start))
                {
                    continue;
                }

                string dir = Path.GetFullPath(start);
                for (int i = 0; i < 10; i++)
                {
                    if (string.Equals(Path.GetFileName(dir), "backend", StringComparison.OrdinalIgnoreCase))
                    {
                        return Path.GetFullPath(Path.Combine(dir, "config"));
                    }

                    string nested = Path.Combine(dir, "backend", "config");
                    if (Directory.Exists(nested))
                    {
                        return Path.GetFullPath(nested);
                    }

                    DirectoryInfo parent = Directory.GetParent(dir);
                    if (parent == null)
                    {
                        break;
                    }

                    dir = parent.FullName;
                }
            }

            return Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", "config"));
        }
    }
}
