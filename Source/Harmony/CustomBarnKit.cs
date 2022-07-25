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
    [HarmonyPatch]
    internal class PatchCBK
    {
        static MethodBase TargetMethod() => AccessTools.TypeByName("CustomBarnKit.CustomBarnKit").GetMethod("LoadUpgradesPrices", AccessTools.all);

        [HarmonyPrefix]
        internal static void Prefix_LoadUpgradesPrices(ref bool ___varLoaded, out bool __state)
        {
            __state = ___varLoaded;
        }

        [HarmonyPostfix]
        internal static void Postfix_LoadUpgradesPrices(ref bool ___varLoaded, bool __state)
        {
            if (___varLoaded && !__state)
            {
                MaintenanceHandler.ClearFacilityCosts();
                MaintenanceHandler.OnRP0MaintenanceChanged.Fire();
            }
        }
    }
}