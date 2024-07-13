using HarmonyLib;
using System.Reflection;

namespace RP0.Harmony
{
    [HarmonyPatch]
    internal class PatchKSCSwitcher
    {
        internal static readonly System.Type KSCSwitcherType = AccessTools.TypeByName("regexKSP.KSCSwitcher");

        internal static MethodBase TargetMethod() => KSCSwitcherType == null ? null : AccessTools.Method(KSCSwitcherType, "SetSite", new System.Type[] { typeof(ConfigNode) });

        [HarmonyPrepare]
        internal static bool Prepare()
        {
            return KSCSwitcherType != null;
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
