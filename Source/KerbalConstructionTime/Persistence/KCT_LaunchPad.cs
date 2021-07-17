using System;
using System.Collections.Generic;
using System.Linq;

namespace KerbalConstructionTime
{
    public class KCT_LaunchPad : ConfigNodeStorage
    {
        public const string LPID = "SpaceCenter/LaunchPad";

        /// <summary>
        /// Zero-based level of the launch pad
        /// </summary>
        [Persistent] public int level = 0;

        /// <summary>
        /// Name given to the launch pad. Is also used for associating rollout/reconditioning with pads.
        /// </summary>
        [Persistent] public string name = "LaunchPad";

        /// <summary>
        /// The default launchsite this pad is hooked up to.
        /// </summary>
        [Persistent] public string launchSiteName = "LaunchPad";

        public ConfigNode DestructionNode = new ConfigNode("DestructionState");

        public bool IsDestroyed
        {
            get
            {
                string nodeStr = level == 2 ? "SpaceCenter/LaunchPad/Facility/LaunchPadMedium/ksp_pad_launchPad" : "SpaceCenter/LaunchPad/Facility/building";
                if (DestructionNode.GetNode(nodeStr) is ConfigNode mainNode)
                    return !bool.Parse(mainNode.GetValue("intact"));
                return false;
            }
        }

        public KCT_LaunchPad(string LPName, int lvl = 0)
        {
            name = LPName;
            level = lvl;
        }

        public void Upgrade(int lvl)
        {
            //sets the new level, assumes completely repaired
            level = lvl;
            UpdateLaunchpadDestructionState(true);
        }

        public bool Delete(out string failReason)
        {
            bool found = false;
            foreach (KSCItem ksc in KCTGameStates.KSCs)
            {
                int idx = ksc.LaunchPads.IndexOf(this);
                if (idx < 0) continue;

                var rr = ksc.Recon_Rollout.FirstOrDefault(r => r.LaunchPadID == name);
                if (rr != null)
                {
                    failReason = rr.IsComplete() ? "a vessel is currently on the pad" : "pad has ongoing rollout or reconditioning";
                    return false;
                }

                foreach (BuildListVessel vessel in ksc.VABWarehouse)
                {
                    if (vessel.LaunchSiteID > idx) vessel.LaunchSiteID--;
                }
                foreach (BuildListVessel vessel in ksc.VABList)
                {
                    if (vessel.LaunchSiteID > idx) vessel.LaunchSiteID--;
                }
                foreach (FacilityUpgrade building in ksc.KSCTech)
                {
                    if (building.FacilityType == SpaceCenterFacility.LaunchPad && building.LaunchpadID > idx) building.LaunchpadID--;
                }

                ksc.LaunchPads.RemoveAt(idx);

                if (ksc == KCTGameStates.ActiveKSC)
                {
                    ksc.SwitchLaunchPad(0);
                }
            }

            failReason = null;
            return !found;
        }

        public void Rename(string newName)
        {
            //find everything that references this launchpad by name and update the name reference
            if (KCTGameStates.KSCs.FirstOrDefault(x => x.LaunchPads.Contains(this)) is KSCItem ksc)
            {
                if (ksc.LaunchPads.Exists(lp => string.Equals(lp.name, newName, StringComparison.OrdinalIgnoreCase)))
                    return; //can't name it something that already is named that

                foreach (ReconRollout rr in ksc.Recon_Rollout)
                {
                    if (rr.LaunchPadID == name)
                    {
                        rr.LaunchPadID = newName;
                    }
                }
                foreach (FacilityUpgrade up in ksc.KSCTech)
                {
                    if (up.IsLaunchpad && up.LaunchpadID == ksc.LaunchPads.IndexOf(this))
                    {
                        up.CommonName = newName;
                    }
                }
            }
            name = newName;
        }

        public void SetActive()
        {
            try
            {
                KCTDebug.Log($"Switching to LaunchPad: {name} lvl: {level} destroyed? {IsDestroyed}");
                KCTGameStates.ActiveKSC.ActiveLaunchPadID = KCTGameStates.ActiveKSC.LaunchPads.IndexOf(this);

                //set the level to this level
                if (Utilities.CurrentGameIsCareer())
                {
                    foreach (Upgradeables.UpgradeableFacility facility in GetUpgradeableFacilityReferences())
                    {
                        KCTEvents.AllowedToUpgrade = true;
                        facility.SetLevel(level);
                    }
                }

                //set the destroyed state to this destroyed state
                UpdateLaunchpadDestructionState(false);
            }
            catch (Exception ex)
            {
                KCTDebug.LogError("Error while calling SetActive: " + ex);
            }
        }

        public void UpdateLaunchpadDestructionState(bool upgradeRepair)
        {
            // Comments suggest may need to wait until next frame.  Unsure why.
            //yield return new WaitForFixedUpdate();
            KCTDebug.Log("Updating launchpad destruction state.");
            if (upgradeRepair)
            {
                RefreshDestructionNode();
                CompletelyRepairNode();
            }
            SetDestructibleStateFromNode();
        }

        public void SetDestructibleStateFromNode()
        {
            foreach (DestructibleBuilding facility in GetDestructibleFacilityReferences())
            {
                if (DestructionNode.GetNode(facility.id) is ConfigNode node)
                    facility.Load(node);
            }
        }

        public void RefreshDestructionNode()
        {
            DestructionNode = new ConfigNode("DestructionState");
            foreach (DestructibleBuilding facility in GetDestructibleFacilityReferences())
            {
                ConfigNode aNode = new ConfigNode(facility.id);
                facility.Save(aNode);
                DestructionNode.AddNode(aNode);
            }
        }

        public void CompletelyRepairNode()
        {
            foreach (ConfigNode node in DestructionNode.GetNodes())
                node.SetValue("intact", "True", false);     // Only update value if already exists
        }

        public List<Upgradeables.UpgradeableFacility> GetUpgradeableFacilityReferences()
        {
            return ScenarioUpgradeableFacilities.protoUpgradeables[LPID].facilityRefs;
        }

        List<DestructibleBuilding> GetDestructibleFacilityReferences()
        {

            List<DestructibleBuilding> destructibles = new List<DestructibleBuilding>();
            foreach (KeyValuePair<string, ScenarioDestructibles.ProtoDestructible> kvp in ScenarioDestructibles.protoDestructibles)
            {
                if (kvp.Key.Contains("LaunchPad"))
                {
                    destructibles.AddRange(kvp.Value.dBuildingRefs);
                }
            }
            return destructibles;
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

