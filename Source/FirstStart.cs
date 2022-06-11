using System.Collections.Generic;
using UnityEngine;

namespace RP0
{
    [KSPScenario((ScenarioCreationOptions)480, new GameScenes[] { GameScenes.SPACECENTER })]
    public class FirstStart : ScenarioModule
    {
        private const string StartTechID = "unlockParts";

        [KSPField(isPersistant = true)]
        public bool isFirstLoad = true;

        private void Start()
        {
            if (isFirstLoad)
            {
                isFirstLoad = false;
                Reputation.Instance.GetType().GetField("rep", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).SetValue(Reputation.Instance, (float)(MaintenanceHandler.Settings.subsidyCurve.Evaluate(0f) / MaintenanceHandler.Settings.repToSubsidyConversion));

                UnlockStartingPartsTechNode();

                if (HighLogic.CurrentGame.Parameters.Difficulty.BypassEntryPurchaseAfterResearch)
                {
                    HighLogic.CurrentGame.Parameters.Difficulty.BypassEntryPurchaseAfterResearch = false;
                    string msg = "'Bypass Entry Purchase' difficulty setting was automatically changed to false because RP-1 doesn't work correctly in this state.";
                    PopupDialog.SpawnPopupDialog(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), "", "Difficulty settings changed", msg, "Understood", false, HighLogic.UISkin);
                }
            }
        }

        private static void UnlockStartingPartsTechNode()
        {
            if (ResearchAndDevelopment.GetTechnologyState(StartTechID) != RDTech.State.Available)
            {
                ProtoTechNode ptn = AssetBase.RnDTechTree.FindTech(StartTechID);
                ptn = new ProtoTechNode
                {
                    techID = ptn.techID,
                    state = RDTech.State.Available,
                    scienceCost = ptn.scienceCost,
                    partsPurchased = new List<AvailablePart>()
                };
                ResearchAndDevelopment.Instance.SetTechState(StartTechID, ptn);
            }
        }
    }
}
