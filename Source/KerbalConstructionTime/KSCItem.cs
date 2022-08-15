using System.Collections.Generic;
using UniLinq;
using UnityEngine;

namespace KerbalConstructionTime
{
    public class KSCItem
    {
        public string KSCName;
        public List<ConstructionBuildItem> Constructions = new List<ConstructionBuildItem>();
        public List<LCItem> LaunchComplexes = new List<LCItem>();
        public KCTObservableList<LCConstruction> LCConstructions = new KCTObservableList<LCConstruction>();
        public KCTObservableList<FacilityUpgrade> FacilityUpgrades = new KCTObservableList<FacilityUpgrade>();
        public int Engineers = 0;
        public int UnassignedEngineers => Engineers - LaunchComplexes.Sum(lc => lc.Engineers);

        public int ActiveLaunchComplexIndex = 0;
        private bool _allowRecalcConstructions = true;

        public LCItem Hangar => LaunchComplexes[0];

        public KSCItem(string name)
        {
            KSCName = name;

            LCConstructions.Added += added;
            LCConstructions.Removed += removed;
            LCConstructions.Updated += updated;
            FacilityUpgrades.Added += added;
            FacilityUpgrades.Removed += removed;
            FacilityUpgrades.Updated += updated;

            void added(int idx, ConstructionBuildItem item) { Constructions.Add(item); }
            void removed(int idx, ConstructionBuildItem item) { Constructions.Remove(item); }
            void updated()
            {
                if (_allowRecalcConstructions) RecalculateBuildRates(false);
                KCTEvents.OnRP0MaintenanceChanged.Fire();
            }
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

        public bool IsEmpty => !FacilityUpgrades.Any() && !LCConstructions.Any() && LaunchComplexes.Count == 1 && Hangar.IsEmpty;

        public void EnsureStartingLaunchComplexes()
        {
            if (LaunchComplexes.Count > 0) return;

            LCItem sph = new LCItem(LCItem.StartingHangar, this);
            sph.IsOperational = true;
            LaunchComplexes.Add(sph);
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
            int count = LaunchComplexes.Count;
            do
            {
                if (forwardDirection)
                {
                    ++startIndex;
                    if (startIndex == count)
                        startIndex = 0;
                }
                else
                {
                    if (startIndex == 0)
                        startIndex = count;
                    --startIndex;
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
            {
                LC_ID = ActiveLaunchComplexIndex;
            }
            else
            {
                if (LC_ID == ActiveLaunchComplexIndex)
                    return;

                ActiveLaunchComplexIndex = LC_ID;
            }

            if(HighLogic.LoadedSceneIsEditor)
                RP0.Harmony.PatchEngineersReport.UpdateCraftStats();

            LaunchComplexes[LC_ID].SwitchLaunchPad();
        }

        /// <summary>
        /// Finds the highest level LaunchPad on the KSC
        /// </summary>
        /// <returns>The instance of the highest level LaunchPad</returns>
        public LCItem GetHighestLevelLaunchComplex()
        {
            LCItem highest = LaunchComplexes.First(p => p.LCType == LaunchComplexType.Pad && p.IsOperational);
            foreach (var lc in LaunchComplexes)
                if (lc.LCType == LaunchComplexType.Pad && lc.IsOperational && lc.MassMax > highest.MassMax)
                    highest = lc;
            return highest;
        }

        public ConfigNode AsConfigNode()
        {
            KCTDebug.Log("Saving KSC " + KSCName);
            var node = new ConfigNode("KSC");
            node.AddValue("KSCName", KSCName);
            node.AddValue("ActiveLCID", ActiveLaunchComplexIndex);
            node.AddValue("Engineers", Engineers);

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
                storageItem.Save(cn);
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
                storageItem.Save(cn);
                cnLCConstructions.AddNode(cn);
            }
            node.AddNode(cnLCConstructions);

            return node;
        }

        public KSCItem FromConfigNode(ConfigNode node)
        {
            _allowRecalcConstructions = false;

            FacilityUpgrades.Clear();
            LCConstructions.Clear();

            KSCName = node.GetValue("KSCName");
            if (!int.TryParse(node.GetValue("ActiveLCID"), out ActiveLaunchComplexIndex))
                ActiveLaunchComplexIndex = 0;

            Engineers = 0;
            node.TryGetValue("Engineers", ref Engineers);

            if (KCTGameStates.LoadedSaveVersion < KCTGameStates.VERSION)
            {
                if (KCTGameStates.LoadedSaveVersion < 1)
                {
                    Engineers *= 2;
                }
            }

            ConfigNode tmp = node.GetNode("LaunchComplexes");
            if (tmp != null)
            {
                LaunchComplexes.Clear();
                foreach (ConfigNode cn in tmp.GetNodes("LaunchComplex"))
                {
                    var tempLC = new LCItem(this);
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
                    storageItem.Load(cn);
                    LCConstructions.Add(storageItem.ToLCConstruction(this));
                }
            }

            tmp = node.GetNode("FacilityUpgrades");
            if (tmp != null)
            {
                foreach (ConfigNode cn in tmp.GetNodes("UpgradingBuilding"))
                {
                    var storageItem = new FacilityUpgradeStorageItem();
                    storageItem.Load(cn);
                    FacilityUpgrades.Add(storageItem.ToFacilityUpgrade());
                }
            }

            Constructions.Sort((a, b) => a.BuildListIndex.CompareTo(b.BuildListIndex));
            _allowRecalcConstructions = true;

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
