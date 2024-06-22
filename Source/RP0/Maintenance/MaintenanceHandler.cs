using System;
using System.Collections.Generic;
using UniLinq;
using RP0.Crew;
using RP0.Programs;
using UnityEngine;
using UnityEngine.Profiling;
using Upgradeables;
using ROUtils;

namespace RP0
{
    public enum FacilityMaintenanceType
    {
        Building,
        LC,
        Hangar,
    }

    [KSPScenario((ScenarioCreationOptions)480, new GameScenes[] { GameScenes.EDITOR, GameScenes.FLIGHT, GameScenes.SPACECENTER, GameScenes.TRACKSTATION })]
    public class MaintenanceHandler : ScenarioModule
    {
        public const double UpdateInterval = 3600d;

        public static MaintenanceHandler Instance { get; private set; } = null;

        private bool _isFirstLoad = true;
        
        public static EventVoid OnRP0MaintenanceChanged = new EventVoid("OnRP0MaintenanceChanged");

        [KSPField(isPersistant = true)]
        public double nextUpdate = -1d;

        [KSPField(isPersistant = true)]
        public double lastUpdate = 0d;

        private double _lastUpdateFixed = 0;

        [KSPField(isPersistant = true)]
        public double lastRepUpdate = 0d;

        [KSPField(isPersistant = true)]
        public MaintenanceGUI.MaintenancePeriod guiSelectedPeriod = MaintenanceGUI.MaintenancePeriod.Day;

        public readonly Dictionary<string, double> IntegrationSalaries = new Dictionary<string, double>();
        public double Researchers => SpaceCenterManagement.Instance.Researchers;

        private bool _wasWarpingHigh = false;
        private int _frameCount = 0;

        #region Component costs

        public readonly Dictionary<SpaceCenterFacility, double> FacilityMaintenanceCosts = new Dictionary<SpaceCenterFacility, double>();
        public readonly SpaceCenterFacility[] FacilitiesForMaintenance = { 
            SpaceCenterFacility.Administration,
            SpaceCenterFacility.AstronautComplex,
            SpaceCenterFacility.MissionControl,
            SpaceCenterFacility.TrackingStation
        };

        public double LCsCostPerDay = 0d;

        public double TrainingUpkeepPerDay = 0d;
        public double NautBaseUpkeepPerDay = 0d;
        public double NautInFlightUpkeepPerDay = 0d;
        public double UpkeepPerDayForDisplay = 0d;

        public double FacilityUpkeepPerDay => FacilityMaintenanceCosts.Values.Sum();

        public double IntegrationSalaryPerDay
        {
            get
            {
                double tmp = 0d;
                foreach (double d in IntegrationSalaries.Values)
                    tmp += d;

                return tmp * Database.SettingsSC.salaryEngineers / 365.25d;
            }
        }

        public double ResearchSalaryPerDay => Researchers * Database.SettingsSC.salaryResearchers / 365.25d;

        public struct SubsidyDetails
        {
            public double minSubsidy, maxSubsidy, maxRep, subsidy;
        }

        public static void FillSubsidyDetails(ref SubsidyDetails details, double ut, double rep)
        {
            const double secsPerYear = 3600 * 24 * 365.25;
            float years = (float)(ut / secsPerYear);
            details.minSubsidy = Database.SettingsSC.subsidyCurve.Evaluate(years);
            details.maxSubsidy = details.minSubsidy * Database.SettingsSC.subsidyMultiplierForMax;
            details.maxRep = (details.maxSubsidy - details.minSubsidy) / Database.SettingsSC.repToSubsidyConversion;
            double invLerp = UtilMath.InverseLerp(0, details.maxRep, UtilMath.Clamp(rep, 0, details.maxRep));
            details.subsidy = UtilMath.LerpUnclamped(details.minSubsidy, details.maxSubsidy, invLerp);
            //RP0Debug.Log($"$$$$ years {years}: minSub: {details.minSubsidy}, conversion {Database.SettingsSC.repToSubsidyConversion}, maxSub {Database.SettingsSC.subsidyMultiplierForMax}, maxRep {details.maxRep}, invLerp {invLerp}, subsidy {details.subsidy}=>{(details.subsidy * (1d / 365.25d))}");
        }

        public static double GetYearlySubsidyAtTimeDelta(double deltaTime)
        {
            double days;
            double rep = Reputation.CurrentRep;
            if (deltaTime > 0)
            {
                days = Math.Floor(deltaTime / 86400d);
                if (days > 0)
                {
                    double loss = rep * (1d - Math.Pow(1d - Database.SettingsSC.repPortionLostPerDay, days));
                    rep += CurrencyUtils.Rep(TransactionReasonsRP0.DailyRepDecline, -loss);
                }
            }

            return GetYearlySubsidyAtTimeDelta(deltaTime, rep);
        }

        public static double GetYearlySubsidyAtTimeDelta(double deltaTime, double rep)
        {
            return GetYearlySubsidyAtTime(Planetarium.GetUniversalTime() + deltaTime, rep);
        }

        public static double GetYearlySubsidyAtTime(double ut, double rep)
        {
            SubsidyDetails details = new SubsidyDetails();
            FillSubsidyDetails(ref details, ut, rep);
            return details.subsidy;
        }

        public static double GetAverageSubsidyForPeriod(double deltaTime, int steps = 0)
        {
            // default is 1 step per month (plus the zeroeth step)
            if (steps == 0)
                steps = 1 + (int)(deltaTime / (86400d * 29.999d));

            double subsidy = GetYearlySubsidyAtTimeDelta(0d);
            if (steps <= 1)
                return subsidy;

            double deltaPerStep = deltaTime / (steps - 1);
            double ut = 0d;
            for (int i = 1; i < steps; ++i)
            {
                ut += deltaPerStep;
                subsidy += GetYearlySubsidyAtTimeDelta(ut);
            }
            return subsidy / steps;
        }

        #endregion

        #region Overrides and Monobehaviour methods

        public override void OnAwake()
        {
            if (Instance != null)
            {
                Destroy(Instance);
            }
            Instance = this;

            GameEvents.OnGameSettingsApplied.Add(SettingsChanged);
            GameEvents.onVesselRecoveryProcessingComplete.Add(onVesselRecoveryProcessingComplete);
            GameEvents.onKerbalInactiveChange.Add(onKerbalInactiveChange);
            GameEvents.onKerbalStatusChange.Add(onKerbalStatusChange);
            GameEvents.onKerbalTypeChanged.Add(onKerbalTypeChanged);
        }

        public void Start()
        {
            OnRP0MaintenanceChanged.Add(ScheduleMaintenanceUpdate);
        }

        public void ScheduleMaintenanceUpdate()
        {
            nextUpdate = 0d;
        }

        private void UpdateKCTSalaries()
        {
            Profiler.BeginSample("RP0Maintenance UpdateKCTSalaries");
            IntegrationSalaries.Clear();
            foreach (LCSpaceCenter ksc in SpaceCenterManagement.Instance.KSCs)
            {
                IntegrationSalaries[ksc.KSCName] = SpaceCenterManagement.Instance.GetEffectiveIntegrationEngineersForSalary(ksc);
            }
            Profiler.EndSample();
        }

        private IEnumerable<FlightLog.Entry> ProficiencyEntries(ProtoCrewMember pcm)
        {
            foreach (var e in pcm.flightLog.entries)
            {
                if (e.type == CrewHandler.TrainingType_Proficiency)
                    yield return e;
            }
            foreach (var e in pcm.careerLog.entries)
            {
                if (e.type == CrewHandler.TrainingType_Proficiency)
                    yield return e;
            }
        }

        private double GetTrainingCostFromBools()
        {
            double yearlyCost = 0d;
            for (int i = Database.SettingsSC.nautUpkeepTrainingBools.Count; i-- > 0;)
            {
                if (Database.SettingsSC.nautUpkeepTrainingBools[i])
                    yearlyCost += Database.SettingsSC.nautYearlyUpkeepPerTraining[Database.SettingsSC.nautUpkeepTrainings[i]];
            }

            return yearlyCost;
        }

        private double GetNautUpkeepFromProficiency(ProtoCrewMember pcm)
        {
            double yearlyCost = 0d;

            foreach (var e in ProficiencyEntries(pcm))
            {
                TrainingDatabase.FillBools(e.target, Database.SettingsSC.nautUpkeepTrainings, Database.SettingsSC.nautUpkeepTrainingBools);
                // Early-out if we have a lot of proficiency training. Note we process recent-first so this is a good trick.
                if (Database.SettingsSC.nautUpkeepTrainingBools.AllTrue())
                    break;
            }

            yearlyCost = GetTrainingCostFromBools();
            Database.SettingsSC.ResetBools();
            return yearlyCost;
        }

        public void GetNautCost(ProtoCrewMember k, out double baseCostPerDay, out double flightCostPerDay)
        {
            flightCostPerDay = 0d;
            baseCostPerDay = Database.SettingsSC.nautYearlyUpkeepPerFacLevel[KCTUtilities.GetFacilityLevel(SpaceCenterFacility.AstronautComplex)];
            if (k.rosterStatus == ProtoCrewMember.RosterStatus.Assigned)
            {
                flightCostPerDay = Database.SettingsSC.nautInFlightDailyRate;
            }
            else
            {
                baseCostPerDay += GetNautUpkeepFromProficiency(k);

                // Note: nauts in training are also inactive, but that's taken care of by the training cost.
                if (k.inactive)
                {
                    baseCostPerDay *= Database.SettingsSC.nautInactiveMult;
                }
            }

            baseCostPerDay /= 365.25d;
        }

        public double ComputeDailyMaintenanceCost(double cost, FacilityMaintenanceType facilityType = FacilityMaintenanceType.Building)
        {
            if (facilityType == FacilityMaintenanceType.Hangar)
                cost = Math.Max(Database.SettingsSC.hangarCostForMaintenanceMin, cost - Database.SettingsSC.hangarCostForMaintenanceOffset);

            double upkeep = Database.SettingsSC.facilityLevelCostMult * Math.Pow(cost, Database.SettingsSC.facilityLevelCostPow);

            if (facilityType == FacilityMaintenanceType.LC)
                upkeep *= Database.SettingsSC.lcCostMultiplier;

            return upkeep;
        }

        private double LCUpkeep(LaunchComplex lc, int padCount)
        {
            switch (lc.LCType)
            {
                case LaunchComplexType.Hangar:
                    return ComputeDailyMaintenanceCost(lc.Stats.GetCostStats(out _, out _, out _), FacilityMaintenanceType.Hangar);

                case LaunchComplexType.Pad:
                    lc.Stats.GetCostStats(out double padCost, out double vabCost, out double resCost);
                    return ComputeDailyMaintenanceCost((vabCost + resCost + padCount * padCost), FacilityMaintenanceType.LC);
            }
            return 0d;
        }

        private double LCUpkeep(LCData lcData, int padCount)
        {
            switch (lcData.lcType)
            {
                case LaunchComplexType.Hangar:
                    return ComputeDailyMaintenanceCost(lcData.GetCostStats(out _, out _, out _), FacilityMaintenanceType.Hangar);

                case LaunchComplexType.Pad:
                    lcData.GetCostStats(out double padCost, out double vabCost, out double resCost);
                    return ComputeDailyMaintenanceCost((vabCost + resCost + padCount * padCost), FacilityMaintenanceType.LC);
            }
            return 0d;
        }

        public double LCUpkeep(LaunchComplex lc)
        {
            if (!lc.IsOperational)
            {
                // find LCConstruction
                foreach (var lcc in lc.KSC.LCConstructions)
                {
                    if (lcc.lcID != lc.ID)
                        return lcc.progress / lcc.BP * LCUpkeep(lcc.lcData, lc.LaunchPadCount);
                }
                return 0d;
            }

            return LCUpkeep(lc, lc.LaunchPadCount);
        }

        private double GetFacilityUpgradeRatio(SpaceCenterFacility facility)
        {
            foreach (var fac in SpaceCenterManagement.Instance.ActiveSC.FacilityUpgrades)
                if (fac.FacilityType == facility)
                    return fac.progress / fac.BP;

            return 0d;
        }

        public void UpdateUpkeep()
        {
            Profiler.BeginSample("RP0Maintenance UpdateUpkeep");

            _isFirstLoad = false;

            UpdateKCTSalaries();

            LCsCostPerDay = 0d;
            foreach (var ksc in SpaceCenterManagement.Instance.KSCs)
            {
                foreach (var lc in ksc.LaunchComplexes)
                    LCsCostPerDay += LCUpkeep(lc);
            }

            foreach (SpaceCenterFacility facility in FacilitiesForMaintenance)
            {
                if (Database.LockedFacilities.Contains(facility))
                    continue;

                if (!Database.FacilityLevelCosts.TryGetValue(facility, out var facCosts))
                    continue;

                double cost = ComputeDailyMaintenanceCost(MathUtils.SumThrough(facCosts, KCTUtilities.GetFacilityLevel(facility)));
                double ratio = GetFacilityUpgradeRatio(facility);
                if (ratio > 0d)
                {
                    double newCost = ComputeDailyMaintenanceCost(MathUtils.SumThrough(facCosts, 1 + KCTUtilities.GetFacilityLevel(facility)));
                    cost = UtilMath.LerpUnclamped(cost, newCost, ratio);
                }
                FacilityMaintenanceCosts[facility] = cost;
            }

            

            TrainingUpkeepPerDay = 0d;
            if (CrewHandler.Instance?.TrainingCourses != null)
            {
                foreach (var course in CrewHandler.Instance.TrainingCourses)
                {
                    if (!course.Started)
                        continue;

                    TrainingDatabase.FillBools(course.Target, Database.SettingsSC.nautUpkeepTrainings, Database.SettingsSC.nautUpkeepTrainingBools);
                    double trainingTypeCost = GetTrainingCostFromBools();
                    Database.SettingsSC.ResetBools();
                    TrainingUpkeepPerDay += course.Students.Count *
                        (Database.SettingsSC.nautTrainingCostPerFacLevel[KCTUtilities.GetFacilityLevel(SpaceCenterFacility.AstronautComplex)]
                        + trainingTypeCost * Database.SettingsSC.nautTrainingTypeCostMult);
                }
                TrainingUpkeepPerDay /= 365.25d;
            }

            
            NautBaseUpkeepPerDay = 0d;
            NautInFlightUpkeepPerDay = 0d;
            for (int i = HighLogic.CurrentGame.CrewRoster.Count; i-- > 0;)
            {
                var k = HighLogic.CurrentGame.CrewRoster[i];
                if (k.rosterStatus == ProtoCrewMember.RosterStatus.Dead || k.rosterStatus == ProtoCrewMember.RosterStatus.Missing ||
                    k.type != ProtoCrewMember.KerbalType.Crew)
                {
                    continue;
                }

                GetNautCost(k, out double baseCost, out double flightCost);
                NautBaseUpkeepPerDay += baseCost;
                NautInFlightUpkeepPerDay += flightCost;
            }

            UpkeepPerDayForDisplay = CurrencyUtils.Funds(TransactionReasonsRP0.StructureRepair, -FacilityUpkeepPerDay)
                + CurrencyUtils.Funds(TransactionReasonsRP0.StructureRepairLC, -LCsCostPerDay)
                + CurrencyUtils.Funds(TransactionReasonsRP0.SalaryEngineers, -IntegrationSalaryPerDay)
                + CurrencyUtils.Funds(TransactionReasonsRP0.SalaryResearchers, -ResearchSalaryPerDay)
                + CurrencyUtils.Funds(TransactionReasonsRP0.SalaryCrew, -NautBaseUpkeepPerDay - NautInFlightUpkeepPerDay)
                + CurrencyUtils.Funds(TransactionReasonsRP0.CrewTraining, -TrainingUpkeepPerDay);
            Profiler.EndSample();
        }

        public void Update()
        {
            if (HighLogic.CurrentGame == null)
                return;

            if (_frameCount++ < 2)
            {
                return;
            }

            double time = Planetarium.GetUniversalTime();
            if (nextUpdate > time && !_isFirstLoad)
            {
                if (_wasWarpingHigh && TimeWarp.CurrentRate <= 100f)
                    _wasWarpingHigh = false;
                else
                    return;
            }

            UpdateUpkeep();

            lastUpdate = time;

            while (lastRepUpdate < time)
            {
                if (lastRepUpdate == 0d)
                    lastRepUpdate = time;

                lastRepUpdate += 3600d * 24d;
                Reputation.Instance.AddReputation((float)(Reputation.Instance.reputation * -Database.SettingsSC.repPortionLostPerDay), TransactionReasonsRP0.DailyRepDecline.Stock());
            }

            if (TimeWarp.CurrentRate <= 100f)
            {
                _wasWarpingHigh = false;
                nextUpdate = time + UpdateInterval;
            }
            else
            {
                _wasWarpingHigh = true;
                // Scale the update interval up with timewarp but don't allow longer than 1 day steps
                nextUpdate = time + Math.Min(3600 * 24, UpdateInterval * TimeWarp.CurrentRate / 100f);
            }
        }

        public void FixedUpdate()
        {
            double UT = Planetarium.GetUniversalTime();
            if (_lastUpdateFixed == 0)
                _lastUpdateFixed = UT;

            double UTDiff = UT - _lastUpdateFixed;

            // Wait until first UpdateUpkeep runs
            if (_isFirstLoad)
                return;

            if (UTDiff < 1d)
                return;

            _lastUpdateFixed = UT;

            // First process crew
            CrewHandler.Instance.Process(UTDiff);

            // Best to deduct maintenance fees and add program funding at the same time.
            // First, increment funds from programs.
            ProgramHandler.Instance.ProcessFunding();

            // Now, handle maintenance.
            double timeFactor = UTDiff * (1d / 86400d);

            // for now, RSS max timewarp means only 1.4 days per fixed frame, so it's not worth doing the above.
            double subsidyForPassedTime = timeFactor * GetYearlySubsidyAtTimeDelta(0d) * (1d / 365.25d);

            // We have to do some weird logic here. We have to get the resultant upkeep first,
            // then add the subsidy, then actually subtract the upkeep.
            // This is because we have to subtract piecemeal.

            double facilityMaintenance = CurrencyUtils.Funds(TransactionReasonsRP0.StructureRepair, -FacilityUpkeepPerDay) * timeFactor;
            double lcMaintenance = CurrencyUtils.Funds(TransactionReasonsRP0.StructureRepairLC, -LCsCostPerDay) * timeFactor;
            double salaryEngineers = CurrencyUtils.Funds(TransactionReasonsRP0.SalaryEngineers, -IntegrationSalaryPerDay) * timeFactor;
            double salaryResearchers = CurrencyUtils.Funds(TransactionReasonsRP0.SalaryResearchers, -ResearchSalaryPerDay) * timeFactor;
            double salaryCrew = CurrencyUtils.Funds(TransactionReasonsRP0.SalaryCrew, -NautBaseUpkeepPerDay - NautInFlightUpkeepPerDay) * timeFactor;
            double crewTraining = CurrencyUtils.Funds(TransactionReasonsRP0.CrewTraining, -TrainingUpkeepPerDay) * timeFactor;
            double totalUpkeep = facilityMaintenance + lcMaintenance + salaryEngineers + salaryResearchers + salaryCrew + crewTraining;

            LogPeriod logPeriod = CareerLog.Instance?.CurrentPeriod;
            if (logPeriod != null)
            {
                logPeriod.FacilityMaintenance -= facilityMaintenance;
                logPeriod.LCMaintenance -= lcMaintenance;
                logPeriod.SalaryEngineers -= salaryEngineers;
                logPeriod.SalaryResearchers -= salaryResearchers;
                logPeriod.SalaryCrew -= salaryCrew;
                logPeriod.TrainingFees -= crewTraining;
            }

            using (new CareerEventScope(CareerEventType.Maintenance))   // TODO: remove this scope at a later date since all the components are now logged separately
            {
                double fundsOld = Funding.Instance.Funds;
                // We have to add subsidy first, to be sure we have enough funds.
                double netSubsidy = Math.Min(CurrencyUtils.Funds(TransactionReasonsRP0.Subsidy, subsidyForPassedTime), -totalUpkeep);
                if (netSubsidy > 0)
                {
                    Funding.Instance.AddFunds(subsidyForPassedTime, TransactionReasonsRP0.Subsidy.Stock());
                    if (logPeriod != null)
                        logPeriod.SubsidyPaidOut += netSubsidy;
                    double overshoot = (Funding.Instance.Funds - fundsOld) - netSubsidy;
                    if (overshoot > 0)
                        Funding.Instance.AddFunds(-overshoot, TransactionReasons.None);
                }

                double preMaint = Funding.Instance.Funds;
                Funding.Instance.AddFunds(-FacilityUpkeepPerDay * timeFactor, TransactionReasons.StructureRepair);
                Funding.Instance.AddFunds(-LCsCostPerDay * timeFactor, TransactionReasonsRP0.StructureRepairLC.Stock());
                Funding.Instance.AddFunds(-IntegrationSalaryPerDay * timeFactor, TransactionReasonsRP0.SalaryEngineers.Stock());
                Funding.Instance.AddFunds(-ResearchSalaryPerDay * timeFactor, TransactionReasonsRP0.SalaryResearchers.Stock());
                Funding.Instance.AddFunds(-(NautBaseUpkeepPerDay + NautInFlightUpkeepPerDay) * timeFactor, TransactionReasonsRP0.SalaryCrew.Stock());
                Funding.Instance.AddFunds(-TrainingUpkeepPerDay * timeFactor, TransactionReasonsRP0.CrewTraining.Stock());
                //RP0Debug.Log($"MaintenanceHandler removing {(-totalUpkeep - netSubsidy)} funds where upkeep is {-totalUpkeep / timeFactor} ({(preMaint - Funding.Instance.Funds)} for period) and subsidy {MaintenanceSubsidyPerDay} ({subsidyForPassedTime} for period). Delta = {(Funding.Instance.Funds - fundsOld)}");
                //double delta = fundsOld + totalUpkeep + netSubsidy - Funding.Instance.Funds;
                //if (Math.Abs(delta) > 0.1)
                //    RP0Debug.LogError($"$$$$ Error! Fund mismatch from prediction in maintenance! Prediction:\nMaintenance: {totalUpkeep}\n Subsidy: {netSubsidy} subsidy\nTotal: {totalUpkeep + netSubsidy}\nbut real delta: {Funding.Instance.Funds - fundsOld} (diff {delta})");
            }

            // Finally, update all builds
            SpaceCenterManagement.Instance.ProgressBuildTime(UTDiff);
        }

        public void OnDestroy()
        {
            GameEvents.OnGameSettingsApplied.Remove(SettingsChanged);
            GameEvents.onVesselRecoveryProcessingComplete.Remove(onVesselRecoveryProcessingComplete);
            GameEvents.onKerbalInactiveChange.Remove(onKerbalInactiveChange);
            GameEvents.onKerbalStatusChange.Remove(onKerbalStatusChange);
            GameEvents.onKerbalTypeChanged.Remove(onKerbalTypeChanged);

            OnRP0MaintenanceChanged.Remove(ScheduleMaintenanceUpdate);
        }

        #endregion

        private void SettingsChanged()
        {
            UpdateUpkeep();
        }

        public double GetSubsidyAmount(double startUT, double endUT)
        {
            double delta = endUT - startUT;
            if (delta == 0d)
                return 0d;

            double startSubsidy = GetYearlySubsidyAtTime(startUT, Reputation.CurrentRep);
            double endSubsidy = GetYearlySubsidyAtTime(endUT, Reputation.CurrentRep);

            return (endUT - startUT) * ((startSubsidy + endSubsidy) * 0.5d) / 365.25d / 86400d;
        }

        private void onVesselRecoveryProcessingComplete(ProtoVessel pv, KSP.UI.Screens.MissionRecoveryDialog mrd, float x)
        {
            if (pv.GetVesselCrew().Count > 0)
                ScheduleMaintenanceUpdate();
        }

        private void onKerbalTypeChanged(ProtoCrewMember pcm, ProtoCrewMember.KerbalType from, ProtoCrewMember.KerbalType to)
        {
            if (from != to && (from == ProtoCrewMember.KerbalType.Crew || to == ProtoCrewMember.KerbalType.Crew))
                ScheduleMaintenanceUpdate();
        }

        private void onKerbalStatusChange(ProtoCrewMember pcm, ProtoCrewMember.RosterStatus from, ProtoCrewMember.RosterStatus to)
        {
            if (from != to)
                ScheduleMaintenanceUpdate();
        }

        private void onKerbalInactiveChange(ProtoCrewMember pcm, bool from, bool to)
        {
            if (from != to)
                ScheduleMaintenanceUpdate();
        }
    }
}
