using HarmonyLib;
using KSP.UI.Screens;
using KSP.UI.Screens.Editor;

namespace RP0.Harmony
{
    [HarmonyPatch(typeof(PartListTooltipController))]
    internal class PatchPartListTooltipController
    {
        /// <summary>
        /// Fix issue where stock editor purchase adds the part to partsPurchased again if it's listed in its own identicalParts.
        /// </summary>
        [HarmonyPrefix]
        [HarmonyPatch("onPurchaseProceed")]
        internal static bool Prefix_onPurchaseProceed(PartListTooltipController __instance)
        {
            if (!HighLogic.LoadedSceneIsEditor)
                return true;

            AvailablePart partInfo = __instance.partInfo;
            ProtoTechNode techState = ResearchAndDevelopment.Instance.GetTechState(partInfo.TechRequired);
            if (!techState.partsPurchased.Contains(partInfo))
            {
                techState.partsPurchased.Add(partInfo);
                GameEvents.OnPartPurchased.Fire(partInfo);
            }
            KCTUtilities.HandlePurchase(partInfo);

            EditorPartList.Instance?.Refresh();
            GamePersistence.SaveGame("persistent", HighLogic.SaveFolder, SaveMode.OVERWRITE);
            __instance.onPurchaseDismiss();

            return false;
        }
    }
}
