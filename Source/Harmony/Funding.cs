using HarmonyLib;
using KSP.UI.Screens.SpaceCenter.MissionSummaryDialog;
using System.Collections.Generic;

namespace RP0.Harmony
{
    [HarmonyPatch(typeof(Funding))]
    internal class PatchFunding
    {
        [HarmonyPrefix]
        [HarmonyPatch("AddFunds")]
        internal static bool Prefix_AddFunds(Funding __instance, double value, TransactionReasons reason)
        {
            if (value == 0d)
                return false;

            __instance.funds += value;
            CurrencyModifierQueryRP0 data = new CurrencyModifierQueryRP0(reason, value, 0f, 0f);
            GameEvents.Modifiers.OnCurrencyModifierQuery.Fire(data);
            GameEvents.Modifiers.OnCurrencyModified.Fire(data);
            if (!HighLogic.CurrentGame.Parameters.CustomParams<GameParameters.AdvancedParams>().AllowNegativeCurrency && __instance.funds < 0d)
                __instance.funds = 0d;

            GameEvents.OnFundsChanged.Fire(__instance.funds, reason);

            return false;
        }

        [HarmonyPrefix]
        [HarmonyPatch("onVesselRecoveryProcessing")]
        internal static bool Prefix_onVesselRecoveryProcessing(Funding __instance, ProtoVessel pv, KSP.UI.Screens.MissionRecoveryDialog mrDialog, float recoveryScore)
        {
            if (pv == null)
                return false;

            if (!KCTUtilities.IsVesselKCTRecovering(pv))
                return true;

            if (mrDialog == null)
                return false;

            List<ProtoPartSnapshot> allProtoPartsIncludingCargo = pv.GetAllProtoPartsIncludingCargo();
            for (int i = 0; i < allProtoPartsIncludingCargo.Count; i++)
            {
                ProtoPartSnapshot protoPartSnapshot = allProtoPartsIncludingCargo[i];
                AvailablePart availablePart = null;
                if (protoPartSnapshot.partInfo == null)
                {
                    if (!string.IsNullOrEmpty(protoPartSnapshot.partName))
                    {
                        availablePart = PartLoader.getPartInfoByName(protoPartSnapshot.partName);
                    }
                }
                else
                {
                    availablePart = protoPartSnapshot.partInfo;
                }
                if (availablePart != null)
                {
                    if (!string.Equals(availablePart.name, "kerbalEVA"))
                    {
                        mrDialog.AddPartWidget(PartWidget.Create(availablePart, 0, 0, mrDialog));
                    }
                    for (int j = 0; j < protoPartSnapshot.resources.Count; j++)
                    {
                        ProtoPartResourceSnapshot protoPartResourceSnapshot = protoPartSnapshot.resources[j];
                        PartResourceDefinition definition = PartResourceLibrary.Instance.GetDefinition(protoPartResourceSnapshot.resourceName);
                        if (definition != null)
                        {
                            mrDialog.AddResourceWidget(ResourceWidget.Create(definition, (float)protoPartResourceSnapshot.amount, 0, mrDialog));
                        }
                    }
                }
            }
            mrDialog.fundsEarned = 0f;

            return false;
        }

        [HarmonyPrefix]
        [HarmonyPatch("onVesselRollout")]
        internal static bool Prefix_onVesselRollout(Funding __instance, ShipConstruct ship)
        {
            if (!KCT_GUI.IsPrimarilyDisabled)
                return false;

            __instance.AddFunds(-KCTUtilities.GetTotalVesselCost(ship.Parts), TransactionReasonsRP0.VesselPurchase.Stock());
            return false;
        }

        // For future use
        [HarmonyPrefix]
        [HarmonyPatch("onCrewHired")]
        internal static bool Prefix_onCrewHired(Funding __instance, ProtoCrewMember crew, int crewCount)
        {
            __instance.AddFunds(-GameVariables.Instance.GetRecruitHireCost(crewCount), TransactionReasons.CrewRecruited);
            return false;
        }
    }
}
