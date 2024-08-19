using HarmonyLib;
using System;
using System.Reflection;

namespace RP0.Harmony
{
    [HarmonyPatch]
    internal class PatchKSCSwitcher
    {
        internal static readonly System.Type _type = AccessTools.TypeByName("regexKSP.KSCSwitcher");

        internal static MethodBase TargetMethod() => AccessTools.Method(_type, "SetSite", new Type[] { typeof(ConfigNode) });

        [HarmonyPrepare]
        internal static bool Prepare()
        {
            return _type != null;
        }

        [HarmonyPostfix]
        internal static void Postfix_SetSite(ConfigNode KSC, bool __result)
        {
            if (__result && SpaceCenterManagement.Instance != null)
            {
                SpaceCenterManagement.Instance.SetActiveKSC(KSC.GetValue("name"));
            }
        }
    }
}
