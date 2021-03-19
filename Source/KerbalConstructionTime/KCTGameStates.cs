using System.Collections.Generic;
using ToolbarControl_NS;
using Upgradeables;

namespace KerbalConstructionTime
{
    public static class KCTGameStates
    {
        internal const string _modId = "KCT_NS";
        internal const string _modName = "Kerbal Construction Time";

        public static double UT, LastUT = 0;
        public static KCTSettings Settings = new KCTSettings();

        public static KSCItem ActiveKSC = null;
        public static List<KSCItem> KSCs = new List<KSCItem>();
        public static string ActiveKSCName = string.Empty;
        public static int TechUpgradesTotal = 0;
        public static float SciPointsTotal = -1f;

        public static KCTObservableList<TechItem> TechList = new KCTObservableList<TechItem>();

        public static List<int> PurchasedUpgrades = new List<int>() { 0, 0 };
        public static int MiscellaneousTempUpgrades = 0, LastKnownTechCount = 0;
        public static int UpgradesResetCounter = 0;
        public static BuildListVessel LaunchedVessel, EditedVessel, RecoveredVessel;
        public static List<CrewedPart> LaunchedCrew = new List<CrewedPart>();

        public static ToolbarControl ToolbarControl;

        public static bool EditorShipEditingMode = false;
        public static bool IsFirstStart = false;
        public static bool IsSimulatedFlight = false;
        public static double EditorBuildTime = 0;
        public static double EditorIntegrationTime = 0;
        public static double EditorRolloutCosts = 0;
        public static double EditorRolloutTime = 0;
        public static double EditorIntegrationCosts = 0;

        public static Dictionary<string, int> BuildingMaxLevelCache = new Dictionary<string, int>();

        public static List<bool> ShowWindows = new List<bool> { false, true };    //build list, editor
        public static string KACAlarmId = string.Empty;
        public static double KACAlarmUT = 0;

        public static bool ErroredDuringOnLoad = false;

        public static bool VesselErrorAlerted = false;
        public static bool PersistenceLoaded = false;
        public static bool IsRefunding = false;

        public static AirlaunchParams AirlaunchParams;
        public static SimulationParams SimulationParams = new SimulationParams();

        public static void Reset()
        {
            IsFirstStart = false;
            VesselErrorAlerted = false;

            IsSimulatedFlight = false;
            SimulationParams.Reset();

            PurchasedUpgrades = new List<int>() { 0, 0 };
            KCT_GUI.ResetFormulaRateHolders();

            MiscellaneousTempUpgrades = 0;

            BuildingMaxLevelCache.Clear();

            LastUT = 0;

            InitAndClearTechList();
        }

        public static void InitAndClearTechList()
        {
            TechList = new KCTObservableList<TechItem>();
            TechList.Updated += KerbalConstructionTime.Instance.UpdateTechlistIconColor;
        }
      
        public static void ClearVesselEditMode()
        {
            EditorShipEditingMode = false;
            EditedVessel = null;

            InputLockManager.RemoveControlLock("KCTEditExit");
            InputLockManager.RemoveControlLock("KCTEditLoad");
            InputLockManager.RemoveControlLock("KCTEditNew");
            InputLockManager.RemoveControlLock("KCTEditLaunch");
            EditorLogic.fetch?.Unlock("KCTEditorMouseLock");
        }

        public static void CreateNewPad(string padName, int padLevel)
        {
            KCT_LaunchPad lp = ActiveKSC.ActiveLPInstance;

            if (lp.GetUpgradeableFacilityReferences()?[0]?.UpgradeLevels is UpgradeableObject.UpgradeLevel[] padUpgdLvls)
            {
                padLevel = UnityEngine.Mathf.Clamp(padLevel, 1, padUpgdLvls.Length);
                ActiveKSC.LaunchPads.Add(new KCT_LaunchPad(padName, padLevel));
            }
        }

        public static void ClearLaunchpadList()
        {
            ActiveKSC.LaunchPads.Clear();
        }
    }

    public class CrewedPart
    {
        public List<ProtoCrewMember> CrewList { get; set; }
        public uint PartID { get; set; }

        public CrewedPart(uint ID, List<ProtoCrewMember> crew)
        {
            PartID = ID;
            CrewList = crew;
        }

        public CrewedPart FromPart(Part part, List<ProtoCrewMember> crew)
        {
            PartID = part.flightID;
            CrewList = crew;
            return this;
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
