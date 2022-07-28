using System;
using System.Collections.Generic;
using System.Linq;
using Upgradeables;

namespace KerbalConstructionTime
{
    public enum LaunchPadState
    {
        None,
        Destroyed,
        Nonoperational,
        Rollout,
        Rollback,
        Reconditioning,
        Free,
    }

    public class KCT_LaunchPad : ConfigNodeStorage
    {
        public const string LPID = "SpaceCenter/LaunchPad";

        /// <summary>
        /// Zero-based level of the launch pad
        /// </summary>
        [Persistent] public int level = 0;

        /// <summary>
        /// Unique ID of the launch pad
        /// </summary>
        [Persistent] public Guid id;

        /// <summary>
        /// Used for creating custom pad sizes that lie somewhere between the full levels
        /// </summary>
        [Persistent] public float fractionalLevel = -1;

        /// <summary>
        /// Whether the pad is fully built. Does not account for destruction state.
        /// </summary>
        [Persistent] public bool isOperational = false;

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

        public LCItem LC
        {
            get
            {
                foreach (KSCItem currentKSC in KCTGameStates.KSCs)
                {
                    if (currentKSC.LaunchComplexes.FirstOrDefault(x => x.LaunchPads.Contains(this)) is LCItem currentLC)
                    {
                        return currentLC;
                    }
                }

                return null;
            }
        }

        public LaunchPadState State
        {
            get
            {
                if (IsDestroyed)
                    return LaunchPadState.Destroyed;

                if (!isOperational)
                    return LaunchPadState.Nonoperational;

                foreach (var rr in LC.Recon_Rollout)
                {
                    if (rr.LaunchPadID == launchSiteName)
                    {
                        switch (rr.RRType)
                        {
                            case ReconRollout.RolloutReconType.Reconditioning: return LaunchPadState.Reconditioning;
                            case ReconRollout.RolloutReconType.Rollback: return LaunchPadState.Rollback;
                            case ReconRollout.RolloutReconType.Rollout: return LaunchPadState.Rollout;
                        }
                        break;
                    }
                }

                return LaunchPadState.Free;
            }
        }

        /// <summary>
        /// Used for deserializing from ConfigNodes.
        /// </summary>
        /// <param name="name"></param>
        public KCT_LaunchPad(string name)
        {
            this.name = name;
        }

        /// <summary>
        /// Creates a new pad with fractional level. Will NOT mark it as built/operational.
        /// </summary>
        /// <param name="id"></param>
        /// <param name="name"></param>
        /// <param name="lvl">0-based level, can be fractional</param>
        public KCT_LaunchPad(Guid id, string name, float lvl)
        {
            this.id = id;
            this.name = name;
            fractionalLevel = lvl;
            level = (int)lvl;
            isOperational = false;
        }

        public override ConfigNode AsConfigNode()
        {
            ConfigNode cn = base.AsConfigNode();
            cn.AddValue(nameof(id), id);
            return cn;
        }

        public bool Delete(out string failReason)
        {
            foreach (KSCItem currentKSC in KCTGameStates.KSCs)
            {
                foreach (LCItem currentLC in currentKSC.LaunchComplexes)
                {
                    int idx = currentLC.LaunchPads.IndexOf(this);
                    if (idx < 0) continue;

                    var rr = currentLC.Recon_Rollout.FirstOrDefault(r => r.LaunchPadID == name);
                    if (rr != null)
                    {
                        failReason = rr.IsComplete() ? "a vessel is currently on the pad" : "pad has ongoing rollout or reconditioning";
                        return false;
                    }

                    foreach (BuildListVessel vessel in currentLC.Warehouse)
                    {
                        if (vessel.LaunchSiteIndex >= idx) vessel.LaunchSiteIndex--;
                    }
                    foreach (BuildListVessel vessel in currentLC.BuildList)
                    {
                        if (vessel.LaunchSiteIndex >= idx) vessel.LaunchSiteIndex--;
                    }
                    
                    try
                    {
                        KCTEvents.OnPadDismantled?.Fire(this);
                    }
                    catch (Exception ex)
                    {
                        UnityEngine.Debug.LogException(ex);
                    }

                    currentLC.LaunchPads.RemoveAt(idx);

                    if (currentLC == KCTGameStates.ActiveKSC.ActiveLaunchComplexInstance)
                    {
                        currentLC.SwitchLaunchPad(0);
                    }
                }
            }

            failReason = null;
            return true;
        }

        public void Rename(string newName)
        {
            //find everything that references this launchpad by name and update the name reference

            LCItem lc = LC;
            if (lc != null)
            {
                if (lc.LaunchPads.Exists(lp => string.Equals(lp.name, newName, StringComparison.OrdinalIgnoreCase)))
                    return; //can't name it something that already is named that

                foreach (ReconRollout rr in lc.Recon_Rollout)
                {
                    if (rr.LaunchPadID == name)
                    {
                        rr.LaunchPadID = newName;
                    }
                }
                foreach (PadConstruction pc in lc.PadConstructions)
                {
                    if (pc.ID == id)
                    {
                        pc.Name = newName;
                    }
                }
            }
            name = newName;
        }

        public void SetActive()
        {
            try
            {
                int idx = KCTGameStates.ActiveKSC.ActiveLaunchComplexInstance.LaunchPads.IndexOf(this);
                KCTDebug.Log($"Switching to LaunchPad: {name} lvl: {level} destroyed? {IsDestroyed}. Index {idx}");

                //set the level to this level
                if (Utilities.CurrentGameIsCareer())
                {
                    UpgradeableFacility facility = GetUpgradeableFacilityReference();
                    KCTEvents.AllowedToUpgrade = true;
                    facility.SetLevel(level);
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

        /// <summary>
        /// Will read the per-component destruction state of the LP and save that to the current pad item.
        /// It is used for keeping track of which pads are damaged.
        /// </summary>
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

        public static UpgradeableFacility GetUpgradeableFacilityReference()
        {
            return ScenarioUpgradeableFacilities.protoUpgradeables.TryGetValue(LPID, out var f) ? f.facilityRefs.FirstOrDefault() : null;
        }

        public void MigrateFromOldState()
        {
            if (level == -1) return;    // This is migrated in PadConstructionStorageItem instead

            fractionalLevel = level;
            if (level >= 0) isOperational = true;
        }

        private List<DestructibleBuilding> GetDestructibleFacilityReferences()
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

