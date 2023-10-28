using HarmonyLib;
using System.Reflection;

namespace RP0.Harmony
{
    [HarmonyPatch]
    internal class PatchCustomBarnKit_LoadUpgradesPrices
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
                MaintenanceHandler.Instance?.ScheduleMaintenanceUpdate();
            }
        }
    }
}