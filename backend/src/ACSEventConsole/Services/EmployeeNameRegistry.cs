using System.Text;

namespace ACSEventConsole.Services;

public static class EmployeeNameRegistry
{
    public static Dictionary<string, string> NameMap { get; } =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    public static void LoadFromFile(string configPath)
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

        EmployeeDirectory.LoadFromJson(json);
        NameMap.Clear();
        foreach (var entry in EmployeeDirectory.GetNameMapSnapshot())
        {
            NameMap[entry.Key] = entry.Value;
        }
    }

    public static bool TryGetName(string key, out string name)
    {
        return NameMap.TryGetValue(key, out name);
    }
}
