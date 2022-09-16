using System;
using System.Collections.Generic;
using UnityEngine;
using Upgradeables;

namespace KerbalConstructionTime
{
    public class FacilityUpgrade : ConstructionBuildItem
    {
        [Persistent]
        public int upgradeLevel;
        [Persistent]
        public int currentLevel;
        [Persistent]
        public string id;
        [Persistent]
        public Guid uid;

        [Persistent]
        protected SpaceCenterFacility sFacilityType;
        public override SpaceCenterFacility FacilityType
        {
            get { return sFacilityType; }
            set { sFacilityType = value; }
        }

        public override string GetItemName()
        {
            return ScenarioUpgradeableFacilities.GetFacilityName(sFacilityType);
        }

        

        public FacilityUpgrade()
        {
        }

        public FacilityUpgrade(SpaceCenterFacility type, string facilityID, int newLevel, int oldLevel, string name)
        {
            uid = Guid.NewGuid();
            sFacilityType = type;
            id = facilityID;
            upgradeLevel = newLevel;
            currentLevel = oldLevel;
            base.name = name;

            KCTDebug.Log($"Upgrade of {name} requested from {oldLevel} to {newLevel}");
        }

        public void Downgrade()
        {
            KCTDebug.Log($"Downgrading {name} to level {currentLevel}");
            foreach (UpgradeableFacility facility in GetFacilityReferencesById(id))
            {
                KCTEvents.AllowedToUpgrade = true;
                facility.SetLevel(currentLevel);
            }
        }

        public void Upgrade()
        {
            KCTDebug.Log($"Upgrading {name} to level {upgradeLevel}");

            List<UpgradeableFacility> facilityRefs = GetFacilityReferencesById(id);
            if (sFacilityType == SpaceCenterFacility.VehicleAssemblyBuilding)
            {
                // Also upgrade the SPH to the same level as VAB when playing with unified build queue
                facilityRefs.AddRange(GetFacilityReferencesByType(SpaceCenterFacility.SpaceplaneHangar));
            }

            KCTEvents.AllowedToUpgrade = true;
            foreach (UpgradeableFacility facility in facilityRefs)
            {
                facility.SetLevel(upgradeLevel);
            }

            int newLvl = Utilities.GetBuildingUpgradeLevel(id);
            upgradeProcessed = newLvl == upgradeLevel;

            KCTDebug.Log($"Upgrade processed: {upgradeProcessed} Current: {newLvl} Desired: {upgradeLevel}");
        }

        public static List<UpgradeableFacility> GetFacilityReferencesById(string id)
        {
            return ScenarioUpgradeableFacilities.protoUpgradeables[id].facilityRefs;
        }

        public static List<UpgradeableFacility> GetFacilityReferencesByType(SpaceCenterFacility facilityType)
        {
            string internalId = ScenarioUpgradeableFacilities.SlashSanitize(facilityType.ToString());
            return GetFacilityReferencesById(internalId);
        }

        public bool AlreadyInProgress()
        {
            return AlreadyInProgressByID(this.id);
        }

        public static bool AlreadyInProgressByID(string id)
        {
            return KCTGameStates.KSCs.Find(ksc => ksc.FacilityUpgrades.Find(ub => ub.id == id) != null) != null;
        }

        protected override void ProcessCancel()
        {
            KSC.FacilityUpgrades.Remove(this);

            try
            {
                KCTEvents.OnFacilityUpgradeCancel?.Fire(this);
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
            }

            KSC.RecalculateBuildRates(false);
        }

        protected override void ProcessComplete()
        {

            if (ScenarioUpgradeableFacilities.Instance != null && !KCTGameStates.ErroredDuringOnLoad)
            {
                Upgrade();

                try
                {
                    KCTEvents.OnFacilityUpgradeComplete?.Fire(this);
                }
                catch (Exception ex)
                {
                    Debug.LogException(ex);
                }
            }
        }
    }
}

/*
    KerbalConstructionTime (c) by Michael Marvin, Zachary Eck

    KerbalConstructionTime is licensed under a
    Creative Commons Attribution-NonCommercial-ShareAlike 4.0 International License.

    You should have received a copy of the license along with this
    work. If not, see <http://creativecommons.org/licenses/by-nc-sa/4.0/>.
*/
