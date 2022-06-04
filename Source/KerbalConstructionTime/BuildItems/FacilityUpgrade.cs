using System;
using System.Collections.Generic;
using UnityEngine;
using Upgradeables;

namespace KerbalConstructionTime
{
    public class FacilityUpgrade : ConstructionBuildItem
    {
        protected SpaceCenterFacility? _facilityType;
        public override SpaceCenterFacility? FacilityType
        {
            get { return _facilityType; }
            set { _facilityType = value; }
        }
        public int UpgradeLevel, CurrentLevel;
        public string FacilityInternalID;
        public Guid ID;

        public FacilityUpgrade()
        {
        }

        public FacilityUpgrade(SpaceCenterFacility? type, string facilityID, int newLevel, int oldLevel, string name)
        {
            ID = Guid.NewGuid();
            _facilityType = type;
            FacilityInternalID = facilityID;
            UpgradeLevel = newLevel;
            CurrentLevel = oldLevel;
            Name = name;

            KCTDebug.Log($"Upgrade of {name} requested from {oldLevel} to {newLevel}");
        }

        public void Downgrade()
        {
            KCTDebug.Log($"Downgrading {Name} to level {CurrentLevel}");
            foreach (UpgradeableFacility facility in GetFacilityReferencesById(FacilityInternalID))
            {
                KCTEvents.AllowedToUpgrade = true;
                facility.SetLevel(CurrentLevel);
            }
        }

        public void Upgrade()
        {
            KCTDebug.Log($"Upgrading {Name} to level {UpgradeLevel}");

            List<UpgradeableFacility> facilityRefs = GetFacilityReferencesById(FacilityInternalID);
            if (_facilityType == SpaceCenterFacility.VehicleAssemblyBuilding)
            {
                // Also upgrade the SPH to the same level as VAB when playing with unified build queue
                facilityRefs.AddRange(GetFacilityReferencesByType(SpaceCenterFacility.SpaceplaneHangar));
            }

            KCTEvents.AllowedToUpgrade = true;
            foreach (UpgradeableFacility facility in facilityRefs)
            {
                facility.SetLevel(UpgradeLevel);
            }

            int newLvl = Utilities.GetBuildingUpgradeLevel(FacilityInternalID);
            UpgradeProcessed = newLvl == UpgradeLevel;

            KCTDebug.Log($"Upgrade processed: {UpgradeProcessed} Current: {newLvl} Desired: {UpgradeLevel}");
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
            return KCTGameStates.KSCs.Find(ksc => ksc.FacilityUpgrades.Find(ub => ub.FacilityInternalID == this.FacilityInternalID) != null) != null;
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
