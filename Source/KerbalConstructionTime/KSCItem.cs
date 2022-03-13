using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace KerbalConstructionTime
{
    public class KSCItem
    {
        public string KSCName;
        public List<LCItem> LaunchComplexes = new List<LCItem>();
        public List<LCConstruction> LCConstructions = new List<LCConstruction>();
        public List<FacilityUpgrade> KSCTech = new List<FacilityUpgrade>();
        public List<int> RDUpgrades = new List<int>() { 0, 0 }; //research/development
        
        public int ActiveLaunchComplexID = 0;

        public KSCItem(string name)
        {
            KSCName = name;
            RDUpgrades[1] = KCTGameStates.TechUpgradesTotal;
        }

        public LCItem ActiveLaunchComplexInstance => LaunchComplexes.Count > ActiveLaunchComplexID ? LaunchComplexes[ActiveLaunchComplexID] : null;

        public int LaunchComplexCount
        {
            get
            {
                int count = 0;
                foreach (LCItem lc in LaunchComplexes)
                    if (lc.isOperational) count++;
                return count;
            }
        }

        public bool IsEmpty => !KSCTech.Any() && !LCConstructions.Any() && LaunchComplexes.All(lc => lc.IsEmpty);

        public void EnsureStartingLaunchComplexes()
        {
            if (LaunchComplexes.Count > 1) return;

            LCItem sph = new LCItem("Launch Complex", -1f, new Vector3(40f, 15f, 40f), false, this);
            sph.isOperational = true;
            LaunchComplexes.Add(sph);
            LCItem starterLC = new LCItem("Launch Complex", 15f, new Vector3(5f, 20f, 5f), true, this);
            starterLC.isOperational = true;
            LaunchComplexes.Add(starterLC);
        }

        public void RecalculateBuildRates()
        {
            foreach (LCItem lc in LaunchComplexes)
                lc.RecalculateBuildRates();
        }

        public void RecalculateUpgradedBuildRates()
        {
            foreach (LCItem lc in LaunchComplexes)
                lc.RecalculateUpgradedBuildRates();
        }

        public void SwitchToPrevLaunchComplex() => SwitchLaunchComplex(false);
        public void SwitchToNextLaunchComplex() => SwitchLaunchComplex(true);

        public void SwitchLaunchComplex(bool forwardDirection)
        {
            if (KCTGameStates.ActiveKSC.LaunchComplexCount < 2) return;

            int idx = KCTGameStates.ActiveKSC.ActiveLaunchComplexID;
            LCItem lc;
            do
            {
                if (forwardDirection)
                {
                    idx = (idx + 1) % LaunchComplexes.Count;
                }
                else
                {
                    //Simple fix for mod function being "weird" in the negative direction
                    //http://stackoverflow.com/questions/1082917/mod-of-negative-number-is-melting-my-brain
                    idx = ((idx - 1) % LaunchComplexes.Count + LaunchComplexes.Count) % LaunchComplexes.Count;
                }
                lc = LaunchComplexes[idx];
            } while (!lc.isOperational);

            KCTGameStates.ActiveKSC.SwitchLaunchComplex(idx);
        }

        public void SwitchLaunchComplex(int LC_ID, bool updateDestrNode = true)
        {
            LaunchComplexes[LC_ID].SwitchLaunchPad();
        }

        /// <summary>
        /// Finds the highest level LaunchPad on the KSC
        /// </summary>
        /// <returns>The instance of the highest level LaunchPad</returns>
        public KCT_LaunchPad GetHighestLevelLaunchPad()
        {
            LCItem highest = LaunchComplexes.First(p => p.isPad && p.isOperational);
            foreach (var lc in LaunchComplexes)
                if (lc.isPad && lc.isOperational && lc.massMax > highest.massMax)
                    highest = lc;
            return highest.LaunchPads.First(p => p.isOperational);
        }

        public ConfigNode AsConfigNode()
        {
            KCTDebug.Log("Saving KSC " + KSCName);
            var node = new ConfigNode("KSC");
            node.AddValue("KSCName", KSCName);
            node.AddValue("ActiveLCID", ActiveLaunchComplexID);

            var cnLCs = new ConfigNode("LaunchComplexes");
            foreach (LCItem lc in LaunchComplexes)
            {
                var lcNode = lc.AsConfigNode();
                cnLCs.AddNode("LaunchComplex", lcNode);
            }
            node.AddNode(cnLCs);

            var cnRDUp = new ConfigNode("RDUpgrades");
            foreach (int upgrade in RDUpgrades)
            {
                cnRDUp.AddValue("Upgrade", upgrade.ToString());
            }
            node.AddNode(cnRDUp);

            var cnUpgradeables = new ConfigNode("KSCTech");
            foreach (FacilityUpgrade buildingTech in KSCTech)
            {
                var storageItem = new FacilityUpgradeStorageItem();
                storageItem.FromFacilityUpgrade(buildingTech);
                var cn = new ConfigNode("UpgradingBuilding");
                cn = ConfigNode.CreateConfigFromObject(storageItem, cn);
                cnUpgradeables.AddNode(cn);
            }
            node.AddNode(cnUpgradeables);

            var cnLCConstructions = new ConfigNode("LCConstructions");
            foreach (LCConstruction pc in LCConstructions)
            {
                var storageItem = new LCConstructionStorageItem();
                storageItem.FromLCConstruction(pc);
                var cn = new ConfigNode("LCConstruction");
                cn = ConfigNode.CreateConfigFromObject(storageItem, cn);
                cnLCConstructions.AddNode(cn);
            }
            node.AddNode(cnLCConstructions);

            return node;
        }

        public KSCItem FromConfigNode(ConfigNode node)
        {
            RDUpgrades.Clear();
            KSCTech.Clear();
            LCConstructions.Clear();

            KSCName = node.GetValue("KSCName");
            if (!int.TryParse(node.GetValue("ActiveLCID"), out ActiveLaunchComplexID))
                ActiveLaunchComplexID = 0;

            ConfigNode rdUp = node.GetNode("RDUpgrades");
            foreach (string upgrade in rdUp.GetValues("Upgrade"))
            {
                RDUpgrades.Add(int.Parse(upgrade));
            }

            ConfigNode tmp = node.GetNode("LaunchComplexes");
            if (tmp != null)
            {
                LaunchComplexes.Clear();
                foreach (ConfigNode cn in tmp.GetNodes("LaunchComplex"))
                {
                    var tempLC = new LCItem("", 0f, Vector3.zero, true, this);
                    tempLC.FromConfigNode(cn);
                    LaunchComplexes.Add(tempLC);
                }
            }

            if (node.HasNode("LCConstructions"))
            {
                tmp = node.GetNode("LCConstructions");
                foreach (ConfigNode cn in tmp.GetNodes("LCConstruction"))
                {
                    var storageItem = new LCConstructionStorageItem();
                    ConfigNode.LoadObjectFromConfig(storageItem, cn);
                    LCConstructions.Add(storageItem.ToLCConstruction());
                }
            }

            if (node.HasNode("KSCTech"))
            {
                tmp = node.GetNode("KSCTech");
                foreach (ConfigNode cn in tmp.GetNodes("UpgradingBuilding"))
                {
                    var storageItem = new FacilityUpgradeStorageItem();
                    ConfigNode.LoadObjectFromConfig(storageItem, cn);
                    KSCTech.Add(storageItem.ToFacilityUpgrade());
                }
            }

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
