using System.Collections.Generic;
using ToolbarControl_NS;

namespace RP0
{
    public static class KCTGameStates
    {
        internal const string _modId = "KCT_NS";
        internal const string _modName = "Kerbal Construction Time";

        public static KCTSettings Settings = new KCTSettings();
        
        public const int VERSION = 4;

        public static ToolbarControl ToolbarControl;

        public static bool EditorShipEditingMode = false;
        public static bool IsFirstStart = false;
        public static double EditorRolloutCost = 0;
        public static double EditorRolloutBP = 0;
        public static double EditorUnlockCosts = 0;
        public static double EditorToolingCosts = 0;
        public static List<string> EditorRequiredTechs = new List<string>();

        public static Dictionary<string, int> BuildingMaxLevelCache = new Dictionary<string, int>();

        public static List<bool> ShowWindows = new List<bool> { false, true };    //build list, editor
        public static string KACAlarmId = string.Empty;
        public static double KACAlarmUT = 0;

        public static bool ErroredDuringOnLoad = false;

        public static bool VesselErrorAlerted = false;
        public static bool IsRefunding = false;

        public static void Reset()
        {
            IsFirstStart = false;
            VesselErrorAlerted = false;

            KCT_GUI.ResetFormulaRateHolders();
            KCT_GUI.ResetShowFirstRunAgain();

            BuildingMaxLevelCache.Clear();
        }

        public static void ClearVesselEditMode()
        {
            EditorShipEditingMode = false;
            KerbalConstructionTimeData.Instance.EditedVessel = new VesselProject();
            KerbalConstructionTimeData.Instance.MergedVessels.Clear();

            InputLockManager.RemoveControlLock("KCTEditExit");
            InputLockManager.RemoveControlLock("KCTEditLoad");
            InputLockManager.RemoveControlLock("KCTEditNew");
            InputLockManager.RemoveControlLock("KCTEditLaunch");
            EditorLogic.fetch?.Unlock("KCTEditorMouseLock");
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
