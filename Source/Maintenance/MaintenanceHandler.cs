using System;
using System.Collections.Generic;
using System.Linq;
using KerbalConstructionTime;
using RP0.Crew;
using RP0.Programs;
using UnityEngine;
using UnityEngine.Profiling;
using Upgradeables;

namespace RP0
{
    [KSPScenario((ScenarioCreationOptions)480, new GameScenes[] { GameScenes.EDITOR, GameScenes.FLIGHT, GameScenes.SPACECENTER, GameScenes.TRACKSTATION })]
    public class MaintenanceHandler : ScenarioModule
    {
        public const double UpdateInterval = 3600d;

        public static MaintenanceHandler Instance { get; private set; } = null;
        public static MaintenanceSettings Settings { get; private set; } = null;

        private static bool _isFirstLoad = true;
        private static readonly Dictionary<SpaceCenterFacility, float[]> _facilityLevelCosts = new Dictionary<SpaceCenterFacility, float[]>();

        [KSPField(isPersistant = true)]
        public double nextUpdate = -1d;

        [KSPField(isPersistant = true)]
        public double lastUpdate = 0d;

        public readonly Dictionary<string, double> IntegrationSalaries = new Dictionary<string, double>();
        public readonly Dictionary<string, double> ConstructionSalaries = new Dictionary<string, double>();
        public readonly Dictionary<string, double> ConstructionMaterials = new Dictionary<string, double>();
        public double Researchers = 0d;

        private double _maintenanceCostMult = 1d;
        private bool _wasWarpingHigh = false;
        private bool _skipOne = true;
        private bool _skipTwo = true;
        private bool _skipThree = true;

        private EventVoid onKctPersonnelChangeEvent;

        #region Component costs

        public double RndCost = 0d;
        public double McCost = 0d;
        public double TsCost = 0d;
        public double AcCost = 0d;

        public double TrainingUpkeepPerDay = 0d;
        public double NautBaseUpkeepPerDay = 0d;
        public double NautInFlightUpkeepPerDay = 0d;
        public double NautUpkeepPerDay = 0d;
        public double TotalUpkeepPerDay => FacilityUpkeepPerDay + IntegrationSalaryPerDay + ConstructionSalaryPerDay + ResearchSalaryPerDay + NautUpkeepPerDay;

        public double FacilityUpkeepPerDay => RndCost + McCost + TsCost + AcCost;

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

        public double ConstructionMaterialsPerDay
        {
            get
            {
                double tmp = 0d;
                foreach (double d in ConstructionMaterials.Values)
                    tmp += d;

                return tmp * _maintenanceCostMult / 365.25d;
            }
        }

        public double ResearchSalaryPerDay => _maintenanceCostMult * Researchers * Settings.salaryEngineers / 365.25d;

        public double MaintenanceSubsidyPerDay
        {
            get
            {
                const double secsPerYear = 3600 * 24 * 365.25;
                float years = (float)(KSPUtils.GetUT() / secsPerYear);
                double minSubsidy = Settings.subsidyCurve.Evaluate(years);
                double minRep = minSubsidy / Settings.repToSubsidyConversion;
                double maxRep = minRep * Settings.subsidyMultiplierForMax;
                double invLerp = UtilMath.InverseLerp(minRep, maxRep, UtilMath.Clamp(Reputation.Instance.reputation, minRep, maxRep));
                double val = UtilMath.LerpUnclamped(minSubsidy, minSubsidy * Settings.subsidyMultiplierForMax, invLerp);
                //Debug.Log($"$$$$ years {years}: minSub: {minSubsidy}, conversion {Settings.repToSubsidyConversion}, maxSub {Settings.subsidyMultiplierForMax}, minRep {minRep}, maxRep {maxRep}, invLerp {invLerp}, val {val}\n{n.ToString()}");
                return val * (1d / 365.25d);
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

            KCTGameStates.ProgramFundingForTime = GetProgramFunding;

            GameEvents.OnGameSettingsApplied.Add(SettingsChanged);
            GameEvents.onGameStateLoad.Add(LoadSettings);
        }

        public void Start()
        {
            onKctPersonnelChangeEvent = GameEvents.FindEvent<EventVoid>("OnKctPesonnelChange");
            if (onKctPersonnelChangeEvent != null)
            {
                onKctPersonnelChangeEvent.Add(UpdateKCTSalaries);
            }
            else
                Debug.LogError("RP-0: Couldn't find OnKctPesonnelChange!");
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

            if (HighLogic.LoadedScene == GameScenes.SPACECENTER)
            {
                // Need to load the facility upgrade prices after CustomBarnKit has finished patching them
                GameEvents.onLevelWasLoaded.Add(LoadUpgradesPrices);
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
            nextUpdate = 0;
        }

        private void UpdateKCTSalaries()
        {
            Profiler.BeginSample("RP0Maintenance UpdateKCTSalaries");
            ConstructionSalaries.Clear();
            IntegrationSalaries.Clear();
            foreach (KSCItem ksc in KCTGameStates.KSCs)
            {
                int constructionWorkers = ksc.ConstructionWorkers;
                ConstructionSalaries[ksc.KSCName] = constructionWorkers;
                IntegrationSalaries[ksc.KSCName] = KCTGameStates.GetEffectiveEngineersForSalary(ksc) - constructionWorkers;
                //for (int j = ksc.LaunchComplexes.Count; j-- > 0;)
                //{
                //    LCItem lc = ksc.LaunchComplexes[j];
                //    if (!lc.isOperational)
                //        continue;

                //    int lpCount = lc.LaunchPadCount;
                //}
            }

            Researchers = KCTGameStates.Researchers;
            Profiler.EndSample();
        }

        public void UpdateUpkeep()
        {
            Profiler.BeginSample("RP0Maintenance UpdateUpkeep");

            if (IntegrationSalaries.Count == 0)
                UpdateKCTSalaries();

            EnsureFacilityLvlCostsLoaded();

            //if (_facilityLevelCosts.TryGetValue(SpaceCenterFacility.LaunchPad, out float[] costs))
            //{
            //    for (int i = 0; i < PadLevelCount; i++)
            //    {
            //        PadCosts[i] = 0d;
            //    }

            //    foreach (float lvl in KCTPadLevels)
            //    {
            //        int roundedPadLvl = (int)Math.Round(lvl);
            //        PadCosts[roundedPadLvl] += _maintenanceCostMult * Settings.facilityLevelCostMult * Math.Pow(SumCosts(costs, lvl), Settings.facilityLevelCostPow);
            //    }
            //    PadCost = 0;
            //    for (int i = PadLevelCount; i-- > 0;)
            //        PadCost += PadCosts[i];
            //}

            //if (_facilityLevelCosts.TryGetValue(SpaceCenterFacility.Runway, out costs))
            //    RunwayCost = _maintenanceCostMult * Settings.facilityLevelCostMult * Math.Pow(SumCosts(costs, (int)(ScenarioUpgradeableFacilities.GetFacilityLevel(SpaceCenterFacility.Runway) * (costs.Length - 0.95f))), Settings.facilityLevelCostPow);

            //if (_facilityLevelCosts.TryGetValue(SpaceCenterFacility.VehicleAssemblyBuilding, out costs))
            //    VabCost = _maintenanceCostMult * Settings.facilityLevelCostMult * Math.Pow(SumCosts(costs, (int)(ScenarioUpgradeableFacilities.GetFacilityLevel(SpaceCenterFacility.VehicleAssemblyBuilding) * (costs.Length - 0.95f))), Settings.facilityLevelCostPow);

            //if (_facilityLevelCosts.TryGetValue(SpaceCenterFacility.SpaceplaneHangar, out costs))
            //    SphCost = _maintenanceCostMult * Settings.facilityLevelCostMult * Math.Pow(SumCosts(costs, (int)(ScenarioUpgradeableFacilities.GetFacilityLevel(SpaceCenterFacility.SpaceplaneHangar) * (costs.Length - 0.95f))), Settings.facilityLevelCostPow);

            ConstructionMaterials.Clear();
            foreach (var ksc in KCTGameStates.KSCs)
            {
                if (ksc.Constructions.Count > 0)
                {
                    var c = ksc.Constructions[0];
                    double br = c.GetBuildRate();
                    if (br > 0d)
                    {
                        ConstructionMaterials[ksc.KSCName] = br * 86400d / c.BP * c.Cost;
                    }
                }
            }

            if (_facilityLevelCosts.TryGetValue(SpaceCenterFacility.ResearchAndDevelopment, out float[] costs))
                RndCost = _maintenanceCostMult * Settings.facilityLevelCostMult * Math.Pow(SumCosts(costs, (int)(ScenarioUpgradeableFacilities.GetFacilityLevel(SpaceCenterFacility.ResearchAndDevelopment) * (costs.Length - 0.95f))), Settings.facilityLevelCostPow);

            if (_facilityLevelCosts.TryGetValue(SpaceCenterFacility.MissionControl, out costs))
                McCost = _maintenanceCostMult * Settings.facilityLevelCostMult * Math.Pow(SumCosts(costs, (int)(ScenarioUpgradeableFacilities.GetFacilityLevel(SpaceCenterFacility.MissionControl) * (costs.Length - 0.95f))), Settings.facilityLevelCostPow);

            if (_facilityLevelCosts.TryGetValue(SpaceCenterFacility.TrackingStation, out costs))
                TsCost = _maintenanceCostMult * Settings.facilityLevelCostMult * Math.Pow(SumCosts(costs, (int)(ScenarioUpgradeableFacilities.GetFacilityLevel(SpaceCenterFacility.TrackingStation) * (costs.Length - 0.95f))), Settings.facilityLevelCostPow);

            TrainingUpkeepPerDay = 0d;
            if (_facilityLevelCosts.TryGetValue(SpaceCenterFacility.AstronautComplex, out costs))
            {
                float lvl = ScenarioUpgradeableFacilities.GetFacilityLevel(SpaceCenterFacility.AstronautComplex);
                int lvlInt = (int)(lvl * (costs.Length - 0.95f));
                AcCost = _maintenanceCostMult * Settings.facilityLevelCostMult * Math.Pow(SumCosts(costs, lvlInt), Settings.facilityLevelCostPow);
                if (CrewHandler.Instance?.ActiveCourses != null)
                {
                    double courses = CrewHandler.Instance.ActiveCourses.Count(c => c.Started);
                    if (courses > 0)
                    {
                        courses -= lvlInt * Settings.freeCoursesPerLevel;
                        if (courses > 0d)
                        {
                            TrainingUpkeepPerDay = AcCost * (courses * (Settings.courseMultiplierDivisor / (Settings.courseMultiplierDivisor + lvlInt)));
                        }
                    }
                }
            }

            double nautYearlyUpkeep = _maintenanceCostMult * Settings.nautYearlyUpkeepBase + ScenarioUpgradeableFacilities.GetFacilityLevel(SpaceCenterFacility.AstronautComplex) * _maintenanceCostMult * Settings.nautYearlyUpkeepAdd;
            NautBaseUpkeepPerDay = 0d;
            NautInFlightUpkeepPerDay = 0d;
            NautUpkeepPerDay = 0d;
            double perNaut = nautYearlyUpkeep * (1d / 365.25d);
            int nautCount = 0;
            for (int i = HighLogic.CurrentGame.CrewRoster.Count; i-- > 0;)
            {
                var k = HighLogic.CurrentGame.CrewRoster[i];
                if (k.rosterStatus == ProtoCrewMember.RosterStatus.Dead || k.rosterStatus == ProtoCrewMember.RosterStatus.Missing ||
                    k.type != ProtoCrewMember.KerbalType.Crew)
                {
                    continue;
                }

                ++nautCount;
                if (k.rosterStatus == ProtoCrewMember.RosterStatus.Assigned)
                    NautInFlightUpkeepPerDay += _maintenanceCostMult * Settings.nautInFlightDailyRate;
                else
                {
                    for (int j = k.flightLog.Count; j-- > 0;)
                    {
                        var e = k.flightLog[j];
                        if (e.type == CrewHandler.TrainingType_Proficiency && TrainingDatabase.HasName(e.target, "Orbit"))
                        {
                            NautBaseUpkeepPerDay += _maintenanceCostMult * Settings.nautOrbitProficiencyDailyRate;
                            break;
                        }
                    }
                }
            }

            NautBaseUpkeepPerDay += nautCount * perNaut;
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

            if (_skipThree)
            {
                if (_skipTwo)
                {
                    if (_skipOne)
                    {
                        _skipOne = false;
                        return;
                    }

                    _skipTwo = false;
                    return;
                }

                _skipThree = false;
                UpdateKCTSalaries();
                return;
            }

            double time = KSPUtils.GetUT();
            if (nextUpdate > time)
            {
                if (_wasWarpingHigh && TimeWarp.CurrentRate <= 100f)
                    _wasWarpingHigh = false;
                else if (_isFirstLoad)
                    _isFirstLoad = false;
                else
                    return;
            }

            UpdateKCTSalaries();
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
                Debug.Log($"[RP-0] MaintenanceHandler removing {costForPassedTime} funds where upkeep is {TotalUpkeepPerDay} and subsidy {MaintenanceSubsidyPerDay}");
                Funding.Instance.AddFunds(-costForPassedTime, TransactionReasons.StructureRepair);
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
            if (onKctPersonnelChangeEvent != null)
                onKctPersonnelChangeEvent.Remove(UpdateKCTSalaries);

            KCTGameStates.ProgramFundingForTime = null;
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

        private void EnsureFacilityLvlCostsLoaded()
        {
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
        }

        private void LoadUpgradesPrices(GameScenes scene)
        {
            EnsureFacilityLvlCostsLoaded();
            GameEvents.onLevelWasLoaded.Remove(LoadUpgradesPrices);
        }

        public double GetSubsidyAmountForSeconds(double seconds)
        {
            return MaintenanceSubsidyPerDay * seconds / 86400d;
        }
    }
}
