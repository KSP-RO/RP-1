using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using KSP;
using UnityEngine;
using System.Reflection;

namespace RP0
{
    [KSPScenario(ScenarioCreationOptions.AddToAllGames, new GameScenes[] { GameScenes.EDITOR, GameScenes.FLIGHT, GameScenes.SPACECENTER, GameScenes.TRACKSTATION })]
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

        public double facilityLevelCostMult = 0.0000005d;
        public double kctBPMult = 20d;
        protected double kctResearchMult = 100d * 86400d;
        protected double nautYearlyUpkeepAdd = 5000d;
        protected double nautYearlyUpkeepBase = 500d;

        protected Dictionary<SpaceCenterFacility, Upgradeables.UpgradeableFacility.UpgradeLevel[]> facilityLevels = new Dictionary<SpaceCenterFacility, Upgradeables.UpgradeableFacility.UpgradeLevel[]>();
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
            return kctBuildRates.Values.Sum() * kctBPMult;
        }}
        public double researchUpkeep = 0d;
        public double nautYearlyUpkeep = 0d;
        public double nautUpkeep = 0d;
        public double totalUpkeep = 0d;

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

        public void updateUpkeep()
        {
            Upgradeables.UpgradeableFacility.UpgradeLevel[] levels;

            // Pad
            if (facilityLevels.TryGetValue(SpaceCenterFacility.LaunchPad, out levels))
            {
                if (kctResearchRate > 0d)
                {
                    int lC = levels.Length;
                    for (int i = 0; i < padLevels; i++)
                    {
                        padCosts[i] = 0d;
                        if (i < lC)
                            padCosts[i] = facilityLevelCostMult * kctPadCounts[i] * levels[i].levelCost;
                    }
                    padCost = padCosts.Sum();
                }
                else
                    padCost = facilityLevelCostMult * levels[(int)(ScenarioUpgradeableFacilities.GetFacilityLevel(SpaceCenterFacility.LaunchPad) * (levels.Length + 0.05f))].levelCost;
            }

            // Runway
            if (facilityLevels.TryGetValue(SpaceCenterFacility.Runway, out levels))
                runwayCost = facilityLevelCostMult * levels[(int)(ScenarioUpgradeableFacilities.GetFacilityLevel(SpaceCenterFacility.Runway) * (levels.Length + 0.05f))].levelCost;

            //VAB
            if (facilityLevels.TryGetValue(SpaceCenterFacility.VehicleAssemblyBuilding, out levels))
                vabCost = facilityLevelCostMult * levels[(int)(ScenarioUpgradeableFacilities.GetFacilityLevel(SpaceCenterFacility.VehicleAssemblyBuilding) * (levels.Length + 0.05f))].levelCost;

            //SPH
            if (facilityLevels.TryGetValue(SpaceCenterFacility.SpaceplaneHangar, out levels))
                sphCost = facilityLevelCostMult * levels[(int)(ScenarioUpgradeableFacilities.GetFacilityLevel(SpaceCenterFacility.SpaceplaneHangar) * (levels.Length + 0.05f))].levelCost;

            //RnD
            if (facilityLevels.TryGetValue(SpaceCenterFacility.ResearchAndDevelopment, out levels))
                rndCost = facilityLevelCostMult * levels[(int)(ScenarioUpgradeableFacilities.GetFacilityLevel(SpaceCenterFacility.ResearchAndDevelopment) * (levels.Length + 0.05f))].levelCost;

            // MC
            if (facilityLevels.TryGetValue(SpaceCenterFacility.MissionControl, out levels))
                mcCost = facilityLevelCostMult * levels[(int)(ScenarioUpgradeableFacilities.GetFacilityLevel(SpaceCenterFacility.MissionControl) * (levels.Length + 0.05f))].levelCost;

            // TS
            if (facilityLevels.TryGetValue(SpaceCenterFacility.TrackingStation, out levels))
                tsCost = facilityLevelCostMult * levels[(int)(ScenarioUpgradeableFacilities.GetFacilityLevel(SpaceCenterFacility.TrackingStation) * (levels.Length + 0.05f))].levelCost;
            
            // AC
            if (facilityLevels.TryGetValue(SpaceCenterFacility.AstronautComplex, out levels))
                acCost = facilityLevelCostMult * levels[(int)(ScenarioUpgradeableFacilities.GetFacilityLevel(SpaceCenterFacility.AstronautComplex) * (levels.Length + 0.05f))].levelCost;

            nautYearlyUpkeep = nautYearlyUpkeepBase + ((double)ScenarioUpgradeableFacilities.GetFacilityLevel(SpaceCenterFacility.AstronautComplex) * nautYearlyUpkeepAdd);
            nautUpkeep = HighLogic.CurrentGame.CrewRoster.GetActiveCrewCount() * nautYearlyUpkeep * (1d / 365d);

            researchUpkeep = kctResearchRate * kctResearchMult;

            totalUpkeep = facilityUpkeep + integrationUpkeep + researchUpkeep + nautUpkeep;
        }

        public void Update()
        {
            if (HighLogic.CurrentGame == null)
                return;
            
            if (skipOne)
            {
                skipOne = false;
                return;
            }

            if (skipTwo)
            {
                skipTwo = false;
                return;
            }

            if (skipThree)
            {
                skipThree = false;
                return;
            }
            
            if (facilityLevels.Count == 0)
            {
                foreach (Upgradeables.UpgradeableFacility facility in GameObject.FindObjectsOfType<Upgradeables.UpgradeableFacility>())
                {
                    facilityLevels[(SpaceCenterFacility)Enum.Parse(typeof(SpaceCenterFacility), facility.name)] = facility.UpgradeLevels;
                }
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

                if (HighLogic.LoadedScene == GameScenes.SPACECENTER)
                {
                    PopupDialog.SpawnPopupDialog(new Vector2(0.5f, 0.5f),
                                                        new Vector2(0.5f, 0.5f),
                                                        "Maintenance",
                                                        "We are paying the following maintenance costs per day/year:\nFacilities:                                  "
                                                        + facilityUpkeep.ToString("N0") + " / " + (facilityUpkeep * 365d).ToString("N0")
                                                        + "\nIntegration / Launch Teams: " + (integrationUpkeep).ToString("N0") + " / " + (integrationUpkeep*365d).ToString("N0")
                                                        + "\nResarch Teams:                       " + (researchUpkeep).ToString("N0") + " / " + (researchUpkeep* 365d).ToString("N0")
                                                        + "\nAstronauts:                             " + (nautUpkeep).ToString("N0") + " / " + (nautUpkeep* 365d).ToString("N0"),
                                                        "OK",
                                                        true,
                                                        HighLogic.UISkin);
                }
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
    }
}
