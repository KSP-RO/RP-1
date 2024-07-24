using System;
using System.Collections.Generic;
using System.IO;
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

                ScienceUtils.MarkExperimentsAsDone(Database.StartCompletedExperiments);
                CopyCraftFiles();
                UnlockCreditHandler.Instance.IncrementCredit("start", HighLogic.CurrentGame.Parameters.CustomParams<RP0Settings>().StartingUnlockCredit);

                HighLogic.CurrentGame.CrewRoster.Update(Planetarium.GetUniversalTime());
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

        private static void CopyCraftFiles()
        {
            try
            {
                if (HighLogic.CurrentGame.Parameters.CustomParams<RP0Settings>().IncludeCraftFiles)
                {
                    string rp1ShipsPath = $"{KSPUtil.ApplicationRootPath}GameData/RP-1/Craft Files/";
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
