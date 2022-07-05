using System;
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

        [KSPField(isPersistant = true)]
        public bool isFirstLoadTemp = true;    // For migration, remove at a later time

        private void Start()
        {
            if (isFirstLoad || isFirstLoadTemp)
            {
                isFirstLoad = false;
                isFirstLoadTemp = false;

                UnlockStartingPartsTechNode();

                if (HighLogic.CurrentGame.Parameters.Difficulty.BypassEntryPurchaseAfterResearch)
                {
                    HighLogic.CurrentGame.Parameters.Difficulty.BypassEntryPurchaseAfterResearch = false;
                    string msg = "'Bypass Entry Purchase' difficulty setting was automatically changed to false because RP-1 doesn't work correctly in this state.";
                    PopupDialog.SpawnPopupDialog(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), "", "Difficulty settings changed", msg, "Understood", false, HighLogic.UISkin);
                }

                MarkExperimentsAsDone();
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

        private static void MarkExperimentsAsDone()
        {
            foreach (ConfigNode rootCN in GameDatabase.Instance.GetConfigNodes("IGNORED_EXPERIMENTS"))
            {
                foreach (ConfigNode bodyCN in rootCN.GetNodes("BODY"))
                {
                    string bodyName = bodyCN.GetValue("name");

                    foreach (ConfigNode expCN in bodyCN.GetNodes("EXPERIMENT"))
                    {
                        string experimentName = expCN.GetValue("name");

                        foreach (ConfigNode sitCN in expCN.GetNodes("SITUATIONS"))
                        {
                            foreach (string sitName in sitCN.GetValues("name"))
                            {
                                MarkExperimentAsDone(experimentName, sitName, bodyName);
                            }
                        }
                    }
                }
            }
        }

        private static void MarkExperimentAsDone(string experimentID, string situationName, string bodyName)
        {
            ScienceExperiment experiment = ResearchAndDevelopment.GetExperiment(experimentID);
            CelestialBody body = FlightGlobals.GetBodyByName(bodyName);
            if (!Enum.TryParse(situationName, out ExperimentSituations situation))
            {
                Debug.LogError($"[RP-0] MarkExperimentAsDone: Invalid situation {situationName}");
                return;
            }

            if (experiment.BiomeIsRelevantWhile(situation))
            {
                List<string> allBiomes = ResearchAndDevelopment.GetBiomeTags(body, false);
                foreach (string biomeName in allBiomes)
                {
                    string biomeTitle = ScienceUtil.GetBiomedisplayName(body, biomeName);
                    MarkAsDone(experiment, situation, body, biomeName, biomeTitle);
                }
            }
            else
            {
                MarkAsDone(experiment, situation, body, string.Empty, string.Empty);
            }
        }

        private static void MarkAsDone(ScienceExperiment experiment, ExperimentSituations situation, CelestialBody body, string biomeName, string biomeTitle)
        {
            ScienceSubject subj = ResearchAndDevelopment.GetExperimentSubject(experiment, situation, body, biomeName, biomeTitle);
            subj.scientificValue = 0;
            subj.science = subj.scienceCap;
        }
    }
}
