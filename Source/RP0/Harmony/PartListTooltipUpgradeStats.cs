using HarmonyLib;
using KSP.UI.Screens.Editor;
using RealFuels;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;

namespace RP0.Harmony
{
    [HarmonyPatch(typeof(PartListTooltip))]
    internal class PatchPartListTooltipUpgradeStats
    {
        private const string UpgradePrefix = "RFUpgrade_";

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
                    if (!(module is ModuleEngineConfigsBase mecb) || mecb.configs == null) continue;

                    ConfigNode baseline = null;
                    foreach (ConfigNode c in mecb.configs)
                    {
                        if (c.GetValue("name") == mecb.configuration) { baseline = c; break; }
                    }
                    if (baseline == null && mecb.configs.Count > 0) baseline = mecb.configs[0];

                    foreach (ConfigNode c in mecb.configs)
                    {
                        string n = c.GetValue("name");
                        if (string.IsNullOrEmpty(n) || _cache.ContainsKey(n)) continue;
                        _cache[n] = new ConfigPair { Config = c, Baseline = baseline };
                    }
                }
            }
        }

        private static string BuildStatsBlock(ConfigNode cfg, ConfigNode baseline)
        {
            double maxT = ParseDouble(cfg.GetValue("maxThrust"));
            double minT = ParseDouble(cfg.GetValue("minThrust"));
            int ignitions = ParseInt(cfg.GetValue("ignitions"), int.MinValue);
            (double ispVac, double ispSL) = ReadIsp(cfg);

            double bMaxT = baseline != null ? ParseDouble(baseline.GetValue("maxThrust")) : double.NaN;
            (double bIspVac, double bIspSL) = baseline != null ? ReadIsp(baseline) : (double.NaN, double.NaN);

            bool sameAsBaseline = baseline != null && baseline.GetValue("name") == cfg.GetValue("name");

            var sb = new StringBuilder();
            sb.Append("<b>Engine stats");
            if (baseline != null && !sameAsBaseline)
                sb.Append(" (vs ").Append(baseline.GetValue("name")).Append(")");
            sb.Append(":</b>");

            if (!double.IsNaN(ispVac))
            {
                sb.Append("\n  Isp vac: ").Append(F(ispVac, "0.#")).Append(" s");
                AppendDelta(sb, ispVac, bIspVac, "0.#", " s");
            }
            if (!double.IsNaN(ispSL))
            {
                sb.Append("\n  Isp SL:  ").Append(F(ispSL, "0.#")).Append(" s");
                AppendDelta(sb, ispSL, bIspSL, "0.#", " s");
            }
            if (!double.IsNaN(maxT))
            {
                sb.Append("\n  Thrust:  ").Append(F(maxT, "0.##")).Append(" kN");
                AppendDelta(sb, maxT, bMaxT, "0.##", " kN");
                if (!double.IsNaN(minT) && minT > 0 && minT < maxT)
                    sb.Append("  (throttles to ").Append(F(minT / maxT * 100.0, "0")).Append("%)");
            }
            if (ignitions != int.MinValue)
                sb.Append("\n  Ignitions: ").Append(ignitions == 0 ? "Unlimited" : ignitions.ToString(CultureInfo.InvariantCulture));

            if (cfg.HasValue("ullage"))
                sb.Append("\n  Ullage required: ").Append(ParseBool(cfg.GetValue("ullage")) ? "Yes" : "No");
            if (cfg.HasValue("pressureFed"))
                sb.Append("\n  Pressure-fed: ").Append(ParseBool(cfg.GetValue("pressureFed")) ? "Yes" : "No");

            string fuelPair = ReadPropellants(cfg);
            if (!string.IsNullOrEmpty(fuelPair))
                sb.Append("\n  Propellant: ").Append(fuelPair);

            return sb.ToString();
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
            if (minAtm == maxAtm) sl = double.NaN;
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

        private static void AppendDelta(StringBuilder sb, double v, double baseline, string fmt, string unit)
        {
            if (double.IsNaN(baseline) || baseline == v) return;
            double d = v - baseline;
            sb.Append(" (").Append(d > 0 ? "+" : "").Append(F(d, fmt)).Append(unit).Append(")");
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
