using System;
using System.Collections.Generic;
using System.Linq;
using RP0.Crew;
using UnityEngine;
using Upgradeables;

namespace RP0
{
    [KSPScenario((ScenarioCreationOptions)480, new GameScenes[] { GameScenes.EDITOR, GameScenes.FLIGHT, GameScenes.SPACECENTER, GameScenes.TRACKSTATION })]
    public class MaintenanceHandler : ScenarioModule
    {
        #region Fields

        [KSPField(isPersistant = true)]
        public double nextUpdate = -1d;

        [KSPField(isPersistant = true)]
        public double lastUpdate = 0d;

        protected double updateInterval = 3600d;
        protected double maintenanceCostMult = 1d;

        protected bool wasWarpingHigh = false;

        protected static bool firstLoad = true;

        protected bool skipOne = true;
        protected bool skipTwo = true;
        protected bool skipThree = true;

        public double kctResearchRate = 0;
        public const int padLevels = 10;
        public int[] kctPadCounts = new int[padLevels];

        protected static Dictionary<SpaceCenterFacility, float[]> facilityLevelCosts = new Dictionary<SpaceCenterFacility, float[]>();
        public Dictionary<string, double> kctBuildRates = new Dictionary<string, double>();

        #region Instance

        private static MaintenanceHandler _instance = null;
        public static MaintenanceHandler Instance
        {
            get
            {
                return _instance;
            }
        }

        #endregion

        #endregion

        #region Component costs

        public double[] padCosts = new double[padLevels];
        public double padCost = 0d;
        public double runwayCost = 0d;
        public double vabCost = 0d;
        public double sphCost = 0d;
        public double rndCost = 0d;
        public double mcCost = 0d;
        public double tsCost = 0d;
        public double acCost = 0d;
        public double facilityUpkeep { get {
            return padCost + runwayCost + vabCost + sphCost + rndCost + mcCost + tsCost + acCost;
        }}
        public double integrationUpkeep { get {
            double tmp = 0d;
            foreach (double d in kctBuildRates.Values)
                tmp += d;
            return tmp * settings.kctBPMult * maintenanceCostMult;
        }}
        public double researchUpkeep = 0d;
        public double trainingUpkeep = 0d;
        public double nautBaseUpkeep = 0d;
        public double nautInFlightUpkeep = 0d;
        public double nautTotalUpkeep = 0d;
        public double totalUpkeep = 0d;

        public MaintenanceSettings settings = new MaintenanceSettings();

        #endregion

        #region Overrides and Monobehaviour methods

        public override void OnAwake()
        {
            if (_instance != null)
            {
                GameObject.Destroy(_instance);
            }
            _instance = this;

            GameEvents.OnGameSettingsApplied.Add(SettingsChanged);
            GameEvents.onGameStateLoad.Add(LoadSettings);
        }

        public override void OnLoad(ConfigNode node)
        {
            base.OnLoad(node);

            foreach (ConfigNode n in GameDatabase.Instance.GetConfigNodes("MAINTENANCESETTINGS"))
                settings.Load(n);

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

        public void UpdateUpkeep()
        {
            float[] costs;
            EnsureFacilityLvlCostsLoaded();

            if (facilityLevelCosts.TryGetValue(SpaceCenterFacility.LaunchPad, out costs))
            {
                if (kctResearchRate > 0d)
                {
                    int lC = costs.Length;
                    for (int i = 0; i < padLevels; i++)
                    {
                        padCosts[i] = 0d;
                        if (i < lC)
                            padCosts[i] = maintenanceCostMult * settings.facilityLevelCostMult * kctPadCounts[i] * Math.Pow(SumCosts(costs, i), settings.facilityLevelCostPow);
                    }
                    padCost = 0;
                    for (int i = padLevels; i-- > 0;)
                        padCost += padCosts[i];
                }
                else
                    padCost = settings.facilityLevelCostMult * Math.Pow(SumCosts(costs, (int)(ScenarioUpgradeableFacilities.GetFacilityLevel(SpaceCenterFacility.LaunchPad) * (costs.Length - 0.95f))), settings.facilityLevelCostPow);
            }

            if (facilityLevelCosts.TryGetValue(SpaceCenterFacility.Runway, out costs))
                runwayCost = maintenanceCostMult * settings.facilityLevelCostMult * Math.Pow(SumCosts(costs, (int)(ScenarioUpgradeableFacilities.GetFacilityLevel(SpaceCenterFacility.Runway) * (costs.Length - 0.95f))), settings.facilityLevelCostPow);

            if (facilityLevelCosts.TryGetValue(SpaceCenterFacility.VehicleAssemblyBuilding, out costs))
                vabCost = maintenanceCostMult * settings.facilityLevelCostMult * Math.Pow(SumCosts(costs, (int)(ScenarioUpgradeableFacilities.GetFacilityLevel(SpaceCenterFacility.VehicleAssemblyBuilding) * (costs.Length - 0.95f))), settings.facilityLevelCostPow);

            if (facilityLevelCosts.TryGetValue(SpaceCenterFacility.SpaceplaneHangar, out costs))
                sphCost = maintenanceCostMult * settings.facilityLevelCostMult * Math.Pow(SumCosts(costs, (int)(ScenarioUpgradeableFacilities.GetFacilityLevel(SpaceCenterFacility.SpaceplaneHangar) * (costs.Length - 0.95f))), settings.facilityLevelCostPow);

            if (facilityLevelCosts.TryGetValue(SpaceCenterFacility.ResearchAndDevelopment, out costs))
                rndCost = maintenanceCostMult * settings.facilityLevelCostMult * Math.Pow(SumCosts(costs, (int)(ScenarioUpgradeableFacilities.GetFacilityLevel(SpaceCenterFacility.ResearchAndDevelopment) * (costs.Length - 0.95f))), settings.facilityLevelCostPow);

            if (facilityLevelCosts.TryGetValue(SpaceCenterFacility.MissionControl, out costs))
                mcCost = maintenanceCostMult * settings.facilityLevelCostMult * Math.Pow(SumCosts(costs, (int)(ScenarioUpgradeableFacilities.GetFacilityLevel(SpaceCenterFacility.MissionControl) * (costs.Length - 0.95f))), settings.facilityLevelCostPow);

            if (facilityLevelCosts.TryGetValue(SpaceCenterFacility.TrackingStation, out costs))
                tsCost = maintenanceCostMult * settings.facilityLevelCostMult * Math.Pow(SumCosts(costs, (int)(ScenarioUpgradeableFacilities.GetFacilityLevel(SpaceCenterFacility.TrackingStation) * (costs.Length - 0.95f))), settings.facilityLevelCostPow);

            trainingUpkeep = 0d;
            if (facilityLevelCosts.TryGetValue(SpaceCenterFacility.AstronautComplex, out costs))
            {
                float lvl = ScenarioUpgradeableFacilities.GetFacilityLevel(SpaceCenterFacility.AstronautComplex);
                int lvlInt = (int)(lvl * (costs.Length - 0.95f));
                acCost = maintenanceCostMult * settings.facilityLevelCostMult * Math.Pow(SumCosts(costs, lvlInt), settings.facilityLevelCostPow);
                if (CrewHandler.Instance?.ActiveCourses != null)
                {
                    double courses = CrewHandler.Instance.ActiveCourses.Count(c => c.Started);
                    if (courses > 0)
                    {
                        courses -= lvlInt * settings.freeCoursesPerLevel;
                        if (courses > 0d)
                        {
                            trainingUpkeep = acCost * (courses * (settings.courseMultiplierDivisor / (settings.courseMultiplierDivisor + lvlInt)));
                        }
                    }
                }
            }

            double nautYearlyUpkeep = maintenanceCostMult * settings.nautYearlyUpkeepBase + ScenarioUpgradeableFacilities.GetFacilityLevel(SpaceCenterFacility.AstronautComplex) * maintenanceCostMult * settings.nautYearlyUpkeepAdd;
            nautBaseUpkeep = 0d;
            nautInFlightUpkeep = 0d;
            nautTotalUpkeep = 0d;
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
                    nautInFlightUpkeep += maintenanceCostMult * settings.nautInFlightDailyRate;
                else
                {
                    // TODO we really should track this independently, in crewhandler, for fast
                    // use since this runs every frame or so.
                    for (int j = k.flightLog.Count; j-- > 0;)
                    {
                        var e = k.flightLog[j];
                        if (e.type == "TRAINING_proficiency" && TrainingDatabase.HasName(e.target, "Orbit"))
                        {
                            nautBaseUpkeep += maintenanceCostMult * settings.nautOrbitProficiencyDailyRate;
                            break;
                        }
                    }
                }
            }

            nautBaseUpkeep += nautCount * perNaut;
            nautTotalUpkeep = nautBaseUpkeep + trainingUpkeep + nautInFlightUpkeep;

            researchUpkeep = maintenanceCostMult * kctResearchRate * settings.kctResearchMult;

            totalUpkeep = facilityUpkeep + integrationUpkeep + researchUpkeep + nautTotalUpkeep;
        }

        public void Update()
        {
            if (HighLogic.CurrentGame == null)
                return;

            if (skipThree)
            {
                if (skipTwo)
                {
                    if (skipOne)
                    {
                        skipOne = false;
                        return;
                    }

                    skipTwo = false;
                    return;
                }

                skipThree = false;
                return;
            }

            double time = Planetarium.GetUniversalTime();
            if (nextUpdate > time)
            {
                if (wasWarpingHigh && TimeWarp.CurrentRate <= 100f)
                    wasWarpingHigh = false;
                else if (firstLoad)
                    firstLoad = false;
                else
                    return;
            }

            UpdateUpkeep();

            double timePassed = time - lastUpdate;

            Funding.Instance.AddFunds(-timePassed * ((totalUpkeep + settings.maintenanceOffset) * (1d / 86400d)), TransactionReasons.StructureRepair);

            lastUpdate = time;

            if (TimeWarp.CurrentRate <= 100f)
            {
                wasWarpingHigh = false;
                nextUpdate = time + updateInterval;
            }
            else
            {
                wasWarpingHigh = true;
                nextUpdate = time + updateInterval * (TimeWarp.CurrentRate * (1f / 100f));
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
            maintenanceCostMult = HighLogic.CurrentGame.Parameters.CustomParams<RP0Settings>().MaintenanceCostMult;
        }

        private void SettingsChanged()
        {
            LoadSettings(null);
            UpdateUpkeep();
        }

        private void EnsureFacilityLvlCostsLoaded()
        {
            if (facilityLevelCosts.Count == 0)
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
                    facilityLevelCosts[(SpaceCenterFacility)Enum.Parse(typeof(SpaceCenterFacility), facility.name)] = costArr;
                }
                Debug.Log($"[RP-0] Updated facilityLevelsCosts, count: {facilityLevelCosts.Count}");
            }
        }

        private void LoadUpgradesPrices(GameScenes scene)
        {
            EnsureFacilityLvlCostsLoaded();
            GameEvents.onLevelWasLoaded.Remove(LoadUpgradesPrices);
        }
    }
}
