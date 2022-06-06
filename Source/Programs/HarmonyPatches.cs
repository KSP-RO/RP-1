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

namespace RP0
{
    public partial class HarmonyPatcher : MonoBehaviour
    {
        //[HarmonyPatch(typeof(Administration))]
        //internal class PatchAdministration
        //{
        //    [HarmonyPrefix]
        //    [HarmonyPatch("UpdateStrategyCount")]
        //    internal static bool Prefix_UpdateStrategyCount(Administration __instance)
        //    {
        //        __instance.activeStratCount.text = $"Active Programs: {Programs.ProgramHandler.Instance.ActivePrograms.Count} [Max: {Programs.ProgramHandler.Instance.ActiveProgramLimit}]";
        //        return false;
        //    }

        //    [HarmonyPrefix]
        //    [HarmonyPatch("AddStrategiesListItem")]
        //    internal static bool Prefix_AddStrategiesListItem(Administration __instance, ref UIList itemList, ref List<Strategies.Strategy> strategies)
        //    {
        //        if (strategies.Count == 0 || strategies[0].Department.Name != "Programs")
        //            return true;

        //        foreach (Program p in ProgramHandler.Programs)
        //        {
        //            if (p.IsActive)
        //                continue;

        //            VirtualStrategy strat;
        //            if (!ProgramHandler.Instance.StubStrategies.TryGetValue(p.name, out strat))
        //            {
        //                Debug.LogError($"[RP-0] Error: Can't find stub strategy for program {p.name}");
        //                continue;
        //            }

        //            Texture icon = strat.Config.IconImage;
        //            if (icon == null)
        //                icon = Administration.Instance.defaultIcon;

        //            UIListItem item = Instantiate<UIListItem>(__instance.prefabStratListItem);

        //            StrategyListItem stratListIcon = item.GetComponent<StrategyListItem>();
        //            stratListIcon.Initialize(icon, strat.Title);

        //            Administration.StrategyWrapper wrapper = new Administration.StrategyWrapper(strat, stratListIcon);
        //            stratListIcon.SetupButton(p.CanAccept, wrapper, wrapper.OnTrue, wrapper.OnFalse);

        //            itemList.AddItem(item);
        //        }

        //        return false;
        //    }
        //}

        [HarmonyPatch(typeof(StrategySystemConfig))]
        internal class PatchStrategySystemConfig
        {
            [HarmonyPostfix]
            [HarmonyPatch("LoadStrategyConfigs")]
            internal static void Postfix_LoadStrategyConfigs(StrategySystemConfig __instance)
            {
                Debug.Log($"%%%% Loading programs as StrategyConfigs. Found {__instance.Departments.Count} departments.");
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

        //[HarmonyPatch(typeof(Strategy))]
        //internal class PatchStrategy
        //{
        //    [HarmonyPrefix]
        //    [HarmonyPatch("Activate")]
        //    internal static bool Prefix_Activate(Strategy __instance, ref bool __result)
        //    {
        //        if (__instance is VirtualStrategy v)
        //        {
        //            __result = v.Activate();
        //            return false;
        //        }

        //        return true;
        //    }

        //    [HarmonyPrefix]
        //    [HarmonyPatch("CanBeActivated")]
        //    internal static bool Prefix_CanBeActivated(Strategy __instance, ref bool __result, ref string reason)
        //    {
        //        if (__instance is VirtualStrategy v)
        //        {
        //            __result = v.CanBeActivated(out reason);
        //            return false;
        //        }

        //        return true;
        //    }

        //    [HarmonyPrefix]
        //    [HarmonyPatch("CanBeDeactivated")]
        //    internal static bool Prefix_CanBeDeactivated(Strategy __instance, ref bool __result, ref string reason)
        //    {
        //        if (__instance is VirtualStrategy v)
        //        {
        //            __result = v.CanBeDeactivated(out reason);
        //            return false;
        //        }

        //        return true;
        //    }

        //    [HarmonyPrefix]
        //    [HarmonyPatch("Deactivate")]
        //    internal static bool Prefix_Deactivate(Strategy __instance, ref bool __result)
        //    {
        //        if (__instance is VirtualStrategy v)
        //        {
        //            __result = v.Deactivate();
        //            return false;
        //        }

        //        return true;
        //    }

        //    [HarmonyPrefix]
        //    [HarmonyPatch("Register")]
        //    internal static bool Prefix_Register(Strategy __instance)
        //    {
        //        if (__instance is VirtualStrategy v)
        //        {
        //            v.Register();
        //            return false;
        //        }

        //        return true;
        //    }

        //    [HarmonyPrefix]
        //    [HarmonyPatch("Unregister")]
        //    internal static bool Prefix_Unregister(Strategy __instance)
        //    {
        //        if (__instance is VirtualStrategy v)
        //        {
        //            v.Unregister();
        //            return false;
        //        }

        //        return true;
        //    }

        //    [HarmonyPrefix]
        //    [HarmonyPatch("Update")]
        //    internal static bool Prefix_Update(Strategy __instance)
        //    {
        //        if (__instance is VirtualStrategy v)
        //        {
        //            v.Update();
        //            return false;
        //        }

        //        return true;
        //    }

        //    [HarmonyPrefix]
        //    [HarmonyPatch("Load")]
        //    internal static bool Prefix_Load(Strategy __instance, ref ConfigNode node)
        //    {
        //        if (__instance is VirtualStrategy v)
        //        {
        //            v.Load(node);
        //            return false;
        //        }

        //        return true;
        //    }

        //    [HarmonyPrefix]
        //    [HarmonyPatch("Save")]
        //    internal static bool Prefix_Save(Strategy __instance, ref ConfigNode node)
        //    {
        //        if (__instance is VirtualStrategy v)
        //        {
        //            v.Save(node);
        //            return false;
        //        }

        //        return true;
        //    }
        //}
    }
}