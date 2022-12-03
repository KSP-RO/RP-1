using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
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

                UnlockStartingPartsTechNode();

                if (HighLogic.CurrentGame.Parameters.Difficulty.BypassEntryPurchaseAfterResearch)
                {
                    HighLogic.CurrentGame.Parameters.Difficulty.BypassEntryPurchaseAfterResearch = false;
                    string msg = "'Bypass Entry Purchase' difficulty setting was automatically changed to false because RP-1 doesn't work correctly in this state.";
                    PopupDialog.SpawnPopupDialog(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), "", "Difficulty settings changed", msg, "Understood", false, HighLogic.UISkin);
                }

                ClobberRealChuteDefaultSettings();
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

        private void ClobberRealChuteDefaultSettings()
        {
            if (AssemblyLoader.loadedAssemblies.FirstOrDefault(a => a.assembly.GetName().Name == "RealChute") is var rcAssembly &&
               rcAssembly.assembly.GetType("RealChute.RealChuteSettings") is Type rcSettings &&
               rcSettings.GetProperty("Instance", BindingFlags.Static | BindingFlags.Public) is PropertyInfo instancePInf &&
               rcSettings.GetProperty("AutoArm", BindingFlags.Instance | BindingFlags.Public) is PropertyInfo autoArmPInf &&
               rcSettings.GetMethod("SaveSettings", BindingFlags.Static | BindingFlags.Public) is MethodInfo saveMInf)
            {
                object settingsInstance = instancePInf.GetValue(null);
                autoArmPInf?.SetValue(settingsInstance, true);
                saveMInf.Invoke(null, null);
            }
            else
            {
                Debug.Log("[RP-0] FirstStart: RealChute not found");
            }
        }
    }
}
