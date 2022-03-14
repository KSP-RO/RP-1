using System;
using System.Collections.Generic;
using System.Linq;
using KerbalConstructionTime;
using RP0.Crew;
using UnityEngine;
using UnityEngine.Profiling;
using Upgradeables;

namespace RP0
{
    [KSPScenario((ScenarioCreationOptions)480, new GameScenes[] { GameScenes.EDITOR, GameScenes.FLIGHT, GameScenes.SPACECENTER, GameScenes.TRACKSTATION })]
    public class MaintenanceHandler : ScenarioModule
    {
        public const double UpdateInterval = 3600d;
        public const int PadLevelCount = 7;
        public const double BuildRateOffset = -0.0001d;    // if we change the min build rate, FIX THIS.

        public static MaintenanceHandler Instance { get; private set; } = null;
        public static MaintenanceSettings Settings { get; private set; } = null;

        private static bool _isFirstLoad = true;
        private static readonly Dictionary<SpaceCenterFacility, float[]> _facilityLevelCosts = new Dictionary<SpaceCenterFacility, float[]>();

        [KSPField(isPersistant = true)]
        public double nextUpdate = -1d;

        [KSPField(isPersistant = true)]
        public double lastUpdate = 0d;

        public readonly Dictionary<string, double> KCTBuildRates = new Dictionary<string, double>();
        public int[] KCTPadCounts = new int[PadLevelCount];
        public List<float> KCTPadLevels = new List<float>();
        public double KCTResearchRate = 0;

        private double _maintenanceCostMult = 1d;
        private bool _wasWarpingHigh = false;
        private bool _skipOne = true;
        private bool _skipTwo = true;
        private bool _skipThree = true;

        #region Component costs

        public double[] PadCosts = new double[PadLevelCount];
        public double PadCost = 0d;
        public double RunwayCost = 0d;
        public double VabCost = 0d;
        public double SphCost = 0d;
        public double RndCost = 0d;
        public double McCost = 0d;
        public double TsCost = 0d;
        public double AcCost = 0d;

        public double ResearchUpkeep = 0d;
        public double TrainingUpkeep = 0d;
        public double NautBaseUpkeep = 0d;
        public double NautInFlightUpkeep = 0d;
        public double NautTotalUpkeep = 0d;
        public double TotalUpkeep = 0d;

        public double FacilityUpkeep => PadCost + RunwayCost + VabCost + SphCost + RndCost + McCost + TsCost + AcCost;

        public double IntegrationUpkeep
        {
            get
            {
                double tmp = 0d;
                foreach (double d in KCTBuildRates.Values)
                    tmp += d;
                return tmp * Settings.kctBPMult * _maintenanceCostMult;
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
        }

        public override void OnLoad(ConfigNode node)
        {
            base.OnLoad(node);

            if (Settings == null)
            {
                Settings = new MaintenanceSettings();
                foreach (ConfigNode n in GameDatabase.Instance.GetConfigNodes("MAINTENANCESETTINGS"))
                    Settings.Load(n);
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

        private void UpdateKCTRates()
        {
            Profiler.BeginSample("RP0Maintenance UpdateKCTRates");
            KCTPadLevels.Clear();
            for (int i = KCTPadCounts.Length; i-- > 0;)
                KCTPadCounts[i] = 0;

            foreach (KSCItem ksc in KCTGameStates.KSCs)
            {
                double buildRate = 0d;
                for (int j = ksc.LaunchComplexes.Count; j-- > 0;)
                {
                    LCItem lc = ksc.LaunchComplexes[j];
                    if (!lc.isOperational)
                        continue;

                    for (int i = lc.Rates.Count; i-- > 0;)
                        buildRate += Math.Max(0d, lc.Rates[i] + BuildRateOffset);

                    if (buildRate < 0.01d) continue;

                    for (int i = lc.LaunchPads.Count; i-- > 0;)
                    {
                        KCT_LaunchPad pad = lc.LaunchPads[i];
                        int roundedPadLvl = (int)Math.Round(pad.fractionalLevel);
                        if (pad.isOperational && roundedPadLvl < PadLevelCount)
                        {
                            ++KCTPadCounts[roundedPadLvl];
                            KCTPadLevels.Add(pad.fractionalLevel);
                        }
                    }
                }

                KCTBuildRates[ksc.KSCName] = buildRate;
            }

            KCTResearchRate = MathParser.ParseNodeRateFormula(10);
            Profiler.EndSample();
        }

        public void UpdateUpkeep()
        {
            Profiler.BeginSample("RP0Maintenance UpdateUpkeep");
            EnsureFacilityLvlCostsLoaded();

            if (_facilityLevelCosts.TryGetValue(SpaceCenterFacility.LaunchPad, out float[] costs))
            {
                for (int i = 0; i < PadLevelCount; i++)
                {
                    PadCosts[i] = 0d;
                }

                foreach (float lvl in KCTPadLevels)
                {
                    int roundedPadLvl = (int)Math.Round(lvl);
                    PadCosts[roundedPadLvl] += _maintenanceCostMult * Settings.facilityLevelCostMult * Math.Pow(SumCosts(costs, lvl), Settings.facilityLevelCostPow);
                }
                PadCost = 0;
                for (int i = PadLevelCount; i-- > 0;)
                    PadCost += PadCosts[i];
            }

            if (_facilityLevelCosts.TryGetValue(SpaceCenterFacility.Runway, out costs))
                RunwayCost = _maintenanceCostMult * Settings.facilityLevelCostMult * Math.Pow(SumCosts(costs, (int)(ScenarioUpgradeableFacilities.GetFacilityLevel(SpaceCenterFacility.Runway) * (costs.Length - 0.95f))), Settings.facilityLevelCostPow);

            if (_facilityLevelCosts.TryGetValue(SpaceCenterFacility.VehicleAssemblyBuilding, out costs))
                VabCost = _maintenanceCostMult * Settings.facilityLevelCostMult * Math.Pow(SumCosts(costs, (int)(ScenarioUpgradeableFacilities.GetFacilityLevel(SpaceCenterFacility.VehicleAssemblyBuilding) * (costs.Length - 0.95f))), Settings.facilityLevelCostPow);

            if (_facilityLevelCosts.TryGetValue(SpaceCenterFacility.SpaceplaneHangar, out costs))
                SphCost = _maintenanceCostMult * Settings.facilityLevelCostMult * Math.Pow(SumCosts(costs, (int)(ScenarioUpgradeableFacilities.GetFacilityLevel(SpaceCenterFacility.SpaceplaneHangar) * (costs.Length - 0.95f))), Settings.facilityLevelCostPow);

            if (_facilityLevelCosts.TryGetValue(SpaceCenterFacility.ResearchAndDevelopment, out costs))
                RndCost = _maintenanceCostMult * Settings.facilityLevelCostMult * Math.Pow(SumCosts(costs, (int)(ScenarioUpgradeableFacilities.GetFacilityLevel(SpaceCenterFacility.ResearchAndDevelopment) * (costs.Length - 0.95f))), Settings.facilityLevelCostPow);

            if (_facilityLevelCosts.TryGetValue(SpaceCenterFacility.MissionControl, out costs))
                McCost = _maintenanceCostMult * Settings.facilityLevelCostMult * Math.Pow(SumCosts(costs, (int)(ScenarioUpgradeableFacilities.GetFacilityLevel(SpaceCenterFacility.MissionControl) * (costs.Length - 0.95f))), Settings.facilityLevelCostPow);

            if (_facilityLevelCosts.TryGetValue(SpaceCenterFacility.TrackingStation, out costs))
                TsCost = _maintenanceCostMult * Settings.facilityLevelCostMult * Math.Pow(SumCosts(costs, (int)(ScenarioUpgradeableFacilities.GetFacilityLevel(SpaceCenterFacility.TrackingStation) * (costs.Length - 0.95f))), Settings.facilityLevelCostPow);

            TrainingUpkeep = 0d;
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
                            TrainingUpkeep = AcCost * (courses * (Settings.courseMultiplierDivisor / (Settings.courseMultiplierDivisor + lvlInt)));
                        }
                    }
                }
            }

            double nautYearlyUpkeep = _maintenanceCostMult * Settings.nautYearlyUpkeepBase + ScenarioUpgradeableFacilities.GetFacilityLevel(SpaceCenterFacility.AstronautComplex) * _maintenanceCostMult * Settings.nautYearlyUpkeepAdd;
            NautBaseUpkeep = 0d;
            NautInFlightUpkeep = 0d;
            NautTotalUpkeep = 0d;
            double perNaut = nautYearlyUpkeep * (1d / 365d);
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
                    NautInFlightUpkeep += _maintenanceCostMult * Settings.nautInFlightDailyRate;
                else
                {
                    for (int j = k.flightLog.Count; j-- > 0;)
                    {
                        var e = k.flightLog[j];
                        if (e.type == CrewHandler.TrainingType_Proficiency && TrainingDatabase.HasName(e.target, "Orbit"))
                        {
                            NautBaseUpkeep += _maintenanceCostMult * Settings.nautOrbitProficiencyDailyRate;
                            break;
                        }
                    }
                }
            }

            NautBaseUpkeep += nautCount * perNaut;
            NautTotalUpkeep = NautBaseUpkeep + TrainingUpkeep + NautInFlightUpkeep;

            ResearchUpkeep = _maintenanceCostMult * KCTResearchRate * Settings.kctResearchMult;

            TotalUpkeep = FacilityUpkeep + IntegrationUpkeep + ResearchUpkeep + NautTotalUpkeep;
            Profiler.EndSample();
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
                UpdateKCTRates();
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

            UpdateKCTRates();
            UpdateUpkeep();

            double timePassed = time - lastUpdate;

            using (new CareerEventScope(CareerEventType.Maintenance))
            {
                double costPerDay = Math.Max(0, TotalUpkeep + Settings.maintenanceOffset);
                double costForPassedSeconds = -timePassed * (costPerDay * (1d / 86400d));
                Debug.Log($"[RP-0] MaintenanceHandler removing {costForPassedSeconds} funds");
                Funding.Instance.AddFunds(costForPassedSeconds, TransactionReasons.StructureRepair);
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
    }
}
