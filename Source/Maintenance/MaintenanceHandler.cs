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

        protected double nextUpdate = -1d;
        protected double lastUpdate = 0d;
        protected double updateInterval = 3600d;
        protected bool wasWarpingHigh = false;

        public double kctBuildRate = 0;
        public double kctResearcRate = 0;
        public int[] kctPadCounts = new int[10];

        protected double facilityLevelCostMult = 0.00001d;
        protected double kctBPMult = 1000000d;
        protected double kctResearchMult = 50000000d / 86400d;
        protected double nautYearlyUpkeepAdd = 5000d;
        protected double nautYearlyUpkeepBase = 500d;

        protected Dictionary<SpaceCenterFacility, Upgradeables.UpgradeableFacility.UpgradeLevel[]> facilityLevels = new Dictionary<SpaceCenterFacility, Upgradeables.UpgradeableFacility.UpgradeLevel[]>();

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

        #region Overrides and Monobehaviour methods

        public override void OnAwake()
        {

            if (_instance != null)
            {
                GameObject.Destroy(_instance);
            }
            _instance = this;
        }

        public void Update()
        {
            if (HighLogic.CurrentGame == null)
                return;

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
                else
                    return;
            }

            Upgradeables.UpgradeableFacility.UpgradeLevel[] levels;
            double facilityUpkeep = 0d;

            // Pad
            if (facilityLevels.TryGetValue(SpaceCenterFacility.LaunchPad, out levels))
            {
                if (kctResearcRate > 0d)
                {
                    int lC = levels.Length - 1;
                    for (int i = kctPadCounts.Length; i-- > 0;)
                    {
                        if (i > lC)
                            continue;

                        facilityUpkeep += facilityLevelCostMult * levels[i].levelCost;
                    }
                }
                else
                    facilityUpkeep += facilityLevelCostMult * levels[(int)(ScenarioUpgradeableFacilities.GetFacilityLevel(SpaceCenterFacility.LaunchPad) * (levels.Length + 0.05f))].levelCost;
            }

            // Runway
            if (facilityLevels.TryGetValue(SpaceCenterFacility.Runway, out levels))
                facilityUpkeep += facilityLevelCostMult * levels[(int)(ScenarioUpgradeableFacilities.GetFacilityLevel(SpaceCenterFacility.Runway) * (levels.Length + 0.05f))].levelCost;

            //VAB
            if (facilityLevels.TryGetValue(SpaceCenterFacility.VehicleAssemblyBuilding, out levels))
                facilityUpkeep += facilityLevelCostMult * levels[(int)(ScenarioUpgradeableFacilities.GetFacilityLevel(SpaceCenterFacility.VehicleAssemblyBuilding) * (levels.Length + 0.05f))].levelCost;

            //SPH
            if (facilityLevels.TryGetValue(SpaceCenterFacility.SpaceplaneHangar, out levels))
                facilityUpkeep += facilityLevelCostMult * levels[(int)(ScenarioUpgradeableFacilities.GetFacilityLevel(SpaceCenterFacility.SpaceplaneHangar) * (levels.Length + 0.05f))].levelCost;

            //RnD
            if (facilityLevels.TryGetValue(SpaceCenterFacility.ResearchAndDevelopment, out levels))
                facilityUpkeep += facilityLevelCostMult * levels[(int)(ScenarioUpgradeableFacilities.GetFacilityLevel(SpaceCenterFacility.ResearchAndDevelopment) * (levels.Length + 0.05f))].levelCost;

            // MC
            if (facilityLevels.TryGetValue(SpaceCenterFacility.MissionControl, out levels))
                facilityUpkeep += facilityLevelCostMult * levels[(int)(ScenarioUpgradeableFacilities.GetFacilityLevel(SpaceCenterFacility.MissionControl) * (levels.Length + 0.05f))].levelCost;

            // TS
            if (facilityLevels.TryGetValue(SpaceCenterFacility.TrackingStation, out levels))
                facilityUpkeep += facilityLevelCostMult * levels[(int)(ScenarioUpgradeableFacilities.GetFacilityLevel(SpaceCenterFacility.TrackingStation) * (levels.Length + 0.05f))].levelCost;
            
            // AC
            if (facilityLevels.TryGetValue(SpaceCenterFacility.AstronautComplex, out levels))
                facilityUpkeep += facilityLevelCostMult * levels[(int)(ScenarioUpgradeableFacilities.GetFacilityLevel(SpaceCenterFacility.AstronautComplex) * (levels.Length + 0.05f))].levelCost;


            double nautUpkeep = HighLogic.CurrentGame.CrewRoster.GetActiveCrewCount()
                * (nautYearlyUpkeepBase
                + ((double)ScenarioUpgradeableFacilities.GetFacilityLevel(SpaceCenterFacility.AstronautComplex)
                    * nautYearlyUpkeepAdd))
                * (1d / (86400d * 365d));

            double kctBPUpkeep = kctBuildRate * kctBPMult;
            double kctRDUpkeep = kctResearcRate * kctResearchMult;

            double totalUpkeep = facilityUpkeep + kctBPUpkeep + kctRDUpkeep + nautUpkeep;

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
                                                        "We are paying the following maintenance costs per day/year:\nFacilities: "
                                                        + facilityUpkeep.ToString("N0") + "/" + (facilityUpkeep * 365d).ToString("N0")
                                                        + "\nIntegration / Pad Support Teams: " + (kctBPUpkeep).ToString("N0") + "/" + (kctBPUpkeep*365d).ToString("N0")
                                                        + "\nResarch Teams:" + (kctRDUpkeep).ToString("N0") + "/" + (kctRDUpkeep* 365d).ToString("N0")
                                                        + "\nAstronauts:" + (nautUpkeep).ToString("N0") + "/" + (nautUpkeep* 365d).ToString("N0"),
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
