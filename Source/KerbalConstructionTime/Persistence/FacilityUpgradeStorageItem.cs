using System;
using UnityEngine;

namespace KerbalConstructionTime
{
    public class FacilityUpgradeStorageItem
    {
        [Persistent]
        public string sFacilityType;

        [Persistent]
        public int upgradeLevel, currentLevel, launchpadID = 0;

        [Persistent]
        public string id, commonName;

        [Persistent]
        public double progress = 0, BP = 0, cost = 0;

        [Persistent]
        public bool UpgradeProcessed = false, isLaunchpad = false;

        public FacilityUpgrade ToFacilityUpgrade()
        {
            // KSP doesn't support automatically persisting nullable enum values.
            // The following code is a workaround for that.
            SpaceCenterFacility? facilityType = null;
            try
            {
                if (!string.IsNullOrEmpty(sFacilityType))
                {
                    facilityType = (SpaceCenterFacility)Enum.Parse(typeof(SpaceCenterFacility), sFacilityType);
                }
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
            }

            var ret = new FacilityUpgrade
            {
                FacilityType = facilityType,
                UpgradeLevel = upgradeLevel,
                CurrentLevel = currentLevel,
                LaunchpadID = launchpadID,
                Id = id,
                CommonName = commonName,
                Progress = progress,
                BP = BP,
                Cost = cost,
                UpgradeProcessed = UpgradeProcessed,
                IsLaunchpad = isLaunchpad
            };
            return ret;
        }

        public FacilityUpgradeStorageItem FromFacilityUpgrade(FacilityUpgrade fu)
        {
            sFacilityType = fu.FacilityType?.ToString();
            upgradeLevel = fu.UpgradeLevel;
            currentLevel = fu.CurrentLevel;
            launchpadID = fu.LaunchpadID;
            id = fu.Id;
            commonName = fu.CommonName;
            progress = fu.Progress;
            BP = fu.BP;
            cost = fu.Cost;
            UpgradeProcessed = fu.UpgradeProcessed;
            isLaunchpad = fu.IsLaunchpad;
            return this;
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
