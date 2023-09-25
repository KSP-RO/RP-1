﻿using System;
using System.Collections.Generic;
using UniLinq;
using KerbalConstructionTime;
using RP0.Crew;
using RP0.Programs;
using UnityEngine;
using UnityEngine.Profiling;
using Upgradeables;
using System.Collections;

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
        private static MaintenanceSettings _settings = null;
        public static MaintenanceSettings Settings
        {
            get
            {
                if (_settings == null)
                {
                    _settings = new MaintenanceSettings();
                    foreach (ConfigNode n in GameDatabase.Instance.GetConfigNodes("MAINTENANCESETTINGS"))
                        ConfigNode.LoadObjectFromConfig(Settings, n);
                }
                return _settings;
            }
        }

        private bool _isFirstLoad = true;
        private static readonly Dictionary<SpaceCenterFacility, float[]> _facilityLevelCosts = new Dictionary<SpaceCenterFacility, float[]>();
        public static void ClearFacilityCosts() { _facilityLevelCosts.Clear(); }

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
        public double Researchers => KerbalConstructionTimeData.Instance.Researchers;

        private bool _wasWarpingHigh = false;
        private int _frameCount = 0;
        private bool _waitingForLevelLoad = false;

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

                return tmp * Settings.salaryEngineers / 365.25d;
            }
        }

        public double ResearchSalaryPerDay => Researchers * Settings.salaryResearchers / 365.25d;

        public struct SubsidyDetails
        {
            public double minSubsidy, maxSubsidy, maxRep, subsidy;
        }

        public static void FillSubsidyDetails(ref SubsidyDetails details, double ut, double rep)
        {
            const double secsPerYear = 3600 * 24 * 365.25;
            float years = (float)(ut / secsPerYear);
            details.minSubsidy = Settings.subsidyCurve.Evaluate(years);
            details.maxSubsidy = details.minSubsidy * Settings.subsidyMultiplierForMax;
            details.maxRep = (details.maxSubsidy - details.minSubsidy) / Settings.repToSubsidyConversion;
            double invLerp = UtilMath.InverseLerp(0, details.maxRep, UtilMath.Clamp(rep, 0, details.maxRep));
            details.subsidy = UtilMath.LerpUnclamped(details.minSubsidy, details.maxSubsidy, invLerp);
            //Debug.Log($"$$$$ years {years}: minSub: {details.minSubsidy}, conversion {Settings.repToSubsidyConversion}, maxSub {Settings.subsidyMultiplierForMax}, maxRep {details.maxRep}, invLerp {invLerp}, subsidy {details.subsidy}=>{(details.subsidy * (1d / 365.25d))}");
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
                    double loss = rep * (1d - Math.Pow(1d - Settings.repPortionLostPerDay, days));
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

        protected double SumCosts(float[] costs, int idx)
        {
            double s = 0d;
            for (int i = idx + 1; i-- > 0;)
                s += costs[i];

            return s;
        }

        protected double SumCosts(float[] costs, float fractionalIdx)
        {
            double s = 0d;
            int i = (int)fractionalIdx;
            if (i != fractionalIdx && i + 1 < costs.Length)
            {
                float fractionOverFullLvl = fractionalIdx - i;
                float fractionCost = costs[i + 1] * fractionOverFullLvl;
                s = fractionCost;
            }

            for (; i >= 0; i--)
                s += costs[i];

            return s;
        }

        public void ScheduleMaintenanceUpdate()
        {
            nextUpdate = 0d;
        }

        private void UpdateKCTSalaries()
        {
            Profiler.BeginSample("RP0Maintenance UpdateKCTSalaries");
            IntegrationSalaries.Clear();
            foreach (KSCItem ksc in KCTGameStates.KSCs)
            {
                IntegrationSalaries[ksc.KSCName] = KCTGameStates.GetEffectiveIntegrationEngineersForSalary(ksc);
            }
            Profiler.EndSample();
        }

        private double GetNautUpkeepFromTraining(FlightLog log)
        {
            bool foundOrbit = false;
            bool foundSubOrb = false;
            double baseCostPerDay = 0d;
            for (int j = log.Count; j-- > 0;)
            {
                var e = log[j];
                if (!foundOrbit && e.type == CrewHandler.TrainingType_Proficiency && TrainingDatabase.HasName(e.target, "Orbital"))
                {
                    baseCostPerDay += Settings.nautOrbitProficiencyUpkeepAdd;
                    foundOrbit = true;
                }
                if (!foundSubOrb && e.type == CrewHandler.TrainingType_Proficiency && TrainingDatabase.HasName(e.target, "Suborbital"))
                {
                    baseCostPerDay += Settings.nautSubOrbitProficiencyUpkeepAdd;
                    foundOrbit = true;
                }
            }
            return baseCostPerDay;
        }

        public void GetNautCost(ProtoCrewMember k, out double baseCostPerDay, out double flightCostPerDay)
        {
            flightCostPerDay = 0d;
            baseCostPerDay = Settings.nautYearlyUpkeepBase + ScenarioUpgradeableFacilities.GetFacilityLevel(SpaceCenterFacility.AstronautComplex) * Settings.nautYearlyUpkeepAdd;
            if (k.rosterStatus == ProtoCrewMember.RosterStatus.Assigned)
            {
                flightCostPerDay = Settings.nautInFlightDailyRate;
            }
            else
            {
                baseCostPerDay += GetNautUpkeepFromTraining(k.flightLog) + GetNautUpkeepFromTraining(k.careerLog);

                if (k.inactive)
                {
                    baseCostPerDay *= Settings.nautInactiveMult;
                }
            }

            baseCostPerDay /= 365.25d;
        }

        public double ComputeDailyMaintenanceCost(double cost, FacilityMaintenanceType facilityType = FacilityMaintenanceType.Building)
        {
            if (facilityType == FacilityMaintenanceType.Hangar)
                cost = Math.Max(Settings.hangarCostForMaintenanceMin, cost - Settings.hangarCostForMaintenanceOffset);

            double upkeep = Settings.facilityLevelCostMult * Math.Pow(cost, Settings.facilityLevelCostPow);

            if (facilityType == FacilityMaintenanceType.LC)
                upkeep *= Settings.lcCostMultiplier;

            return upkeep;
        }

        private double LCUpkeep(LCItem lc, int padCount)
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

        public double LCUpkeep(LCItem lc)
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
            foreach (var fac in KCTGameStates.ActiveKSC.FacilityUpgrades)
                if (fac.FacilityType == facility)
                    return fac.progress / fac.BP;

            return 0d;
        }

        public void UpdateUpkeep()
        {
            Profiler.BeginSample("RP0Maintenance UpdateUpkeep");

            _isFirstLoad = false;

            UpdateKCTSalaries();

            EnsureFacilityLvlCostsLoaded();

            LCsCostPerDay = 0d;
            foreach (var ksc in KCTGameStates.KSCs)
            {
                foreach (var lc in ksc.LaunchComplexes)
                    LCsCostPerDay += LCUpkeep(lc);
            }

            foreach (SpaceCenterFacility facility in FacilitiesForMaintenance)
            {
                if (Database.LockedFacilities.Contains(facility))
                    continue;

                if (!_facilityLevelCosts.TryGetValue(facility, out float[] facCosts))
                    continue;

                double cost = ComputeDailyMaintenanceCost(SumCosts(facCosts, (int)(ScenarioUpgradeableFacilities.GetFacilityLevel(facility) * (facCosts.Length - 0.95f))));
                double ratio = GetFacilityUpgradeRatio(facility);
                if (ratio > 0d)
                {
                    double newCost = ComputeDailyMaintenanceCost(SumCosts(facCosts, 1 + (int)(ScenarioUpgradeableFacilities.GetFacilityLevel(facility) * (facCosts.Length - 0.95f))));
                    cost = UtilMath.LerpUnclamped(cost, newCost, ratio);
                }
                FacilityMaintenanceCosts[facility] = cost;
            }

            

            TrainingUpkeepPerDay = 0d;
            if (_facilityLevelCosts.TryGetValue(SpaceCenterFacility.AstronautComplex, out float[] costs))
            {
                float lvl = ScenarioUpgradeableFacilities.GetFacilityLevel(SpaceCenterFacility.AstronautComplex);
                int lvlInt = (int)(lvl * (costs.Length - 0.95f));
                if (CrewHandler.Instance?.TrainingCourses != null)
                {
                    double mult = 1d + Math.Pow((double)lvlInt, Settings.nautTrainingACLevelPow) * Settings.nautTrainingACLevelMult;
                    foreach (var course in CrewHandler.Instance.TrainingCourses)
                    {
                        if (!course.Started)
                            continue;

                        TrainingUpkeepPerDay += course.Students.Count * Settings.nautTrainingCostMultiplier * mult;
                    }
                }
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
                Reputation.Instance.AddReputation((float)(Reputation.Instance.reputation * -Settings.repPortionLostPerDay), TransactionReasonsRP0.DailyRepDecline.Stock());
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
                //RP0Debug.Log($"[RP-0] MaintenanceHandler removing {(-totalUpkeep - netSubsidy)} funds where upkeep is {-totalUpkeep / timeFactor} ({(preMaint - Funding.Instance.Funds)} for period) and subsidy {MaintenanceSubsidyPerDay} ({subsidyForPassedTime} for period). Delta = {(Funding.Instance.Funds - fundsOld)}");
                //double delta = fundsOld + totalUpkeep + netSubsidy - Funding.Instance.Funds;
                //if (Math.Abs(delta) > 0.1)
                //    Debug.LogError($"[RP-0] $$$$ Error! Fund mismatch from prediction in maintenance! Prediction:\nMaintenance: {totalUpkeep}\n Subsidy: {netSubsidy} subsidy\nTotal: {totalUpkeep + netSubsidy}\nbut real delta: {Funding.Instance.Funds - fundsOld} (diff {delta})");
            }

            // Finally, update all builds
            KerbalConstructionTime.KerbalConstructionTime.Instance.ProgressBuildTime(UTDiff);
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

        private bool EnsureFacilityLvlCostsLoaded()
        {
            if(_waitingForLevelLoad)
                return false;

            if (_facilityLevelCosts.Count == 0)
            {
                // Facility level upgrade costs should be loaded only once. These do not change and are actually unavailable 
                // when outside HomePlanet's SOI and also in the tracking station in some cases.
                foreach (UpgradeableFacility facility in FindObjectsOfType<UpgradeableFacility>())
                {
                    var costArr = new float[facility.UpgradeLevels.Length];
                    for (int i = 0; i < facility.UpgradeLevels.Length; i++)
                    {
                        costArr[i] = facility.UpgradeLevels[i].levelCost;
                    }
                    _facilityLevelCosts[(SpaceCenterFacility)Enum.Parse(typeof(SpaceCenterFacility), facility.name)] = costArr;
                }
                Debug.Log($"[RP-0] Updated facilityLevelsCosts, count: {_facilityLevelCosts.Count}");
            }
            return true;
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
