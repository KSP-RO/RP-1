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

namespace RP0.Harmony
{
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
}