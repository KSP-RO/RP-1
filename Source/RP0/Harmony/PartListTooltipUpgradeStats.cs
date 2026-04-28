using HarmonyLib;
using KSP.UI.Screens.Editor;
using RealFuels;
using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace RP0.Harmony
{
    [HarmonyPatch(typeof(PartListTooltip))]
    internal class PatchPartListTooltipUpgradeStats
    {
        private const string UpgradePrefix = "RFUpgrade_";
        private const double FloatEpsilon = 1e-9;

        private struct ConfigPair { public ConfigNode Config; public ConfigNode Baseline; }
        private static Dictionary<string, ConfigPair> _cache;

        [HarmonyPostfix]
        [HarmonyPatch("Setup")]
        [HarmonyPatch(new Type[] { typeof(AvailablePart), typeof(PartUpgradeHandler.Upgrade), typeof(Callback<PartListTooltip>), typeof(RenderTexture) })]
        internal static void Postfix_Setup(PartListTooltip __instance, PartUpgradeHandler.Upgrade up)
        {
            if (up == null || string.IsNullOrEmpty(up.name) || !up.name.StartsWith(UpgradePrefix, StringComparison.Ordinal))
                return;
            if (__instance.textInfoBasic == null)
                return;

            string configName = up.name.Substring(UpgradePrefix.Length);
            ConfigPair pair;
            try
            {
                if (_cache == null) BuildCache();
                if (!_cache.TryGetValue(configName, out pair) || pair.Config == null) return;
            }
            catch (Exception e)
            {
                RP0Debug.LogError("Engine upgrade stats lookup failed for " + configName + ": " + e);
                return;
            }

            string statsBlock = BuildStatsBlock(pair.Config, pair.Baseline);
            if (string.IsNullOrEmpty(statsBlock)) return;

            string existing = __instance.textInfoBasic.text;
            __instance.textInfoBasic.text = string.IsNullOrEmpty(existing) ? statsBlock : existing + "\n" + statsBlock;
        }

        private static void BuildCache()
        {
            _cache = new Dictionary<string, ConfigPair>(StringComparer.Ordinal);
            if (PartLoader.LoadedPartsList == null) return;

            foreach (AvailablePart ap in PartLoader.LoadedPartsList)
            {
                Part prefab = ap?.partPrefab;
                if (prefab == null) continue;
                foreach (PartModule module in prefab.Modules)
                {
                    if (module is ModuleEngineConfigsBase mecb && mecb.configs != null)
                        IndexConfigs(mecb);
                }
            }
        }

        private static void IndexConfigs(ModuleEngineConfigsBase mecb)
        {
            ConfigNode baseline = FindBaseline(mecb);
            foreach (ConfigNode c in mecb.configs)
            {
                string n = c.GetValue("name");
                if (string.IsNullOrEmpty(n) || _cache.ContainsKey(n)) continue;
                _cache[n] = new ConfigPair { Config = c, Baseline = baseline };
            }
        }

        private static ConfigNode FindBaseline(ModuleEngineConfigsBase mecb)
        {
            foreach (ConfigNode c in mecb.configs)
            {
                if (c.GetValue("name") == mecb.configuration) return c;
            }
            return mecb.configs.Count > 0 ? mecb.configs[0] : null;
        }

        private static string BuildStatsBlock(ConfigNode cfg, ConfigNode baseline)
        {
            double maxT = ParseDouble(cfg.GetValue("maxThrust"));
            double minT = ParseDouble(cfg.GetValue("minThrust"));
            int ignitions = ParseInt(cfg.GetValue("ignitions"), int.MinValue);
            (double ispVac, double ispSL) = ReadIsp(cfg);

            bool hasBase = baseline != null;
            double bMaxT = hasBase ? ParseDouble(baseline.GetValue("maxThrust")) : double.NaN;
            (double bIspVac, double bIspSL) = hasBase ? ReadIsp(baseline) : (double.NaN, double.NaN);
            bool sameAsBaseline = hasBase && baseline.GetValue("name") == cfg.GetValue("name");

            var lines = new List<string>
            {
                BuildHeader(baseline, sameAsBaseline),
                BuildIspLine("Isp vac", ispVac, bIspVac),
                BuildIspLine("Isp SL", ispSL, bIspSL),
                BuildThrustLine(maxT, minT, bMaxT),
                BuildIgnitionsLine(ignitions),
                BuildBoolFlagLine(cfg, "ullage", "Ullage required"),
                BuildBoolFlagLine(cfg, "pressureFed", "Pressure-fed"),
                BuildPropellantsLine(cfg),
            };
            lines.RemoveAll(string.IsNullOrEmpty);
            return string.Join("\n", lines.ToArray());
        }

        private static string BuildHeader(ConfigNode baseline, bool sameAsBaseline)
        {
            string vsClause = (baseline != null && !sameAsBaseline) ? " (vs " + baseline.GetValue("name") + ")" : "";
            return "<b>Engine stats" + vsClause + ":</b>";
        }

        private static string BuildIspLine(string label, double v, double baseline)
        {
            if (double.IsNaN(v)) return null;
            return label + ": " + F(v, "0.#") + " s" + DeltaSuffix(v, baseline, "0.#", " s");
        }

        private static string BuildThrustLine(double maxT, double minT, double bMaxT)
        {
            if (double.IsNaN(maxT)) return null;
            string throttle = (!double.IsNaN(minT) && minT > 0 && minT < maxT)
                ? "  (throttles to " + F(minT / maxT * 100.0, "0") + "%)"
                : "";
            return "Thrust: " + F(maxT, "0.##") + " kN" + DeltaSuffix(maxT, bMaxT, "0.##", " kN") + throttle;
        }

        private static string BuildIgnitionsLine(int ignitions)
        {
            if (ignitions == int.MinValue) return null;
            return "Ignitions: " + (ignitions == 0 ? "Unlimited" : ignitions.ToString(CultureInfo.InvariantCulture));
        }

        private static string BuildBoolFlagLine(ConfigNode cfg, string key, string label)
        {
            if (!cfg.HasValue(key)) return null;
            return label + ": " + (ParseBool(cfg.GetValue(key)) ? "Yes" : "No");
        }

        private static string BuildPropellantsLine(ConfigNode cfg)
        {
            string fuelPair = ReadPropellants(cfg);
            return string.IsNullOrEmpty(fuelPair) ? null : "Propellant: " + fuelPair;
        }

        private static string DeltaSuffix(double v, double baseline, string fmt, string unit)
        {
            if (double.IsNaN(baseline)) return "";
            double d = v - baseline;
            if (Math.Abs(d) < FloatEpsilon) return "";
            return " (" + (d > 0 ? "+" : "") + F(d, fmt) + unit + ")";
        }

        private static (double vac, double sl) ReadIsp(ConfigNode cfg)
        {
            double vac = double.NaN, sl = double.NaN;
            double minAtm = double.PositiveInfinity, maxAtm = double.NegativeInfinity;
            ConfigNode curve = cfg.GetNode("atmosphereCurve");
            if (curve == null) return (vac, sl);
            foreach (string k in curve.GetValues("key"))
            {
                string[] parts = k.Trim().Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length < 2) continue;
                if (!double.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out double atm)) continue;
                if (!double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out double isp)) continue;
                if (atm < minAtm) { minAtm = atm; vac = isp; }
                if (atm > maxAtm) { maxAtm = atm; sl = isp; }
            }
            if (Math.Abs(maxAtm - minAtm) < FloatEpsilon) sl = double.NaN;
            return (vac, sl);
        }

        private static string ReadPropellants(ConfigNode cfg)
        {
            ConfigNode[] nodes = cfg.GetNodes("PROPELLANT");
            if (nodes == null || nodes.Length == 0) return null;
            var fuels = new List<string>();
            foreach (ConfigNode p in nodes)
            {
                if (ParseBool(p.GetValue("ignoreForIsp"))) continue;
                string n = p.GetValue("name");
                if (!string.IsNullOrEmpty(n)) fuels.Add(n);
            }
            return string.Join(" / ", fuels.ToArray());
        }

        private static string F(double v, string fmt) => v.ToString(fmt, CultureInfo.InvariantCulture);

        private static double ParseDouble(string s) =>
            !string.IsNullOrEmpty(s) && double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out double v) ? v : double.NaN;

        private static int ParseInt(string s, int fb) =>
            !string.IsNullOrEmpty(s) && int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out int v) ? v : fb;

        private static bool ParseBool(string s) =>
            !string.IsNullOrEmpty(s) && bool.TryParse(s, out bool b) && b;
    }
}
