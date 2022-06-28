using System;
using System.Collections.Generic;
using System.IO;
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

                MarkExperimentsAsDone();
                ClobberRealChuteDefaultSettings();
                CopyCraftFiles();
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

        private static void CopyCraftFiles()
        {
            try
            {
                if (HighLogic.CurrentGame.Parameters.CustomParams<RP0Settings>().IncludeCraftFiles)
                {
                    string rp1ShipsPath = $"{KSPUtil.ApplicationRootPath}GameData/RP-0/Craft Files/";
                    string saveShipsPath = $"{KSPUtil.ApplicationRootPath}saves/{HighLogic.SaveFolder}/Ships/";
                    var subfolders = new[] { "SPH/", "VAB/" };
                    foreach (string sf in subfolders)
                    {
                        string sourceFolderPath = rp1ShipsPath + sf;
                        string destFolderPath = saveShipsPath + sf;

                        var srcDInf = new DirectoryInfo(sourceFolderPath);
                        foreach (FileInfo fi in srcDInf.GetFiles())
                        {
                            string destFilePath = destFolderPath + fi.Name;
                            fi.CopyTo(destFilePath, true);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
            }
        }
    }
}
