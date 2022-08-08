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
        internal static bool Prefix_GetStrategies(StrategySystem __instance, ref string department, ref List<Strategy> __result, ref List<Strategy> ___strategies)
        {
            var list = new List<Strategy>();

            if (department != "Programs")
            {
                // FIXME support other strategy types?
                for (int i = 0; i < ___strategies.Count; ++i)
                {
                    Strategy strat = ___strategies[i];
                    if (strat.DepartmentName != department)
                        continue;

                    if (strat.Config is StrategyConfigRP0 cfg && strat is StrategyRP0 stratR)
                    {
                        if (cfg.RemoveOnDeactivate)
                        {
                            double nameDeactivate = StrategyConfigRP0.ActivatedStrategies.ValueOrDefault(cfg.Name);
                            double tagDeactivate = 0d;
                            if (!string.IsNullOrEmpty(cfg.RemoveOnDeactivateTag))
                                tagDeactivate = StrategyConfigRP0.ActivatedStrategies.ValueOrDefault(cfg.RemoveOnDeactivateTag);

                            // we are skipping the case where the strategy or its tag is active, but 
                            // groupTags will take care of that.
                            double lastActive = System.Math.Max(nameDeactivate, tagDeactivate);
                            if (lastActive > 0d)
                            {
                                if (cfg.ReactivateCooldown == 0d || stratR.DateDeactivated + cfg.ReactivateCooldown > KSPUtils.GetUT())
                                    continue;
                            }
                        }


                        // Check unlock criteria
                        if (cfg.UnlockByContractComplete.Count > 0 || cfg.UnlockByProgramComplete.Count > 0)
                        {
                            bool skip = true;
                            foreach (string s in cfg.UnlockByContractComplete)
                            {
                                if (ProgramHandler.Instance.CompletedCCContracts.Contains(s))
                                {
                                    skip = false;
                                    break;
                                }
                            }
                            if (skip)
                            {
                                foreach (string s in cfg.UnlockByProgramComplete)
                                {
                                    if (ProgramHandler.Instance.CompletedPrograms.Find(p => p.name == s) != null)
                                    {
                                        skip = false;
                                        break;
                                    }
                                }
                            }

                            if (skip)
                                continue;
                        }
                    }

                    list.Add(strat);
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
            for (int i = 0; i < ___strategies.Count; ++i)
            {
                Strategy strat = ___strategies[i];
                if (strat.DepartmentName != department)
                    continue;

                string name = strat.Config.Name;
                if (acceptablePrograms.Contains(name) && !completedPrograms.Contains(name))
                    list.Add(strat);
            }

            // then insert other programs
            for (int i = 0; i < ___strategies.Count; ++i)
            {
                Strategy strat = ___strategies[i];
                if (strat.DepartmentName != department)
                    continue;

                string name = strat.Config.Name;
                if (!acceptablePrograms.Contains(name) && !completedPrograms.Contains(name) && !ProgramHandler.Instance.DisabledPrograms.Contains(name))
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
        internal static bool Prefix_HasConflictingActiveStrategies(StrategySystem __instance, ref bool __result, string[] groupTags)
        {
            // Vastly simplify all this code.
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
            while (count-- > 0)
            {
                Strategy strategy = _activeStrats[count];
                int idxTargetTag = strategy.GroupTags.Length;
                while (idxTargetTag-- > 0)
                {
                    int idxSourceTag = groupTags.Length;
                    while (idxSourceTag-- > 0)
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
            KerbalConstructionTime.KCTGameStates.RecalculateBuildRates();
        }
    }
}