using System;
using System.Collections.Generic;
using UniLinq;
using Upgradeables;
using ROUtils;

namespace RP0
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

    public class LCLaunchPad : IConfigNode
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

        public LaunchComplex LC
        {
            get
            {
                foreach (LCSpaceCenter currentKSC in SpaceCenterManagement.Instance.KSCs)
                {
                    if (currentKSC.LaunchComplexes.FirstOrDefault(x => x.LaunchPads.Contains(this)) is LaunchComplex currentLC)
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
                    if (rr.launchPadID == name)
                    {
                        switch (rr.RRType)
                        {
                            case ReconRolloutProject.RolloutReconType.Reconditioning: return LaunchPadState.Reconditioning;
                            case ReconRolloutProject.RolloutReconType.Rollback: return LaunchPadState.Rollback;
                            case ReconRolloutProject.RolloutReconType.Rollout: return LaunchPadState.Rollout;
                        }
                        break;
                    }
                }

                return LaunchPadState.Free;
            }
        }

        public LCLaunchPad() { }

        /// <summary>
        /// Creates a new pad with fractional level. Will NOT mark it as built/operational.
        /// </summary>
        /// <param name="id"></param>
        /// <param name="name"></param>
        /// <param name="lvl">0-based level, can be fractional</param>
        public LCLaunchPad(Guid id, string name, float lvl)
        {
            this.id = id;
            this.name = name;
            fractionalLevel = lvl;
            level = (int)lvl;
            isOperational = false;

            SpaceCenterManagement.Instance.RegisterLP(this);
        }

        public bool Delete(out string failReason)
        {
            foreach (LCSpaceCenter currentKSC in SpaceCenterManagement.Instance.KSCs)
            {
                foreach (LaunchComplex currentLC in currentKSC.LaunchComplexes)
                {
                    int idx = currentLC.LaunchPads.IndexOf(this);
                    if (idx < 0) continue;

                    foreach (var rr in currentLC.Recon_Rollout)
                    {
                        if (rr.launchPadID != name)
                            continue;

                        if (rr.RRType != ReconRolloutProject.RolloutReconType.Reconditioning)
                        {
                            failReason = rr.IsComplete() ? "a vessel is currently on the pad" : "pad has ongoing rollout";
                            return false;
                        }
                    }

                    foreach (VesselProject vessel in currentLC.Warehouse)
                    {
                        if (vessel.launchSiteIndex >= idx) vessel.launchSiteIndex--;
                    }
                    foreach (VesselProject vessel in currentLC.BuildList)
                    {
                        if (vessel.launchSiteIndex >= idx) vessel.launchSiteIndex--;
                    }
                    
                    try
                    {
                        SCMEvents.OnPadDismantled?.Fire(this);
                    }
                    catch (Exception ex)
                    {
                        UnityEngine.Debug.LogException(ex);
                    }

                    currentLC.LaunchPads.RemoveAt(idx);

                    if (currentLC == SpaceCenterManagement.Instance.ActiveSC.ActiveLC)
                    {
                        currentLC.SwitchLaunchPad(0);
                    }

                    SpaceCenterManagement.Instance.UnregsiterLP(this);
                }
            }

            failReason = null;
            return true;
        }

        public void Rename(string newName)
        {
            //find everything that references this launchpad by name and update the name reference

            LaunchComplex lc = LC;
            if (lc != null)
            {
                if (lc.LaunchPads.Exists(lp => string.Equals(lp.name, newName, StringComparison.OrdinalIgnoreCase)))
                    return; //can't name it something that already is named that

                foreach (ReconRolloutProject rr in lc.Recon_Rollout)
                {
                    if (rr.launchPadID == name)
                    {
                        rr.launchPadID = newName;
                    }
                }
                foreach (PadConstructionProject pc in lc.PadConstructions)
                {
                    if (pc.id == id)
                    {
                        pc.name = newName;
                    }
                }
            }
            name = newName;
        }

        public void SetActive()
        {
            if (HighLogic.LoadedScene == GameScenes.TRACKSTATION)
                return;

            try
            {
                int idx = SpaceCenterManagement.Instance.ActiveSC.ActiveLC.LaunchPads.IndexOf(this);
                RP0Debug.Log($"Switching to LaunchPad: {name} lvl: {level} destroyed? {IsDestroyed}. Index {idx}");

                //set the level to this level
                if (KSPUtils.CurrentGameIsCareer())
                {
                    UpgradeableFacility facility = GetUpgradeableFacilityReference();
                    facility.SetLevel(level);
                }

                //set the destroyed state to this destroyed state
                UpdateLaunchpadDestructionState(false);
            }
            catch (Exception ex)
            {
                RP0Debug.LogError("Error while calling SetActive: " + ex);
            }
        }

        public void UpdateLaunchpadDestructionState(bool upgradeRepair)
        {
            // Comments suggest may need to wait until next frame.  Unsure why.
            //yield return new WaitForFixedUpdate();
            RP0Debug.Log("Updating launchpad destruction state.");
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

        public void Load(ConfigNode node)
        {
            ConfigNode.LoadObjectFromConfig(this, node);
            DestructionNode = node.GetNode("DestructionState");
        }

        public void Save(ConfigNode node)
        {
            ConfigNode.CreateConfigFromObject(this, node);
            node.AddNode(DestructionNode);
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

