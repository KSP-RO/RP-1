using HarmonyLib;
using System;
using System.Reflection;

namespace RP0.Harmony
{
    [HarmonyPatch]
    internal class ROHSPartModulePatcher
    {
        internal static readonly Type _type = AccessTools.TypeByName("ROHeatshields.ModuleROHeatshield");
        internal static MethodInfo _mi;

        internal static MethodBase TargetMethod() => _mi;

        [HarmonyPrepare]
        internal static bool Prepare()
        {
            if (_type != null)
            {
                _mi ??= AccessTools.Method(_type, "CanAffordEntryCost", new Type[] { typeof(float) });
            }
            return _mi != null;
        }

        [HarmonyPrefix]
        internal static bool Prefix_CanAffordEntryCost(float cost, ref bool __result)
        {
            var cmq = CurrencyModifierQueryRP0.RunQuery(TransactionReasonsRP0.PartOrUpgradeUnlock, -cost, 0d, 0d);
            double postCMQcost = -cmq.GetTotal(CurrencyRP0.Funds, false);
            double credit = UnlockCreditHandler.Instance.TotalCredit;
            cmq.AddPostDelta(CurrencyRP0.Funds, credit, true);
            __result = cmq.CanAfford();

            return false;
        }
    }
}
