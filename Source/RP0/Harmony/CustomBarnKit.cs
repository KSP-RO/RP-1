using HarmonyLib;
using System;
using System.Reflection;

namespace RP0.Harmony
{
    [HarmonyPatch]
    internal class PatchCustomBarnKit_LoadUpgradesPrices
    {
        internal static readonly Type _type = AccessTools.TypeByName("CustomBarnKit.CustomBarnKit");

        internal static MethodBase TargetMethod() => AccessTools.Method(_type, "LoadUpgradesPrices");

        [HarmonyPrepare]
        internal static bool Prepare()
        {
            return _type != null;
        }

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
                MaintenanceHandler.Instance?.ScheduleMaintenanceUpdate();
            }
        }
    }
}