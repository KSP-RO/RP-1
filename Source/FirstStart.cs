using UnityEngine;

namespace RP0
{
    [KSPScenario((ScenarioCreationOptions)480, new GameScenes[] { GameScenes.SPACECENTER })]
    public class FirstStart : ScenarioModule
    {
        [KSPField(isPersistant = true)]
        public bool isFirstLoad = true;

        private void Start()
        {
            if (isFirstLoad)
            {
                isFirstLoad = false;

                if (HighLogic.CurrentGame.Parameters.Difficulty.BypassEntryPurchaseAfterResearch)
                {
                    HighLogic.CurrentGame.Parameters.Difficulty.BypassEntryPurchaseAfterResearch = false;
                    string msg = "'Bypass Entry Purchase' difficulty setting was automatically changed to false because RP-1 doesn't work correctly in this state.";
                    PopupDialog.SpawnPopupDialog(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), "", "Difficulty settings changed", msg, "Understood", false, HighLogic.UISkin);
                }
            }
        }
    }
}
