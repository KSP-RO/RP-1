using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace KerbalConstructionTime
{
    public class KSCItem
    {
        public string KSCName;
        public List<IConstructionBuildItem> Constructions = new List<IConstructionBuildItem>();
        public List<LCItem> LaunchComplexes = new List<LCItem>();
        public KCTObservableList<LCConstruction> LCConstructions = new KCTObservableList<LCConstruction>();
        public KCTObservableList<FacilityUpgrade> FacilityUpgrades = new KCTObservableList<FacilityUpgrade>();
        public int Personnel = 0;
        public int FreePersonnel => Personnel - LaunchComplexes.Sum(lc => lc.Personnel);
        
        public int ActiveLaunchComplexIndex = 1;

        public LCItem Hangar => LaunchComplexes[0];

        public KSCItem(string name)
        {
            KSCName = name;

            LCConstructions.Added += added;
            LCConstructions.Removed += removed;
            FacilityUpgrades.Added += added;
            FacilityUpgrades.Removed += removed;

            void added(int idx, IConstructionBuildItem item) { Constructions.Add(item); }
            void removed(int idx, IConstructionBuildItem item) { Constructions.Remove(item); }
        }

        public LCItem ActiveLaunchComplexInstance => LaunchComplexes.Count > ActiveLaunchComplexIndex ? LaunchComplexes[ActiveLaunchComplexIndex] : null;

        public int LaunchComplexCount
        {
            get
            {
                int count = 0;
                foreach (LCItem lc in LaunchComplexes)
                    if (lc.IsOperational) count++;
                return count;
            }
        }

        public bool IsEmpty => !FacilityUpgrades.Any() && !LCConstructions.Any() && LaunchComplexes.All(lc => lc.IsEmpty);

        public void EnsureStartingLaunchComplexes()
        {
            if (LaunchComplexes.Count > 1) return;

            LCItem sph = new LCItem(LCItem.StartingHangar, this);
            sph.IsOperational = true;
            LaunchComplexes.Add(sph);
            LCItem starterLC = new LCItem(LCItem.StartingLC, this);
            starterLC.IsOperational = true;
            LaunchComplexes.Add(starterLC);
        }

        public void RecalculateBuildRates(bool all = true)
        {
            if(all)
                foreach (LCItem lc in LaunchComplexes)
                    lc.RecalculateBuildRates();

            for (int j = 0; j < Constructions.Count; j++)
                Constructions[j].UpdateBuildRate(j);
        }

        public void SwitchToPrevLaunchComplex() => SwitchLaunchComplex(false);
        public void SwitchToNextLaunchComplex() => SwitchLaunchComplex(true);

        public int SwitchLaunchComplex(bool forwardDirection, int startIndex = -1, bool doSwitch = true)
        {
            if (LaunchComplexCount < 2) return startIndex < 0 ? ActiveLaunchComplexIndex : startIndex;

            if (startIndex < 0)
                startIndex = ActiveLaunchComplexIndex;

            LCItem lc;
            do
            {
                if (forwardDirection)
                {
                    startIndex = (startIndex + 1) % LaunchComplexes.Count;
                }
                else
                {
                    //Simple fix for mod function being "weird" in the negative direction
                    //http://stackoverflow.com/questions/1082917/mod-of-negative-number-is-melting-my-brain
                    startIndex = ((startIndex - 1) % LaunchComplexes.Count + LaunchComplexes.Count) % LaunchComplexes.Count;
                }
                lc = LaunchComplexes[startIndex];
            } while (!lc.IsOperational);

            if (doSwitch)
                SwitchLaunchComplex(startIndex);

            return startIndex;
        }

        public void SwitchLaunchComplex(int LC_ID, bool updateDestrNode = true)
        {
            if (LC_ID < 0)
                LC_ID = ActiveLaunchComplexIndex;
            else
                ActiveLaunchComplexIndex = LC_ID;

            LaunchComplexes[LC_ID].SwitchLaunchPad();
            KCT_GUI._LCIndex = LC_ID;
        }

        /// <summary>
        /// Finds the highest level LaunchPad on the KSC
        /// </summary>
        /// <returns>The instance of the highest level LaunchPad</returns>
        public LCItem GetHighestLevelLaunchComplex()
        {
            LCItem highest = LaunchComplexes.First(p => p.IsPad && p.IsOperational);
            foreach (var lc in LaunchComplexes)
                if (lc.IsPad && lc.IsOperational && lc.MassMax > highest.MassMax)
                    highest = lc;
            return highest;
        }

        public ConfigNode AsConfigNode()
        {
            KCTDebug.Log("Saving KSC " + KSCName);
            var node = new ConfigNode("KSC");
            node.AddValue("KSCName", KSCName);
            node.AddValue("ActiveLCID", ActiveLaunchComplexIndex);
            node.AddValue("Personnel", Personnel);

            var cnLCs = new ConfigNode("LaunchComplexes");
            foreach (LCItem lc in LaunchComplexes)
            {
                var lcNode = lc.AsConfigNode();
                cnLCs.AddNode("LaunchComplex", lcNode);
            }
            node.AddNode(cnLCs);

            var cnUpgradeables = new ConfigNode("FacilityUpgrades");
            foreach (FacilityUpgrade facUpgd in FacilityUpgrades)
            {
                facUpgd.BuildListIndex = Constructions.IndexOf(facUpgd);
                var storageItem = new FacilityUpgradeStorageItem();
                storageItem.FromFacilityUpgrade(facUpgd);
                var cn = new ConfigNode("UpgradingBuilding");
                cn = ConfigNode.CreateConfigFromObject(storageItem, cn);
                cnUpgradeables.AddNode(cn);
            }
            node.AddNode(cnUpgradeables);

            var cnLCConstructions = new ConfigNode("LCConstructions");
            foreach (LCConstruction lcc in LCConstructions)
            {
                lcc.BuildListIndex = Constructions.IndexOf(lcc);
                var storageItem = new LCConstructionStorageItem();
                storageItem.FromLCConstruction(lcc);
                var cn = new ConfigNode("LCConstruction");
                cn = ConfigNode.CreateConfigFromObject(storageItem, cn);
                cnLCConstructions.AddNode(cn);
            }
            node.AddNode(cnLCConstructions);

            return node;
        }

        public KSCItem FromConfigNode(ConfigNode node)
        {
            FacilityUpgrades.Clear();
            LCConstructions.Clear();

            KSCName = node.GetValue("KSCName");
            if (!int.TryParse(node.GetValue("ActiveLCID"), out ActiveLaunchComplexIndex))
                ActiveLaunchComplexIndex = 0;

            Personnel = 0;
            node.TryGetValue("Personnel", ref Personnel);

            ConfigNode tmp = node.GetNode("LaunchComplexes");
            if (tmp != null)
            {
                LaunchComplexes.Clear();
                foreach (ConfigNode cn in tmp.GetNodes("LaunchComplex"))
                {
                    var tempLC = new LCItem("", 0f, Vector3.zero, true, false, this);
                    tempLC.FromConfigNode(cn);
                    LaunchComplexes.Add(tempLC);
                }
            }

            tmp = node.GetNode("LCConstructions");
            if (tmp != null)
            {
                foreach (ConfigNode cn in tmp.GetNodes("LCConstruction"))
                {
                    var storageItem = new LCConstructionStorageItem();
                    ConfigNode.LoadObjectFromConfig(storageItem, cn);
                    LCConstructions.Add(storageItem.ToLCConstruction());
                }
            }

            tmp = node.GetNode("FacilityUpgrades");
            if (tmp != null)
            {
                foreach (ConfigNode cn in tmp.GetNodes("UpgradingBuilding"))
                {
                    var storageItem = new FacilityUpgradeStorageItem();
                    ConfigNode.LoadObjectFromConfig(storageItem, cn);
                    FacilityUpgrades.Add(storageItem.ToFacilityUpgrade());
                }
            }

            Constructions.Sort((a, b) => a.BuildListIndex.CompareTo(b.BuildListIndex));

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
