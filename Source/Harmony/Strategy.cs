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