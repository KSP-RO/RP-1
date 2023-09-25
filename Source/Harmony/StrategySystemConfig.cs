﻿using HarmonyLib;
using Strategies;
using UnityEngine;
using RP0.Programs;

namespace RP0.Harmony
{
    // Make sure Programs is the first department
    [HarmonyPatch(typeof(StrategySystemConfig))]
    internal class PatchStrategySystemConfig
    {
        [HarmonyPostfix]
        [HarmonyPatch("LoadDepartmentConfigs")]
        internal static void Postfix_LoadDepartmentConfigs(StrategySystemConfig __instance)
        {
            DepartmentConfig dep = null;
            for (int i = __instance.Departments.Count; i-- > 0;)
            {
                var d = __instance.Departments[i];
                if (d.Name == "Programs")
                {
                    dep = d;
                    __instance.Departments.RemoveAt(i);
                    break;
                }
            }
            if (dep != null)
                __instance.Departments.Insert(0, dep);
        }

        // Create all the ProgramStrategies dynamically after we load all the cfg-based strategies
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
                node.AddValue("desc", p.description ?? string.Empty);
                if (!string.IsNullOrEmpty(p.icon))
                {
                    node.AddValue("icon", p.icon);
                }
                else
                {
                    string tName = $"RP-1/Programs/{p.name}";
                    if (GameDatabase.Instance.ExistsTexture(tName))
                        node.AddValue("icon", tName);
                }
                node.AddValue("groupTag", p.name); // will never conflict except with itself.
                StrategyConfig cfg = StrategyConfig.Create(node, __instance.Departments);
                __instance.Strategies.Add(cfg);
            }
        }
    }
}