using HarmonyLib;
using Strategies;
using System.Collections.Generic;
using RP0.Programs;
using UniLinq;

namespace RP0.Harmony
{
    [HarmonyPatch(typeof(StrategySystem))]
    internal class PatchStrategySystem
    {
        internal static HashSet<string> acceptablePrograms = new HashSet<string>();
        internal static HashSet<string> completedPrograms = new HashSet<string>();

        [HarmonyPrefix]
        [HarmonyPatch("GetStrategies")]
        internal static bool Prefix_GetStrategies(StrategySystem __instance, string department, out List<Strategy> __result)
        {
            var list = new List<Strategy>();

            if (department != "Programs")
            {
                // FIXME support other strategy types?
                // For now we just skip everything that isn't a StrategyRP0 (and isn't a Program in the Programs department)
                for (int i = 0; i < __instance.strategies.Count; ++i)
                {
                    StrategyRP0 stratR = __instance.strategies[i] as StrategyRP0;
                    if (stratR == null)
                        continue;

                    StrategyConfigRP0 cfg = stratR.ConfigRP0;

                    if (cfg == null || cfg.IsDisabled)
                        continue;

                    if (stratR.DepartmentName != department && (string.IsNullOrEmpty(cfg.DepartmentNameAlt) || cfg.DepartmentNameAlt != department))
                        continue;

                    if (!cfg.IsAvailable(stratR.DateDeactivated))
                        continue;

                    list.Add(stratR);
                }

                __result = list;
                return false;
            }

            // Cache what programs can be accepted (and which have been completed)
            // We don't care about confidence thresholds because you can always accept at Slow speed
            foreach (Program p in ProgramHandler.Programs)
                if (p.CanAccept && !ProgramHandler.Instance.DisabledPrograms.Contains(p.name))
                    acceptablePrograms.Add(p.name);

            foreach (Program p in ProgramHandler.Instance.CompletedPrograms)
                completedPrograms.Add(p.name);

            // Insert acceptable programs first
            for (int i = 0; i < __instance.strategies.Count; ++i)
            {
                Strategy strat = __instance.strategies[i];
                if (strat.DepartmentName != department)
                    continue;

                string name = strat.Config.Name;
                if (acceptablePrograms.Contains(name) && !completedPrograms.Contains(name))
                    list.Add(strat);
            }

            // then insert other programs
            for (int i = 0; i < __instance.strategies.Count; ++i)
            {
                Strategy strat = __instance.strategies[i];
                if (strat.DepartmentName != department)
                    continue;

                string name = strat.Config.Name;
                if (!acceptablePrograms.Contains(name) && !completedPrograms.Contains(name) && !ProgramHandler.Instance.DisabledPrograms.Contains(name) && strat is ProgramStrategy p && !p.Program.isDisabled)
                    list.Add(strat);
            }

            __result = list;
            acceptablePrograms.Clear();
            completedPrograms.Clear();
            return false;
        }

        internal static List<Strategy> _activeStrats = new List<Strategy>();
        [HarmonyPrefix]
        [HarmonyPatch("HasConflictingActiveStrategies")]
        internal static bool Prefix_HasConflictingActiveStrategies(StrategySystem __instance, out bool __result, string[] groupTags)
        {
            // Stock code is wacky. So let's vastly simplify all this code.
            // If we match on a single tag, we can't activate.
            int count = __instance.Strategies.Count;
            while (count-- > 0)
            {
                Strategy strategy = __instance.Strategies[count];
                if (strategy.IsActive)
                {
                    _activeStrats.Add(strategy);
                }
            }

            count = _activeStrats.Count;
            int contractorCount = 0;
            while (count-- > 0)
                if (_activeStrats[count].DepartmentName == "Contractor1")
                    ++contractorCount;

            count = _activeStrats.Count;
            while (count-- > 0)
            {
                Strategy strategy = _activeStrats[count];
                int idxSourceTag = groupTags.Length;
                while (idxSourceTag-- > 0) 
                {
                    if (groupTags[idxSourceTag] == "Contractor")
                    {
                        if (contractorCount < 2)
                            continue;

                        _activeStrats.Clear();
                        __result = true;
                        return false;
                    }

                    int idxTargetTag = strategy.GroupTags.Length;
                    while (idxTargetTag-- > 0)
                    {
                        if ((strategy.GroupTags[idxTargetTag] == groupTags[idxSourceTag]))
                        {
                            _activeStrats.Clear();
                            __result = true;
                            return false;
                        }
                    }
                }
            }
            _activeStrats.Clear();
            __result = false;
            return false;
        }

        // Save and load the deactivation date data
        [HarmonyPostfix]
        [HarmonyPatch("OnSave")]
        internal static void Postfix_OnSave(StrategySystem __instance, ConfigNode gameNode)
        {
            ConfigNode node = gameNode.AddNode("DEACTIVATIONDATES");
            foreach (var kvp in StrategyConfigRP0.ActivatedStrategies)
                node.AddValue(kvp.Key, kvp.Value.ToString("G17"));
        }

        [HarmonyPostfix]
        [HarmonyPatch("OnLoad")]
        internal static void Postfix_OnLoad(StrategySystem __instance, ConfigNode gameNode)
        {
            StrategyConfigRP0.ActivatedStrategies.Clear();
            ConfigNode node = gameNode.GetNode("DEACTIVATIONDATES");
            if (node != null)
            {
                foreach (ConfigNode.Value v in node.values)
                    StrategyConfigRP0.ActivatedStrategies.Add(v.name, double.Parse(v.value));
            }
        }

        [HarmonyPostfix]
        [HarmonyPatch("LoadStrategies")]
        internal static void Postfix_LoadStrategies()
        {
            // Ensure all active programs have their strategies registered
            foreach (var p in ProgramHandler.Instance.ActivePrograms)
            {
                var s = p.GetStrategy();
                if (s == null)
                {
                    UnityEngine.Debug.LogError("[RP-0]: Error: Could not find ProgramStrategy for program " + p.name);
                    continue;
                }

                if (!s.isActive)
                {
                    UnityEngine.Debug.LogWarning($"[RP-0]: Warning: Program {p.name} does not have its strategy activated. Doing so now.");
                    if (s is ProgramStrategy ps)
                    {
                        ps.PerformActivate(false);
                        ps.dateActivated = p.acceptedUT; // overwrite with correct activation UT
                    }
                }
            }

            ProgramHandler.Instance.OnLoadStrategiesComplete();

            KerbalConstructionTime.KCTGameStates.RecalculateBuildRates();
        }
    }
}