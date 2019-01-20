using System;
using System.Collections.Generic;
using KSP;
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
            return tmp * settings.kctBPMult;
        }}
        public double researchUpkeep = 0d;
        public double nautYearlyUpkeep = 0d;
        public double nautUpkeep = 0d;
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

        public void updateUpkeep()
        {
            float[] costs;
            EnsureFacilityLvlCostsLoaded();

            // Pad
            if (facilityLevelCosts.TryGetValue(SpaceCenterFacility.LaunchPad, out costs))
            {
                if (kctResearchRate > 0d)
                {
                    int lC = costs.Length;
                    for (int i = 0; i < padLevels; i++)
                    {
                        padCosts[i] = 0d;
                        if (i < lC)
                            padCosts[i] = settings.facilityLevelCostMult * kctPadCounts[i] * Math.Pow(costs[i], settings.facilityLevelCostPow);
                    }
                    padCost = 0;
                    for (int i = padLevels; i-- > 0;)
                        padCost += padCosts[i];
                }
                else
                    padCost = settings.facilityLevelCostMult * Math.Pow(costs[(int)(ScenarioUpgradeableFacilities.GetFacilityLevel(SpaceCenterFacility.LaunchPad) * (costs.Length - 0.95f))], settings.facilityLevelCostPow);
            }

            // Runway
            if (facilityLevelCosts.TryGetValue(SpaceCenterFacility.Runway, out costs))
                runwayCost = settings.facilityLevelCostMult * Math.Pow(costs[(int)(ScenarioUpgradeableFacilities.GetFacilityLevel(SpaceCenterFacility.Runway) * (costs.Length - 0.95f))], settings.facilityLevelCostPow);

            //VAB
            if (facilityLevelCosts.TryGetValue(SpaceCenterFacility.VehicleAssemblyBuilding, out costs))
                vabCost = settings.facilityLevelCostMult * Math.Pow(costs[(int)(ScenarioUpgradeableFacilities.GetFacilityLevel(SpaceCenterFacility.VehicleAssemblyBuilding) * (costs.Length - 0.95f))], settings.facilityLevelCostPow);

            //SPH
            if (facilityLevelCosts.TryGetValue(SpaceCenterFacility.SpaceplaneHangar, out costs))
                sphCost = settings.facilityLevelCostMult * Math.Pow(costs[(int)(ScenarioUpgradeableFacilities.GetFacilityLevel(SpaceCenterFacility.SpaceplaneHangar) * (costs.Length - 0.95f))], settings.facilityLevelCostPow);

            //RnD
            if (facilityLevelCosts.TryGetValue(SpaceCenterFacility.ResearchAndDevelopment, out costs))
                rndCost = settings.facilityLevelCostMult * Math.Pow(costs[(int)(ScenarioUpgradeableFacilities.GetFacilityLevel(SpaceCenterFacility.ResearchAndDevelopment) * (costs.Length - 0.95f))], settings.facilityLevelCostPow);

            // MC
            if (facilityLevelCosts.TryGetValue(SpaceCenterFacility.MissionControl, out costs))
                mcCost = settings.facilityLevelCostMult * Math.Pow(costs[(int)(ScenarioUpgradeableFacilities.GetFacilityLevel(SpaceCenterFacility.MissionControl) * (costs.Length - 0.95f))], settings.facilityLevelCostPow);

            // TS
            if (facilityLevelCosts.TryGetValue(SpaceCenterFacility.TrackingStation, out costs))
                tsCost = settings.facilityLevelCostMult * Math.Pow(costs[(int)(ScenarioUpgradeableFacilities.GetFacilityLevel(SpaceCenterFacility.TrackingStation) * (costs.Length - 0.95f))], settings.facilityLevelCostPow);

            // AC
            if (facilityLevelCosts.TryGetValue(SpaceCenterFacility.AstronautComplex, out costs))
                acCost = settings.facilityLevelCostMult * Math.Pow(costs[(int)(ScenarioUpgradeableFacilities.GetFacilityLevel(SpaceCenterFacility.AstronautComplex) * (costs.Length - 0.95f))], settings.facilityLevelCostPow);

            nautYearlyUpkeep = settings.nautYearlyUpkeepBase + ((double)ScenarioUpgradeableFacilities.GetFacilityLevel(SpaceCenterFacility.AstronautComplex) * settings.nautYearlyUpkeepAdd);
            nautUpkeep = HighLogic.CurrentGame.CrewRoster.GetActiveCrewCount() * nautYearlyUpkeep * (1d / 365d);

            researchUpkeep = kctResearchRate * settings.kctResearchMult;

            totalUpkeep = facilityUpkeep + integrationUpkeep + researchUpkeep + nautUpkeep;
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

            updateUpkeep();

            double timePassed = time - lastUpdate;

            Funding.Instance.AddFunds(-timePassed * (totalUpkeep * (1d / 86400d)), TransactionReasons.StructureRepair);

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
           
        }

        #endregion

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
