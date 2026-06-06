using System;
using System.Collections.Generic;
using System.Text;

namespace GetACSEvent
{
    public static class EmployeeDirectory
    {
        private static readonly object SyncRoot = new object();
        private static readonly List<Dictionary<string, string>> Employees = new List<Dictionary<string, string>>();

        private static readonly string[] EmployeeIdKeys = new string[]
        {
            "employeeId", "employeeNo", "jobNo", "workNo", "staffNo", "personId", "id", "cardNo"
        };

        private static readonly string[] EmployeeNameKeys = new string[]
        {
            "name", "employeeName", "personName", "realName", "staffName"
        };

        public static void LoadFromJson(string json)
        {
            var parsed = ParseArrayOfObjects(json);
            lock (SyncRoot)
            {
                Employees.Clear();
                foreach (var employee in parsed)
                {
                    Employees.Add(Clone(employee));
                }
            }
        }

        public static Dictionary<string, string> GetNameMapSnapshot()
        {
            var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            lock (SyncRoot)
            {
                foreach (var employee in Employees)
                {
                    string name = FirstValue(employee, EmployeeNameKeys);
                    if (string.IsNullOrEmpty(name))
                    {
                        continue;
                    }

                    foreach (var key in EmployeeIdKeys)
                    {
                        string value;
                        if (employee.TryGetValue(key, out value))
                        {
                            AddCandidate(map, value, name);
                        }
                    }
                }
            }
            return map;
        }

        public static List<Dictionary<string, string>> Snapshot()
        {
            var result = new List<Dictionary<string, string>>();
            lock (SyncRoot)
            {
                foreach (var employee in Employees)
                {
                    result.Add(Clone(employee));
                }
            }
            return result;
        }

        public static Dictionary<string, string> FindBestMatch(string employeeNo, string cardNo, string personName)
        {
            lock (SyncRoot)
            {
                Dictionary<string, string> employee = FindByCandidates(employeeNo, EmployeeIdKeys);
                if (employee != null)
                {
                    return Clone(employee);
                }

                employee = FindByCandidates(cardNo, EmployeeIdKeys);
                if (employee != null)
                {
                    return Clone(employee);
                }

                if (!string.IsNullOrEmpty(personName))
                {
                    string expectedName = personName.Trim();
                    foreach (var item in Employees)
                    {
                        string name = FirstValue(item, EmployeeNameKeys);
                        if (!string.IsNullOrEmpty(name) &&
                            string.Equals(name.Trim(), expectedName, StringComparison.OrdinalIgnoreCase))
                        {
                            return Clone(item);
                        }
                    }
                }

                return null;
            }
        }

        private static Dictionary<string, string> FindByCandidates(string rawValue, string[] keys)
        {
            var candidates = BuildCandidates(rawValue);
            if (candidates.Count == 0)
            {
                return null;
            }

            foreach (var employee in Employees)
            {
                foreach (var key in keys)
                {
                    string value;
                    if (!employee.TryGetValue(key, out value))
                    {
                        continue;
                    }

                    foreach (var candidate in candidates)
                    {
                        if (string.Equals((value ?? string.Empty).Trim(), candidate, StringComparison.OrdinalIgnoreCase))
                        {
                            return employee;
                        }
                    }
                }
            }

            return null;
        }

        private static void AddCandidate(Dictionary<string, string> map, string rawValue, string name)
        {
            foreach (var candidate in BuildCandidates(rawValue))
            {
                if (!map.ContainsKey(candidate))
                {
                    map[candidate] = name.Trim();
                }
            }
        }

        private static List<string> BuildCandidates(string rawValue)
        {
            var result = new List<string>();
            if (string.IsNullOrEmpty(rawValue))
            {
                return result;
            }

            string value = rawValue.Trim();
            if (value.Length == 0)
            {
                return result;
            }

            AddIfMissing(result, value);
            AddIfMissing(result, value.TrimStart('0'));
            AddIfMissing(result, value.ToUpperInvariant());
            AddIfMissing(result, value.ToLowerInvariant());

            return result;
        }

        private static void AddIfMissing(List<string> list, string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return;
            }

            foreach (var existing in list)
            {
                if (string.Equals(existing, value, StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }
            }

            list.Add(value);
        }

        private static string FirstValue(Dictionary<string, string> source, params string[] keys)
        {
            if (source == null)
            {
                return string.Empty;
            }

            foreach (var key in keys)
            {
                string value;
                if (source.TryGetValue(key, out value) && !string.IsNullOrEmpty((value ?? string.Empty).Trim()))
                {
                    return value.Trim();
                }
            }

            return string.Empty;
        }

        private static Dictionary<string, string> Clone(Dictionary<string, string> source)
        {
            var copy = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (source == null)
            {
                return copy;
            }

            foreach (var pair in source)
            {
                copy[pair.Key] = pair.Value;
            }
            return copy;
        }

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

        private static void SkipWs(string s, ref int i)
        {
            while (i < s.Length && char.IsWhiteSpace(s[i])) i++;
        }

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
    }
}
