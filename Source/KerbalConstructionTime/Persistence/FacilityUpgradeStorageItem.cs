using System;
using UnityEngine;

namespace KerbalConstructionTime
{
    public class FacilityUpgradeStorageItem : ConstructionStorage
    {
        [Persistent]
        public string sFacilityType;

        [Persistent]
        public int upgradeLevel, currentLevel;

        [Persistent]
        public string id, commonName;

        [Persistent]
        public Guid uid;

        [Persistent]
        public bool UpgradeProcessed;

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

            var ret = new FacilityUpgrade();
            LoadFields(ret);
            ret.FacilityType = facilityType;
            ret.UpgradeLevel = upgradeLevel;
            ret.CurrentLevel = currentLevel;
            ret.FacilityInternalID = id;
            ret.ID = uid;
            // back-compat
            if (!string.IsNullOrEmpty(commonName))
            {
                ret.Name = commonName;
                ret.UpgradeProcessed = UpgradeProcessed;
            }
            return ret;
        }

        public FacilityUpgradeStorageItem FromFacilityUpgrade(FacilityUpgrade fu)
        {
            SaveFields(fu);
            sFacilityType = fu.FacilityType?.ToString();
            upgradeLevel = fu.UpgradeLevel;
            currentLevel = fu.CurrentLevel;
            id = fu.FacilityInternalID;
            uid = fu.ID;
            return this;
        }

        public override void Load(ConfigNode node)
        {
            base.Load(node);
            node.TryGetValue(nameof(uid), ref uid);
        }

        public override void Save(ConfigNode node)
        {
            base.Save(node);
            node.AddValue(nameof(uid), uid);
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
