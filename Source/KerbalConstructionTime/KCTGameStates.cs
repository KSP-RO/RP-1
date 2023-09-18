using System.Collections.Generic;
using ToolbarControl_NS;
using Upgradeables;
using RP0.DataTypes;

namespace KerbalConstructionTime
{
    public static class KCTGameStates
    {
        internal const string _modId = "KCT_NS";
        internal const string _modName = "Kerbal Construction Time";

        public static KCTSettings Settings = new KCTSettings();

        public static KSCItem ActiveKSC => KerbalConstructionTimeData.Instance?.ActiveKSC ?? null;
        private static readonly List<KSCItem> _EmptyKSCs = new List<KSCItem>();
        public static List<KSCItem> KSCs => KerbalConstructionTimeData.Instance?.KSCs ?? _EmptyKSCs;
        
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
            KerbalConstructionTimeData.Instance.EditedVessel = new BuildListVessel();
            KerbalConstructionTimeData.Instance.MergedVessels.Clear();

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
            return KerbalConstructionTimeData.Instance.LC(guid);
        }

        public static void RecalculateBuildRates()
        {
            LCEfficiency.RecalculateConstants();

            foreach (var ksc in KSCs)
                ksc.RecalculateBuildRates(true);

            for (int i = KerbalConstructionTimeData.Instance.TechList.Count; i-- > 0;)
            {
                TechItem tech = KerbalConstructionTimeData.Instance.TechList[i];
                tech.UpdateBuildRate(i);
            }

            RP0.Crew.CrewHandler.Instance?.RecalculateBuildRates();

            KCTEvents.OnRecalculateBuildRates.Fire();
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

                if (lc.IsHumanRated && lc.BuildList.Count > 0 && !lc.BuildList[0].humanRated)
                {
                    int num = System.Math.Min(lc.Engineers, lc.MaxEngineersFor(lc.BuildList[0]));
                    return num * lc.RushSalary + (lc.Engineers - num) * PresetManager.Instance.ActivePreset.GeneralSettings.IdleSalaryMult;
                }

                return lc.Engineers * lc.RushSalary;
            }

            return 0;
        }

        public static double GetBudgetDelta(double deltaTime)
        {
            // note NetUpkeepPerDay is negative or 0.
            
            double averageSubsidyPerDay = RP0.CurrencyUtils.Funds(RP0.TransactionReasonsRP0.Subsidy, RP0.MaintenanceHandler.GetAverageSubsidyForPeriod(deltaTime)) * (1d / 365.25d);
            double fundDelta = System.Math.Min(0d, RP0.MaintenanceHandler.Instance.UpkeepPerDayForDisplay + averageSubsidyPerDay) * deltaTime * (1d / 86400d)
                + GetConstructionCostOverTime(deltaTime) + GetRolloutCostOverTime(deltaTime) + GetAirlaunchCostOverTime(deltaTime)
                + RP0.Programs.ProgramHandler.Instance.GetDisplayProgramFunding(deltaTime);

            return fundDelta;
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
            for(int i = 1; i < ksc.LaunchComplexes.Count; ++i)
                delta += GetRolloutCostOverTime(time, ksc.LaunchComplexes[i]);

            return delta;
        }

        public static double GetRolloutCostOverTime(double time, LCItem lc)
        {
            double delta = 0;
            foreach (var rr in lc.Recon_Rollout)
            {
                if (rr.RRType != ReconRollout.RolloutReconType.Rollout)
                    continue;
                
                double t = rr.GetTimeLeft();
                double fac = 1d;
                if (t > time)
                    fac = time / t;

                delta += RP0.CurrencyUtils.Funds(RP0.TransactionReasonsRP0.RocketRollout, -rr.cost * (1d - rr.progress / rr.BP) * fac);
            }

            return delta;
        }

        public static double GetAirlaunchCostOverTime(double time)
        {
            double delta = 0;
            foreach (var ksc in KSCs)
            {
                delta += GetAirlaunchCostOverTime(time, ksc);
            }
            return delta;
        }

        public static double GetAirlaunchCostOverTime(double time, KSCItem ksc)
        {
            double delta = 0;
            foreach (var al in ksc.Hangar.Airlaunch_Prep)
            {
                if (al.direction == AirlaunchPrep.PrepDirection.Mount)
                {
                    double t = al.GetTimeLeft();
                    double fac = 1d;
                    if (t > time)
                        fac = time / t;

                    delta += RP0.CurrencyUtils.Funds(RP0.TransactionReasonsRP0.AirLaunchRollout, -al.cost * (1d - al.progress / al.BP) * fac);
                }
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
                foreach (var ksc in KSCs)
                    eng += ksc.Engineers;

                return eng;
            }
        }

        public static double WeightedAverageEfficiencyEngineers
        {
            get
            {
                double effic = 0d;
                int engineers = 0;
                foreach (var ksc in KSCs)
                {
                    foreach (var lc in ksc.LaunchComplexes)
                    {
                        if (!lc.IsOperational || lc.LCType == LaunchComplexType.Hangar)
                            continue;

                        if (lc.Engineers == 0d)
                            continue;

                        engineers += lc.Engineers;
                        effic += lc.Efficiency * engineers;
                    }
                }
                
                if (engineers == 0)
                    return 0d;

                return effic / engineers;
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
