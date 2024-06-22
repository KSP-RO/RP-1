using System;
using System.Collections.Generic;
using UnityEngine;
using Upgradeables;

namespace RP0
{
    public class FacilityUpgradeProject : ConstructionProject
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

        public FacilityUpgradeProject()
        {
        }

        public FacilityUpgradeProject(SpaceCenterFacility type, string facilityID, int newLevel, int oldLevel, string name)
        {
            uid = Guid.NewGuid();
            sFacilityType = type;
            id = facilityID;
            upgradeLevel = newLevel;
            currentLevel = oldLevel;
            base.name = name;

            RP0Debug.Log($"Upgrade of {name} requested from {oldLevel} to {newLevel}");
        }

        public void Abort()
        {
            RP0Debug.Log($"Downgrading {name} to level {currentLevel}");
            foreach (UpgradeableFacility facility in GetFacilityReferencesById(id))
            {
                facility.SetLevel(currentLevel);
            }
        }

        public void Apply()
        {
            RP0Debug.Log($"Upgrading {name} to level {upgradeLevel}");
            KCTUtilities.SetFacilityLevel(FacilityType, upgradeLevel);

            int newLvl = KCTUtilities.GetFacilityLevel(sFacilityType);
            upgradeProcessed = newLvl == upgradeLevel;
            if (upgradeProcessed)
            {
                UpgradeLockedFacilities();
                RP0Debug.Log($"Upgrade processed: {upgradeProcessed} Current: {newLvl} Desired: {upgradeLevel}");
            }
            else
            {
                RP0Debug.LogError($"Setting facility level failed, Current: {newLvl} Desired: {upgradeLevel}");
            }
        }

        public static List<UpgradeableFacility> GetFacilityReferencesById(string id)
        {
            return ScenarioUpgradeableFacilities.protoUpgradeables[id].facilityRefs;
        }

        public static void UpgradeLockedFacilities()
        {
            float avgLevel = 0f;
            int facCount = 0;
            for (SpaceCenterFacility fac = SpaceCenterFacility.Administration; fac <= SpaceCenterFacility.VehicleAssemblyBuilding; ++fac)
            {
                if (fac == SpaceCenterFacility.Runway || fac == SpaceCenterFacility.LaunchPad)
                    continue;
                if (Database.LockedFacilities.Contains(fac))
                    continue;

                ++facCount;
                avgLevel += ScenarioUpgradeableFacilities.GetFacilityLevel(fac);
            }
            
            avgLevel /= (float)facCount;
            int desiredLevel = (int)Math.Round(avgLevel * 2d);

            for (SpaceCenterFacility fac = SpaceCenterFacility.Administration; fac <= SpaceCenterFacility.VehicleAssemblyBuilding; ++fac)
            {
                if (fac == SpaceCenterFacility.Runway || fac == SpaceCenterFacility.LaunchPad)
                    continue;
                if (!Database.LockedFacilities.Contains(fac))
                    continue;

                KCTUtilities.SetFacilityLevel(fac, desiredLevel);
            }
        }

        public bool AlreadyInProgress()
        {
            return AlreadyInProgressByID(this.id);
        }

        public static bool AlreadyInProgressByID(string id)
        {
            return SpaceCenterManagement.Instance.KSCs.Find(ksc => ksc.FacilityUpgrades.Find(ub => ub.id == id) != null) != null;
        }

        protected override void ProcessCancel()
        {
            KSC.FacilityUpgrades.Remove(this);

            try
            {
                SCMEvents.OnFacilityUpgradeCancel?.Fire(this);
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
            }

            KSC.RecalculateBuildRates(false);
        }

        protected override void ProcessComplete()
        {
            if (ScenarioUpgradeableFacilities.Instance != null && !SpaceCenterManagement.Instance.ErroredDuringOnLoad)
            {
                Apply();

                try
                {
                    SCMEvents.OnFacilityUpgradeComplete?.Fire(this);
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
