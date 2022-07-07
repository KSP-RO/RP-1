using System;
using System.Collections.Generic;
using System.Linq;
using KerbalConstructionTime;
using RP0.Crew;
using RP0.Programs;
using UnityEngine;
using UnityEngine.Profiling;
using Upgradeables;
using System.Collections;

namespace RP0
{
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

        [KSPField(isPersistant = true)]
        public MaintenanceGUI.MaintenancePeriod guiSelectedPeriod = MaintenanceGUI.MaintenancePeriod.Day;

        public readonly Dictionary<string, double> IntegrationSalaries = new Dictionary<string, double>();
        public readonly Dictionary<string, double> ConstructionSalaries = new Dictionary<string, double>();
        public double Researchers = 0d;

        private double _maintenanceCostMult = 1d;
        private bool _wasWarpingHigh = false;
        private int _frameCount = 0;
        private bool _waitingForLevelLoad = false;

        #region Component costs

        public double RndCost = 0d;
        public double McCost = 0d;
        public double TsCost = 0d;
        public double AcCost = 0d;
        public double LCsCost = 0d;

        public double TrainingUpkeepPerDay = 0d;
        public double NautBaseUpkeepPerDay = 0d;
        public double NautInFlightUpkeepPerDay = 0d;
        public double NautUpkeepPerDay = 0d;
        public double TotalUpkeepPerDay => FacilityUpkeepPerDay + IntegrationSalaryPerDay + ConstructionSalaryPerDay + ResearchSalaryPerDay + NautUpkeepPerDay;

        public double FacilityUpkeepPerDay => RndCost + McCost + TsCost + AcCost + LCsCost;

        // TODO this is duplicate code with the KCT side
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

        public double ConstructionSalaryPerDay
        {
            get
            {
                double tmp = 0d;
                foreach (double d in ConstructionSalaries.Values)
                    tmp += d;
                return tmp * Settings.salaryEngineers * _maintenanceCostMult / 365.25d;
            }
        }

        public double ResearchSalaryPerDay => _maintenanceCostMult * Researchers * Settings.salaryEngineers / 365.25d;

        public struct SubsidyDetails
        {
            public double minSubsidy, maxSubsidy, minRep, maxRep, subsidy;
        }

        public SubsidyDetails GetSubsidyDetails()
        {
            var details = new SubsidyDetails();
            const double secsPerYear = 3600 * 24 * 365.25;
            float years = (float)(KSPUtils.GetUT() / secsPerYear);
            details.minSubsidy = Settings.subsidyCurve.Evaluate(years);
            details.minRep = details.minSubsidy / Settings.repToSubsidyConversion;
            details.maxRep = details.minRep * Settings.subsidyMultiplierForMax;
            details.maxSubsidy = details.minSubsidy * Settings.subsidyMultiplierForMax;
            double invLerp = UtilMath.InverseLerp(details.minRep, details.maxRep, UtilMath.Clamp(Reputation.Instance.reputation, details.minRep, details.maxRep));
            details.subsidy = UtilMath.LerpUnclamped(details.minSubsidy, details.maxSubsidy, invLerp);
            //Debug.Log($"$$$$ years {years}: minSub: {minSubsidy}, conversion {Settings.repToSubsidyConversion}, maxSub {Settings.subsidyMultiplierForMax}, minRep {minRep}, maxRep {maxRep}, invLerp {invLerp}, val {val}=>{(val * (1d / 365.25d))}");
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

            KCTGameStates.ProgramFundingForTimeDelegate = GetProgramFunding;
            KCTGameStates.FacilityDailyMaintenanceDelegate = ComputeDailyMaintenanceCost;

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

                UpdateKCTSalarySettings();
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
            ConstructionSalaries.Clear();
            IntegrationSalaries.Clear();
            foreach (KSCItem ksc in KCTGameStates.KSCs)
            {
                ConstructionSalaries[ksc.KSCName] = KCTGameStates.GetEffectiveConstructionEngineersForSalary(ksc);
                IntegrationSalaries[ksc.KSCName] = KCTGameStates.GetEffectiveIntegrationEngineersForSalary(ksc);
            }

            Researchers = KCTGameStates.Researchers;
            Profiler.EndSample();
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
                bool foundOrbit = false;
                bool foundSubOrb = false;
                for (int j = k.flightLog.Count; j-- > 0;)
                {
                    var e = k.flightLog[j];
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
                if (k.inactive)
                {
                    baseCostPerDay *= Settings.nautInactiveMult;
                }
            }

            baseCostPerDay *= (_maintenanceCostMult / 365.25d);
        }

        protected double ComputeDailyMaintenanceCost(double cost) => ComputeDailyMaintenanceCost(cost, 0);

        protected double ComputeDailyMaintenanceCost(double cost, int facilityType)
        {
            if (facilityType == 2)
                cost = Math.Max(Settings.hangarCostForMaintenanceMin, cost - Settings.hangarCostForMaintenanceOffset);

            double upkeep = _maintenanceCostMult * Settings.facilityLevelCostMult * Math.Pow(cost, Settings.facilityLevelCostPow);

            if (facilityType == 1)
                upkeep *= Settings.lcCostMultiplier;

            return upkeep;
        }

        public double LCUpkeep(LCItem lc)
        {
            if (!lc.IsOperational)
                return 0d;

            switch (lc.LCType)
            {
                case LaunchComplexType.Hangar:
                    return ComputeDailyMaintenanceCost(Math.Max(Settings.hangarCostForMaintenanceMin, KCT_GUI.GetPadStats(lc.MassMax, lc.SizeMax, lc.IsHumanRated, out _, out _, out _) - Settings.hangarCostForMaintenanceOffset));

                case LaunchComplexType.Pad:
                    KCT_GUI.GetPadStats(lc.MassMax, lc.SizeMax, lc.IsHumanRated, out double padCost, out double vabCost, out _);
                    return ComputeDailyMaintenanceCost((vabCost + lc.LaunchPadCount * padCost) * Settings.lcCostMultiplier);
            }

            return 0d;
        }

        public void UpdateUpkeep()
        {
            Profiler.BeginSample("RP0Maintenance UpdateUpkeep");

            _isFirstLoad = false;

            UpdateKCTSalaries();

            EnsureFacilityLvlCostsLoaded();

            LCsCost = 0d;
            foreach (var ksc in KCTGameStates.KSCs)
            {
                foreach (var lc in ksc.LaunchComplexes)
                    LCsCost += LCUpkeep(lc);
            }

            if (_facilityLevelCosts.TryGetValue(SpaceCenterFacility.ResearchAndDevelopment, out float[] costs))
                RndCost = ComputeDailyMaintenanceCost(SumCosts(costs, (int)(ScenarioUpgradeableFacilities.GetFacilityLevel(SpaceCenterFacility.ResearchAndDevelopment) * (costs.Length - 0.95f))));

            if (_facilityLevelCosts.TryGetValue(SpaceCenterFacility.MissionControl, out costs))
                McCost = ComputeDailyMaintenanceCost(SumCosts(costs, (int)(ScenarioUpgradeableFacilities.GetFacilityLevel(SpaceCenterFacility.MissionControl) * (costs.Length - 0.95f))));

            if (_facilityLevelCosts.TryGetValue(SpaceCenterFacility.TrackingStation, out costs))
                TsCost = ComputeDailyMaintenanceCost(SumCosts(costs, (int)(ScenarioUpgradeableFacilities.GetFacilityLevel(SpaceCenterFacility.TrackingStation) * (costs.Length - 0.95f))));

            TrainingUpkeepPerDay = 0d;
            if (_facilityLevelCosts.TryGetValue(SpaceCenterFacility.AstronautComplex, out costs))
            {
                float lvl = ScenarioUpgradeableFacilities.GetFacilityLevel(SpaceCenterFacility.AstronautComplex);
                int lvlInt = (int)(lvl * (costs.Length - 0.95f));
                AcCost = ComputeDailyMaintenanceCost(SumCosts(costs, lvlInt));
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
            NautUpkeepPerDay = 0d;
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

            NautUpkeepPerDay = NautBaseUpkeepPerDay + TrainingUpkeepPerDay + NautInFlightUpkeepPerDay;

            KCTGameStates.NetUpkeep = -Math.Max(0d, TotalUpkeepPerDay - MaintenanceSubsidyPerDay);
            Profiler.EndSample();
        }

        private double GetProgramFunding(double utOffset)
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

            double timePassed = time - lastUpdate;

            // Best to deduct maintenance fees and add program funding at the same time
            ProgramHandler.Instance.ProcessFunding();

            using (new CareerEventScope(CareerEventType.Maintenance))
            {
                double timeFactor = timePassed * (1d / 86400d);
                double upkeepForPassedTime = timeFactor * TotalUpkeepPerDay;
                double subsidyForPassedTime = timeFactor * MaintenanceSubsidyPerDay;
                double costForPassedTime = Math.Max(0, upkeepForPassedTime - subsidyForPassedTime);
                double fundsOld = Funding.Instance.Funds;
                Funding.Instance.AddFunds(-costForPassedTime, TransactionReasons.StructureRepair);
                Debug.Log($"[RP-0] MaintenanceHandler removing {costForPassedTime} funds where upkeep is {TotalUpkeepPerDay} ({upkeepForPassedTime} for period) and subsidy {MaintenanceSubsidyPerDay} ({subsidyForPassedTime} for period). Delta = {(Funding.Instance.Funds - fundsOld)}");
                CareerLog.Instance.CurrentPeriod.SubsidyPaidOut += Math.Min(upkeepForPassedTime, subsidyForPassedTime);
            }

            lastUpdate = time;

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

        public void OnDestroy()
        {
            GameEvents.onGameStateLoad.Remove(LoadSettings);
            GameEvents.OnGameSettingsApplied.Remove(SettingsChanged);
            GameEvents.onVesselRecoveryProcessingComplete.Remove(onVesselRecoveryProcessingComplete);
            GameEvents.onKerbalInactiveChange.Remove(onKerbalInactiveChange);
            GameEvents.onKerbalStatusChange.Remove(onKerbalStatusChange);
            GameEvents.onKerbalTypeChanged.Remove(onKerbalTypeChanged);

            OnRP0MaintenanceChanged.Remove(ScheduleMaintenanceUpdate);

            KCTGameStates.ProgramFundingForTimeDelegate = null;
            KCTGameStates.FacilityDailyMaintenanceDelegate = null;
        }

        #endregion

        private void LoadSettings(ConfigNode data)
        {
            _maintenanceCostMult = HighLogic.CurrentGame.Parameters.CustomParams<RP0Settings>().MaintenanceCostMult;
        }

        private void SettingsChanged()
        {
            LoadSettings(null);
            UpdateKCTSalarySettings();
            UpdateUpkeep();
        }

        private void UpdateKCTSalarySettings()
        {
            KCTGameStates.SalaryEngineers = Settings.salaryEngineers;
            KCTGameStates.SalaryResearchers = Settings.salaryResearchers;
            KCTGameStates.SalaryMultiplier = _maintenanceCostMult;
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
