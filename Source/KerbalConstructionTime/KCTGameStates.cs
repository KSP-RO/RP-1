using System.Collections.Generic;
using ToolbarControl_NS;
using Upgradeables;

namespace KerbalConstructionTime
{
    public static class KCTGameStates
    {
        internal const string _modId = "KCT_NS";
        internal const string _modName = "Kerbal Construction Time";

        public static KCTSettings Settings = new KCTSettings();

        public static KSCItem ActiveKSC = null;
        public static List<KSCItem> KSCs = new List<KSCItem>();
        public static string ActiveKSCName = string.Empty;
        public static float SciPointsTotal = -1f;
        public static bool MergingAvailable;
        public static List<BuildListVessel> MergedVessels = new List<BuildListVessel>();

        public static KCTObservableList<TechItem> TechList = new KCTObservableList<TechItem>();
        public static void UpdateTechTimes()
        {
            for (int j = 0; j < TechList.Count; j++)
                TechList[j].UpdateBuildRate(j);
        }

        //public static List<int> PurchasedUpgrades = new List<int>() { 0, 0 };
        //public static int MiscellaneousTempUpgrades = 0, LastKnownTechCount = 0;
        public static int UnassignedPersonnel = 0;
        public static int Researchers = 0;
        private const double StartingEfficiencyResearchers = 0.25d;
        public static double EfficiencyResearchers = StartingEfficiencyResearchers;
        public static double LastResearchers = 0;
        private const double StartingEfficiencyEngineers = 0.25d;
        public static double EfficiencyEngineers = StartingEfficiencyEngineers;
        public static double LastEngineers = 0;
        public static BuildListVessel LaunchedVessel, EditedVessel, RecoveredVessel;
        public static List<PartCrewAssignment> LaunchedCrew = new List<PartCrewAssignment>();
        public static int LoadedSaveVersion = 0;
        public const int VERSION = 4;

        public static ToolbarControl ToolbarControl;

        public static bool EditorShipEditingMode = false;
        public static bool IsFirstStart = false;
        public static bool StarterLCBuilding = false;
        public static bool HiredStarterApplicants = false;
        public static bool StartedProgram = false;
        public static bool FirstRunNotComplete => !(StarterLCBuilding && HiredStarterApplicants && StartedProgram);
        public static bool IsSimulatedFlight = false;
        public static double EditorBuildPoints = 0;
        public static double EditorShipMass = 0;
        public static UnityEngine.Vector3 EditorShipSize = UnityEngine.Vector3.zero;
        public static bool EditorIsHumanRated = false;
        public static double EditorIntegrationPoints = 0;
        public static double EditorRolloutCosts = 0;
        public static double EditorRolloutTime = 0;
        public static double EditorIntegrationCosts = 0;
        public static double EditorUnlockCosts = 0;
        public static List<string> EditorRequiredTechs = new List<string>();

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
            StarterLCBuilding = false;
            HiredStarterApplicants = false;
            StartedProgram = false;
            VesselErrorAlerted = false;

            IsSimulatedFlight = false;
            SimulationParams.Reset();

            AirlaunchParams = null;

            KCT_GUI.ResetFormulaRateHolders();
            KCT_GUI.ResetShowFirstRunAgain();

            BuildingMaxLevelCache.Clear();

            InitAndClearTechList();

            UnassignedPersonnel = 0;
            Researchers = 0;
            EfficiencyResearchers = StartingEfficiencyResearchers;
            LastResearchers = 0;
            EfficiencyEngineers = StartingEfficiencyEngineers;
            LastEngineers = 0;
            LoadedSaveVersion = 0;
    }

        public static void InitAndClearTechList()
        {
            TechList = new KCTObservableList<TechItem>();
            if (KerbalConstructionTime.Instance != null)    // Can be null/destroyed in the main menu scene
            {
                TechList.Updated += KerbalConstructionTime.Instance.ForceUpdateRndScreen;
            }

            void updated() { KCTEvents.OnRP0MaintenanceChanged.Fire(); }
            TechList.Updated += updated;
        }

        public static void ClearVesselEditMode()
        {
            EditorShipEditingMode = false;
            EditedVessel = null;
            MergedVessels.Clear();

            InputLockManager.RemoveControlLock("KCTEditExit");
            InputLockManager.RemoveControlLock("KCTEditLoad");
            InputLockManager.RemoveControlLock("KCTEditNew");
            InputLockManager.RemoveControlLock("KCTEditLaunch");
            EditorLogic.fetch?.Unlock("KCTEditorMouseLock");
        }

        public static void ClearLaunchpadList()
        {
            ActiveKSC.ActiveLaunchComplexInstance.LaunchPads.Clear();
        }

        public static LCItem FindLCFromID(System.Guid guid)
        {
            foreach (var ksc in KSCs)
                foreach (LCItem lc in ksc.LaunchComplexes)
                    if (lc.ID == guid)
                        return lc;

            return null;
        }

        public static int GetSalaryEngineers()
        {
            double engineers = 0d;
            foreach (var ksc in KCTGameStates.KSCs)
                engineers += GetEffectiveEngineersForSalary(ksc);

            return (int)(engineers * RP0.MaintenanceHandler.Settings.salaryEngineers * RP0.MaintenanceHandler.Instance.MaintenanceCostMult);
        }

        public static double GetEffectiveIntegrationEngineersForSalary(KSCItem ksc)
        {
            double engineers = 0d;
            foreach (var lc in ksc.LaunchComplexes)
                engineers += GetEffectiveEngineersForSalary(lc);
            return engineers + ksc.UnassignedEngineers * PresetManager.Instance.ActivePreset.GeneralSettings.IdleSalaryMult;
        }

        public static double GetEffectiveEngineersForSalary(KSCItem ksc) => GetEffectiveIntegrationEngineersForSalary(ksc);

        public static double GetEffectiveEngineersForSalary(LCItem lc)
        {
            if (lc.IsOperational && lc.Engineers > 0)
            {
                if (lc.IsIdle) // not IsActive because completed rollouts/airlaunches still count
                    return lc.Engineers * PresetManager.Instance.ActivePreset.GeneralSettings.IdleSalaryMult;

                if (lc.IsHumanRated && lc.BuildList.Count > 0 && !lc.BuildList[0].IsHumanRated)
                {
                    int num = System.Math.Min(lc.Engineers, lc.MaxEngineersFor(lc.BuildList[0]));
                    return num * lc.RushSalary + (lc.Engineers - num) * PresetManager.Instance.ActivePreset.GeneralSettings.IdleSalaryMult;
                }

                return lc.Engineers * lc.RushSalary;
            }

            return 0;
        }

        public static int GetSalaryResearchers() => (int)(Researchers * RP0.MaintenanceHandler.Settings.salaryResearchers * RP0.MaintenanceHandler.Instance.MaintenanceCostMult);
        public static int GetTotalSalary() => GetSalaryEngineers() + GetSalaryResearchers();

        public static double GetBudgetDelta(double time)
        {
            // note NetUpkeepPerDay is negative or 0.
            double delta = RP0.MaintenanceHandler.Instance.NetUpkeepPerDay * time / 86400d - GetConstructionCostOverTime(time) - GetRolloutCostOverTime(time);
            delta += RP0.MaintenanceHandler.Instance.GetProgramFunding(time);

            return delta;
        }

        public static double GetConstructionCostOverTime(double time)
        {
            double delta = 0;
            foreach (var ksc in KSCs)
            {
                delta += GetConstructionCostOverTime(time, ksc);
            }
            return delta;
        }

        public static double GetConstructionCostOverTime(double time, KSCItem ksc)
        {
            double delta = 0;
            foreach (var c in ksc.Constructions)
                delta += c.GetConstructionCostOverTime(time);

            return delta;
        }

        public static double GetConstructionCostOverTime(double time, string kscName)
        {
            foreach (var ksc in KSCs)
            {
                if (ksc.KSCName == kscName)
                {
                    return GetConstructionCostOverTime(time, ksc);
                }
            }

            return 0d;
        }

        public static double GetRolloutCostOverTime(double time)
        {
            double delta = 0;
            foreach (var ksc in KSCs)
            {
                delta += GetRolloutCostOverTime(time, ksc);
            }
            return delta;
        }

        public static double GetRolloutCostOverTime(double time, KSCItem ksc)
        {
            double delta = 0;
            foreach (var lc in ksc.LaunchComplexes)
                delta += GetRolloutCostOverTime(time, lc);

            return delta;
        }

        public static double GetRolloutCostOverTime(double time, LCItem lc)
        {
            double delta = 0;
            foreach (var rr in lc.Recon_Rollout)
            {
                if (rr.RRType != ReconRollout.RolloutReconType.Rollout)
                    continue;
                delta += rr.Cost * (1d - rr.Progress / rr.BP);
            }
            foreach (var al in lc.AirlaunchPrep)
            {
                if (al.Direction == AirlaunchPrep.PrepDirection.Mount)
                    delta += al.Cost * (1d - al.Progress / al.BP);
            }

            return delta;
        }

        public static double GetRolloutCostOverTime(double time, string kscName)
        {
            foreach (var ksc in KSCs)
            {
                if (ksc.KSCName == kscName)
                {
                    return GetRolloutCostOverTime(time, ksc);
                }
            }

            return 0d;
        }

        public static int TotalEngineers
        {
            get
            {
                int eng = 0;
                foreach (var ksc in KCTGameStates.KSCs)
                    eng += ksc.Engineers;

                return eng;
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
