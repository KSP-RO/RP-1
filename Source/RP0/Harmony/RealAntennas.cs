using HarmonyLib;

namespace RP0.Harmony
{
    [HarmonyPatch(typeof(RealAntennas.ModuleRealAntenna))]
    internal class RAPartModulePatcher
    {
        [HarmonyPrepare]
        internal static bool Prepare()
        {
            var m = AccessTools.Method(typeof(RealAntennas.ModuleRealAntenna), "CanAffordEntryCost", new System.Type[] { typeof(float) });
            return m != null;
        }

        [HarmonyPrefix]
        [HarmonyPatch("CanAffordEntryCost")]
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
