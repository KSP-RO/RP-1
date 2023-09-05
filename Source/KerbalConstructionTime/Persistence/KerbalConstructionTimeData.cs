using System;
using System.Reflection;
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

        [KSPField(isPersistant = true)] public float SciPointsTotal = -1f;
        [KSPField(isPersistant = true)] public bool IsSimulatedFlight = false;
        [KSPField(isPersistant = true)] public bool ExperimentalPartsEnabled = true;
        [KSPField(isPersistant = true)] public bool DisableFailuresInSim = true;
        [KSPField(isPersistant = true)] public int Researchers = 0;
        [KSPField(isPersistant = true)] public int Applicants = 0;
        [KSPField(isPersistant = true)] public bool StarterLCBuilding = false;
        [KSPField(isPersistant = true)] public bool HiredStarterApplicants = false;
        [KSPField(isPersistant = true)] public bool StartedProgram = false;
        [KSPField(isPersistant = true)] public bool AcceptedContract = false;
        public bool FirstRunNotComplete => !(StarterLCBuilding && HiredStarterApplicants && StartedProgram && AcceptedContract);

        [KSPField(isPersistant = true)] public int LoadedSaveVersion = -1;

        [KSPField(isPersistant = true)] public SimulationParams SimulationParams = new SimulationParams();


        [KSPField(isPersistant = true)]
        private PersistentList<LCEfficiency> _lcEfficiencies = new PersistentList<LCEfficiency>();
        public PersistentList<LCEfficiency> LCEfficiencies => _lcEfficiencies;
        public Dictionary<LCItem, LCEfficiency> LCToEfficiency = new Dictionary<LCItem, LCEfficiency>();

        private readonly Dictionary<Guid, LCItem> _LCIDtoLC = new Dictionary<Guid, LCItem>();
        public LCItem LC(Guid id) => _LCIDtoLC.TryGetValue(id, out var lc) ? lc : null;
        private readonly Dictionary<Guid, KCT_LaunchPad> _LPIDtoLP = new Dictionary<Guid, KCT_LaunchPad>();
        public KCT_LaunchPad LP(Guid id) => _LPIDtoLP[id];

        [KSPField(isPersistant = true)]
        public KCTObservableList<TechItem> TechList = new KCTObservableList<TechItem>();
        public bool TechIgnoreUpdates = false;

        [KSPField(isPersistant = true)]
        public PersistentSortedListValueTypeKey<string, BuildListVessel> BuildPlans = new PersistentSortedListValueTypeKey<string, BuildListVessel>();

        [KSPField(isPersistant = true)]
        public PersistentList<KSCItem> KSCs = new PersistentList<KSCItem>();
        public KSCItem ActiveKSC = null;

        [KSPField(isPersistant = true)]
        public BuildListVessel LaunchedVessel = new BuildListVessel();
        [KSPField(isPersistant = true)]
        public BuildListVessel EditedVessel = new BuildListVessel();
        [KSPField(isPersistant = true)]
        public BuildListVessel RecoveredVessel = new BuildListVessel();

        [KSPField(isPersistant = true)]
        public PersistentList<PartCrewAssignment> LaunchedCrew = new PersistentList<PartCrewAssignment>();

        [KSPField(isPersistant = true)]
        public AirlaunchParams AirlaunchParams = new AirlaunchParams();

        [KSPField(isPersistant = true)]
        public FundTarget fundTarget = new FundTarget();

        public bool MergingAvailable;
        public List<BuildListVessel> MergedVessels = new List<BuildListVessel>();

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

                TechList.Updated += techListUpdated;

                // Check for stating a new game
                if (LoadedSaveVersion == -1)
                {
                    KCTGameStates.IsFirstStart = true;
                    LoadedSaveVersion = KCTGameStates.VERSION;
                }

                bool foundStockKSC = false;
                foreach (var ksc in KSCs)
                {
                    if (ksc.KSCName.Length > 0 && string.Equals(ksc.KSCName, _legacyDefaultKscId, StringComparison.OrdinalIgnoreCase))
                    {
                        foundStockKSC = true;
                        break;
                    }
                }

                SetActiveKSCToRSS();
                if (foundStockKSC)
                    TryMigrateStockKSC();

                // Prune bad or inactive KSCs.
                for (int i = KSCs.Count; i-- > 0;)
                {
                    KSCItem ksc = KSCs[i];
                    if (ksc.KSCName == null || ksc.KSCName.Length == 0 || (ksc.IsEmpty && ksc != ActiveKSC))
                        KSCs.RemoveAt(i);
                }

                foreach (var blv in BuildPlans.Values)
                    blv.LinkToLC(null);

                LaunchedVessel.LinkToLC(LC(LaunchedVessel.LCID));
                RecoveredVessel.LinkToLC(LC(RecoveredVessel.LCID));
                EditedVessel.LinkToLC(LC(EditedVessel.LCID));

                LCEfficiency.RelinkAll();

                if (LoadedSaveVersion < KCTGameStates.VERSION)
                {
                    if (LoadedSaveVersion < 4)
                    {
                        foreach (var ksc in KSCs)
                        {
                            foreach (var lc in ksc.LaunchComplexes)
                            {
                                foreach (var blv in lc.BuildList)
                                    blv.RecalculateFromNode(true);
                                foreach (var blv in lc.Warehouse)
                                    blv.RecalculateFromNode(true);
                            }
                        }
                    }
                    LoadedSaveVersion = KCTGameStates.VERSION;
                }
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
            KSCItem stockKsc = KSCs.Find(k => string.Equals(k.KSCName, _legacyDefaultKscId, StringComparison.OrdinalIgnoreCase));
            if (KSCs.Count == 1)
            {
                // Rename the stock KSC to the new default (Cape)
                stockKsc.KSCName = _defaultKscId;
                SetActiveKSC(stockKsc.KSCName);
                return;
            }

            if (stockKsc.IsEmpty)
            {
                // Nothing provisioned into the stock KSC so it's safe to just delete it
                KSCs.Remove(stockKsc);
                SetActiveKSCToRSS();
                return;
            }

            int numOtherUsedKSCs = KSCs.Count(k => !k.IsEmpty && k != stockKsc);
            if (numOtherUsedKSCs == 0)
            {
                string kscName = GetActiveRSSKSC() ?? _defaultKscId;
                KSCItem newDefault = KSCs.Find(k => string.Equals(k.KSCName, kscName, StringComparison.OrdinalIgnoreCase));
                if (newDefault != null)
                {
                    // Stock KSC isn't empty but the new default one is - safe to rename the stock and remove the old default item
                    stockKsc.KSCName = newDefault.KSCName;
                    KSCs.Remove(newDefault);
                    SetActiveKSC(stockKsc);
                    return;
                }
            }

            // Can't really do anything if there's multiple KSCs in use.
            if (!IsKSCSwitcherInstalled)
            {
                // Need to switch back to the legacy "Stock" KSC if KSCSwitcher isn't installed
                SetActiveKSC(stockKsc.KSCName);
            }
        }

        public bool TechListHas(string techID)
        {
            for (int i = TechList.Count; i-- > 0;)
                if (TechList[i].techID == techID)
                    return true;

            return false;
        }

        public int TechListIndex(string techID)
        {
            for (int i = TechList.Count; i-- > 0;)
                if (TechList[i].techID == techID)
                    return i;

            return -1;
        }

        public void UpdateTechTimes()
        {
            for (int j = 0; j < TechList.Count; j++)
                TechList[j].UpdateBuildRate(j);
        }

        private void techListUpdated()
        {
            if (TechIgnoreUpdates)
                return;

            TechListUpdated();
        }

        public void TechListUpdated()
        {
            RP0.MaintenanceHandler.Instance?.ScheduleMaintenanceUpdate();
            RP0.Harmony.PatchRDTechTree.Instance?.RefreshUI();
        }

        public void RegisterLC(LCItem lc)
        {
            _LCIDtoLC[lc.ID] = lc;
        }

        public bool UnregisterLC(LCItem lc)
        {
            return _LCIDtoLC.Remove(lc.ID);
        }

        public void RegisterLP(KCT_LaunchPad lp)
        {
            _LPIDtoLP[lp.id] = lp;
        }

        public bool UnregsiterLP(KCT_LaunchPad lp)
        {
            return _LPIDtoLP.Remove(lp.id);
        }

        #region KSCSwitcher section

        private static bool? _isKSCSwitcherInstalled = null;
        private static FieldInfo _fiKSCSwInstance;
        private static FieldInfo _fiKSCSwSites;
        private static FieldInfo _fiKSCSwLastSite;
        private static FieldInfo _fiKSCSwDefaultSite;
        private const string _legacyDefaultKscId = "Stock";
        private const string _defaultKscId = "us_cape_canaveral";

        private static bool IsKSCSwitcherInstalled
        {
            get
            {
                if (!_isKSCSwitcherInstalled.HasValue)
                {
                    Assembly a = AssemblyLoader.loadedAssemblies.FirstOrDefault(la => string.Equals(la.name, "KSCSwitcher", StringComparison.OrdinalIgnoreCase))?.assembly;
                    _isKSCSwitcherInstalled = a != null;
                    if (_isKSCSwitcherInstalled.Value)
                    {
                        Type t = a.GetType("regexKSP.KSCLoader");
                        _fiKSCSwInstance = t?.GetField("instance", BindingFlags.Public | BindingFlags.Static);
                        _fiKSCSwSites = t?.GetField("Sites", BindingFlags.Public | BindingFlags.Instance | BindingFlags.FlattenHierarchy);

                        t = a.GetType("regexKSP.KSCSiteManager");
                        _fiKSCSwLastSite = t?.GetField("lastSite", BindingFlags.Public | BindingFlags.Instance | BindingFlags.FlattenHierarchy);
                        _fiKSCSwDefaultSite = t?.GetField("defaultSite", BindingFlags.Public | BindingFlags.Instance | BindingFlags.FlattenHierarchy);

                        if (_fiKSCSwInstance == null || _fiKSCSwSites == null || _fiKSCSwLastSite == null || _fiKSCSwDefaultSite == null)
                        {
                            KCTDebug.LogError("Failed to bind to KSCSwitcher");
                            _isKSCSwitcherInstalled = false;
                        }
                    }
                }
                return _isKSCSwitcherInstalled.Value;
            }
        }

        private string GetActiveRSSKSC()
        {
            if (!IsKSCSwitcherInstalled) return null;

            // get the LastKSC.KSCLoader.instance object
            // check the Sites object (KSCSiteManager) for the lastSite, if "" then get defaultSite

            object loaderInstance = _fiKSCSwInstance.GetValue(null);
            if (loaderInstance == null)
                return null;
            object sites = _fiKSCSwSites.GetValue(loaderInstance);
            string lastSite = _fiKSCSwLastSite.GetValue(sites) as string;

            if (lastSite == string.Empty)
                lastSite = _fiKSCSwDefaultSite.GetValue(sites) as string;
            return lastSite;
        }

        #endregion

        public void SetActiveKSCToRSS()
        {
            string site = GetActiveRSSKSC();
            SetActiveKSC(site);
        }

        public void SetActiveKSC(string site)
        {
            if (site == null || site.Length == 0)
                site = _defaultKscId;
            if (ActiveKSC == null || site != ActiveKSC.KSCName)
            {
                KCTDebug.Log($"Setting active site to {site}");
                KSCItem newKsc = KSCs.FirstOrDefault(ksc => ksc.KSCName == site);
                if (newKsc == null)
                {
                    newKsc = new KSCItem(site);
                    newKsc.EnsureStartingLaunchComplexes();
                    KSCs.Add(newKsc);
                }

                SetActiveKSC(newKsc);
            }
        }

        public void SetActiveKSC(KSCItem ksc)
        {
            if (ksc == null || ksc == ActiveKSC)
                return;

            // TODO: Allow setting KSC outside the tracking station
            // which will require doing some work on KSC switch
            ActiveKSC = ksc;
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
