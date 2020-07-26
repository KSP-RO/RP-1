using System.Collections.Generic;
using ToolbarControl_NS;

namespace KerbalConstructionTime
{
    public static class KCTGameStates
    {
        internal const string _modId = "KCT_NS";
        internal const string _modName = "Kerbal Construction Time";

        public static double UT, LastUT = 0;
        public static bool CanWarp = false, WarpInitiated = false;
        public static int LastWarpRate = 0;
        public static KCTSettings Settings = new KCTSettings();

        public static KSCItem ActiveKSC = null;
        public static List<KSCItem> KSCs = new List<KSCItem>();
        public static string ActiveKSCName = string.Empty;
        public static bool UpdateLaunchpadDestructionState = false;
        public static int TechUpgradesTotal = 0;
        public static float SciPointsTotal = -1f;

        public static KCTObservableList<TechItem> TechList;

        public static List<int> PurchasedUpgrades = new List<int>() { 0, 0 };
        public static int MiscellaneousTempUpgrades = 0, LastKnownTechCount = 0;
        public static int UpgradesResetCounter = 0;
        public static BuildListVessel LaunchedVessel, EditedVessel, RecoveredVessel;
        public static List<CrewedPart> LaunchedCrew = new List<CrewedPart>();

        public static ToolbarControl ToolbarControl;

        public static bool EditorShipEditingMode = false;
        public static bool IsFirstStart = false;
        public static IKCTBuildItem TargetedItem = null;
        public static double EditorBuildTime = 0;
        public static double EditorIntegrationTime = 0;
        public static double EditorRolloutCosts = 0;
        public static double EditorRolloutTime = 0;
        public static double EditorIntegrationCosts = 0;
        public static bool LaunchFromTS = false;
        public static List<AvailablePart> ExperimentalParts = new List<AvailablePart>();

        public static Dictionary<string, int> BuildingMaxLevelCache = new Dictionary<string, int>();

        public static List<bool> ShowWindows = new List<bool> { false, true };    //build list, editor
        public static string KACAlarmId = string.Empty;
        public static double KACAlarmUT = 0;

        public static KCTOnLoadError ErroredDuringOnLoad = new KCTOnLoadError();

        public static int TemporaryModAddedUpgradesButReallyWaitForTheAPI = 0;    //Reset when returned to the MainMenu
        public static int PermanentModAddedUpgradesButReallyWaitForTheAPI = 0;    //Saved to the save file

        public static bool VesselErrorAlerted = false;
        public static bool PersistenceLoaded = false;
        public static bool IsRefunding = false;

        public static AirlaunchParams AirlaunchParams;

        public static void Reset()
        {
            IsFirstStart = false;
            VesselErrorAlerted = false;

            PurchasedUpgrades = new List<int>() { 0, 0 };
            TargetedItem = null;
            KCT_GUI.ResetFormulaRateHolders();

            ExperimentalParts.Clear();
            MiscellaneousTempUpgrades = 0;

            BuildingMaxLevelCache.Clear();

            LastUT = 0;

            InitAndClearTechList();
        }

        public static void InitAndClearTechList()
        {
            if (TechList != null)
                TechList.Clear();
            else
                TechList = new KCTObservableList<TechItem>();
            TechList.Updated += KerbalConstructionTime.Instance.UpdateTechlistIconColor;
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
