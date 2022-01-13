using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Upgradeables;

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
        /// Used for creating custom pad sizes that lie somewhere between the full levels
        /// </summary>
        [Persistent] public float fractionalLevel = -1;

        /// <summary>
        /// Whether the pad is fully built. Does not account for destruction state.
        /// </summary>
        [Persistent] public bool isOperational = false;

        /// <summary>
        /// Max mass in tons that can be launched from this pad
        /// </summary>
        [Persistent] public float supportedMass = 0;

        /// <summary>
        /// Max size of the vessel that can be launched from this pad (width x height x width)
        /// </summary>
        [Persistent] public Vector3 supportedSize;

        /// <summary>
        /// Name given to the launch pad. Is also used for associating rollout/reconditioning with pads.
        /// </summary>
        [Persistent] public string name = "LaunchPad";

        /// <summary>
        /// The default launchsite this pad is hooked up to.
        /// </summary>
        [Persistent] public string launchSiteName = "LaunchPad";

        public ConfigNode DestructionNode = new ConfigNode("DestructionState");

        public float SupportedMass
        {
            get
            {
                EnsureMassAndSizeInitialized();
                return supportedMass;
            }
        }

        public Vector3 SupportedSize
        {
            get
            {
                EnsureMassAndSizeInitialized();
                return supportedSize;
            }
        }

        public string SupportedMassAsPrettyText => SupportedMass == float.MaxValue ? "unlimited" : $"{SupportedMass:#.#}t";

        public string SupportedSizeAsPrettyText => SupportedSize.y == float.MaxValue ? "unlimited" : $"{SupportedSize.x:#.#}x{SupportedSize.y:#.#}m";

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

        /// <summary>
        /// Used for deserializing from ConfigNodes.
        /// </summary>
        /// <param name="name"></param>
        public KCT_LaunchPad(string name)
        {
            this.name = name;
        }

        /// <summary>
        /// Creates a new pad with non-fractional level. Will also mark it as built/operational.
        /// </summary>
        /// <param name="name"></param>
        /// <param name="lvl">0-based level</param>
        public KCT_LaunchPad(string name, int lvl)
        {
            this.name = name;
            fractionalLevel = lvl;
            level = lvl;
            isOperational = true;

            EnsureMassAndSizeInitialized();
        }

        /// <summary>
        /// Creates a new pad with fractional level. Will NOT mark it as built/operational.
        /// </summary>
        /// <param name="name"></param>
        /// <param name="lvl">0-based level, can be fractional</param>
        /// <param name="supportedMass"></param>
        /// <param name="supportedSize"></param>
        public KCT_LaunchPad(string name, float lvl, float supportedMass, Vector3 supportedSize)
        {
            this.name = name;
            fractionalLevel = lvl;
            level = (int)lvl;
            isOperational = false;
            this.supportedMass = supportedMass;
            this.supportedSize = supportedSize;
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
                foreach (PadConstruction building in ksc.PadConstructions)
                {
                    if (building.LaunchpadIndex > idx) building.LaunchpadIndex--;
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
                foreach (PadConstruction pc in ksc.PadConstructions)
                {
                    if (pc.LaunchpadIndex == ksc.LaunchPads.IndexOf(this))
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
                EnsureMassAndSizeInitialized();

                KCTDebug.Log($"Switching to LaunchPad: {name} lvl: {level} destroyed? {IsDestroyed}");
                KCTGameStates.ActiveKSC.ActiveLaunchPadID = KCTGameStates.ActiveKSC.LaunchPads.IndexOf(this);

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

        private void EnsureMassAndSizeInitialized()
        {
            if (supportedMass == default || supportedSize == default)
            {
                var upgdFacility = GetUpgradeableFacilityReference();
                if (upgdFacility == null) return;   // Looks like facility upgrades are not initialized yet. Need to retry at a later time.

                float normalizedLevel = (float)level / (float)upgdFacility.MaxLevel;
                float massLimit = GameVariables.Instance.GetCraftMassLimit(normalizedLevel, true);
                Vector3 sizeLimit = GameVariables.Instance.GetCraftSizeLimit(normalizedLevel, true);

                supportedMass = massLimit;
                supportedSize = sizeLimit;
                fractionalLevel = level;
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

            EnsureMassAndSizeInitialized();
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

