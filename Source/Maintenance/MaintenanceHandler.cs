using System;
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
        public static MaintenanceSettings Settings { get; private set; } = null;

        private bool _isFirstLoad = true;
        private static readonly Dictionary<SpaceCenterFacility, float[]> _facilityLevelCosts = new Dictionary<SpaceCenterFacility, float[]>();
        public static void ClearFacilityCosts() { _facilityLevelCosts.Clear(); }

        public static EventVoid OnRP0MaintenanceChanged;

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
        public double Researchers => KCTGameStates.Researchers;

        private double _maintenanceCostMult = 1d;
        public double MaintenanceCostMult => _maintenanceCostMult;
        private bool _wasWarpingHigh = false;
        private int _frameCount = 0;
        private bool _waitingForLevelLoad = false;

        #region Component costs

        public readonly Dictionary<SpaceCenterFacility, double> FacilityMaintenanceCosts = new Dictionary<SpaceCenterFacility, double>();
        public readonly SpaceCenterFacility[] FacilitiesForMaintenance = { 
            SpaceCenterFacility.Administration,
            SpaceCenterFacility.AstronautComplex,
            SpaceCenterFacility.MissionControl,
            SpaceCenterFacility.ResearchAndDevelopment,
            SpaceCenterFacility.TrackingStation
        };

        public double LCsCostPerDay = 0d;

        public double TrainingUpkeepPerDay = 0d;
        public double NautBaseUpkeepPerDay = 0d;
        public double NautInFlightUpkeepPerDay = 0d;
        public double NetUpkeepPerDay = 0d;

        public double FacilityUpkeepPerDay => FacilityMaintenanceCosts.Values.Sum();

        public double IntegrationSalaryPerDay
        {
            get
            {
                double tmp = 0d;
                foreach (double d in IntegrationSalaries.Values)
                    tmp += d;

                return tmp * Settings.salaryEngineers * _maintenanceCostMult / 365.25d;
            }
        }

        public double ResearchSalaryPerDay => _maintenanceCostMult * Researchers * Settings.salaryResearchers / 365.25d;

        public struct SubsidyDetails
        {
            public double minSubsidy, maxSubsidy, maxRep, subsidy;
        }

        public SubsidyDetails GetSubsidyDetails()
        {
            var details = new SubsidyDetails();
            const double secsPerYear = 3600 * 24 * 365.25;
            float years = (float)(KSPUtils.GetUT() / secsPerYear);
            details.minSubsidy = Settings.subsidyCurve.Evaluate(years);
            details.maxRep = details.minSubsidy * Settings.subsidyMultiplierForMax / Settings.repToSubsidyConversion;
            details.maxSubsidy = details.minSubsidy * Settings.subsidyMultiplierForMax;
            double invLerp = UtilMath.InverseLerp(0, details.maxRep, UtilMath.Clamp(Reputation.Instance.reputation, 0, details.maxRep));
            details.subsidy = UtilMath.LerpUnclamped(details.minSubsidy, details.maxSubsidy, invLerp);
            //Debug.Log($"$$$$ years {years}: minSub: {details.minSubsidy}, conversion {Settings.repToSubsidyConversion}, maxSub {Settings.subsidyMultiplierForMax}, maxRep {details.maxRep}, invLerp {invLerp}, subsidy {details.subsidy}=>{(details.subsidy * (1d / 365.25d))}");
            return details;
        }

        public double MaintenanceSubsidyPerDay
        {
            get
            {
                SubsidyDetails details = GetSubsidyDetails();
                return details.subsidy * (1d / 365.25d);
            }
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
            GameEvents.onGameStateLoad.Add(LoadSettings);
            GameEvents.onVesselRecoveryProcessingComplete.Add(onVesselRecoveryProcessingComplete);
            GameEvents.onKerbalInactiveChange.Add(onKerbalInactiveChange);
            GameEvents.onKerbalStatusChange.Add(onKerbalStatusChange);
            GameEvents.onKerbalTypeChanged.Add(onKerbalTypeChanged);
        }

        public void Start()
        {
            OnRP0MaintenanceChanged.Add(ScheduleMaintenanceUpdate);
        }

        public override void OnLoad(ConfigNode node)
        {
            base.OnLoad(node);

            if (Settings == null)
            {
                Settings = new MaintenanceSettings();
                foreach (ConfigNode n in GameDatabase.Instance.GetConfigNodes("MAINTENANCESETTINGS"))
                    ConfigNode.LoadObjectFromConfig(Settings, n);
            }
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
                flightCostPerDay = _maintenanceCostMult * Settings.nautInFlightDailyRate;
            }
            else
            {
                baseCostPerDay += GetNautUpkeepFromTraining(k.flightLog) + GetNautUpkeepFromTraining(k.careerLog);

                if (k.inactive)
                {
                    baseCostPerDay *= Settings.nautInactiveMult;
                }
            }

            baseCostPerDay *= (_maintenanceCostMult / 365.25d);
        }

        public double ComputeDailyMaintenanceCost(double cost, FacilityMaintenanceType facilityType = FacilityMaintenanceType.Building)
        {
            if (facilityType == FacilityMaintenanceType.Hangar)
                cost = Math.Max(Settings.hangarCostForMaintenanceMin, cost - Settings.hangarCostForMaintenanceOffset);

            double upkeep = _maintenanceCostMult * Settings.facilityLevelCostMult * Math.Pow(cost, Settings.facilityLevelCostPow);

            if (facilityType == FacilityMaintenanceType.LC)
                upkeep *= Settings.lcCostMultiplier;

            return upkeep;
        }

        private double LCUpkeep(float massMax, Vector3 sizeMax, bool humanRated, int padCount, LaunchComplexType type)
        {
            switch (type)
            {
                case LaunchComplexType.Hangar:
                    return ComputeDailyMaintenanceCost(KerbalConstructionTime.Utilities.GetPadStats(massMax, sizeMax, humanRated, out _, out _, out _), FacilityMaintenanceType.Hangar);

                case LaunchComplexType.Pad:
                    KerbalConstructionTime.Utilities.GetPadStats(massMax, sizeMax, humanRated, out double padCost, out double vabCost, out _);
                    return ComputeDailyMaintenanceCost((vabCost + padCount * padCost), FacilityMaintenanceType.LC);
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
                    if (lcc.LCID != lc.ID)
                        return lcc.Progress / lcc.BP * LCUpkeep(lcc.LCData.massMax, lcc.LCData.sizeMax, lcc.LCData.isHumanRated, lc.LaunchPadCount, lcc.LCData.lcType);
                }
                return 0d;
            }

            return LCUpkeep(lc.MassMax, lc.SizeMax, lc.IsHumanRated, lc.LaunchPadCount, lc.LCType);
        }

        private double GetFacilityUpgradeRatio(SpaceCenterFacility facility)
        {
            foreach (var fac in KCTGameStates.ActiveKSC.FacilityUpgrades)
                if (fac.FacilityType == facility)
                    return fac.Progress / fac.BP;

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
                FacilityMaintenanceCosts.TryGetValue(SpaceCenterFacility.AstronautComplex, out double AcCost);
                if (CrewHandler.Instance?.ActiveCourses != null)
                {
                    double courses = CrewHandler.Instance.ActiveCourses.Count(c => c.Started);
                    if (courses > 0)
                    {
                        courses -= lvlInt * Settings.freeCoursesPerLevel;
                        if (courses > 0d)
                        {
                            TrainingUpkeepPerDay = Settings.nautTrainingCostMultiplier * AcCost * (courses * (Settings.courseMultiplierDivisor / (Settings.courseMultiplierDivisor + lvlInt)));
                        }
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

            double totalUpkeep = CurrencyUtils.Funds(TransactionReasonsRP0.StructureRepair, -FacilityUpkeepPerDay)
                + CurrencyUtils.Funds(TransactionReasonsRP0.StructureRepairLC, -LCsCostPerDay)
                + CurrencyUtils.Funds(TransactionReasonsRP0.SalaryEngineers, -IntegrationSalaryPerDay)
                + CurrencyUtils.Funds(TransactionReasonsRP0.SalaryResearchers, -ResearchSalaryPerDay)
                + CurrencyUtils.Funds(TransactionReasonsRP0.SalaryCrew, -NautBaseUpkeepPerDay - NautInFlightUpkeepPerDay)
                + CurrencyUtils.Funds(TransactionReasonsRP0.CrewTraining, -TrainingUpkeepPerDay);
            NetUpkeepPerDay = Math.Min(0d, CurrencyUtils.Funds(TransactionReasonsRP0.Subsidy, MaintenanceSubsidyPerDay) + totalUpkeep);
            Profiler.EndSample();
        }

        public double GetProgramFunding(double utOffset)
        {
            double programBudget = 0d;
            foreach (Program p in Programs.ProgramHandler.Instance.ActivePrograms)
            {
                programBudget += p.GetFundsForFutureTimestamp(KSPUtils.GetUT() + utOffset) - p.GetFundsForFutureTimestamp(KSPUtils.GetUT());
            }
            return programBudget;
        }

        public void Update()
        {
            if (HighLogic.CurrentGame == null)
                return;

            if (_frameCount++ < 2)
            {
                return;
            }

            double time = KSPUtils.GetUT();
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
                Reputation.Instance.AddReputation((float)(Reputation.Instance.reputation * -Settings.repPortionLostPerDay), TransactionReasonsRP0.RepDecline.Stock());
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
            double UT = KSPUtils.GetUT();
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
            CrewHandler.Instance.Process();

            // Best to deduct maintenance fees and add program funding at the same time
            ProgramHandler.Instance.ProcessFunding();

            using (new CareerEventScope(CareerEventType.Maintenance))
            {
                double timeFactor = UTDiff * (1d / 86400d);
                double subsidyForPassedTime = timeFactor * MaintenanceSubsidyPerDay;
                // We have to do some weird logic here. We have to get the resultant upkeep first,
                // then add the subsidy, then actually subtract the upkeep.
                // This is because we have to subtract piecemeal.
                double fundsOld = Funding.Instance.Funds;
                double totalUpkeep = CurrencyUtils.Funds(TransactionReasonsRP0.StructureRepair, -FacilityUpkeepPerDay)
                    + CurrencyUtils.Funds(TransactionReasonsRP0.StructureRepairLC, -LCsCostPerDay)
                    + CurrencyUtils.Funds(TransactionReasonsRP0.SalaryEngineers, -IntegrationSalaryPerDay)
                    + CurrencyUtils.Funds(TransactionReasonsRP0.SalaryResearchers, -ResearchSalaryPerDay)
                    + CurrencyUtils.Funds(TransactionReasonsRP0.SalaryCrew, -NautBaseUpkeepPerDay - NautInFlightUpkeepPerDay)
                    + CurrencyUtils.Funds(TransactionReasonsRP0.CrewTraining, -TrainingUpkeepPerDay);
                // We have to add subsidy first, to be sure we have enough funds.
                double netSubsidy = Math.Min(CurrencyUtils.Funds(TransactionReasonsRP0.Subsidy, subsidyForPassedTime), -totalUpkeep * timeFactor);
                if (netSubsidy > 0)
                {
                    Funding.Instance.AddFunds(subsidyForPassedTime, (TransactionReasons)TransactionReasonsRP0.Subsidy);
                    double overshoot = (Funding.Instance.Funds - fundsOld) - netSubsidy;
                    if (overshoot > 0)
                        Funding.Instance.AddFunds(-overshoot, TransactionReasons.None);
                }
                double preMaint = Funding.Instance.Funds;
                Funding.Instance.AddFunds(-FacilityUpkeepPerDay * timeFactor, TransactionReasons.StructureRepair);
                Funding.Instance.AddFunds(-LCsCostPerDay * timeFactor, (TransactionReasons)TransactionReasonsRP0.StructureRepairLC);
                Funding.Instance.AddFunds(-IntegrationSalaryPerDay * timeFactor, (TransactionReasons)TransactionReasonsRP0.SalaryEngineers);
                Funding.Instance.AddFunds(-ResearchSalaryPerDay * timeFactor, (TransactionReasons)TransactionReasonsRP0.SalaryResearchers);
                Funding.Instance.AddFunds(-(NautBaseUpkeepPerDay + NautInFlightUpkeepPerDay) * timeFactor, (TransactionReasons)TransactionReasonsRP0.SalaryCrew);
                Funding.Instance.AddFunds(-TrainingUpkeepPerDay * timeFactor, (TransactionReasons)TransactionReasonsRP0.CrewTraining);
                RP0Debug.Log($"[RP-0] MaintenanceHandler removing {(-totalUpkeep * timeFactor - netSubsidy)} funds where upkeep is {-totalUpkeep} ({(preMaint - Funding.Instance.Funds)} for period) and subsidy {MaintenanceSubsidyPerDay} ({subsidyForPassedTime} for period). Delta = {(Funding.Instance.Funds - fundsOld)}");
                if (Math.Abs(Funding.Instance.Funds - fundsOld - (-totalUpkeep * timeFactor - netSubsidy)) > 0.1)
                    Debug.LogError($"[RP-0] $$$$ Error! Fund mismatch from prediction in maintenance! Prediction: {-totalUpkeep * timeFactor} maintenace - {netSubsidy} subsidy should = {Funding.Instance.Funds - fundsOld}");
                CareerLog.Instance.CurrentPeriod.SubsidyPaidOut += netSubsidy;
            }

            // Finally, update all builds
            KerbalConstructionTime.KerbalConstructionTime.Instance.ProgressBuildTime(UTDiff);
        }

        public void OnDestroy()
        {
            GameEvents.onGameStateLoad.Remove(LoadSettings);
            GameEvents.OnGameSettingsApplied.Remove(SettingsChanged);
            GameEvents.onVesselRecoveryProcessingComplete.Remove(onVesselRecoveryProcessingComplete);
            GameEvents.onKerbalInactiveChange.Remove(onKerbalInactiveChange);
            GameEvents.onKerbalStatusChange.Remove(onKerbalStatusChange);
            GameEvents.onKerbalTypeChanged.Remove(onKerbalTypeChanged);

            OnRP0MaintenanceChanged.Remove(ScheduleMaintenanceUpdate);
        }

        #endregion

        private void LoadSettings(ConfigNode data)
        {
            _maintenanceCostMult = HighLogic.CurrentGame.Parameters.CustomParams<RP0Settings>().MaintenanceCostMult;
        }

        private void SettingsChanged()
        {
            LoadSettings(null);
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

        public double GetSubsidyAmountForSeconds(double seconds)
        {
            return MaintenanceSubsidyPerDay * seconds / 86400d;
        }

        private void onVesselRecoveryProcessingComplete(ProtoVessel pv, KSP.UI.Screens.MissionRecoveryDialog mrd, float x)
        {
            if (pv.GetVesselCrew().Count > 0)
                MaintenanceHandler.OnRP0MaintenanceChanged.Fire();
        }

        private void onKerbalTypeChanged(ProtoCrewMember pcm, ProtoCrewMember.KerbalType from, ProtoCrewMember.KerbalType to)
        {
            if (from != to && (from == ProtoCrewMember.KerbalType.Crew || to == ProtoCrewMember.KerbalType.Crew))
                OnRP0MaintenanceChanged.Fire();
        }

        private void onKerbalStatusChange(ProtoCrewMember pcm, ProtoCrewMember.RosterStatus from, ProtoCrewMember.RosterStatus to)
        {
            if (from != to)
                OnRP0MaintenanceChanged.Fire();
        }

        private void onKerbalInactiveChange(ProtoCrewMember pcm, bool from, bool to)
        {
            if (from != to)
                OnRP0MaintenanceChanged.Fire();
        }
    }
}
