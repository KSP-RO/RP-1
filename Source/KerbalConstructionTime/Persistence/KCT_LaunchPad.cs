using System;
using System.Collections.Generic;
using System.Linq;

namespace KerbalConstructionTime
{
    public class KCT_LaunchPad : ConfigNodeStorage
    {
        public const string LPID = "SpaceCenter/LaunchPad";

        [Persistent] public int level = 0;
        [Persistent] public string name = "LaunchPad";

        public ConfigNode DestructionNode = new ConfigNode("DestructionState");
        public bool upgradeRepair = false;

        public bool IsDestroyed
        {
            get
            {
                string nodeStr = level == 2 ? "SpaceCenter/LaunchPad/Facility/LaunchPadMedium/ksp_pad_launchPad" : "SpaceCenter/LaunchPad/Facility/building";
                ConfigNode mainNode = DestructionNode.GetNode(nodeStr);
                if (mainNode == null)
                    return false;
                else
                    return !bool.Parse(mainNode.GetValue("intact"));
            }
        }

        public KCT_LaunchPad(string LPName, int lvl=0)
        {
            name = LPName;
            level = lvl;
        }

        public void Upgrade(int lvl)
        {
            //sets the new level, assumes completely repaired
            level = lvl;

            KCTGameStates.UpdateLaunchpadDestructionState = true;
            upgradeRepair = true;
        }

        public bool Delete(out string failReason)
        {
            bool found = false;
            foreach (KSCItem ksc in KCTGameStates.KSCs)
            {
                int idx = KCTGameStates.ActiveKSC.LaunchPads.IndexOf(this);
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
            foreach (KSCItem ksc in KCTGameStates.KSCs)
            {
                if (ksc.LaunchPads.Contains(this))
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

                    break;
                }
            }
            name = newName;
        }

        public void SetActive()
        {
            try
            {
                KCTDebug.Log("Switching to LaunchPad: " + name + " lvl: " + level + " destroyed? " + IsDestroyed);
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
                //might need to do this one frame later?
                //RefreshDesctructibleState();
                KCTGameStates.UpdateLaunchpadDestructionState = true;
                upgradeRepair = false;
            }
            catch (Exception ex)
            {
                KCTDebug.LogError("Error while calling SetActive: " + ex);
            }
        }

        public void SetDestructibleStateFromNode()
        {
            foreach (DestructibleBuilding facility in GetDestructibleFacilityReferences())
            {
                ConfigNode aNode = DestructionNode.GetNode(facility.id);
                if (aNode != null)
                    facility.Load(aNode);
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
            {
                if (node.HasValue("intact"))
                    node.SetValue("intact", "True");
            }
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

