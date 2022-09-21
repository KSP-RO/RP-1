using HarmonyLib;
using System.Reflection;
using UnityEngine;
using KerbalConstructionTime;

namespace RP0.Harmony
{
    [HarmonyPatch]
    internal class PatchKSCSwitcher
    {
        internal static readonly System.Type KSCSwitcherType = AccessTools.TypeByName("regexKSP.KSCSwitcher");

        internal static MethodBase TargetMethod() => KSCSwitcherType?.GetMethod("SetSite", AccessTools.all);

        [HarmonyPrepare]
        internal static bool Prepare()
        {
            return KSCSwitcherType != null;
        }

        [HarmonyPostfix]
        internal static void Postfix_SetSite(ConfigNode KSC, bool __result)
        {
            if (__result && KerbalConstructionTimeData.Instance != null)
            {
                KerbalConstructionTimeData.Instance.SetActiveKSC(KSC.name);
            }
        }
    }
}
