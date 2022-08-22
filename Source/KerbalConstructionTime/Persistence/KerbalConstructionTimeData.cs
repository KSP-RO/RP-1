using System;
using System.Collections.Generic;
using UniLinq;
using UnityEngine;
using RP0.DataTypes;

namespace KerbalConstructionTime
{
    [KSPScenario(ScenarioCreationOptions.AddToAllGames, new GameScenes[] { GameScenes.EDITOR, GameScenes.FLIGHT, GameScenes.SPACECENTER, GameScenes.TRACKSTATION })]
    public class KerbalConstructionTimeData : ScenarioModule
    {
        public static Dictionary<string, string> techNameToTitle = new Dictionary<string, string>();
        public static Dictionary<string, List<string>> techNameToParents = new Dictionary<string, List<string>>();

        [KSPField(isPersistant = true)]
        public bool enabledForSave = HighLogic.CurrentGame.Mode == Game.Modes.CAREER ||
                                     HighLogic.CurrentGame.Mode == Game.Modes.SCIENCE_SANDBOX ||
                                     HighLogic.CurrentGame.Mode == Game.Modes.SANDBOX;

        [KSPField(isPersistant = true)] public string ActiveKSCName = string.Empty;
        [KSPField(isPersistant = true)] public float SciPointsTotal = -1f;
        [KSPField(isPersistant = true)] public bool IsSimulatedFlight = false;
        [KSPField(isPersistant = true)] public bool DisableFailuresInSim = true;
        [KSPField(isPersistant = true)] public int Researchers = 0;
        [KSPField(isPersistant = true)] public int Applicants = 0;
        [KSPField(isPersistant = true)] public bool StarterLCBuilding = false;
        [KSPField(isPersistant = true)] public bool HiredStarterApplicants = false;
        [KSPField(isPersistant = true)] public bool StartedProgram = false;
        [KSPField(isPersistant = true)] public bool AcceptedContract = false;
        public bool FirstRunNotComplete => !(StarterLCBuilding && HiredStarterApplicants && StartedProgram && AcceptedContract);

        [KSPField(isPersistant = true)] public int LoadedSaveVersion;

        [KSPField(isPersistant = true)] public SimulationParams SimulationParams = new SimulationParams();


        [KSPField(isPersistant = true)]
        private PersistentList<LCEfficiency> _lcEfficiencies = new PersistentList<LCEfficiency>();
        public PersistentList<LCEfficiency> LCEfficiencies => _lcEfficiencies;
        public Dictionary<LCItem, LCEfficiency> LCToEfficiency = new Dictionary<LCItem, LCEfficiency>();

        [KSPField(isPersistant = true)]
        public KCTObservableList<TechItem> TechList = new KCTObservableList<TechItem>();

        [KSPField(isPersistant = true)]
        public PersistentSortedListValueTypeKey<string, BuildListVessel> BuildPlans = new PersistentSortedListValueTypeKey<string, BuildListVessel>();

        public static KerbalConstructionTimeData Instance { get; protected set; }

        public override void OnAwake()
        {
            base.OnAwake();
            if (Instance != null)
                Destroy(Instance);

            Instance = this;
        }

        public void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        protected void LoadTree()
        {
            if (HighLogic.CurrentGame.Mode == Game.Modes.SCIENCE_SANDBOX || HighLogic.CurrentGame.Mode == Game.Modes.CAREER)
            {
                // On starting a new game, MM has not yet patched the tech tree URL so we're
                // going to use that directly instead of the one in HighLogic.
                if (HighLogic.CurrentGame.Parameters.Career.TechTreeUrl.Contains("Squad"))
                    HighLogic.CurrentGame.Parameters.Career.TechTreeUrl = System.IO.Path.Combine("GameData", "ModuleManager.TechTree");

                string fullPath = KSPUtil.ApplicationRootPath + HighLogic.CurrentGame.Parameters.Career.TechTreeUrl;
                KCTDebug.Log($"Loading tech tree from {fullPath}");

                if (ConfigNode.Load(fullPath) is ConfigNode fileNode && fileNode.HasNode("TechTree"))
                {
                    techNameToTitle.Clear();
                    techNameToParents.Clear();

                    ConfigNode treeNode = fileNode.GetNode("TechTree");
                    foreach (ConfigNode n in treeNode.GetNodes("RDNode"))
                    {
                        string techID = n.GetValue("id");
                        if (techID != null)
                        {
                            string title = n.GetValue("title");
                            if (title != null)
                                techNameToTitle[techID] = title;

                            var pList = new List<string>();
                            foreach (ConfigNode p in n.GetNodes("Parent"))
                            {
                                string pID = p.GetValue("parentID");
                                if(pID != null)
                                    pList.Add(pID);
                            }
                            techNameToParents[techID] = pList;
                        }
                    }
                }
            }
        }

        public override void OnSave(ConfigNode node)
        {
            if (Utilities.CurrentGameIsMission()) return;

            KCTDebug.Log("Writing to persistence.");
            base.OnSave(node);
            ActiveKSCName = KCTGameStates.ActiveKSC.KSCName;

            foreach (KSCItem KSC in KCTGameStates.KSCs.Where(x => x?.KSCName?.Length > 0))
            {
                // Don't bother saving KSCs that aren't active
                if (KSC.IsEmpty && KSC != KCTGameStates.ActiveKSC)
                    continue;

                var n = node.AddNode("KSC");
                KSC.Save(n);
            }

            KCT_GUI.GuiDataSaver.Save();
        }

        public override void OnLoad(ConfigNode node)
        {
            try
            {
                base.OnLoad(node);
                LoadTree();

                if (Utilities.CurrentGameIsMission()) return;

                KCTDebug.Log("Reading from persistence.");
                KCTGameStates.KSCs.Clear();
                KCTGameStates.ActiveKSC = null;
                KCTGameStates.InitTechList();

                // Special check
                if (LoadedSaveVersion == 0)
                {
                    // Keep this around another few versions for back-compat.
                    var kctVS = new KCT_DataStorage();
                    if (node.GetNode(kctVS.GetType().Name) is ConfigNode cn)
                    {
                        ConfigNode.LoadObjectFromConfig(kctVS, cn);
                        kctVS.ReadFields();
                    }
                    // This could also be because we started a new game.
                    if (LoadedSaveVersion == 0)
                    {
                        KCTGameStates.IsFirstStart = true;
                        LoadedSaveVersion = KCTGameStates.VERSION;
                    }
                }

                bool foundStockKSC = false;
                foreach (ConfigNode ksc in node.GetNodes("KSC"))
                {
                    string name = ksc.GetValue("KSCName");
                    var loaded_KSC = new KSCItem(name);
                    loaded_KSC.Load(ksc);
                    if (loaded_KSC.KSCName?.Length > 0)
                    {
                        if (KCTGameStates.KSCs.Find(k => k.KSCName == loaded_KSC.KSCName) == null)
                            KCTGameStates.KSCs.Add(loaded_KSC);
                        foundStockKSC |= string.Equals(loaded_KSC.KSCName, Utilities._legacyDefaultKscId, StringComparison.OrdinalIgnoreCase);
                    }
                }

                Utilities.SetActiveKSCToRSS();
                if (foundStockKSC)
                    TryMigrateStockKSC();

                var inDevProtoTechNodes = new Dictionary<string, ProtoTechNode>(); // list of all the protoTechNodes that are being researched

                // Load tech data from techlist
                foreach (var techItem in TechList)
                {
                    // save proto nodes that are in development
                    inDevProtoTechNodes.Add(techItem.ProtoNode.techID, techItem.ProtoNode);
                }
                if (HighLogic.LoadedSceneIsEditor)
                {
                    // get the nodes that have been researched from ResearchAndDevelopment
                    var protoTechNodes = Utilities.GetUnlockedProtoTechNodes();
                    // iterate through all loaded parts to check if any of them should be experimental
                    foreach (AvailablePart ap in PartLoader.LoadedPartsList)
                    {
                        if (Utilities.PartIsUnlockedButNotPurchased(protoTechNodes, ap) || inDevProtoTechNodes.ContainsKey(ap.TechRequired))
                        {
                            Utilities.AddExperimentalPart(ap);
                        }
                    }
                }

                if (LoadedSaveVersion < KCTGameStates.VERSION)
                {
                    if (LoadedSaveVersion < 14 && node.GetNode("Plans") is ConfigNode planNode)
                    {
                        foreach (ConfigNode cnV in planNode.GetNodes("KCTVessel"))
                        {
                            var blv = new BuildListVessel();
                            blv.Load(cnV);
                            BuildPlans.Remove(blv.shipName);
                            BuildPlans.Add(blv.shipName, blv);
                        }
                    }
                }

                foreach (var blv in BuildPlans.Values)
                    blv.LinkToLC(null);

                LCEfficiency.RelinkAll();
                
                LoadedSaveVersion = KCTGameStates.VERSION;
            }
            catch (Exception ex)
            {
                KCTGameStates.ErroredDuringOnLoad = true;
                Debug.LogError("[KCT] ERROR! An error while KCT loading data occurred. Things will be seriously broken!\n" + ex);
                PopupDialog.SpawnPopupDialog(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), "errorPopup", "Error Loading KCT Data", "ERROR! An error occurred while loading KCT data. Things will be seriously broken! Please report this error to RP-1 GitHub and attach the log file. The game will be UNPLAYABLE in this state!", "Understood", false, HighLogic.UISkin);
            }
        }

        private void TryMigrateStockKSC()
        {
            KSCItem stockKsc = KCTGameStates.KSCs.Find(k => string.Equals(k.KSCName, Utilities._legacyDefaultKscId, StringComparison.OrdinalIgnoreCase));
            if (KCTGameStates.KSCs.Count == 1)
            {
                // Rename the stock KSC to the new default (Cape)
                stockKsc.KSCName = Utilities._defaultKscId;
                Utilities.SetActiveKSC(stockKsc.KSCName);
                return;
            }

            if (stockKsc.IsEmpty)
            {
                // Nothing provisioned into the stock KSC so it's safe to just delete it
                KCTGameStates.KSCs.Remove(stockKsc);
                Utilities.SetActiveKSCToRSS();
                return;
            }

            int numOtherUsedKSCs = KCTGameStates.KSCs.Count(k => !k.IsEmpty && k != stockKsc);
            if (numOtherUsedKSCs == 0)
            {
                string kscName = Utilities.GetActiveRSSKSC() ?? Utilities._defaultKscId;
                KSCItem newDefault = KCTGameStates.KSCs.Find(k => string.Equals(k.KSCName, kscName, StringComparison.OrdinalIgnoreCase));
                if (newDefault != null)
                {
                    // Stock KSC isn't empty but the new default one is - safe to rename the stock and remove the old default item
                    stockKsc.KSCName = newDefault.KSCName;
                    KCTGameStates.KSCs.Remove(newDefault);
                    Utilities.SetActiveKSC(stockKsc);
                    return;
                }
            }

            // Can't really do anything if there's multiple KSCs in use.
            if (!Utilities.IsKSCSwitcherInstalled)
            {
                // Need to switch back to the legacy "Stock" KSC if KSCSwitcher isn't installed
                Utilities.SetActiveKSC(stockKsc.KSCName);
            }
        }
    }
}

/*
    KerbalConstructionTime (c) by Michael Marvin, Zachary Eck

    KerbalConstructionTime is licensed under a
    Creative Commons Attribution-NonCommercial-ShareAlike 4.0 International License.

    You should have received a copy of the license along with this
    work. If not, see <http://creativecommons.org/licenses/by-nc-sa/4.0/>.
*/
