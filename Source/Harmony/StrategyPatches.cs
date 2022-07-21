using HarmonyLib;
using KSP.UI.Screens;
using KSP.UI;
using Strategies;
using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using System.Reflection;
using RP0.Programs;
using UniLinq;

namespace RP0
{
    public partial class HarmonyPatcher : MonoBehaviour
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
                if (department != "Programs")
                    return true;

                List<Strategy> list = new List<Strategy>();

                // Cache what programs can be accepted (and which have been completed)
                // We don't care about trust thresholds because you can always accept at Slow speed
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
        }

        [HarmonyPatch(typeof(StrategySystemConfig))]
        internal class PatchStrategySystemConfig
        {
            [HarmonyPostfix]
            [HarmonyPatch("LoadStrategyConfigs")]
            internal static void Postfix_LoadStrategyConfigs(StrategySystemConfig __instance)
            {
                ProgramHandler.EnsurePrograms();
                foreach (Program p in ProgramHandler.Programs)
                {
                    Debug.Log($"Added Program {p.name} as config");
                    System.Type type = StrategySystem.GetStrategyType(p.name);
                    if (type == null)
                        Debug.LogError("ERROR! Can't find type!");
                    else
                        Debug.Log($"Found type: {type.FullName}");
                    ConfigNode node = new ConfigNode();
                    node.AddValue("name", p.name);
                    node.AddValue("department", "Programs");
                    node.AddValue("title", p.title);
                    node.AddValue("desc", p.description);
                    if (!string.IsNullOrEmpty(p.icon))
                        node.AddValue("icon", p.icon);
                    node.AddValue("groupTag", p.name); // will never conflict except with itself.
                    StrategyConfig cfg = StrategyConfig.Create(node, __instance.Departments);
                    __instance.Strategies.Add(cfg);
                }
            }
        }

        [HarmonyPatch(typeof(Strategy))]
        internal class PatchStrategy
        {
            [HarmonyPostfix]
            [HarmonyPatch("SetupConfig")]
            internal static void Postfix_SetupConfig(Strategy __instance)
            {
                MethodInfo OnSetupConfigMethod = __instance.GetType().GetMethod("OnSetupConfig");
                if (OnSetupConfigMethod != null)
                    OnSetupConfigMethod.Invoke(__instance, new object[] { });
            }
        }
    }
}