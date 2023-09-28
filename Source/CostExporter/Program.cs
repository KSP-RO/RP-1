using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace CostExporter
{
    class Program
    {
        class PBData
        {
            public string name { get; set; }
            public string title { get; set; }
            public string description { get; set; }
            public string mod { get; set; }
            [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
            public int cost { get; set; }
            [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
            public int entry_cost { get; set; }
            public string category { get; set; }
            public string info { get; set; }
            public string year { get; set; }
            public string technology { get; set; }
            public string era { get; set; }
            public bool ro { get; set; }
            public bool rp0 { get; set; }
            public bool orphan { get; set; }
            public bool rp0_conf { get; set; }
            public string spacecraft { get; set; }
            public string engine_config { get; set; }
            public bool upgrade { get; set; }
            public string entry_cost_mods { get; set; }
            public string identical_part_name { get; set; }
            public List<string> module_tags { get; set; }

            public void Fix()
            {
                if (era == null)
                    era = string.Empty;
            }
        }

        public class CostData
        {
            public string name { get; set; }
            public string config { get; set; }
            public int cost { get; set; }
        }

        private static string FixJSON(string json)
        {
            var ret = json.Replace("\r\n    \"rp0\": false,", string.Empty);
            ret = ret.Replace("\r\n  ", "\r\nxxxx").Replace("\r\nxxxx  ", "\r\nxxxxxxxx").Replace("\r\nxxxxxxxx  ", "\r\n            ");
            ret = ret.Replace("\r\nxxxxxxxx", "\r\n        ").Replace("\r\nxxxx", "\r\n    ");
            return ret;
        }

        static void Main(string[] args)
        {
            var dir = new DirectoryInfo(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "data"));
            List<PBData> parts = new List<PBData>();
            List<PBData> configs = new List<PBData>();
            foreach (var fi in dir.GetFiles())
            {
                var dest = fi.Name == "Engine_Config.json" ? configs : parts;
                string fileData = File.ReadAllText(fi.FullName);
                var data = JsonSerializer.Deserialize<List<PBData>>(fileData);
                dest.AddRange(data);
            }
            List<CostData> newParts = JsonSerializer.Deserialize<List<CostData>>(File.ReadAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "costdataParts.json")));
            Dictionary<string, int> newConfigs = JsonSerializer.Deserialize<Dictionary<string, int>>(File.ReadAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "costdataConfigs.json")));
            List<string> missedParts = new List<string>();
            List<string> missedConfigs = new List<string>();
            string output = "Configs changed:";
            foreach (var kvp in newConfigs)
            {
                string item = kvp.Key;
                int cost = kvp.Value;

                bool missing = true;
                foreach (var sub in configs)
                {
                    if (string.Equals(sub.name, item, StringComparison.InvariantCultureIgnoreCase))
                    {
                        missing = false;
                        if (sub.cost != cost)
                        {
                            output += "\n" + item + " : was " + sub.cost + ", now " + cost;
                            sub.cost = cost;
                        }
                    }
                }
                if (missing)
                    missedConfigs.Add(item);
            }

            foreach (var c in newParts)
            {
                bool missing = true;
                foreach (var sub in parts)
                {
                    if (string.Equals(sub.engine_config, c.name, StringComparison.InvariantCultureIgnoreCase)
                        || string.Equals(sub.title, c.name, StringComparison.InvariantCultureIgnoreCase)
                        || string.Equals(sub.entry_cost_mods, c.name, StringComparison.InvariantCultureIgnoreCase)
                        || string.Equals(sub.engine_config, c.config, StringComparison.InvariantCultureIgnoreCase)
                        || string.Equals(sub.title, c.config, StringComparison.InvariantCultureIgnoreCase)
                        || string.Equals(sub.entry_cost_mods, c.config, StringComparison.InvariantCultureIgnoreCase))
                    {

                        missing = false;
                        break;
                    }
                }
                if (missing)
                    missedParts.Add(c.config + " : " + c.name);
            }

            foreach (var pb in configs)
                pb.Fix();
            var opts = new JsonSerializerOptions(JsonSerializerDefaults.General);
            opts.WriteIndented = true;
            var files = dir.GetFiles();
            output += "\n\nParts changed:";
            foreach (var fi in files)
            {
                bool isConfigs = fi.Name == "Engine_Config.json";
                if (isConfigs)
                {
                    string cfgText = JsonSerializer.Serialize(configs, opts);
                    cfgText = FixJSON(cfgText);
                    File.WriteAllText(fi.FullName, cfgText);
                    continue;
                }

                var data = JsonSerializer.Deserialize<List<PBData>>(File.ReadAllText(fi.FullName));
                foreach (var pb in data)
                    pb.Fix();
                foreach (var c in newParts)
                {
                    foreach (var sub in data)
                    {
                        if (string.Equals(sub.engine_config, c.name, StringComparison.InvariantCultureIgnoreCase)
                            || string.Equals(sub.title, c.name, StringComparison.InvariantCultureIgnoreCase)
                            || string.Equals(sub.entry_cost_mods, c.name, StringComparison.InvariantCultureIgnoreCase)
                            || string.Equals(sub.engine_config, c.config, StringComparison.InvariantCultureIgnoreCase)
                            || string.Equals(sub.title, c.config, StringComparison.InvariantCultureIgnoreCase)
                            || string.Equals(sub.entry_cost_mods, c.config, StringComparison.InvariantCultureIgnoreCase))
                        {
                            if (sub.cost != c.cost)
                            {
                                output += "\n" + sub.name + "(" + sub.title + ") : was " + sub.cost + ", now " + c.cost;
                                sub.cost = c.cost;
                            }
                        }
                    }
                }
                string text = JsonSerializer.Serialize(data, opts);
                text = FixJSON(text);
                File.WriteAllText(fi.FullName, text);
            }

            string logStr = "\nMissing parts:";
            foreach (var s in missedParts)
                logStr += "\n" + s;
            logStr += "\n\nMissing Configs:";
            foreach (var s in missedConfigs)
                logStr += "\n" + s;

            Console.WriteLine(logStr + "\n\nChanges:\n" + output);
        }
    }
}
