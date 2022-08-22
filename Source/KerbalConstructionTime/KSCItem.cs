using System.Collections.Generic;
using UniLinq;
using UnityEngine;
using RP0.DataTypes;

namespace KerbalConstructionTime
{
    public class KSCItem : IConfigNode
    {
        [Persistent]
        public string KSCName;
        [Persistent]
        public int Engineers = 0;
        public int UnassignedEngineers => Engineers - LaunchComplexes.Sum(lc => lc.Engineers);
        [Persistent]
        public int ActiveLaunchComplexIndex = 0;

        [Persistent]
        public PersistentList<LCItem> LaunchComplexes = new PersistentList<LCItem>();
        [Persistent]
        public KCTObservableList<LCConstruction> LCConstructions = new KCTObservableList<LCConstruction>();
        [Persistent]
        public KCTObservableList<FacilityUpgrade> FacilityUpgrades = new KCTObservableList<FacilityUpgrade>();
                

        public List<ConstructionBuildItem> Constructions = new List<ConstructionBuildItem>();

        private bool _allowRecalcConstructions = true;

        public LCItem Hangar => LaunchComplexes[0];

        void added(int idx, ConstructionBuildItem item) { Constructions.Add(item); }
        void removed(int idx, ConstructionBuildItem item) { Constructions.Remove(item); }
        void updated()
        {
            if (_allowRecalcConstructions) RecalculateBuildRates(false);
            RP0.MaintenanceHandler.Instance?.ScheduleMaintenanceUpdate();
        }

        private void SetListeners()
        {
            LCConstructions.Added += added;
            LCConstructions.Removed += removed;
            LCConstructions.Updated += updated;
            FacilityUpgrades.Added += added;
            FacilityUpgrades.Removed += removed;
            FacilityUpgrades.Updated += updated;
        }

        public KSCItem(string name)
        {
            KSCName = name;

            SetListeners();
        }

        public LCItem ActiveLaunchComplexInstance => LaunchComplexes.Count > ActiveLaunchComplexIndex ? LaunchComplexes[ActiveLaunchComplexIndex] : null;

        public int LaunchComplexCount
        {
            get
            {
                int count = 0;
                foreach (LCItem lc in LaunchComplexes)
                    if (lc.IsOperational) 
                        ++count;
                return count;
            }
        }

        public bool IsAnyLCOperational
        {
            get
            {
                {
                    for (int i = LaunchComplexes.Count; i-- > 1;)
                        if (LaunchComplexes[i].IsOperational)
                            return true;

                    return false;
                }
            }
        }

        public bool IsEmpty => !FacilityUpgrades.Any() && !LCConstructions.Any() && LaunchComplexes.Count == 1 && Hangar.IsEmpty;

        public void EnsureStartingLaunchComplexes()
        {
            if (LaunchComplexes.Count > 0) return;

            LCItem sph = new LCItem(LCData.StartingHangar, this);
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

        public void Save(ConfigNode node)
        {
            KCTDebug.Log("Saving KSC " + KSCName);
            ConfigNode.CreateConfigFromObject(this, node);
        }

        public void Load(ConfigNode node)
        {
            _allowRecalcConstructions = false;
            ConfigNode.LoadObjectFromConfig(this, node);

                foreach (var lc in LaunchComplexes)
                {
                    lc.PostLoad(this);
                }

            

            _allowRecalcConstructions = true;

            if (KerbalConstructionTimeData.Instance.LoadedSaveVersion < KCTGameStates.VERSION)
            {
                if (KerbalConstructionTimeData.Instance.LoadedSaveVersion < 18)
                {
                    if (!int.TryParse(node.GetValue("ActiveLCID"), out ActiveLaunchComplexIndex))
                        ActiveLaunchComplexIndex = 0;
                }
            }
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
